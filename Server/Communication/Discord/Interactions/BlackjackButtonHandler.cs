using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Server.Client.Blackjack;
using Server.Client.Users;
using Server.Client.Utils;
using Server.Infrastructure;
using Server.Communication.Discord.Commands;
using Server.Infrastructure.Discord;

namespace Server.Communication.Discord.Interactions
{
    public static class BlackjackButtonHandler
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

            var user = await usersService.GetUserAsync(game.Identifier);
            if (user == null)
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("User not found.").AsEphemeral(true));
                return;
            }

            // Check for rematch actions
            bool isRematchAction = string.Equals(action, "rm", StringComparison.OrdinalIgnoreCase)
                                   || string.Equals(action, "half", StringComparison.OrdinalIgnoreCase)
                                   || string.Equals(action, "x2", StringComparison.OrdinalIgnoreCase)
                                   || string.Equals(action, "max", StringComparison.OrdinalIgnoreCase);

            if (isRematchAction)
            {
                // Calculate new amount
                long baseAmount = game.BetAmount; // Use the initial bet amount of the game
                long newAmount = baseAmount;

                if (string.Equals(action, "half", StringComparison.OrdinalIgnoreCase))
                {
                    newAmount = Math.Max(GpFormatter.MinimumBetAmountK, baseAmount / 2);
                }
                else if (string.Equals(action, "x2", StringComparison.OrdinalIgnoreCase))
                {
                    newAmount = baseAmount * 2;
                }
                else if (string.Equals(action, "max", StringComparison.OrdinalIgnoreCase))
                {
                    newAmount = user.Balance;
                }

                if (newAmount < GpFormatter.MinimumBetAmountK)
                {
                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent($"Minimum bet is `{GpFormatter.Format(GpFormatter.MinimumBetAmountK)}`.").AsEphemeral(true));
                    return;
                }

                if (user.Balance < newAmount)
                {
                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("You don't have enough balance for this rematch.").AsEphemeral(true));
                    return;
                }

                if (!await usersService.RemoveBalanceAsync(user.Identifier, newAmount))
                {
                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("Failed to lock balance for this rematch. Please try again.").AsEphemeral(true));
                    return;
                }

                // Update local user balance for display
                user.Balance -= newAmount;

                var newGame = await blackjackService.CreateGameAsync(user, newAmount);
                if (newGame == null)
                {
                    await usersService.AddBalanceAsync(user.Identifier, newAmount);
                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("Failed to create rematch game. Please try again later.").AsEphemeral(true));
                    return;
                }

                // If game finished immediately (e.g. Blackjack), refresh user balance
                if (newGame.Status == BlackjackGameStatus.Finished)
                {
                    user = await usersService.GetUserAsync(user.Identifier);
                }

                // Update OLD message
                var updateBuilder = new DiscordInteractionResponseBuilder();
                if (e.Message.Embeds.Count > 0)
                {
                    updateBuilder.AddEmbed(e.Message.Embeds[0]);
                }

                var disabledHalfButton = new DiscordButtonComponent(
                    string.Equals(action, "half", StringComparison.OrdinalIgnoreCase) ? DiscordButtonStyle.Success : DiscordButtonStyle.Secondary,
                    $"bj_half_{game.Id}", "1/2", true, new DiscordComponentEmoji(DiscordIds.BlackjackRmHalfEmojiId));

                var disabledRmButton = new DiscordButtonComponent(
                    string.Equals(action, "rm", StringComparison.OrdinalIgnoreCase) ? DiscordButtonStyle.Success : DiscordButtonStyle.Secondary,
                    $"bj_rm_{game.Id}", "RM", true, new DiscordComponentEmoji(DiscordIds.BlackjackRmEmojiId));

                var disabledX2Button = new DiscordButtonComponent(
                    string.Equals(action, "x2", StringComparison.OrdinalIgnoreCase) ? DiscordButtonStyle.Success : DiscordButtonStyle.Secondary,
                    $"bj_x2_{game.Id}", "X2", true, new DiscordComponentEmoji(DiscordIds.BlackjackRmX2EmojiId));

                var disabledMaxButton = new DiscordButtonComponent(
                    string.Equals(action, "max", StringComparison.OrdinalIgnoreCase) ? DiscordButtonStyle.Success : DiscordButtonStyle.Secondary,
                    $"bj_max_{game.Id}", "Max", true);

                updateBuilder.AddActionRowComponent(disabledHalfButton, disabledRmButton, disabledX2Button, disabledMaxButton);
                
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage, updateBuilder);

                // Send NEW message
                var newGameEmbed = BlackjackCommand.BuildGameEmbed(newGame, user, client);
                var newGameButtons = BlackjackCommand.BuildButtons(newGame);

                var newGameBuilder = new DiscordMessageBuilder().AddEmbed(newGameEmbed);
                if (newGameButtons.Length > 0)
                {
                    newGameBuilder.AddActionRowComponent(new DiscordActionRowComponent(newGameButtons));
                }

                var message = await e.Channel.SendMessageAsync(newGameBuilder);
                await blackjackService.UpdateMessageInfoAsync(newGame.Id, message.Id, message.Channel.Id);

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
            var embed = Commands.BlackjackCommand.BuildGameEmbed(game, user, client);
            var buttons = Commands.BlackjackCommand.BuildButtons(game);

            var builder = new DiscordInteractionResponseBuilder()
                .AddEmbed(embed);

            if (buttons.Length > 0)
            {
                builder.AddActionRowComponent(new DiscordActionRowComponent(buttons));
            }

            await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage, builder);
        }
    }
}
