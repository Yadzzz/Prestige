using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Server.Client.Blackjack;
using Server.Client.Coinflips;
using Server.Client.Stakes;
using Server.Client.Utils;
using Server.Infrastructure;

namespace Server.Communication.Discord.Commands
{
    public class CancelCommand : ModuleBase<SocketCommandContext>
    {
        [Command("cancel")]
        public async Task Cancel()
        {
            var env = ServerEnvironment.GetServerEnvironment();
            var serverManager = env.ServerManager;
            var usersService = serverManager.UsersService;
            var coinflipsService = serverManager.CoinflipsService;
            var stakesService = serverManager.StakesService;

            var displayName = (Context.User as SocketGuildUser)?.DisplayName ?? Context.User.Username;
            var user = await usersService.EnsureUserAsync(Context.User.Id.ToString(), Context.User.Username, displayName);
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
                // Refund
                await usersService.AddBalanceAsync(user.Identifier, flip.AmountK);
                totalRefundedK += flip.AmountK;

                // Update Status
                await coinflipsService.UpdateCoinflipOutcomeAsync(
                    flip.Id,
                    flip.ChoseHeads ?? false,
                    flip.ResultHeads ?? false,
                    CoinflipStatus.Cancelled,
                    flip.MessageId ?? 0,
                    flip.ChannelId ?? 0);

                // Update Discord Message (Disable buttons)
                if (flip.ChannelId.HasValue && flip.MessageId.HasValue)
                {
                    try
                    {
                        var channel = Context.Client.GetChannel(flip.ChannelId.Value) as IMessageChannel;
                        if (channel != null)
                        {
                            var msg = await channel.GetMessageAsync(flip.MessageId.Value) as IUserMessage;
                            if (msg != null)
                            {
                                await msg.ModifyAsync(mb =>
                                {
                                    // Keep existing embeds, remove components
                                    mb.Components = new ComponentBuilder().Build(); 
                                });
                            }
                        }
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
                    if (flip.ChannelId.HasValue && flip.MessageId.HasValue && Context.Guild != null)
                    {
                        var url = $"https://discord.com/channels/{Context.Guild.Id}/{flip.ChannelId}/{flip.MessageId}";
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
                        var channel = Context.Client.GetChannel(stake.UserChannelId.Value) as IMessageChannel;
                        if (channel != null)
                        {
                            var msg = await channel.GetMessageAsync(stake.UserMessageId.Value) as IUserMessage;
                            if (msg != null)
                            {
                                await msg.ModifyAsync(mb =>
                                {
                                    mb.Components = new ComponentBuilder().Build(); // Remove all buttons
                                });
                            }
                        }
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
                        var channel = Context.Client.GetChannel(stake.StaffChannelId.Value) as IMessageChannel;
                        if (channel != null)
                        {
                            var msg = await channel.GetMessageAsync(stake.StaffMessageId.Value) as IUserMessage;
                            if (msg != null)
                            {
                                var oldEmbed = msg.Embeds.FirstOrDefault();
                                var staffEmbed = new EmbedBuilder();
                                if (oldEmbed != null)
                                {
                                    staffEmbed.WithTitle(oldEmbed.Title)
                                              .WithDescription($"User: {displayName} – the **{GpFormatter.Format(stake.AmountK)}** stake was cancelled by user.")
                                              .WithColor(Color.Orange);
                                    // Copy other fields if needed, but description is overwritten
                                }
                                else
                                {
                                    staffEmbed.WithTitle("❌ Stake Cancelled")
                                              .WithDescription($"User: {displayName} – the **{GpFormatter.Format(stake.AmountK)}** stake was cancelled by user.")
                                              .WithColor(Color.Orange);
                                }

                                await msg.ModifyAsync(mb =>
                                {
                                    mb.Embed = staffEmbed.Build();
                                    mb.Components = new ComponentBuilder().Build(); // Remove all buttons
                                });
                            }
                        }
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
                    if (stake.UserChannelId.HasValue && stake.UserMessageId.HasValue && Context.Guild != null)
                    {
                        var url = $"https://discord.com/channels/{Context.Guild.Id}/{stake.UserChannelId}/{stake.UserMessageId}";
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
                        var channel = Context.Client.GetChannel(game.ChannelId.Value) as IMessageChannel;
                        if (channel != null)
                        {
                            var msg = await channel.GetMessageAsync(game.MessageId.Value) as IUserMessage;
                            if (msg != null)
                            {
                                await msg.ModifyAsync(mb =>
                                {
                                    mb.Components = new ComponentBuilder().Build(); // Remove all buttons
                                });
                            }
                        }
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
                    if (game.ChannelId.HasValue && game.MessageId.HasValue && Context.Guild != null)
                    {
                        var url = $"https://discord.com/channels/{Context.Guild.Id}/{game.ChannelId}/{game.MessageId}";
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
                var embed = new EmbedBuilder()
                    .WithTitle($"Games Cancelled ({cancelledCount})")
                    .WithDescription("This is a short-hand view of the cancelled games.")
                    .WithColor(Color.Gold)
                    .WithThumbnailUrl("https://i.imgur.com/HwpWAYS.gif")
                    .AddField("Game", sbGame.ToString(), true)
                    .AddField("Amount", sbAmount.ToString(), true)
                    .AddField("Panel", sbPanel.ToString(), true);

                await ReplyAsync(embed: embed.Build());
            }
            else
            {
                await ReplyAsync("You have no active bets to cancel.");
            }
        }
    }
}
