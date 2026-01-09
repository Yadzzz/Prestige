using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Server.Client.Chest;
using Server.Client.Users;
using Server.Client.Utils;
using Server.Infrastructure;
using Server.Infrastructure.Discord;

namespace Server.Communication.Discord.Interactions
{
    public static class ChestButtonHandler
    {
        public static async Task Handle(DiscordClient client, ComponentInteractionCreatedEventArgs e)
        {
            var parts = e.Id.Split('_');
            // chest_action_gameId_itemId(optional)
            if (parts.Length < 3) return;

            var action = parts[1];
            if (!int.TryParse(parts[2], out var gameId)) return;

            var env = ServerEnvironment.GetServerEnvironment();
            var chestService = env.ServerManager.ChestService;
            var usersService = env.ServerManager.UsersService;

            var game = await chestService.GetGameAsync(gameId);
            if (game == null) return;

            if (e.User.Id.ToString() != game.Identifier)
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("This is not your game.").AsEphemeral(true));
                return;
            }

            if (game.Status != ChestGameStatus.Selection)
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("This game is already finished or cancelled.").AsEphemeral(true));
                return;
            }

            if (action == "cancel")
            {
                await chestService.CancelGameAsync(game.Id);
                await usersService.AddBalanceAsync(game.Identifier, game.BetAmountK);
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage,
                    new DiscordInteractionResponseBuilder().WithContent("Game cancelled and bet refunded."));
                return;
            }

            if (action == "select")
            {
                var itemId = string.Join("_", parts.Skip(3));
                var currentIds = game.GetSelectedIds();
                
                if (currentIds.Contains(itemId))
                {
                    currentIds.Remove(itemId);
                }
                else
                {
                    currentIds.Add(itemId);
                }

                await chestService.UpdateSelectionAsync(game.Id, currentIds, e.Message.Id);
                
                // Re-fetch to get updated state or just use local vars
                // Update Embed
                var embed = BuildGameEmbed(game, currentIds, chestService);
                var rows = RebuildButtons(game, currentIds);

                var builder = new DiscordInteractionResponseBuilder().AddEmbed(embed);
                foreach (var row in rows)
                {
                    builder.AddActionRowComponent(row);
                }

                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage, builder);
                return;
            }

            if (action == "confirm")
            {
                var currentIds = game.GetSelectedIds();
                if (currentIds.Count == 0)
                {
                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("Please select at least one item.").AsEphemeral(true));
                    return;
                }

                // Lock balance
                var user = await usersService.GetUserAsync(game.Identifier);
                if (user == null)
                {
                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("User not found.").AsEphemeral(true));
                    return;
                }

                // Balance is already locked at start of game.

                // Calculate Result
                long totalValueK = currentIds.Sum(id => ChestItem.Items.FirstOrDefault(i => i.Id == id)?.ValueK ?? 0);
                double chance = chestService.CalculateWinChance(game.BetAmountK, totalValueK);
                
                // RNG
                double roll = RandomNumberGenerator.GetInt32(0, 100000) / 1000.0; // 0.000 to 99.999
                bool win = roll < (chance * 100);

                await chestService.CompleteGameAsync(game.Id, win, totalValueK, ChestGameStatus.Finished);

                if (win)
                {
                    await usersService.AddBalanceAsync(user.Identifier, totalValueK);
                }

                // Show Result
                var resultEmbed = BuildResultEmbed(user, game, win, totalValueK, currentIds);
                
                var resultBuilder = new DiscordInteractionResponseBuilder().AddEmbed(resultEmbed);
                resultBuilder.ClearComponents();

                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage, resultBuilder);
            }
        }

        private static DiscordEmbed BuildGameEmbed(ChestGame game, List<string> selectedIds, ChestService service)
        {
            long totalValueK = selectedIds.Sum(id => ChestItem.Items.FirstOrDefault(i => i.Id == id)?.ValueK ?? 0);
            double chance = service.CalculateWinChance(game.BetAmountK, totalValueK);
            
            var selectedItems = selectedIds
                .Select(id => ChestItem.Items.FirstOrDefault(i => i.Id == id))
                .Where(i => i != null)
                .Select(i => i!)
                .ToList();

            var itemNames = selectedItems.Select(i => i.Name).ToList();
            string itemsStr = itemNames.Any() ? string.Join("\n", itemNames) : "None";

            // Use the icon of the most valuable item selected, or a default chest icon
            var bestItem = selectedItems.OrderByDescending(i => i.ValueK).FirstOrDefault();
            var thumbUrl = bestItem?.IconUrl ?? "https://runescape.wiki/images/Treasure_chest_%28Construction%29_detail.png";

            return new DiscordEmbedBuilder()
                .WithTitle("Chest Game")
                .WithDescription($"Bet: **{GpFormatter.Format(game.BetAmountK)}**")
                .WithColor(DiscordColor.Gold)
                .AddField("Selected Items", itemsStr, true)
                .AddField("Total Prize Value", GpFormatter.Format(totalValueK), true)
                .AddField("Win Chance", $"{chance:P2}", true)
                .WithThumbnail(thumbUrl)
                .WithFooter("Select items below | Click Confirm to play")
                .Build();
        }

        private static IEnumerable<DiscordActionRowComponent> RebuildButtons(ChestGame game, List<string> selectedIds)
        {
            var rows = new List<DiscordActionRowComponent>();
            var buttons1 = new List<DiscordComponent>();
            var buttons2 = new List<DiscordComponent>();

            int count = 0;
            foreach (var item in ChestItem.Items)
            {
                bool isSelected = selectedIds.Contains(item.Id);
                var style = isSelected ? DiscordButtonStyle.Success : DiscordButtonStyle.Secondary;
                var emoji = item.EmojiId > 0 ? new DiscordComponentEmoji(item.EmojiId) : null;
                
                var btn = new DiscordButtonComponent(
                    style,
                    $"chest_select_{game.Id}_{item.Id}",
                    item.Name,
                    false,
                    emoji
                );

                if (count < 5) buttons1.Add(btn);
                else buttons2.Add(btn);
                count++;
            }

            rows.Add(new DiscordActionRowComponent(buttons1));
            if (buttons2.Count > 0) rows.Add(new DiscordActionRowComponent(buttons2));

            var controlButtons = new List<DiscordComponent>
            {
                new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"chest_confirm_{game.Id}", "Play", false, new DiscordComponentEmoji("🔑")),
                new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"chest_cancel_{game.Id}", "Cancel", false, new DiscordComponentEmoji(DiscordIds.CoinflipExitEmojiId))
            };
            rows.Add(new DiscordActionRowComponent(controlButtons));

            return rows;
        }

        private static DiscordEmbed BuildResultEmbed(User user, ChestGame game, bool win, long prizeValueK, List<string> selectedIds)
        {
            var selectedItems = selectedIds
                .Select(id => ChestItem.Items.FirstOrDefault(i => i.Id == id))
                .Where(i => i != null)
                .Select(i => i!)
                .ToList();

            var itemNames = selectedItems.Select(i => i.Name).ToList();
            string itemsStr = string.Join(", ", itemNames);

            var bestItem = selectedItems.OrderByDescending(i => i.ValueK).FirstOrDefault();
            var thumbUrl = bestItem?.IconUrl ?? "https://runescape.wiki/images/Treasure_chest_%28Construction%29_detail.png";

            var color = win ? DiscordColor.SpringGreen : DiscordColor.Red;
            var title = win ? "Chest Opened!" : "Chest Locked";
            var desc = win 
                ? $"You opened the chest and found:\n**{itemsStr}**\n\nValue: `{GpFormatter.Format(prizeValueK)}` added to your balance."
                : $"The chest remained locked.\nYou lost `{GpFormatter.Format(game.BetAmountK)}`.";

            return new DiscordEmbedBuilder()
                .WithTitle(title)
                .WithDescription(desc)
                .WithColor(color)
                .WithThumbnail(thumbUrl)
                .WithFooter(ServerConfiguration.ServerName)
                .WithTimestamp(DateTime.UtcNow)
                .Build();
        }
    }
}
