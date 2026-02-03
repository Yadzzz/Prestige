using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Server.Client.Cracker;
using Server.Client.Users;
using Server.Client.Utils;
using Server.Infrastructure;
using Server.Communication.Discord.Commands;
using Server.Infrastructure.Discord;

namespace Server.Communication.Discord.Interactions
{
    public static class CrackerButtonHandler
    {
        public static async Task Handle(DiscordClient client, ComponentInteractionCreatedEventArgs e)
        {
            if (RateLimiter.IsRateLimited(e.User.Id))
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                    .AddEmbed(new DiscordEmbedBuilder().WithDescription("You're doing that too fast.").WithColor(DiscordColor.Red))
                    .AsEphemeral(true));
                return;
            }

            var parts = e.Id.Split('_');
            if (parts.Length < 3)
                return;

            // id format: cracker_{action}_{gameId} or cracker_{action}_{arg}_{gameId} (for toggle)
            // actions: pull, cancel, rm, half, x2, max
            // toggle action: toggle_{color}_{gameId} -> parts[1]="toggle", parts[2]=color, parts[3]=gameId

            string action = parts[1];
            int gameIdIndex = parts.Length - 1;
            if (!int.TryParse(parts[gameIdIndex], out var gameId))
                return;

            var env = ServerEnvironment.GetServerEnvironment();
            var serverManager = env.ServerManager;
            var crackerService = serverManager.CrackerService;
            var usersService = serverManager.UsersService;

            var game = await crackerService.GetGameAsync(gameId);
            if (game == null)
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                    .AddEmbed(new DiscordEmbedBuilder().WithDescription("Game not found.").WithColor(DiscordColor.Red))
                    .AsEphemeral(true));
                return;
            }

            // Only owner
            if (e.User.Id.ToString() != game.Identifier)
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                    .AddEmbed(new DiscordEmbedBuilder().WithDescription("This game doesn't belong to you.").WithColor(DiscordColor.Red))
                    .AsEphemeral(true));
                return;
            }

            var user = await usersService.GetUserAsync(game.Identifier);

            // Rematch logic
             bool isRematchAction = action == "rm" || action == "half" || action == "x2" || action == "max";
             if (isRematchAction)
             {
                 long baseAmount = game.BetAmount;
                 long newAmount = baseAmount;
                 
                 if (action == "half") newAmount = Math.Max(GpFormatter.MinimumBetAmountK, baseAmount / 2);
                 else if (action == "x2") newAmount = baseAmount * 2;
                 else if (action == "max") newAmount = user.Balance;
                 
                 if (newAmount < GpFormatter.MinimumBetAmountK)
                 {
                      await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                        .AddEmbed(new DiscordEmbedBuilder().WithDescription($"Minimum bet is `{GpFormatter.Format(GpFormatter.MinimumBetAmountK)}`.").WithColor(DiscordColor.Red))
                        .AsEphemeral(true));
                    return;
                 }
                 
                 if (user.Balance < newAmount)
                 {
                      await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                        .AddEmbed(new DiscordEmbedBuilder().WithDescription("You don't have enough balance for this rematch.").WithColor(DiscordColor.Red))
                        .AsEphemeral(true));
                    return;
                 }
                 
                 if (!await usersService.RemoveBalanceAsync(user.Identifier, newAmount, isWager: true))
                 {
                     await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                        .AddEmbed(new DiscordEmbedBuilder().WithDescription("Failed to lock balance.").WithColor(DiscordColor.Red))
                        .AsEphemeral(true));
                    return;
                 }
                 
                 user.Balance -= newAmount; // local update
                 
                 var newGame = await crackerService.CreateGameAsync(user, newAmount);
                 if (newGame == null)
                 {
                     await usersService.AddBalanceAsync(user.Identifier, newAmount);
                     await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                        .AddEmbed(new DiscordEmbedBuilder().WithDescription("Failed to create game.").WithColor(DiscordColor.Red))
                        .AsEphemeral(true));
                     return;
                 }
                 
                 // Update the old message: disable buttons and highlight the selected rematch option
                 var disabledButtons = CrackerCommand.BuildRematchButtons(game, user.Balance, action, true);
                 var oldMessageUpdate = new DiscordInteractionResponseBuilder();
                 if (e.Message.Embeds.Count > 0)
                 {
                     oldMessageUpdate.AddEmbed(e.Message.Embeds[0]);
                 }
                 foreach(var row in disabledButtons) oldMessageUpdate.AddActionRowComponent(new DiscordActionRowComponent(row));
                 
                 await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage, oldMessageUpdate);

                 // Send new game message
                 var embed = CrackerCommand.BuildGameEmbed(newGame, user);
                 var buttons = CrackerCommand.BuildButtons(newGame);
                 var builder = new DiscordMessageBuilder().AddEmbed(embed);
                 foreach(var row in buttons) builder.AddActionRowComponent(new DiscordActionRowComponent(row));

                 var newMessage = await client.SendMessageAsync(e.Channel, builder);

                 newGame.MessageId = newMessage.Id;
                 newGame.ChannelId = newMessage.ChannelId;
                 await crackerService.UpdateGameAsync(newGame);
                 return;
             }

            // Game Action Logic
            if (game.Status != CrackerGameStatus.Active)
            {
                 await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                    .AddEmbed(new DiscordEmbedBuilder().WithDescription("This game is finished.").WithColor(DiscordColor.Red))
                    .AsEphemeral(true));
                return;
            }

            if (action == "toggle")
            {
                 var color = parts[2];
                 await crackerService.ToggleHatAsync(game, color);
                 
                 var embed = CrackerCommand.BuildGameEmbed(game, user);
                 var buttons = CrackerCommand.BuildButtons(game);
                 var builder = new DiscordInteractionResponseBuilder().AddEmbed(embed);
                 foreach(var row in buttons) builder.AddActionRowComponent(new DiscordActionRowComponent(row));
                 
                 await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage, builder);
            }
            else if (action == "pull")
            {
                 if (game.SelectedHats.Count == 0)
                 {
                     await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                        .AddEmbed(new DiscordEmbedBuilder().WithDescription("Please select at least one hat.").WithColor(DiscordColor.Red))
                        .AsEphemeral(true));
                     return;
                 }
                 
                 await crackerService.PullAsync(game, user);
                 user = await usersService.GetUserAsync(user.Identifier); // Refresh balance
                 
                 var embed = CrackerCommand.BuildGameEmbed(game, user);
                 var buttons = CrackerCommand.BuildRematchButtons(game, user.Balance);
                 var builder = new DiscordInteractionResponseBuilder().AddEmbed(embed);
                 foreach(var row in buttons) builder.AddActionRowComponent(new DiscordActionRowComponent(row));
                 
                 await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage, builder);
            }
            else if (action == "cancel")
            {
                 await crackerService.CancelGameAsync(game);
                 user = await usersService.GetUserAsync(user.Identifier); // Refresh balance
                 
                 var embed = CrackerCommand.BuildGameEmbed(game, user);
                 // No buttons for cancelled game usually, or maybe disabled ones
                 var builder = new DiscordInteractionResponseBuilder().AddEmbed(embed);
                 
                 await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage, builder);
            }
        }
    }
}
