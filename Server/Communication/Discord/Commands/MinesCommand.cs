using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Server.Client.Mines;
using Server.Client.Users;
using Server.Client.Utils;
using Server.Infrastructure;
using Server.Infrastructure.Discord;

namespace Server.Communication.Discord.Commands
{
    public class MinesCommand : BaseCommandModule
    {
        private static readonly TimeSpan RateLimitInterval = TimeSpan.FromSeconds(1);

        [Command("mines")]
        public async Task Mines(CommandContext ctx, string amount = null, int minesCount = 3)
        {
            if (!await DiscordChannelPermissionService.EnforceMinesChannelAsync(ctx))
            {
                return;
            }

            if (RateLimiter.IsRateLimited(ctx.User.Id, "mines", RateLimitInterval))
            {
                await ctx.RespondAsync("You're doing that too fast. Please wait a moment.");
                return;
            }

            if (!ctx.Member.IsStaff())
            {
                await ctx.RespondAsync("This command is currently restricted to staff only.");
                return;
            }

            if (string.IsNullOrWhiteSpace(amount))
            {
                await ctx.RespondAsync("Please specify an amount. Usage: `!mines <amount> <mines>` (e.g. `!mines 100m 3`).");
                return;
            }

            if (minesCount < 1 || minesCount > 23)
            {
                await ctx.RespondAsync("Mines count must be between 1 and 23.");
                return;
            }

            var env = ServerEnvironment.GetServerEnvironment();
            var serverManager = env.ServerManager;
            var usersService = serverManager.UsersService;
            var minesService = serverManager.MinesService; // Will be added later

            var user = await usersService.EnsureUserAsync(ctx.User.Id.ToString(), ctx.User.Username, ctx.Member.DisplayName);
            if (user == null) return;

            long betAmount;
            if (!GpParser.TryParseAmountInK(amount, out betAmount, out var error))
            {
                await ctx.RespondAsync($"Invalid amount: {error}");
                return;
            }

            if (betAmount < GpFormatter.MinimumBetAmountK)
            {
                await ctx.RespondAsync($"Minimum bet is `{GpFormatter.Format(GpFormatter.MinimumBetAmountK)}`.");
                return;
            }

            if (user.Balance < betAmount)
            {
                await ctx.RespondAsync("You don't have enough balance for this bet.");
                return;
            }

            if (!await usersService.RemoveBalanceAsync(user.Identifier, betAmount, isWager: true))
            {
                await ctx.RespondAsync("Failed to lock balance. Please try again.");
                return;
            }

            // Update local balance
            user.Balance -= betAmount;

            var game = await minesService.CreateGameAsync(user, betAmount, minesCount);
            if (game == null)
            {
                await usersService.AddBalanceAsync(user.Identifier, betAmount);
                await ctx.RespondAsync("Failed to create mines game.");
                return;
            }

            // Register wager for race
            //if (!ctx.Member.IsStaff())
            {
                var raceService = serverManager.RaceService;
                await raceService.RegisterWagerAsync(user.Identifier, user.DisplayName, betAmount);
            }

            var embed = BuildGameEmbed(game, user);
            var buttons = BuildButtons(game);

            var builder = new DiscordMessageBuilder().AddEmbed(embed);
            foreach (var row in buttons)
            {
                builder.AddActionRowComponent(new DiscordActionRowComponent(row));
            }

            var msg = await ctx.RespondAsync(builder);
            
            // Update game with message ID for future edits
            game.MessageId = msg.Id;
            game.ChannelId = msg.ChannelId;
            await minesService.UpdateGameAsync(game);
        }

        public static DiscordEmbed BuildGameEmbed(MinesGame game, User user)
        {
            var env = ServerEnvironment.GetServerEnvironment();
            var minesService = env.ServerManager.MinesService;
            
            int hits = game.RevealedTiles.Count;
            double multiplier = minesService.CalculateMultiplier(game.MinesCount, hits);
            double nextMultiplier = minesService.CalculateMultiplier(game.MinesCount, hits + 1);
            long currentPayout = (long)(game.BetAmount * multiplier);
            
            var status = "Active";
            var color = DiscordColor.Gold;
            string thumbnailUrl = "https://i.imgur.com/IZpipDr.png"; // Starting

            if (game.Status == MinesGameStatus.Lost)
            {
                status = "BOOM! You hit a mine!";
                color = DiscordColor.Red;
                currentPayout = 0;
                multiplier = 0;
                nextMultiplier = 0;
                thumbnailUrl = "https://i.imgur.com/laBZBWa.png";
            }
            else if (game.Status == MinesGameStatus.CashedOut)
            {
                status = $"Survived! Won {GpFormatter.Format(currentPayout)}";
                color = DiscordColor.Green;
                nextMultiplier = 0;
                thumbnailUrl = "https://i.imgur.com/gdhNxb1.png";
            }
            else if (game.Status == MinesGameStatus.Cancelled)
            {
                status = "Cancelled (Refunded)";
                color = DiscordColor.Gray;
                currentPayout = game.BetAmount;
                multiplier = 1;
                nextMultiplier = 0;
                thumbnailUrl = "https://i.imgur.com/IZpipDr.png";
            }
            else if (game.RevealedTiles.Count > 0)
            {
                thumbnailUrl = "https://i.imgur.com/7krPRqX.png"; // Ongoing
                //thumbnailUrl = "https://i.imgur.com/I80hcLH.gif"; // Ongoing
            }

            var embed = new DiscordEmbedBuilder()
                .WithTitle($"💣 Mines #{game.Id}")
                .AddField("Bet", $"**{GpFormatter.Format(game.BetAmount)}**", true)
                .AddField("Mines", $"**{game.MinesCount}**", true)
                .AddField("Status", $"**{status}**", true)
                .AddField("Multiplier", $"**{multiplier:0.00}x**", true)
                .AddField("Payout", $"**{GpFormatter.Format(currentPayout)}**", true)
                .AddField("Next", $"**{nextMultiplier:0.00}x**", true)
                .WithColor(color)
                .WithThumbnail(thumbnailUrl)
                .WithFooter($"{ServerConfiguration.ServerName} Game ID: {game.Id}")
                .WithTimestamp(DateTimeOffset.UtcNow);

            return embed.Build();
        }

        public static List<List<DiscordComponent>> BuildButtons(MinesGame game)
        {
            var rows = new List<List<DiscordComponent>>();
            
            for (int i = 0; i < 5; i++)
            {
                var row = new List<DiscordComponent>();
                for (int j = 0; j < 5; j++)
                {
                    int index = i * 5 + j;
                    
                    if (index == 24) // Cashout/Cancel/Replay button
                    {
                        if (game.Status != MinesGameStatus.Active)
                        {
                            // Replay button
                            row.Add(new DiscordButtonComponent(
                                DiscordButtonStyle.Primary,
                                $"mines_replay_{game.Id}",
                                " ",
                                false,
                                new DiscordComponentEmoji("🔄")
                            ));
                        }
                        else if (game.RevealedTiles.Count == 0)
                        {
                            // Cancel button
                            row.Add(new DiscordButtonComponent(
                                DiscordButtonStyle.Danger,
                                $"mines_cancel_{game.Id}",
                                "Cancel",
                                false,
                                new DiscordComponentEmoji("✖️")
                            ));
                        }
                        else
                        {
                            // Cashout button
                            row.Add(new DiscordButtonComponent(
                                DiscordButtonStyle.Secondary,
                                $"mines_cashout_{game.Id}",
                                " ",
                                false,
                                new DiscordComponentEmoji("💰")
                            ));
                        }
                    }
                    else
                    {
                        // Tile button
                        bool isRevealed = game.RevealedTiles.Contains(index);
                        bool isMine = game.MineLocations.Contains(index);
                        
                        var style = DiscordButtonStyle.Secondary;
                        var label = " ";
                        var emoji = new DiscordComponentEmoji("🔹"); // Hidden

                        if (game.Status != MinesGameStatus.Active)
                        {
                            // Reveal everything at end
                            if (isMine)
                            {
                                style = DiscordButtonStyle.Danger;
                                emoji = new DiscordComponentEmoji("💣");
                                if (game.Status == MinesGameStatus.Lost && game.RevealedTiles.Contains(index))
                                {
                                    // The mine that killed you
                                    style = DiscordButtonStyle.Danger; 
                                }
                            }
                            else
                            {
                                style = DiscordButtonStyle.Success;
                                emoji = new DiscordComponentEmoji("💎");
                            }
                        }
                        else if (isRevealed)
                        {
                            style = DiscordButtonStyle.Success;
                            emoji = new DiscordComponentEmoji("💎");
                        }

                        row.Add(new DiscordButtonComponent(
                            style,
                            $"mines_click_{game.Id}_{index}",
                            label,
                            game.Status != MinesGameStatus.Active || isRevealed, // Disabled if revealed or game over
                            emoji
                        ));
                    }
                }
                rows.Add(row);
            }
            return rows;
        }
    }
}
