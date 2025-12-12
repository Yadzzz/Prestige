using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Server.Client.Blackjack;
using Server.Client.Coinflips;
using Server.Client.Stakes;
using Server.Client.Utils;
using Server.Infrastructure;

namespace Server.Communication.Discord.Commands
{
    public class CancelCommand : BaseCommandModule
    {
        [Command("cancel")]
        public async Task Cancel(CommandContext ctx)
        {
            var env = ServerEnvironment.GetServerEnvironment();
            var serverManager = env.ServerManager;
            var usersService = serverManager.UsersService;
            var coinflipsService = serverManager.CoinflipsService;
            var stakesService = serverManager.StakesService;

            var user = await usersService.EnsureUserAsync(ctx.User.Id.ToString(), ctx.User.Username, ctx.Member.DisplayName);
            if (user == null)
                return;

            int cancelledCount = 0;
            long totalRefundedK = 0;

            var sbGame = new System.Text.StringBuilder();
            var sbAmount = new System.Text.StringBuilder();
            var sbPanel = new System.Text.StringBuilder();

            // 1. Cancel Pending Coinflips
            var pendingFlips = await coinflipsService.GetPendingCoinflipsByUserIdAsync(user.Id);
            foreach (var flip in pendingFlips)
            {
                // Try to set status to Cancelled first, ensuring it is still Pending
                var success = await coinflipsService.UpdateCoinflipOutcomeAsync(
                    flip.Id,
                    flip.ChoseHeads ?? false,
                    flip.ResultHeads ?? false,
                    CoinflipStatus.Cancelled,
                    flip.MessageId ?? 0,
                    flip.ChannelId ?? 0,
                    expectedStatus: CoinflipStatus.Pending);

                if (!success)
                {
                    // If update failed, it means the flip is no longer Pending (race condition), so skip refund
                    continue;
                }

                // Refund
                await usersService.AddBalanceAsync(user.Identifier, flip.AmountK);
                totalRefundedK += flip.AmountK;

                // Update Discord Message (Disable buttons)
                if (flip.ChannelId.HasValue && flip.MessageId.HasValue)
                {
                    try
                    {
                        var channel = await ctx.Client.GetChannelAsync(flip.ChannelId.Value);
                        var msg = await channel.GetMessageAsync(flip.MessageId.Value);
                        
                        await msg.ModifyAsync(mb =>
                        {
                            mb.ClearEmbeds();
                            if (msg.Embeds.Count > 0) mb.AddEmbed(msg.Embeds[0]);
                            mb.ClearComponents(); // Remove all buttons
                        });
                        await Task.Delay(100); // Rate limit protection
                    }
                    catch
                    {
                        // Message might be deleted, ignore
                    }
                }

                if (sbGame.Length < 950)
                {
                    sbGame.AppendLine("`COINFLIP`");
                    sbAmount.AppendLine($"`{GpFormatter.Format(flip.AmountK)}`");
                    if (flip.ChannelId.HasValue && flip.MessageId.HasValue && ctx.Guild != null)
                    {
                        var url = $"https://discord.com/channels/{ctx.Guild.Id}/{flip.ChannelId}/{flip.MessageId}";
                        sbPanel.AppendLine($"[Click Here]({url})");
                    }
                    else
                    {
                        sbPanel.AppendLine("N/A");
                    }
                }
                else if (!sbGame.ToString().EndsWith("...\r\n"))
                {
                    sbGame.AppendLine("...");
                    sbAmount.AppendLine("...");
                    sbPanel.AppendLine("...");
                }

                cancelledCount++;
            }

            // 2. Cancel Pending Stakes
            var pendingStakes = await stakesService.GetPendingStakesByUserIdAsync(user.Id);
            foreach (var stake in pendingStakes)
            {
                // Refund
                await usersService.AddBalanceAsync(user.Identifier, stake.AmountK);
                totalRefundedK += stake.AmountK;

                // Update Status
                await stakesService.UpdateStakeStatusAsync(stake.Id, StakeStatus.Cancelled);

                // Update User Discord Message (Disable buttons)
                if (stake.UserChannelId.HasValue && stake.UserMessageId.HasValue)
                {
                    try
                    {
                        var channel = await ctx.Client.GetChannelAsync(stake.UserChannelId.Value);
                        var msg = await channel.GetMessageAsync(stake.UserMessageId.Value);

                        await msg.ModifyAsync(mb =>
                        {
                            mb.ClearEmbeds();
                            if (msg.Embeds.Count > 0) mb.AddEmbed(msg.Embeds[0]);
                            mb.ClearComponents(); // Remove all buttons
                        });
                    }
                    catch
                    {
                        // Message might be deleted, ignore
                    }
                }

                // Update Staff Discord Message (Disable buttons)
                if (stake.StaffChannelId.HasValue && stake.StaffMessageId.HasValue)
                {
                    try
                    {
                        var channel = await ctx.Client.GetChannelAsync(stake.StaffChannelId.Value);
                        var msg = await channel.GetMessageAsync(stake.StaffMessageId.Value);

                        var staffEmbed = new DiscordEmbedBuilder(msg.Embeds[0])
                            .WithTitle("❌ Stake Cancelled")
                            .WithDescription($"User: {user.DisplayName} – the **{GpFormatter.Format(stake.AmountK)}** stake was cancelled by user.")
                            .WithColor(DiscordColor.Orange);

                        await msg.ModifyAsync(mb =>
                        {
                            mb.ClearEmbeds();
                            mb.AddEmbed(staffEmbed);
                            mb.ClearComponents(); // Remove all buttons
                        });
                        await Task.Delay(100); // Rate limit protection
                    }
                    catch
                    {
                        // Message might be deleted, ignore
                    }
                }

                if (sbGame.Length < 950)
                {
                    sbGame.AppendLine("`STAKE`");
                    sbAmount.AppendLine($"`{GpFormatter.Format(stake.AmountK)}`");
                    if (stake.UserChannelId.HasValue && stake.UserMessageId.HasValue && ctx.Guild != null)
                    {
                        var url = $"https://discord.com/channels/{ctx.Guild.Id}/{stake.UserChannelId}/{stake.UserMessageId}";
                        sbPanel.AppendLine($"[Click Here]({url})");
                    }
                    else
                    {
                        sbPanel.AppendLine("N/A");
                    }
                }
                else if (!sbGame.ToString().EndsWith("...\r\n"))
                {
                    sbGame.AppendLine("...");
                    sbAmount.AppendLine("...");
                    sbPanel.AppendLine("...");
                }

                cancelledCount++;
            }

            // 3. Cancel Pending Blackjack Games
            var blackjackService = serverManager.BlackjackService;
            var activeGames = await blackjackService.GetActiveGamesByUserIdAsync(user.Id);
            foreach (var game in activeGames)
            {
                // Refund total bet amount (including splits/doubles)
                long totalBet = game.PlayerHands.Sum(h => h.BetAmount);
                if (game.InsuranceTaken)
                {
                    totalBet += game.BetAmount / 2;
                }

                await usersService.AddBalanceAsync(user.Identifier, totalBet);
                totalRefundedK += totalBet;

                // Update Status
                await blackjackService.UpdateGameStatusAsync(game.Id, BlackjackGameStatus.Finished);

                // Update Discord Message (Disable buttons)
                if (game.ChannelId.HasValue && game.MessageId.HasValue)
                {
                    try
                    {
                        var channel = await ctx.Client.GetChannelAsync(game.ChannelId.Value);
                        var msg = await channel.GetMessageAsync(game.MessageId.Value);

                        await msg.ModifyAsync(mb =>
                        {
                            mb.ClearEmbeds();
                            if (msg.Embeds.Count > 0) mb.AddEmbed(msg.Embeds[0]);
                            mb.ClearComponents(); // Remove all buttons
                        });
                        await Task.Delay(100); // Rate limit protection
                    }
                    catch
                    {
                        // Message might be deleted, ignore
                    }
                }

                if (sbGame.Length < 950)
                {
                    sbGame.AppendLine("`BLACKJACK`");
                    sbAmount.AppendLine($"`{GpFormatter.Format(totalBet)}`");
                    if (game.ChannelId.HasValue && game.MessageId.HasValue && ctx.Guild != null)
                    {
                        var url = $"https://discord.com/channels/{ctx.Guild.Id}/{game.ChannelId}/{game.MessageId}";
                        sbPanel.AppendLine($"[Click Here]({url})");
                    }
                    else
                    {
                        sbPanel.AppendLine("N/A");
                    }
                }
                else if (!sbGame.ToString().EndsWith("...\r\n"))
                {
                    sbGame.AppendLine("...");
                    sbAmount.AppendLine("...");
                    sbPanel.AppendLine("...");
                }

                cancelledCount++;
            }

            if (cancelledCount > 0)
            {
                var embed = new DiscordEmbedBuilder()
                    .WithTitle($"Games Cancelled ({cancelledCount})")
                    .WithDescription("This is a short-hand view of the cancelled games.")
                    .WithColor(DiscordColor.Gold)
                    .WithThumbnail("https://i.imgur.com/HwpWAYS.gif")
                    .AddField("Game", sbGame.ToString(), true)
                    .AddField("Amount", sbAmount.ToString(), true)
                    .AddField("Panel", sbPanel.ToString(), true);

                await ctx.RespondAsync(embed);
            }
            else
            {
                await ctx.RespondAsync("You have no active bets to cancel.");
            }
        }
    }
}
