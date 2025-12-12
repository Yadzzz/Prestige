using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
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
        public static async Task Handle(DiscordSocketClient client, SocketMessageComponent component)
        {
            if (RateLimiter.IsRateLimited(component.User.Id))
            {
                await component.RespondAsync("You're doing that too fast.", ephemeral: true);
                return;
            }

            var parts = component.Data.CustomId.Split('_');
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
                await component.RespondAsync("Flip not found.", ephemeral: true);
                return;
            }

            var user = await usersService.GetUserAsync(flip.Identifier);
            if (user == null)
            {
                await component.RespondAsync("User not found.", ephemeral: true);
                return;
            }

            // Only the creator of the flip may interact with its buttons
            if (component.User == null || component.User.Id.ToString() != flip.Identifier)
            {
                await component.RespondAsync("This coinflip doesn't belong to you.", ephemeral: true);
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
                await component.RespondAsync("This coinflip is already finished.", ephemeral: true);
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
                    await component.RespondAsync("User not found.", ephemeral: true);
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
                    await component.RespondAsync("Invalid rematch amount.", ephemeral: true);
                    return;
                }

                if (user.Balance < newAmountK)
                {
                    await component.RespondAsync("You don't have enough balance for this rematch.", ephemeral: true);
                    return;
                }

                if (!await usersService.RemoveBalanceAsync(user.Identifier, newAmountK))
                {
                    await component.RespondAsync("Failed to lock balance for this rematch. Please try again.", ephemeral: true);
                    return;
                }

                var newFlip = await coinflipsService.CreateCoinflipAsync(user, newAmountK);
                if (newFlip == null)
                {
                    // Refund if we couldn't create a new flip row
                    await usersService.AddBalanceAsync(user.Identifier, newAmountK);
                    await component.RespondAsync("Failed to create rematch flip. Please try again later.", ephemeral: true);
                    return;
                }

                var prettyAmount = GpFormatter.Format(newFlip.AmountK);
                var embedRematch = new EmbedBuilder()
                    .WithTitle("One, two, three")
                    .WithDescription("**We are gonna see...**\n\n*What's it gonna be?*")
                    .WithColor(Color.Gold)
                    .WithThumbnailUrl("https://i.imgur.com/W6mx4qd.gif")
                    .WithFooter(ServerConfiguration.ServerName)
                    .WithCurrentTimestamp();

                var headsButtonRematch = new ButtonBuilder(" ", $"cf_heads_{newFlip.Id}", ButtonStyle.Secondary, emote: new Emote(DiscordIds.CoinflipHeadsEmojiId, "heads", false));
                var tailsButtonRematch = new ButtonBuilder(" ", $"cf_tails_{newFlip.Id}", ButtonStyle.Secondary, emote: new Emote(DiscordIds.CoinflipTailsEmojiId, "tails", false));
                var exitButtonRematch = new ButtonBuilder("Refund", $"cf_exit_{newFlip.Id}", ButtonStyle.Secondary, emote: new Emote(DiscordIds.CoinflipExitEmojiId, "exit", false));

                // Disable buttons on the old result/rematch message but keep them visible
                var disabledRmButton = new ButtonBuilder("RM", $"cf_rm_{flip.Id}", string.Equals(action, "rm", StringComparison.OrdinalIgnoreCase) ? ButtonStyle.Success : ButtonStyle.Secondary, emote: new Emote(DiscordIds.CoinflipRmEmojiId, "rm", false)).WithDisabled(true);
                var disabledHalfButton = new ButtonBuilder("1/2", $"cf_half_{flip.Id}", string.Equals(action, "half", StringComparison.OrdinalIgnoreCase) ? ButtonStyle.Success : ButtonStyle.Secondary, emote: new Emote(DiscordIds.CoinflipHalfEmojiId, "half", false)).WithDisabled(true);
                var disabledX2Button = new ButtonBuilder("X2", $"cf_x2_{flip.Id}", string.Equals(action, "x2", StringComparison.OrdinalIgnoreCase) ? ButtonStyle.Success : ButtonStyle.Secondary, emote: new Emote(DiscordIds.CoinflipX2EmojiId, "x2", false)).WithDisabled(true);
                var disabledMaxButton = new ButtonBuilder("MAX", $"cf_max_{flip.Id}", string.Equals(action, "max", StringComparison.OrdinalIgnoreCase) ? ButtonStyle.Success : ButtonStyle.Secondary, emote: new Emote(DiscordIds.CoinflipMaxEmojiId, "max", false)).WithDisabled(true);

                await component.UpdateAsync(msg =>
                {
                    // Keep existing embeds
                    // msg.Embeds is read-only, but we can set msg.Embeds = ...
                    // But UpdateAsync delegate modifies MessageProperties.
                    // We don't need to set Embeds if we don't want to change them, but we want to keep them.
                    // Actually, if we don't set Embeds, they are kept.
                    // But we want to update components.
                    msg.Components = new ComponentBuilder()
                        .WithButton(disabledHalfButton)
                        .WithButton(disabledRmButton)
                        .WithButton(disabledX2Button)
                        .WithButton(disabledMaxButton)
                        .Build();
                });

                var channelForNew = client.GetChannel(component.Channel.Id) as IMessageChannel;
                if (channelForNew != null)
                {
                    try
                    {
                        var newMessage = await channelForNew.SendMessageAsync(embed: embedRematch.Build(), components: new ComponentBuilder()
                            .WithButton(headsButtonRematch)
                            .WithButton(tailsButtonRematch)
                            .WithButton(exitButtonRematch)
                            .Build());

                        await coinflipsService.UpdateCoinflipOutcomeAsync(newFlip.Id, choseHeads: false, resultHeads: false, status: CoinflipStatus.Pending, messageId: newMessage.Id, channelId: newMessage.Channel.Id);
                    }
                    catch (Exception ex)
                    {
                        // Failed to send message, refund and cancel to prevent ghost flips
                        env.ServerManager.LoggerManager.LogError($"[CoinflipButtonHandler] Failed to send rematch message: {ex}");
                        
                        await usersService.AddBalanceAsync(user.Identifier, newAmountK);
                        await coinflipsService.UpdateCoinflipOutcomeAsync(newFlip.Id, false, false, CoinflipStatus.Cancelled, 0, 0);
                        
                        try 
                        {
                            await component.FollowupAsync("Failed to start rematch. Your bet has been refunded.", ephemeral: true);
                        }
                        catch { /* Ignore if followup fails */ }
                    }
                }
                return;
            }

            // Exit on the original request (the message with Heads / Tails / Exit)
            // This must ALWAYS refund the locked stake for a pending flip
            if (string.Equals(action, "exit", StringComparison.OrdinalIgnoreCase) && (flip.Status == CoinflipStatus.Pending))
            {
                if (flip.AmountK > 0)
                {
                    // Give back the reserved amount
                    await usersService.AddBalanceAsync(user.Identifier, flip.AmountK);

                    // Mark this flip as cancelled so any later exits won't refund again
                    await coinflipsService.UpdateCoinflipOutcomeAsync(
                        flip.Id,
                        flip.ChoseHeads ?? false,
                        flip.ResultHeads ?? false,
                        CoinflipStatus.Cancelled,
                        flip.MessageId ?? 0,
                        flip.ChannelId ?? 0);
                }

                if (flip.ChannelId.HasValue && flip.MessageId.HasValue)
                {
                    var channel = client.GetChannel(flip.ChannelId.Value) as IMessageChannel;
                    if (channel != null)
                    {
                        var originalMessage = await channel.GetMessageAsync(flip.MessageId.Value) as IUserMessage;
                        if (originalMessage != null)
                        {
                            await originalMessage.ModifyAsync(mb =>
                            {
                                mb.Components = new ComponentBuilder().Build();
                            });
                        }
                    }
                }

                await component.RespondAsync("Game cancelled and your bet was refunded.");
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

            // Persist updated flip baseline FIRST.
            // If this fails, we must abort to prevent "free rolls" (playing without committing result).
            if (!await coinflipsService.UpdateCoinflipOutcomeAsync(flip.Id, choseHeads, resultHeads, CoinflipStatus.Finished, flip.MessageId ?? 0, flip.ChannelId ?? 0))
            {
                env.ServerManager.LoggerManager.LogError($"[CoinflipButtonHandler] Failed to update outcome for flip {flip.Id}. User: {user.Identifier}. Aborting to prevent exploit.");
                await component.RespondAsync("Failed to process game result. Please try again.", ephemeral: true);
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

            var rematchRow = new ComponentBuilder()
                .WithButton("1/2", $"cf_half_{flip.Id}", ButtonStyle.Secondary, new Emote(DiscordIds.CoinflipHalfEmojiId, "half", false))
                .WithButton("RM", $"cf_rm_{flip.Id}", ButtonStyle.Secondary, new Emote(DiscordIds.CoinflipRmEmojiId, "rm", false))
                .WithButton("X2", $"cf_x2_{flip.Id}", ButtonStyle.Secondary, new Emote(DiscordIds.CoinflipX2EmojiId, "x2", false))
                .WithButton("MAX", $"cf_max_{flip.Id}", ButtonStyle.Secondary, new Emote(DiscordIds.CoinflipMaxEmojiId, "max", false))
                .Build();

            // Replace the original request message with the result embed + rematch buttons
            if (flip.ChannelId.HasValue && flip.MessageId.HasValue)
            {
                try
                {
                    var channel = client.GetChannel(flip.ChannelId.Value) as IMessageChannel;
                    if (channel != null)
                    {
                        var originalMessage = await channel.GetMessageAsync(flip.MessageId.Value) as IUserMessage;
                        if (originalMessage != null)
                        {
                            await originalMessage.ModifyAsync(mb =>
                            {
                                mb.Embed = embed.Build();
                                mb.Components = rematchRow;
                            });

                            // Acknowledge the interaction by updating the same message (no new message sent)
                            // Since we already modified the message, we can just defer or update again.
                            // But UpdateAsync is cleaner.
                            await component.UpdateAsync(msg =>
                            {
                                msg.Embed = embed.Build();
                                msg.Components = rematchRow;
                            });
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    env.ServerManager.LoggerManager.LogError($"[CoinflipButtonHandler] Failed to modify original message for flip {flip.Id}. Error: {ex.Message}");
                }
            }

            // Fallback
            await component.RespondAsync(embed: embed.Build(), components: rematchRow);
        }

        private static EmbedBuilder BuildResultEmbed(User? user, Coinflip flip, long betAmountK, long totalWinK, long preFlipBalanceK, bool win, bool choseHeads, bool resultHeads)
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
                description = "OMG! It's a max win!";
            }
            else if (win && isBigBet)
            {
                description = "HUGE! Amazing big win!";
            }
            else if (win)
            {
                description = "RNGesus is with you!";
            }
            else
            {
                description = "Haha, tough luck...";
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
                suffix = "Will you max success again?";
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

            var color = Color.Red;
            if (win)
            {
                if (isAllInWin)
                {
                    color = new Color(79, 174, 221); // light blue #4FAEDD
                }
                else if (isBigBet)
                {
                    color = new Color(170, 102, 255); // purple #AA66FF
                }
                else
                {
                    color = Color.Green;
                }
            }

            return new EmbedBuilder()
                .WithTitle(title)
                .WithDescription($"**{description}**\n\n{body}")
                .WithColor(color)
                .WithThumbnailUrl(thumbnailUrl)
                .WithFooter(ServerConfiguration.ServerName)
                .WithCurrentTimestamp();
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
