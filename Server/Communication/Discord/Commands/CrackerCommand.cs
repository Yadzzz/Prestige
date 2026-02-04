using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Server.Client.Cracker;
using Server.Client.Users;
using Server.Client.Utils;
using Server.Infrastructure;
using Server.Infrastructure.Discord;

namespace Server.Communication.Discord.Commands
{
    public class CrackerCommand : BaseCommandModule
    {
        private static readonly TimeSpan RateLimitInterval = TimeSpan.FromSeconds(1);

        [Command("cracker")]
        [Aliases("cr")]
        public async Task Cracker(CommandContext ctx, string amount = null)
        {
            // Use same channel enforcement as Blackjack for now
            if (!await DiscordChannelPermissionService.EnforceBlackjackChannelAsync(ctx))
            {
                return;
            }

            if (RateLimiter.IsRateLimited(ctx.User.Id, "cracker", RateLimitInterval))
            {
                var errorEmbed = new DiscordEmbedBuilder()
                    .WithDescription("You're doing that too fast. Please wait a moment.")
                    .WithColor(DiscordColor.Red);
                await ctx.RespondAsync(errorEmbed);
                return;
            }

            var env = ServerEnvironment.GetServerEnvironment();
            var serverManager = env.ServerManager;
            var usersService = serverManager.UsersService;
            var crackerService = serverManager.CrackerService; // Will need to add this property to ServerManager

            if (crackerService == null)
            {
                var errorEmbed = new DiscordEmbedBuilder()
                     .WithDescription("Cracker service is not initialized.")
                     .WithColor(DiscordColor.Red);
                await ctx.RespondAsync(errorEmbed);
                return;
            }

            var user = await usersService.EnsureUserAsync(ctx.User.Id.ToString(), ctx.User.Username, ctx.Member.DisplayName);
            if (user == null)
                return;

            long betAmount;

            if (string.IsNullOrWhiteSpace(amount))
            {
                betAmount = user.Balance;
            }
            else if (!GpParser.TryParseAmountInK(amount, out betAmount, out var error))
            {
                var errorEmbed = new DiscordEmbedBuilder()
                    .WithDescription($"Invalid amount: {error}\nExamples: `!cracker 100`, `!cr 0.5`, `!cr 1b`, `!cr 1000m`, or `!cracker` for all-in.")
                    .WithColor(DiscordColor.Red);
                await ctx.RespondAsync(errorEmbed);
                return;
            }

            if (betAmount < GpFormatter.MinimumBetAmountK)
            {
                var errorEmbed = new DiscordEmbedBuilder()
                    .WithDescription($"Minimum bet is `{GpFormatter.Format(GpFormatter.MinimumBetAmountK)}`.")
                    .WithColor(DiscordColor.Red);
                await ctx.RespondAsync(errorEmbed);
                return;
            }

            if (user.Balance < betAmount)
            {
                var errorEmbed = new DiscordEmbedBuilder()
                    .WithDescription("You don't have enough balance for this bet.")
                    .WithColor(DiscordColor.Red);
                await ctx.RespondAsync(errorEmbed);
                return;
            }

            if (!await usersService.RemoveBalanceAsync(user.Identifier, betAmount, isWager: true))
            {
                var errorEmbed = new DiscordEmbedBuilder()
                    .WithDescription("Failed to lock balance for this game. Please try again.")
                    .WithColor(DiscordColor.Red);
                await ctx.RespondAsync(errorEmbed);
                return;
            }

            // Update local user balance for display
            user.Balance -= betAmount;

            var game = await crackerService.CreateGameAsync(user, betAmount);
            if (game == null)
            {
                await usersService.AddBalanceAsync(user.Identifier, betAmount);
                var errorEmbed = new DiscordEmbedBuilder()
                    .WithDescription("Failed to create cracker game. Please try again later.")
                    .WithColor(DiscordColor.Red);
                await ctx.RespondAsync(errorEmbed);
                return;
            }

            var embed = BuildGameEmbed(game, user);
            var buttons = BuildButtons(game);

            var builder = new DiscordMessageBuilder().AddEmbed(embed);
            foreach (var row in buttons)
            {
                builder.AddActionRowComponent(new DiscordActionRowComponent(row));
            }

            var message = await ctx.RespondAsync(builder);
            game.MessageId = message.Id;
            game.ChannelId = message.ChannelId;
            await crackerService.UpdateGameAsync(game);
        }

        public static DiscordEmbed BuildGameEmbed(CrackerGame game, User user)
        {
            var selectedCount = game.SelectedHats.Count;
            // 0 selected -> Multiplier 0? Or just show what it WOULD be for 1? 
            // The prompt says "Multiplier should scale with odds: baseMultiplier = 6/k".
            // If k=0, undefined. We can show "Pick a hat".
            
            var service = ServerEnvironment.GetServerEnvironment().ServerManager.CrackerService;
            double potentialMult = selectedCount > 0 ? service.CalculateMultiplier(selectedCount) : 0;
            
            var embed = new DiscordEmbedBuilder();
            embed.WithTitle($"Cracker Game #{game.Id}");
            
            if (game.Status == CrackerGameStatus.Active)
            {
                embed.WithColor(DiscordColor.Blurple);
                embed.WithDescription("Select partyhats to bet on!\nClick 'Pull' when ready.");
                embed.WithThumbnail(GetCrackerImageUrl(null, false, true));
            }
            else if (game.Status == CrackerGameStatus.Finished)
            {
                bool won = game.Payout > 0;
                embed.WithColor(won ? DiscordColor.SpringGreen : DiscordColor.Red);

                var body = won 
                     ? $"You won `{GpFormatter.Format(game.Payout)}`.\nYour gold bag now holds `{GpFormatter.Format(user.Balance)}`."
                     : $"You lost `{GpFormatter.Format(game.BetAmount)}`.\nYour gold bag now holds `{GpFormatter.Format(user.Balance)}`.";
                 
                var resultTitle = won ? "You picked the right hat!" : "Wrong hat picked.";
                var suffix = won ? "Think you can do that again?" : "Perhaps next time...";
                 
                embed.WithDescription($"**{resultTitle}**\n\n{body}\n\n*{suffix}*");
                embed.WithThumbnail(GetCrackerImageUrl(game.ResultHat, won));
            }
            else if (game.Status == CrackerGameStatus.Cancelled)
            {
                embed.WithColor(DiscordColor.Gray);
                embed.WithDescription($"**Game Cancelled**\n\nYour bet of `{GpFormatter.Format(game.BetAmount)}` has been refunded.");
                embed.WithThumbnail(GetCrackerImageUrl(null, false, true));
            }

            embed.AddField("Multiplier", $"`{potentialMult}x`", true);
            embed.AddField("Bet", $"`{GpFormatter.Format(game.BetAmount)}`", true);
            
            if (game.Status == CrackerGameStatus.Finished && !string.IsNullOrEmpty(game.ResultHat))
            {
                var resultEmoji = GetDiscordEmoji(game.ResultHat);
                embed.AddField("Winning hat", $"<:{resultEmoji.Name}:{resultEmoji.Id}> {game.ResultHat}", true);
            }

            embed.WithFooter("Prestige Bets", null); 
            embed.WithTimestamp(DateTime.UtcNow);

            return embed.Build();
        }

        public static List<List<DiscordComponent>> BuildButtons(CrackerGame game)
        {
            var rows = new List<List<DiscordComponent>>();

            if (game.Status != CrackerGameStatus.Active)
                return rows;

            var hatColors = CrackerService.AllHats;
            
            // Row 1: First 3 hats
            var row1 = new List<DiscordComponent>();
            for (int i = 0; i < 3; i++)
            {
                var color = hatColors[i];
                bool isSelected = game.SelectedHats.Contains(color);
                // "marked ones should be blue like the image color"
                var style = isSelected ? DiscordButtonStyle.Primary : DiscordButtonStyle.Secondary;
                // Emojis mapping (approximate based on color names)
                var emoji = GetDiscordEmoji(color); 
                
                row1.Add(new DiscordButtonComponent(
                    style, 
                    $"cracker_toggle_{color}_{game.Id}", 
                    null, 
                    false, 
                    emoji));
            }
            rows.Add(row1);

            // Row 2: Next 3 hats
            var row2 = new List<DiscordComponent>();
            for (int i = 3; i < 6; i++)
            {
                var color = hatColors[i];
                bool isSelected = game.SelectedHats.Contains(color);
                // "marked ones should be blue like the image color"
                var style = isSelected ? DiscordButtonStyle.Primary : DiscordButtonStyle.Secondary;
                var emoji = GetDiscordEmoji(color);

                row2.Add(new DiscordButtonComponent(
                    style, 
                    $"cracker_toggle_{color}_{game.Id}", 
                    null, 
                    false, 
                    emoji));
            }
            rows.Add(row2);

            // Row 3: Pull and Cancel
            // Pull disabled if 0 hats selected
            bool pullDisabled = game.SelectedHats.Count == 0;
            
            // Rematch buttons? "Add a 'Cancel' button using the SAME cancel icon/style/customId pattern as our blackjack cancel."
            // Blackjack cancel pattern is usually just a button with id "bj_cancel_{id}".
            // Wait, users says "SAME cancel icon/style/customId pattern".
            // Let's check Blackjack but since this is Cracker, customId should probably be "cracker_cancel_{id}".
            
            var row3 = new List<DiscordComponent>();

            // Rematch buttons (1/2, RM, X2, MAX) - usually shown AFTER game ends.
            // But prompt says "Game UI like our screenshot".
            // Screenshot shows rematch buttons at the bottom.
            // BUT screenshot also says "You won..." so it's a finished game.
            // While active, we need Pull and Cancel.

            /* 
               Active State:
               [Hat] [Hat] [Hat]
               [Hat] [Hat] [Hat]
               [Pull (Green)] [Cancel (Red)]
            */

             /* 
               Finished State:
               [Hat] [Hat] [Hat] (Disabled?)
               [Hat] [Hat] [Hat] (Disabled?)
               [1/2] [RM]
               [X2] [MAX]
            */
            
            // We are building buttons for ACTIVE state here mainly, 
            // but logic for finished state handles rematch buttons in ButtonHandler usually?
            // Re-reading BlackjackCommand: `if (buttons.Length > 0)`.
            // BlackjackService `BuildButtons` (not shown but inferred) likely handles state.
            
            // I'll put the logic here.
            
            var pullBtn = new DiscordButtonComponent(
                DiscordButtonStyle.Secondary, // "pull and cancel should be transparent no color (gray)"
                $"cracker_pull_{game.Id}",
                "Pull",
                pullDisabled,
                new DiscordComponentEmoji("🧨")); // Cracker emoji?

            // Using Blackjack cancel style? I need to know the emoji.
            // Assuming generic Cross for now or check BlackjackButtonHandler if it has emoji.
            // It just splits IDs.
            
            // "cancel should only have icon, no text, use the same icon as coinflip game"
            var cancelBtn = new DiscordButtonComponent(
                DiscordButtonStyle.Secondary, // "pull and cancel should be transparent no color (gray)"
                $"cracker_cancel_{game.Id}",
                " ", // "no text"
                false,
                new DiscordComponentEmoji(DiscordIds.CoinflipExitEmojiId));

            row3.Add(pullBtn);
            row3.Add(cancelBtn);
            rows.Add(row3);

            return rows;
        }

        public static List<List<DiscordComponent>> BuildRematchButtons(CrackerGame game, long userBalance, string highlightedAction = null, bool disableAll = false)
        {
             var rows = new List<List<DiscordComponent>>();
             
             var hatColors = CrackerService.AllHats;
             var row1 = new List<DiscordComponent>();
             for(int i=0; i<3; i++) {
                 var color = hatColors[i];
                 var style = DiscordButtonStyle.Secondary;
                 if (game.SelectedHats.Contains(color)) style = DiscordButtonStyle.Primary; // Show what they picked?
                  row1.Add(new DiscordButtonComponent(style, $"cracker_dummy_{i}", null, true, GetDiscordEmoji(color)));
             }
             rows.Add(row1);
             
             var row2 = new List<DiscordComponent>();
             for(int i=3; i<6; i++) {
                 var color = hatColors[i];
                 var style = DiscordButtonStyle.Secondary;
                 if (game.SelectedHats.Contains(color)) style = DiscordButtonStyle.Primary;
                 row2.Add(new DiscordButtonComponent(style, $"cracker_dummy_{i}", null, true, GetDiscordEmoji(color)));
             }
             rows.Add(row2);
             
             // Rematch Rows
             var row3 = new List<DiscordComponent>();
             long halfBet = Math.Max(GpFormatter.MinimumBetAmountK, game.BetAmount / 2);
             bool canHalf = userBalance >= halfBet; 
             
             var styleHalf = string.Equals(highlightedAction, "half", StringComparison.OrdinalIgnoreCase) ? DiscordButtonStyle.Success : DiscordButtonStyle.Secondary;
             row3.Add(new DiscordButtonComponent(styleHalf, $"cracker_half_{game.Id}", $"1/2 ({GpFormatter.Format(halfBet)})", disableAll || !canHalf, new DiscordComponentEmoji("🍬")));
             
             bool canRm = userBalance >= game.BetAmount;
             var styleRm = string.Equals(highlightedAction, "rm", StringComparison.OrdinalIgnoreCase) ? DiscordButtonStyle.Success : DiscordButtonStyle.Secondary;
             row3.Add(new DiscordButtonComponent(styleRm, $"cracker_rm_{game.Id}", $"RM ({GpFormatter.Format(game.BetAmount)})", disableAll || !canRm, new DiscordComponentEmoji("🧨")));
             rows.Add(row3);
             
             var row4 = new List<DiscordComponent>();
             long doubleBet = game.BetAmount * 2;
             bool canDouble = userBalance >= doubleBet;
             var styleX2 = string.Equals(highlightedAction, "x2", StringComparison.OrdinalIgnoreCase) ? DiscordButtonStyle.Success : DiscordButtonStyle.Secondary;
             row4.Add(new DiscordButtonComponent(styleX2, $"cracker_x2_{game.Id}", $"X2 ({GpFormatter.Format(doubleBet)})", disableAll || !canDouble, new DiscordComponentEmoji("🍬")));
             
             var styleMax = string.Equals(highlightedAction, "max", StringComparison.OrdinalIgnoreCase) ? DiscordButtonStyle.Success : DiscordButtonStyle.Secondary;
             row4.Add(new DiscordButtonComponent(styleMax, $"cracker_max_{game.Id}", $"MAX ({GpFormatter.Format(userBalance)})", disableAll, new DiscordComponentEmoji("🍬")));
             rows.Add(row4);
             
             return rows;
        }

        private static string GetCrackerImageUrl(string hatColor, bool isWin, bool isMenu = false)
        {
            if (isMenu) return "https://i.imgur.com/VZG4OIM.gif";

            return (hatColor, isWin) switch
            {
                ("Blue", false) => "https://i.imgur.com/eTqhrev.gif",
                ("Green", false) => "https://i.imgur.com/WdQuwhC.gif",
                ("Purple", false) => "https://i.imgur.com/TTdqRqX.gif",
                ("Red", false) => "https://i.imgur.com/U2UcRJH.gif",
                ("Yellow", false) => "https://i.imgur.com/Qa5VNRp.gif",
                ("White", false) => "https://i.imgur.com/CIoBGSk.gif",

                ("Blue", true) => "https://i.imgur.com/paWZMGK.gif",
                ("Green", true) => "https://i.imgur.com/QFD9Ich.gif",
                ("Purple", true) => "https://i.imgur.com/VHOH1Xt.gif",
                ("Red", true) => "https://i.imgur.com/la2dctD.gif",
                ("White", true) => "https://i.imgur.com/Sik1GpL.gif",
                ("Yellow", true) => "https://i.imgur.com/D6ZQ2sa.gif",
                _ => "https://i.imgur.com/VZG4OIM.gif"
            };
        }

        private static DiscordComponentEmoji GetDiscordEmoji(string color)
        {
            return color switch
            {
                "Red" => new DiscordComponentEmoji(DiscordIds.CrackerRedEmojiId),
                "Yellow" => new DiscordComponentEmoji(DiscordIds.CrackerYellowEmojiId),
                "Green" => new DiscordComponentEmoji(DiscordIds.CrackerGreenEmojiId),
                "Blue" => new DiscordComponentEmoji(DiscordIds.CrackerBlueEmojiId),
                "Purple" => new DiscordComponentEmoji(DiscordIds.CrackerPurpleEmojiId),
                "White" => new DiscordComponentEmoji(DiscordIds.CrackerWhiteEmojiId),
                _ => new DiscordComponentEmoji(DiscordIds.CrackerYellowEmojiId)
            };
        }
    }
}
