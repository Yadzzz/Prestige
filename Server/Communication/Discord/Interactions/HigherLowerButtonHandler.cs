using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Server.Client.HigherLower;
using Server.Client.Users;
using Server.Client.Utils;
using Server.Infrastructure;
using Server.Communication.Discord.Commands;
using Server.Infrastructure.Discord;

namespace Server.Communication.Discord.Interactions
{
    public static class HigherLowerButtonHandler
    {
        public static async Task Handle(DiscordClient client, ComponentInteractionCreatedEventArgs e)
        {
            if (RateLimiter.IsRateLimited(e.User.Id))
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("You're doing that too fast.").AsEphemeral(true));
                return;
            }

            var parts = e.Id.Split('_');
            if (parts.Length < 3)
                return;

            var action = parts[1];
            if (!int.TryParse(parts[2], out var gameId))
                return;

            var env = ServerEnvironment.GetServerEnvironment();
            var serverManager = env.ServerManager;
            var hlService = serverManager.HigherLowerService;
            var usersService = serverManager.UsersService;

            var game = await hlService.GetGameAsync(gameId);
            if (game == null)
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("Game not found.").AsEphemeral(true));
                return;
            }

            // Only the game owner can interact
            if (e.User == null || e.User.Id.ToString() != game.Identifier)
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("This game doesn't belong to you.")
                        .AsEphemeral(true));
                return;
            }

            if (game.Status != HigherLowerGameStatus.Active)
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage,
                    new DiscordInteractionResponseBuilder().WithContent("Game is already finished."));
                return;
            }

            await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.DeferredMessageUpdate);

            if (action == "higher" || action == "lower")
            {
                bool guessHigher = action == "higher";
                var (updatedGame, isWin, newCard) = await hlService.GuessAsync(gameId, guessHigher);
                
                var user = await usersService.GetUserAsync(game.Identifier);
                var embed = HigherLowerCommand.BuildGameEmbed(updatedGame, user, client);
                var buttons = HigherLowerCommand.BuildButtons(updatedGame);

                var builder = new DiscordMessageBuilder().AddEmbed(embed);
                if (buttons.Length > 0)
                {
                    builder.AddActionRowComponent(new DiscordActionRowComponent(buttons));
                }

                await e.Message.ModifyAsync(builder);
            }
            else if (action == "cashout")
            {
                var updatedGame = await hlService.CashoutAsync(gameId);
                
                var user = await usersService.GetUserAsync(game.Identifier);
                var embed = HigherLowerCommand.BuildGameEmbed(updatedGame, user, client);
                var buttons = HigherLowerCommand.BuildButtons(updatedGame);

                var builder = new DiscordMessageBuilder().AddEmbed(embed);
                if (buttons.Length > 0)
                {
                    builder.AddActionRowComponent(new DiscordActionRowComponent(buttons));
                }

                await e.Message.ModifyAsync(builder);
            }
        }
    }
}
