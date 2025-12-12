using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Server.Client.Blackjack;
using Server.Client.Users;
using Server.Client.Utils;
using Server.Infrastructure;
using Server.Communication.Discord.Commands;

namespace Server.Communication.Discord.Interactions
{
    public static class BlackjackButtonHandler
    {
        public static async Task Handle(DiscordSocketClient client, SocketMessageComponent component)
        {
            if (RateLimiter.IsRateLimited(component.User.Id))
            {
                await component.RespondAsync("You're doing that too fast.", ephemeral: true);
                return;
            }

            var parts = component.Data.CustomId.Split('_');
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
                await component.RespondAsync("Game not found.", ephemeral: true);
                return;
            }

            // Only the game owner can interact
            if (component.User == null || component.User.Id.ToString() != game.Identifier)
            {
                await component.RespondAsync("This game doesn't belong to you.", ephemeral: true);
                return;
            }

            // Game already finished
            if (game.Status == BlackjackGameStatus.Finished)
            {
                await component.RespondAsync("This game is already finished.", ephemeral: true);
                return;
            }

            var user = await usersService.GetUserAsync(game.Identifier);
            if (user == null)
            {
                await component.RespondAsync("User not found.", ephemeral: true);
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
                        await component.RespondAsync("You don't have enough balance to double.", ephemeral: true);
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
                        await component.RespondAsync("You don't have enough balance to split.", ephemeral: true);
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
                        await component.RespondAsync("You don't have enough balance for insurance.", ephemeral: true);
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
                    await component.RespondAsync("Unknown action.", ephemeral: true);
                    return;
            }

            if (!success && errorMessage != null)
            {
                await component.RespondAsync(errorMessage, ephemeral: true);
                return;
            }

            // Refresh game and user
            game = await blackjackService.GetGameAsync(gameId);
            user = await usersService.GetUserAsync(user.Identifier);

            if (game == null || user == null)
            {
                await component.RespondAsync("Failed to refresh game state.", ephemeral: true);
                return;
            }

            // Update the message
            var embed = Commands.BlackjackCommand.BuildGameEmbed(game, user, client);
            var buttons = Commands.BlackjackCommand.BuildButtons(game);

            var builder = new ComponentBuilder();
            foreach (var btn in buttons)
            {
                builder.WithButton(btn);
            }

            await component.UpdateAsync(msg =>
            {
                msg.Embed = embed.Build();
                msg.Components = builder.Build();
            });
        }
    }
}
