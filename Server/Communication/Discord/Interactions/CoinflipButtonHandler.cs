using System;
using System.Diagnostics;
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
using Server.Infrastructure.Logger;
using Server.Communication.Discord.Commands;

namespace Server.Communication.Discord.Interactions
{
    public static class CoinflipButtonHandler
    {
        public static async Task Handle(DiscordClient client, ComponentInteractionCreatedEventArgs e)
        {
            if (RateLimiter.IsRateLimited(e.User.Id))
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("You're doing that too fast.").AsEphemeral(true));
                return;
            }

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

            var flip = await coinflipsService.GetCoinflipByIdAsync(flipId);
            if (flip == null)
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("Flip not found.").AsEphemeral(true));
                return;
            }

            var user = await usersService.GetUserAsync(flip.Identifier);
            if (user == null)
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("User not found.").AsEphemeral(true));
                return;
            }

            // Only the creator of the flip may interact with its buttons
            if (e.User == null || e.User.Id.ToString() != flip.Identifier)
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("This coinflip doesn't belong to you.")
                        .AsEphemeral(true));
                return;
            }

            // Check if the flip is already finished (to prevent double-playing)
            // Rematch buttons (rm, half, x2, max) are allowed on finished flips because they create NEW flips.
            // But "heads", "tails", and "exit" (refund) should only work on PENDING flips.
            bool isRematchAction = string.Equals(action, "rm", StringComparison.OrdinalIgnoreCase)
                                   || string.Equals(action, "half", StringComparison.OrdinalIgnoreCase)
                                   || string.Equals(action, "x2", StringComparison.OrdinalIgnoreCase)
                                   || string.Equals(action, "max", StringComparison.OrdinalIgnoreCase);

            if (!isRematchAction && flip.Status != CoinflipStatus.Pending)
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("This coinflip is already finished.")
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
                user = await usersService.GetUserAsync(user.Identifier);
                if (user == null)
                {
                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
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
                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("Invalid rematch amount.").AsEphemeral(true));
                    return;
                }

                if (user.Balance < newAmountK)
                {
                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("You don't have enough balance for this rematch.").AsEphemeral(true));
                    return;
                }

                if (!await usersService.RemoveBalanceAsync(user.Identifier, newAmountK, isWager: true))
                {
                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("Failed to lock balance for this rematch. Please try again.").AsEphemeral(true));
                    return;
                }

                var newFlip = await coinflipsService.CreateCoinflipAsync(user, newAmountK);
                if (newFlip == null)
                {
                    // Refund if we couldn't create a new flip row
                    await usersService.AddBalanceAsync(user.Identifier, newAmountK);
                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("Failed to create rematch flip. Please try again later.").AsEphemeral(true));
                    return;
                }

                var prettyAmount = GpFormatter.Format(newFlip.AmountK);
                var embedRematch = new DiscordEmbedBuilder()
                    .WithTitle("One, two, three")
                    .WithDescription("**We are gonna see...**\n\n*What's it gonna be?*")
                    .WithColor(DiscordColor.Gold)
                    .WithThumbnail("https://i.imgur.com/W6mx4qd.gif")
                    .WithFooter(ServerConfiguration.ServerName)
                    .WithTimestamp(DateTimeOffset.UtcNow);

                var headsButtonRematch = new DiscordButtonComponent(
                    DiscordButtonStyle.Secondary,
                    $"cf_heads_{newFlip.Id}",
                    " ",
                    emoji: new DiscordComponentEmoji(DiscordIds.CoinflipHeadsEmojiId));

                var tailsButtonRematch = new DiscordButtonComponent(
                    DiscordButtonStyle.Secondary,
                    $"cf_tails_{newFlip.Id}",
                    " ",
                    emoji: new DiscordComponentEmoji(DiscordIds.CoinflipTailsEmojiId));

                var exitButtonRematch = new DiscordButtonComponent(
                    DiscordButtonStyle.Secondary,
                    $"cf_exit_{newFlip.Id}",
                    "Refund",
                    emoji: new DiscordComponentEmoji(DiscordIds.CoinflipExitEmojiId));

                // Disable buttons on the old result/rematch message but keep them visible
                var updateBuilder = new DiscordInteractionResponseBuilder();
                if (e.Message.Embeds.Count > 0)
                {
                    updateBuilder.AddEmbed(e.Message.Embeds[0]);
                }

                var disabledRmButton = new DiscordButtonComponent(
                    string.Equals(action, "rm", StringComparison.OrdinalIgnoreCase) ? DiscordButtonStyle.Success : DiscordButtonStyle.Secondary,
                    $"cf_rm_{flip.Id}",
                    "RM",
                    true,
                    emoji: new DiscordComponentEmoji(DiscordIds.CoinflipRmEmojiId));

                var disabledHalfButton = new DiscordButtonComponent(
                    string.Equals(action, "half", StringComparison.OrdinalIgnoreCase) ? DiscordButtonStyle.Success : DiscordButtonStyle.Secondary,
                    $"cf_half_{flip.Id}",
                    "1/2",
                    true,
                    emoji: new DiscordComponentEmoji(DiscordIds.CoinflipHalfEmojiId));

                var disabledX2Button = new DiscordButtonComponent(
                    string.Equals(action, "x2", StringComparison.OrdinalIgnoreCase) ? DiscordButtonStyle.Success : DiscordButtonStyle.Secondary,
                    $"cf_x2_{flip.Id}",
                    "X2",
                    true,
                    emoji: new DiscordComponentEmoji(DiscordIds.CoinflipX2EmojiId));

                var disabledMaxButton = new DiscordButtonComponent(
                    string.Equals(action, "max", StringComparison.OrdinalIgnoreCase) ? DiscordButtonStyle.Success : DiscordButtonStyle.Secondary,
                    $"cf_max_{flip.Id}",
                    "MAX",
                    true,
                    emoji: new DiscordComponentEmoji(DiscordIds.CoinflipMaxEmojiId));

                /*
                var disabledExitButton = new DiscordButtonComponent(
                    string.Equals(action, "exit", StringComparison.OrdinalIgnoreCase) ? DiscordButtonStyle.Danger : DiscordButtonStyle.Secondary,
                    $"cf_exit_{flip.Id}",
                    "Refund",
                    true,
                    emoji: new DiscordComponentEmoji(DiscordIds.CoinflipExitEmojiId));
                */

                updateBuilder.ClearComponents();
                updateBuilder.AddActionRowComponent(new DiscordActionRowComponent(new[] { disabledHalfButton, disabledRmButton, disabledX2Button, disabledMaxButton }));

                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage, updateBuilder);

                var channelForNew = await client.GetChannelAsync(e.Channel.Id);
                var newMessage = await channelForNew.SendMessageAsync(new DiscordMessageBuilder()
                    .AddEmbed(embedRematch)
                    .AddActionRowComponent(new DiscordActionRowComponent(new[] { headsButtonRematch, tailsButtonRematch, exitButtonRematch })));

                await coinflipsService.UpdateCoinflipOutcomeAsync(newFlip.Id, choseHeads: false, resultHeads: false, status: CoinflipStatus.Pending, messageId: newMessage.Id, channelId: channelForNew.Id, expectedStatus: CoinflipStatus.Pending);
                return;
            }

            // Exit on the original request (the message with Heads / Tails / Exit)
            // This must ALWAYS refund the locked stake for a pending flip
            if (string.Equals(action, "exit", StringComparison.OrdinalIgnoreCase) && (flip.Status == CoinflipStatus.Pending))
            {
                // Try to mark as cancelled first to prevent race conditions
                var success = await coinflipsService.UpdateCoinflipOutcomeAsync(
                    flip.Id,
                    flip.ChoseHeads ?? false,
                    flip.ResultHeads ?? false,
                    CoinflipStatus.Cancelled,
                    flip.MessageId ?? 0,
                    flip.ChannelId ?? 0,
                    expectedStatus: CoinflipStatus.Pending);

                if (success)
                {
                    if (flip.AmountK > 0)
                    {
                        // Give back the reserved amount
                        await usersService.AddBalanceAsync(user.Identifier, flip.AmountK);
                    }

                    if (flip.ChannelId.HasValue && flip.MessageId.HasValue)
                    {
                        var channel = await client.GetChannelAsync(flip.ChannelId.Value);
                        var originalMessage = await channel.GetMessageAsync(flip.MessageId.Value);

                        await originalMessage.ModifyAsync(mb =>
                        {
                            mb.ClearEmbeds();
                            if (originalMessage.Embeds.Count > 0)
                            {
                                mb.AddEmbed(originalMessage.Embeds[0]);
                            }
                            mb.ClearComponents();
                        });
                    }

                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("Game cancelled and your bet was refunded."));
                }
                else
                {
                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("Game already finished or cancelled.").AsEphemeral(true));
                }
                return;
            }

            // Exit on the result/rematch message: just close those buttons, no money back
            // This is the message that shows win/lose + RM / 1/2 / X2 / MAX / Exit
            /*
            if (string.Equals(action, "exit", StringComparison.OrdinalIgnoreCase))
            {
                var builder = new DiscordInteractionResponseBuilder();
                if (e.Message.Embeds.Count > 0)
                {
                    builder.AddEmbed(e.Message.Embeds[0]);
                }
                builder.ClearComponents();

                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage, builder);
                return;
            }
            */

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

            /* 
             * [DOCS - DO NOT REMOVE]
             * The following line defers the interaction to prevent "Interaction Failed" errors if processing takes >3 seconds.
             * This adds ONE extra API call (Defer -> Edit).
             * 
             * TO REVERT TO SINGLE CALL (Faster, but risks timeout errors):
             * 1. Remove the 'await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.DeferredMessageUpdate);' line below.
             * 2. Change 'EditOriginalResponseAsync' at the bottom to 'CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage, ...)'
             * 3. Change 'CreateFollowupMessageAsync' in the fallback to 'CreateResponseAsync'
             */
            // Defer to prevent timeout
            await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.DeferredMessageUpdate);

            // At this point the stake is already locked in CoinflipCommand; do NOT remove balance again

            // Use cryptographically strong RNG for a fair 50/50 outcome
            int roll = RandomNumberGenerator.GetInt32(0, 100); // 0–99
            var resultHeads = roll < 50; // 50/50
            var win = choseHeads == resultHeads;

            // Persist updated flip baseline FIRST.
            // If this fails, we must abort to prevent "free rolls" (playing without committing result).
            if (!await coinflipsService.UpdateCoinflipOutcomeAsync(flip.Id, choseHeads, resultHeads, CoinflipStatus.Finished, flip.MessageId ?? 0, flip.ChannelId ?? 0, expectedStatus: CoinflipStatus.Pending))
            {
                env.ServerManager.LoggerManager.LogError($"[CoinflipButtonHandler] Failed to update outcome for flip {flip.Id}. User: {user.Identifier}. Aborting to prevent exploit.");
                await e.Interaction.CreateFollowupMessageAsync(
                    new DiscordFollowupMessageBuilder().WithContent("Failed to process game result. Please try again.").AsEphemeral(true));
                return;
            }

            long feeK = 0;
            long payoutK = 0;
            long totalWinK = 0;

            // Capture balance before resolving the flip so we can
            // correctly detect true all-in wins for visuals.
            long preFlipBalanceK = user.Balance;

            if (win)
            {
                // Stake was already removed up-front.
                // On win, tax 5% of the winning amount and give back
                // the original stake plus net profit.
                feeK = (long)Math.Round(betAmountK * 0.05m, MidpointRounding.AwayFromZero);
                payoutK = betAmountK - feeK;

                if (payoutK < 0)
                {
                    payoutK = 0;
                }

                totalWinK = betAmountK + payoutK;

                // Total returned to user: original stake + net profit.
                await usersService.AddBalanceAsync(user.Identifier, totalWinK);
            }

            // Fire-and-forget live feed entry for coinflip games.
            // Pass the same amount we show to the user.
            try
            {
                env.ServerManager.LiveFeedService?.PublishCoinflip(win && totalWinK > 0 ? totalWinK : betAmountK, win, resultHeads);
            }
            catch
            {
                // Live feed must never affect gameplay
            }

            // Register wager for race (only on completion)
            var raceName = user.DisplayName ?? user.Username;
            await env.ServerManager.RaceService.RegisterWagerAsync(user.Identifier, raceName, betAmountK);

            user = await usersService.GetUserAsync(user.Identifier);
            var embed = BuildResultEmbed(user, flip, betAmountK, totalWinK, preFlipBalanceK, win, choseHeads, resultHeads);

            var rematchRow = new DiscordComponent[]
            {
                new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"cf_half_{flip.Id}", "1/2", emoji: new DiscordComponentEmoji(DiscordIds.CoinflipHalfEmojiId)),
                new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"cf_rm_{flip.Id}",   "RM",  emoji: new DiscordComponentEmoji(DiscordIds.CoinflipRmEmojiId)),
                new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"cf_x2_{flip.Id}",   "X2",  emoji: new DiscordComponentEmoji(DiscordIds.CoinflipX2EmojiId)),
                new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"cf_max_{flip.Id}",  "MAX", emoji: new DiscordComponentEmoji(DiscordIds.CoinflipMaxEmojiId)),
                //new DiscordButtonComponent(DiscordButtonStyle.Danger,    $"cf_exit_{flip.Id}", "Exit", emoji: new DiscordComponentEmoji(DiscordIds.CoinflipExitEmojiId))
            };

            // Replace the original request message with the result embed + rematch buttons
            try
            {
                // Since we deferred, we must use EditOriginalResponseAsync
                var webhookBuilder = new DiscordWebhookBuilder()
                    .AddEmbed(embed)
                    .AddActionRowComponent(new DiscordActionRowComponent(rematchRow));

                await e.Interaction.EditOriginalResponseAsync(webhookBuilder);
                return;
            }
            catch (Exception ex)
            {
                env.ServerManager.LoggerManager.LogError($"[CoinflipButtonHandler] Failed to update message for flip {flip.Id}. Error: {ex.Message}");
                // Fall through to fallback
            }

            // Fallback: if we somehow don't have stored IDs or the original message
            // is gone, respond with a new message showing the result + rematch row.
            await e.Interaction.CreateFollowupMessageAsync(
                new DiscordFollowupMessageBuilder()
                    .AddEmbed(embed)
                    .AddActionRowComponent(new DiscordActionRowComponent(rematchRow)));
        }

        private static DiscordEmbedBuilder BuildResultEmbed(User? user, Coinflip flip, long betAmountK, long totalWinK, long preFlipBalanceK, bool win, bool choseHeads, bool resultHeads)
        {
            var balanceK = user?.Balance ?? 0L;
            var balancePretty = GpFormatter.Format(balanceK);
            var amountPretty = GpFormatter.Format(betAmountK);
            var totalWinPretty = win && totalWinK > 0 ? GpFormatter.Format(totalWinK) : amountPretty;

            // All-in means user had 0 left before this flip resolved.
            bool isAllInWin = win && preFlipBalanceK == 0;
            // Big bet is determined by the total returned amount (stake + winnings)
            long totalReturnedK = win && totalWinK > 0 ? totalWinK : betAmountK;
            bool isBigBet = totalReturnedK >= 1_000_000L; // >= 1B

            var title = win ? $"Coinflip won #{flip.Id}" : $"Coinflip lost #{flip.Id}";

            string description;
            if (win && isAllInWin)
            {
                description = "NO WAY — max win!";
            }
            else if (win && isBigBet)
            {
                description = "MASSIVE — big bet win!";
            }
            else if (win)
            {
                description = "Let’s go — you hit!";
            }
            else
            {
                description = "Ouch… unlucky.";
            }

            var body = win
                ? $"You won `{totalWinPretty}`.\nYour gold bag now holds `{balancePretty}`."
                : $"You lost `{amountPretty}`.\nYour gold bag now holds `{balancePretty}`.";

            string suffix;
            if (!win)
            {
                suffix = "Perhaps next time...";
            }
            else if (isAllInWin)
            {
                suffix = "Think you can do that again?";
            }
            else if (isBigBet)
            {
                suffix = "Just one more?";
            }
            else
            {
                suffix = "Dare to try your luck again?";
            }

            body += $"\n\n*{suffix}*";

            var thumbnailUrl = SelectThumbnailUrl(win, choseHeads, isBigBet, isAllInWin);

            // Color bar logic:
            // - normal win: green
            // - all-in win: light blue (match all-in icon vibe)
            // - big win (1B+): purple
            // - loss: red
            var color = DiscordColor.Red;
            if (win)
            {
                if (isAllInWin)
                {
                    color = new DiscordColor("#4FAEDD"); // light blue
                }
                else if (isBigBet)
                {
                    color = new DiscordColor("#AA66FF"); // purple
                }
                else
                {
                    color = DiscordColor.SpringGreen;
                }
            }

            return new DiscordEmbedBuilder()
                .WithTitle(title)
                .WithDescription($"**{description}**\n\n{body}")
                .WithColor(color)
                .WithThumbnail(thumbnailUrl)
                .WithFooter(ServerConfiguration.ServerName)
                .WithTimestamp(DateTimeOffset.UtcNow);
        }

        private static string SelectThumbnailUrl(bool win, bool choseHeads, bool isBigBet, bool isAllInWin)
        {
            // Stake-like mapping using the provided GIFs.
            if (win)
            {
                if (isAllInWin)
                {
                    // All-in win
                    return choseHeads
                        ? "https://i.imgur.com/umfkRzp.gif"   // all-in heads win
                        : "https://i.imgur.com/YWD0RG4.gif";  // all-in tails win
                }

                if (isBigBet)
                {
                    // Big win >= 1B
                    return choseHeads
                        ? "https://i.imgur.com/snYXA01.gif"   // big heads win
                        : "https://i.imgur.com/EEbdZr1.gif";  // big tails win
                }

                // Regular win under 1B
                return choseHeads
                    ? "https://i.imgur.com/pjbK9Cf.gif"       // small heads win
                    : "https://i.imgur.com/XwVz5Ng.gif";      // small tails win
            }

            // Losses
            // If user chose heads and lost, result was tails -> show tails loss
            // If user chose tails and lost, result was heads -> show heads loss
            return choseHeads
                ? "https://i.imgur.com/q8e2eXR.gif"           // chose heads, lost -> result tails
                : "https://i.imgur.com/r7vmMon.gif";          // chose tails, lost -> result heads
        }
    }
}
