using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Server.Client.Coinflips;
using Server.Client.Users;
using Server.Client.Utils;
using Server.Infrastructure;
using Server.Infrastructure.Discord;

namespace Server.Communication.Discord.Interactions
{
    public static class CoinflipButtonHandler
    {
        public static async Task Handle(DiscordClient client, ComponentInteractionCreateEventArgs e)
        {
            var parts = e.Id.Split('_');
            if (parts.Length < 3)
                return;

            // id format is always cf_<action>_<flipId>
            var prefix = parts[0];
            var action = parts[1];
            if (!int.TryParse(parts[2], out var flipId))
                return;

            var env = ServerEnvironment.GetServerEnvironment();
            var serverManager = env.ServerManager;
            var coinflipsService = serverManager.CoinflipsService;
            var usersService = serverManager.UsersService;

            var flip = coinflipsService.GetCoinflipById(flipId);
            if (flip == null)
            {
                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("Flip not found.").AsEphemeral(true));
                return;
            }

            if (!usersService.TryGetUser(flip.Identifier, out var user) || user == null)
            {
                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("User not found.").AsEphemeral(true));
                return;
            }

            // Only the creator of the flip may interact with its buttons
            if (e.User == null || e.User.Id.ToString() != flip.Identifier)
            {
                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("This coinflip doesn't belong to you.")
                        .AsEphemeral(true));
                return;
            }

            // Handle rematch-style buttons first: they each create a brand new coinflip row
            if (string.Equals(action, "rm", StringComparison.OrdinalIgnoreCase)
                || string.Equals(action, "half", StringComparison.OrdinalIgnoreCase)
                || string.Equals(action, "x2", StringComparison.OrdinalIgnoreCase)
                || string.Equals(action, "max", StringComparison.OrdinalIgnoreCase))
            {
                long baseAmountK = flip.AmountK;

                // Refresh user to get latest balance
                usersService.TryGetUser(user.Identifier, out user);
                if (user == null)
                {
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("User not found.").AsEphemeral(true));
                    return;
                }

                long newAmountK = baseAmountK;
                if (string.Equals(action, "half", StringComparison.OrdinalIgnoreCase))
                {
                    newAmountK = Math.Max(1, baseAmountK / 2);
                }
                else if (string.Equals(action, "x2", StringComparison.OrdinalIgnoreCase))
                {
                    newAmountK = baseAmountK * 2;
                }
                else if (string.Equals(action, "max", StringComparison.OrdinalIgnoreCase))
                {
                    newAmountK = user.Balance;
                }

                if (newAmountK <= 0)
                {
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("Invalid rematch amount.").AsEphemeral(true));
                    return;
                }

                if (user.Balance < newAmountK)
                {
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("You don't have enough balance for this rematch.").AsEphemeral(true));
                    return;
                }

                if (!usersService.RemoveBalance(user.Identifier, newAmountK))
                {
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("Failed to lock balance for this rematch. Please try again.").AsEphemeral(true));
                    return;
                }

                var newFlip = coinflipsService.CreateCoinflip(user, newAmountK);
                if (newFlip == null)
                {
                    // Refund if we couldn't create a new flip row
                    usersService.AddBalance(user.Identifier, newAmountK);
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("Failed to create rematch flip. Please try again later.").AsEphemeral(true));
                    return;
                }

                var prettyAmount = GpFormatter.Format(newFlip.AmountK);
                var embedRematch = new DiscordEmbedBuilder()
                    .WithTitle("One, two, three")
                    .WithDescription("We are gonna see...\n\nWhat's it gonna be?")
                    .AddField("Bet", prettyAmount, true)
                    .WithColor(DiscordColor.Gold)
                    .WithThumbnail("https://i.imgur.com/W6mx4qd.gif")
                    .WithFooter("Prestige Bets")
                    .WithTimestamp(DateTimeOffset.UtcNow);

                var headsButtonRematch = new DiscordButtonComponent(
                    ButtonStyle.Success,
                    $"cf_heads_{newFlip.Id}",
                    "Heads",
                    emoji: new DiscordComponentEmoji(DiscordIds.CoinflipHeadsEmojiId));

                var tailsButtonRematch = new DiscordButtonComponent(
                    ButtonStyle.Primary,
                    $"cf_tails_{newFlip.Id}",
                    "Tails",
                    emoji: new DiscordComponentEmoji(DiscordIds.CoinflipTailsEmojiId));

                var exitButtonRematch = new DiscordButtonComponent(
                    ButtonStyle.Danger,
                    $"cf_exit_{newFlip.Id}",
                    "Exit");

                // Disable buttons on the old result/rematch message
                var updateBuilder = new DiscordInteractionResponseBuilder();
                if (e.Message.Embeds.Count > 0)
                {
                    updateBuilder.AddEmbed(e.Message.Embeds[0]);
                }
                updateBuilder.ClearComponents();

                await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, updateBuilder);

                var channelForNew = await client.GetChannelAsync(e.Channel.Id);
                var newMessage = await channelForNew.SendMessageAsync(new DiscordMessageBuilder()
                    .AddEmbed(embedRematch)
                    .AddComponents(headsButtonRematch, tailsButtonRematch, exitButtonRematch));

                coinflipsService.UpdateCoinflipOutcome(newFlip.Id, choseHeads: false, resultHeads: false, status: CoinflipStatus.Pending, messageId: newMessage.Id, channelId: newMessage.Channel.Id);
                return;
            }

            // Exit on the original request (the message with Heads / Tails / Exit)
            // This must ALWAYS refund the locked stake for a pending flip
            if (string.Equals(action, "exit", StringComparison.OrdinalIgnoreCase) && (flip.Status == CoinflipStatus.Pending))
            {
                if (flip.AmountK > 0)
                {
                    // Give back the reserved amount
                    usersService.AddBalance(user.Identifier, flip.AmountK);

                    // Mark this flip as cancelled so any later exits won't refund again
                    coinflipsService.UpdateCoinflipOutcome(
                        flip.Id,
                        flip.ChoseHeads ?? false,
                        flip.ResultHeads ?? false,
                        CoinflipStatus.Cancelled,
                        flip.MessageId ?? 0,
                        flip.ChannelId ?? 0);
                }

                if (flip.ChannelId.HasValue && flip.MessageId.HasValue)
                {
                    var channel = await client.GetChannelAsync(flip.ChannelId.Value);
                    var originalMessage = await channel.GetMessageAsync(flip.MessageId.Value);

                    await originalMessage.ModifyAsync(mb =>
                    {
                        mb.Embed = originalMessage.Embeds.Count > 0 ? originalMessage.Embeds[0] : null;
                        mb.ClearComponents();
                    });
                }

                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("Game cancelled and your bet was refunded."));
                return;
            }

            // Exit on the result/rematch message: just close those buttons, no money back
            // This is the message that shows win/lose + RM / 1/2 / X2 / MAX / Exit
            if (string.Equals(action, "exit", StringComparison.OrdinalIgnoreCase))
            {
                var builder = new DiscordInteractionResponseBuilder();
                if (e.Message.Embeds.Count > 0)
                {
                    builder.AddEmbed(e.Message.Embeds[0]);
                }
                builder.ClearComponents();

                await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, builder);
                return;
            }

            // From here on, we handle the initial Heads/Tails choice (one bet per command)

            var betAmountK = flip.AmountK;

            bool choseHeads;
            if (string.Equals(action, "heads", StringComparison.OrdinalIgnoreCase))
            {
                choseHeads = true;
            }
            else if (string.Equals(action, "tails", StringComparison.OrdinalIgnoreCase))
            {
                choseHeads = false;
            }
            else
            {
                return;
            }

            // At this point the stake is already locked in CoinflipCommand; do NOT remove balance again

            // Use cryptographically strong RNG for a fair 50/50 outcome
            int roll = RandomNumberGenerator.GetInt32(0, 100); // 0–99
            var resultHeads = roll < 50; // 50/50
            var win = choseHeads == resultHeads;

            long feeK = 0;
            long payoutK = 0;
            long totalWinK = 0;

            if (win)
            {
                // Stake was already removed up-front.
                // On win, tax 10% of the winning amount and give back
                // the original stake plus net profit (same as stakes).
                feeK = (long)Math.Round(betAmountK * 0.10m, MidpointRounding.AwayFromZero);
                payoutK = betAmountK - feeK;

                if (payoutK < 0)
                {
                    payoutK = 0;
                }

                totalWinK = betAmountK + payoutK;

                // Total returned to user: original stake + net profit.
                usersService.AddBalance(user.Identifier, totalWinK);
            }

            // Persist updated flip baseline
            coinflipsService.UpdateCoinflipOutcome(flip.Id, choseHeads, resultHeads, CoinflipStatus.Finished, flip.MessageId ?? 0, flip.ChannelId ?? 0);

            usersService.TryGetUser(user.Identifier, out user);
            var balancePretty = user != null ? GpFormatter.Format(user.Balance) : "-";
            var amountPretty = GpFormatter.Format(betAmountK);
            var totalWinPretty = win && totalWinK > 0 ? GpFormatter.Format(totalWinK) : amountPretty;

            var title = win ? $"Coinflip won #{flip.Id}" : $"Coinflip lost #{flip.Id}";
            var description = win
                ? "RNGesus is with you!"
                : "Haha, tough luck.";

            var body = win
                ? $"You ~~scammed~~ won {totalWinPretty} shiny coins, which I regret losing :(\nYour gold bag holds now {balancePretty}."
                : $"You lost {amountPretty} shiny coins, I bet that hurt your pocket ;)\nYour gold bag holds now {balancePretty}.";

            var thumbnailUrl = win
                ? (choseHeads ? "https://i.imgur.com/xud6Yuc.gif" : "https://i.imgur.com/jSIsAPV.gif")
                : (choseHeads ? "https://i.imgur.com/E0T9ZXM.gif" : "https://i.imgur.com/COKLFCI.gif");

            var embed = new DiscordEmbedBuilder()
                .WithTitle(title)
                .WithDescription($"{description}\n\n{body}")
                .WithColor(win ? DiscordColor.SpringGreen : DiscordColor.Red)
                .WithThumbnail(thumbnailUrl)
                .WithFooter("Prestige Bets")
                .WithTimestamp(DateTimeOffset.UtcNow);

            var rematchRow = new DiscordComponent[]
            {
                new DiscordButtonComponent(ButtonStyle.Success,   $"cf_rm_{flip.Id}",   "RM",  emoji: new DiscordComponentEmoji(DiscordIds.CoinflipRmEmojiId)),
                new DiscordButtonComponent(ButtonStyle.Secondary, $"cf_half_{flip.Id}", "1/2", emoji: new DiscordComponentEmoji(DiscordIds.CoinflipHalfEmojiId)),
                new DiscordButtonComponent(ButtonStyle.Primary,   $"cf_x2_{flip.Id}",   "X2",  emoji: new DiscordComponentEmoji(DiscordIds.CoinflipX2EmojiId)),
                new DiscordButtonComponent(ButtonStyle.Secondary, $"cf_max_{flip.Id}",  "MAX", emoji: new DiscordComponentEmoji(DiscordIds.CoinflipMaxEmojiId)),
                new DiscordButtonComponent(ButtonStyle.Danger,    $"cf_exit_{flip.Id}", "Exit")
            };

            // Disable buttons on the original request message
            if (flip.ChannelId.HasValue && flip.MessageId.HasValue)
            {
                var channel = await client.GetChannelAsync(flip.ChannelId.Value);
                var originalMessage = await channel.GetMessageAsync(flip.MessageId.Value);

                await originalMessage.ModifyAsync(mb =>
                {
                    mb.Embed = originalMessage.Embeds.Count > 0 ? originalMessage.Embeds[0] : null;
                    mb.ClearComponents();
                });
            }

            // Send result as a new message (no Heads/Tails buttons here)
            await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .AddEmbed(embed)
                    .AddComponents(rematchRow));
        }
    }
}
