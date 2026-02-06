using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Server.Infrastructure.Discord;

namespace Server.Communication.Discord.Commands
{
    public class GamesCommand : BaseCommandModule
    {
        [Command("games")]
        public async Task Games(CommandContext ctx)
        {
             // "buttons should be transparent" -> Secondary
             // "no embed message etc... just buttons" -> We need a message body, but keep it minimal.
             
             var coinflipBtn = new DiscordButtonComponent(
                 DiscordButtonStyle.Secondary,
                 "games_select_coinflip",
                 "Coinflip",
                 false,
                 new DiscordComponentEmoji(DiscordIds.CoinflipHeadsEmojiId)); 
                 
             var blackjackBtn = new DiscordButtonComponent(
                 DiscordButtonStyle.Secondary,
                 "games_select_blackjack",
                 "Blackjack",
                 false,
                 new DiscordComponentEmoji(DiscordIds.BlackjackSpadesEmojiId)); 
                 
             var crackerBtn = new DiscordButtonComponent(
                 DiscordButtonStyle.Secondary,
                 "games_select_cracker",
                 "Cracker",
                 false,
                 new DiscordComponentEmoji(DiscordIds.CrackerRedEmojiId));

             var minesBtn = new DiscordButtonComponent(
                 DiscordButtonStyle.Secondary,
                 "games_select_mines",
                 "Mines",
                 false,
                 new DiscordComponentEmoji("💣"));

             var hlBtn = new DiscordButtonComponent(
                 DiscordButtonStyle.Secondary,
                 "games_select_higherlower",
                 "Higher/Lower",
                 false,
                 new DiscordComponentEmoji(1449749026353451049)); // Higher/Lower emoji

             var chestBtn = new DiscordButtonComponent(
                 DiscordButtonStyle.Secondary,
                 "games_select_chest",
                 "Chest",
                 false,
                 new DiscordComponentEmoji("📦"));

             var embed = new DiscordEmbedBuilder()
                 .WithTitle("🎲 Games")
                 .WithDescription("Select a game below to start playing!")
                 .WithColor(DiscordColor.Purple);

             var builder = new DiscordMessageBuilder()
                 .AddEmbed(embed)
                 .AddActionRowComponent(new DiscordActionRowComponent(new [] { coinflipBtn, blackjackBtn, crackerBtn }))
                 .AddActionRowComponent(new DiscordActionRowComponent(new [] { minesBtn, hlBtn, chestBtn }));
                 
             await ctx.RespondAsync(builder);
        }
    }
}
