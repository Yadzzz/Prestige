using System;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Server.Client.Mines;
using Server.Communication.Discord.Commands;
using Server.Infrastructure;

namespace Server.Communication.Discord.Interactions
{
    public class MinesButtonHandler
    {
        public static async Task Handle(DiscordClient client, ComponentInteractionCreatedEventArgs e)
        {
            var parts = e.Id.Split('_');
            // Format: mines_click_{gameId}_{tileIndex} or mines_cashout_{gameId}
            
            if (parts.Length < 3) return;

            var action = parts[1]; // click or cashout
            if (!int.TryParse(parts[2], out int gameId)) return;

            var env = ServerEnvironment.GetServerEnvironment();
            var minesService = env.ServerManager.MinesService;
            var usersService = env.ServerManager.UsersService;

            var game = await minesService.GetGameAsync(gameId);
            if (game == null)
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                    new DiscordInteractionResponseBuilder().WithContent("Game not found.").AsEphemeral(true));
                return;
            }

            // Check if user is the owner
            var user = await usersService.GetUserAsync(game.Identifier);
            if (user == null || user.Id != game.UserId) // Assuming game.UserId matches user.Id (int)
            {
                // Wait, game.UserId is int, user.Id is int.
                // But we need to check if the Discord User ID matches.
                // The User object has Identifier which is the Discord ID string.
                // Let's check e.User.Id.ToString() == game.Identifier
                
                if (e.User.Id.ToString() != game.Identifier)
                {
                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                        new DiscordInteractionResponseBuilder().WithContent("This is not your game.").AsEphemeral(true));
                    return;
                }
            }
            
            // If game is finished, do nothing (or show result)
            if (game.Status != MinesGameStatus.Active)
            {
                 await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage, 
                    new DiscordInteractionResponseBuilder().WithContent("Game is already finished."));
                 return;
            }

            MinesGame updatedGame = null;

            if (action == "click")
            {
                if (parts.Length < 4 || !int.TryParse(parts[3], out int tileIndex)) return;
                updatedGame = await minesService.RevealTileAsync(gameId, tileIndex);
            }
            else if (action == "cashout")
            {
                updatedGame = await minesService.CashoutAsync(gameId);
            }
            else if (action == "cancel")
            {
                updatedGame = await minesService.CancelGameAsync(gameId);
            }

            if (updatedGame == null)
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                    new DiscordInteractionResponseBuilder().WithContent("Error updating game.").AsEphemeral(true));
                return;
            }

            // Rebuild UI
            var embed = MinesCommand.BuildGameEmbed(updatedGame, user);
            var buttons = MinesCommand.BuildButtons(updatedGame);

            var builder = new DiscordInteractionResponseBuilder()
                .AddEmbed(embed);
            
            foreach (var row in buttons)
            {
                builder.AddActionRowComponent(new DiscordActionRowComponent(row));
            }

            await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage, builder);
        }
    }
}
