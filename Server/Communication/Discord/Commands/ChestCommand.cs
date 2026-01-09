using System;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Server.Client.Chest;
using Server.Client.Utils;
using Server.Infrastructure;
using Server.Infrastructure.Discord;

namespace Server.Communication.Discord.Commands
{
    public class ChestCommand : BaseCommandModule
    {
        [Command("chest")]
        [Description("Start a chest game to win rare items.")]
        public async Task Chest(CommandContext ctx, [Description("Amount to bet")] string amountString)
        {
            if (!await DiscordChannelPermissionService.EnforceChestChannelAsync(ctx))
            {
                return;
            }

            if (!GpParser.TryParseAmountInK(amountString, out var amountK))
            {
                await ctx.RespondAsync("Invalid amount.");
                return;
            }

            if (amountK <= 0)
            {
                await ctx.RespondAsync("Amount must be greater than 0.");
                return;
            }

            var env = ServerEnvironment.GetServerEnvironment();
            var user = await env.ServerManager.UsersService.GetUserAsync(ctx.User.Id.ToString());
            if (user == null)
            {
                await ctx.RespondAsync("You are not registered. Type !bal to register.");
                return;
            }

            if (user.Balance < amountK)
            {
                await ctx.RespondAsync("You don't have enough balance.");
                return;
            }

            if (!await env.ServerManager.UsersService.RemoveBalanceAsync(user.Identifier, amountK))
            {
                await ctx.RespondAsync("Failed to lock balance.");
                return;
            }

            // Create game
            var game = await env.ServerManager.ChestService.CreateGameAsync(user, amountK, ctx.Channel.Id);
            if (game == null)
            {
                // Refund if creation fails
                await env.ServerManager.UsersService.AddBalanceAsync(user.Identifier, amountK);
                await ctx.RespondAsync("Failed to start game.");
                return;
            }

            // Build initial embed
            var embed = new DiscordEmbedBuilder()
                .WithTitle("Chest Game")
                .WithDescription($"Bet: **{GpFormatter.Format(amountK)}**\n\nSelect items to add to your chest. The more value you add, the lower your chance to win!")
                .WithColor(DiscordColor.Gold)
                .AddField("Selected Items", "None", true)
                .AddField("Total Prize Value", "0 GP", true)
                .AddField("Win Chance", "0%", true)
                .WithFooter("Select items below | Click Confirm to play");

            // Build buttons for items
            // We have 9 items. 5 per row max.
            var rows = new List<DiscordActionRowComponent>();
            var buttons1 = new List<DiscordComponent>();
            var buttons2 = new List<DiscordComponent>();

            int count = 0;
            foreach (var item in ChestItem.Items)
            {
                var emoji = item.EmojiId > 0 ? new DiscordComponentEmoji(item.EmojiId) : null;
                var btn = new DiscordButtonComponent(
                    DiscordButtonStyle.Secondary,
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

            // Control row
            var controlButtons = new List<DiscordComponent>
            {
                new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"chest_confirm_{game.Id}", "Play", false, new DiscordComponentEmoji("🔑")),
                new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"chest_cancel_{game.Id}", "Cancel", false, new DiscordComponentEmoji(DiscordIds.CoinflipExitEmojiId))
            };
            rows.Add(new DiscordActionRowComponent(controlButtons));

            var builder = new DiscordMessageBuilder().AddEmbed(embed);
            foreach (var row in rows)
            {
                builder.AddActionRowComponent(row);
            }

            var msg = await ctx.RespondAsync(builder);
            
            // Update game with message ID
            await env.ServerManager.ChestService.UpdateSelectionAsync(game.Id, new List<string>(), msg.Id);
        }
    }
}
