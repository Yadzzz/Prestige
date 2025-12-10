using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Server.Client.Blackjack;
using Server.Client.Users;
using Server.Infrastructure;

namespace Server.Communication.Discord.Interactions
{
    public static class BlackjackButtonHandler
    {
        public static async Task Handle(DiscordClient client, ComponentInteractionCreatedEventArgs e)
        {
            var parts = e.Id.Split('_');
            if (parts.Length < 3)
                return;

            var action = parts[1];
            if (!int.TryParse(parts[2], out var gameId))
                return;

            var env = ServerEnvironment.GetServerEnvironment();
            var serverManager = env.ServerManager;
            var blackjackService = serverManager.BlackjackService;
            var usersService = serverManager.UsersService;

            var game = await blackjackService.GetGameAsync(gameId);
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

            // Game already finished
            if (game.Status == BlackjackGameStatus.Finished)
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("This game is already finished.")
                        .AsEphemeral(true));
                return;
            }

            var user = await usersService.GetUserAsync(game.Identifier);
            if (user == null)
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("User not found.").AsEphemeral(true));
                return;
            }

            bool success = false;
            string errorMessage = null;

            switch (action.ToLower())
            {
                case "hit":
                    // Implicitly decline insurance if offered
                    if (!game.InsuranceTaken && !game.InsuranceDeclined && game.DealerHand.Cards.Count > 0 && game.DealerHand.Cards[0].Rank == "A")
                    {
                        await blackjackService.DeclineInsuranceAsync(game);
                        game = await blackjackService.GetGameAsync(gameId); // Refresh
                        
                        // If dealer had Blackjack, the game is now finished. Do not proceed with Hit.
                        if (game.Status == BlackjackGameStatus.Finished)
                        {
                            success = true;
                            break;
                        }
                    }

                    success = await blackjackService.HitAsync(game);
                    if (!success)
                        errorMessage = "Cannot hit right now.";
                    break;

                case "stand":
                    // Implicitly decline insurance if offered
                    if (!game.InsuranceTaken && !game.InsuranceDeclined && game.DealerHand.Cards.Count > 0 && game.DealerHand.Cards[0].Rank == "A")
                    {
                        await blackjackService.DeclineInsuranceAsync(game);
                        game = await blackjackService.GetGameAsync(gameId); // Refresh

                        // If dealer had Blackjack, the game is now finished. Do not proceed with Stand.
                        if (game.Status == BlackjackGameStatus.Finished)
                        {
                            success = true;
                            break;
                        }
                    }

                    success = await blackjackService.StandAsync(game);
                    if (!success)
                        errorMessage = "Cannot stand right now.";
                    break;

                case "double":
                    // Implicitly decline insurance if offered
                    if (!game.InsuranceTaken && !game.InsuranceDeclined && game.DealerHand.Cards.Count > 0 && game.DealerHand.Cards[0].Rank == "A")
                    {
                        await blackjackService.DeclineInsuranceAsync(game);
                        game = await blackjackService.GetGameAsync(gameId); // Refresh

                        // If dealer had Blackjack, the game is now finished. Do not proceed with Double.
                        if (game.Status == BlackjackGameStatus.Finished)
                        {
                            success = true;
                            break;
                        }
                    }

                    // Refresh user balance
                    user = await usersService.GetUserAsync(user.Identifier);
                    var currentHand = game.GetCurrentHand();
                    
                    if (currentHand != null && user.Balance < currentHand.BetAmount)
                    {
                        await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                            new DiscordInteractionResponseBuilder()
                                .WithContent("You don't have enough balance to double.")
                                .AsEphemeral(true));
                        return;
                    }

                    if (currentHand != null && await usersService.RemoveBalanceAsync(user.Identifier, currentHand.BetAmount))
                    {
                        success = await blackjackService.DoubleAsync(game, user);
                        if (!success)
                        {
                            // Refund if double failed
                            await usersService.AddBalanceAsync(user.Identifier, currentHand.BetAmount);
                            errorMessage = "Cannot double right now.";
                        }
                    }
                    else
                    {
                        errorMessage = "Failed to lock balance for double.";
                    }
                    break;

                case "split":
                    // Implicitly decline insurance if offered
                    if (!game.InsuranceTaken && !game.InsuranceDeclined && game.DealerHand.Cards.Count > 0 && game.DealerHand.Cards[0].Rank == "A")
                    {
                        await blackjackService.DeclineInsuranceAsync(game);
                        game = await blackjackService.GetGameAsync(gameId); // Refresh

                        // If dealer had Blackjack, the game is now finished. Do not proceed with Split.
                        if (game.Status == BlackjackGameStatus.Finished)
                        {
                            success = true;
                            break;
                        }
                    }

                    // Refresh user balance
                    user = await usersService.GetUserAsync(user.Identifier);
                    
                    if (user.Balance < game.BetAmount)
                    {
                        await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                            new DiscordInteractionResponseBuilder()
                                .WithContent("You don't have enough balance to split.")
                                .AsEphemeral(true));
                        return;
                    }

                    if (await usersService.RemoveBalanceAsync(user.Identifier, game.BetAmount))
                    {
                        success = await blackjackService.SplitAsync(game, user);
                        if (!success)
                        {
                            // Refund if split failed
                            await usersService.AddBalanceAsync(user.Identifier, game.BetAmount);
                            errorMessage = "Cannot split right now.";
                        }
                    }
                    else
                    {
                        errorMessage = "Failed to lock balance for split.";
                    }
                    break;

                case "ins":
                    // Refresh user balance
                    user = await usersService.GetUserAsync(user.Identifier);
                    long insuranceCost = game.BetAmount / 2;
                    
                    if (user.Balance < insuranceCost)
                    {
                        await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                            new DiscordInteractionResponseBuilder()
                                .WithContent("You don't have enough balance for insurance.")
                                .AsEphemeral(true));
                        return;
                    }

                    if (await usersService.RemoveBalanceAsync(user.Identifier, insuranceCost))
                    {
                        success = await blackjackService.TakeInsuranceAsync(game, user);
                        if (!success)
                        {
                            // Refund if insurance failed
                            await usersService.AddBalanceAsync(user.Identifier, insuranceCost);
                            errorMessage = "Cannot take insurance right now.";
                        }
                    }
                    else
                    {
                        errorMessage = "Failed to lock balance for insurance.";
                    }
                    break;

                case "noins":
                    success = await blackjackService.DeclineInsuranceAsync(game);
                    if (!success)
                        errorMessage = "Cannot decline insurance right now.";
                    break;

                default:
                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("Unknown action.")
                            .AsEphemeral(true));
                    return;
            }

            if (!success && errorMessage != null)
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent(errorMessage)
                        .AsEphemeral(true));
                return;
            }

            // Refresh game and user
            game = await blackjackService.GetGameAsync(gameId);
            user = await usersService.GetUserAsync(user.Identifier);

            if (game == null || user == null)
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("Failed to refresh game state.")
                        .AsEphemeral(true));
                return;
            }

            // Update the message
            var embed = Commands.BlackjackCommand.BuildGameEmbed(game, user);
            var buttons = Commands.BlackjackCommand.BuildButtons(game);

            var builder = new DiscordInteractionResponseBuilder()
                .AddEmbed(embed);

            if (buttons.Length > 0)
            {
                builder.AddComponents(buttons);
            }

            await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage, builder);
        }
    }
}
