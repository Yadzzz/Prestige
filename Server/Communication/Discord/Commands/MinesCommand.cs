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

            if (!await usersService.RemoveBalanceAsync(user.Identifier, betAmount))
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
            var color = DiscordColor.Blue;

            if (game.Status == MinesGameStatus.Lost)
            {
                status = "BOOM! You hit a mine.";
                color = DiscordColor.Red;
            }
            else if (game.Status == MinesGameStatus.CashedOut)
            {
                status = $"Cashed Out! Won {GpFormatter.Format(currentPayout)}";
                color = DiscordColor.Green;
            }

            var embed = new DiscordEmbedBuilder()
                .WithTitle($"💣 Mines | {user.DisplayName}")
                .WithDescription($"Bet: **{GpFormatter.Format(game.BetAmount)}**\nMines: **{game.MinesCount}**\n\n" +
                                 $"Current Multiplier: **{multiplier:0.00}x**\n" +
                                 $"Current Payout: **{GpFormatter.Format(currentPayout)}**\n" +
                                 $"Next Multiplier: **{nextMultiplier:0.00}x**\n\n" +
                                 $"Status: {status}")
                .WithColor(color)
                .WithFooter($"Game ID: {game.Id}");

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
                    
                    if (index == 24) // Cashout button
                    {
                        bool canCashout = game.Status == MinesGameStatus.Active && game.RevealedTiles.Count > 0;
                        row.Add(new DiscordButtonComponent(
                            DiscordButtonStyle.Secondary,
                            $"mines_cashout_{game.Id}",
                            "Cashout",
                            !canCashout,
                            new DiscordComponentEmoji("💰")
                        ));
                    }
                    else
                    {
                        // Tile button
                        bool isRevealed = game.RevealedTiles.Contains(index);
                        bool isMine = game.MineLocations.Contains(index);
                        
                        var style = DiscordButtonStyle.Secondary;
                        var label = " ";
                        var emoji = new DiscordComponentEmoji("❓"); // Hidden

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
