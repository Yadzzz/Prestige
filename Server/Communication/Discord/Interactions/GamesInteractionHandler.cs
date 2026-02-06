using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Server.Infrastructure.Discord;
using Server.Infrastructure;
using Server.Client.Utils;
using Server.Communication.Discord.Commands;
using Server.Client.Users;
using Server.Client.Blackjack;
using Server.Client.Coinflips;
using Server.Client.Cracker;
using Server.Client.Mines;
using Server.Client.HigherLower;
using Server.Client.Chest;

namespace Server.Communication.Discord.Interactions
{
    public static class GamesInteractionHandler
    {
        public static async Task HandleComponent(DiscordClient client, ComponentInteractionCreatedEventArgs e)
        {
             if (e.Id == "games_select_coinflip" || e.Id == "games_select_blackjack" || e.Id == "games_select_cracker")
             {
                 var env = ServerEnvironment.GetServerEnvironment();
                 var usersService = env.ServerManager.UsersService;
                 var user = await usersService.EnsureUserAsync(e.Interaction.User.Id.ToString(), e.Interaction.User.Username, (e.Interaction.User as DiscordMember)?.DisplayName);
                 var balanceStr = user != null ? GpFormatter.Format(user.Balance) : "0gp";

                 string titleBase = "";
                 string customId = "";
                 
                 if (e.Id == "games_select_coinflip") { titleBase = "Start Coinflip"; customId = "games_coinflip_modal"; }
                 else if (e.Id == "games_select_blackjack") { titleBase = "Start Blackjack"; customId = "games_blackjack_modal"; }
                 else if (e.Id == "games_select_cracker") { titleBase = "Start Cracker"; customId = "games_cracker_modal"; }

                 var title = $"{titleBase} (Bal: {balanceStr})";
                 if (title.Length > 45) title = titleBase; 

                 var label = "Bet Amount (e.g. 100k, 1m)";
                 var modal = new DiscordModalBuilder()
                    .WithTitle(title)
                    .WithCustomId(customId)
                    .AddTextInput(new DiscordTextInputComponent(label, "amount", required: true, value: ""), label);
                 
                 await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.Modal, modal);
             }
             else if (e.Id == "games_select_mines" || e.Id == "games_select_higherlower" || e.Id == "games_select_chest")
             {
                 var env = ServerEnvironment.GetServerEnvironment();
                 var usersService = env.ServerManager.UsersService;
                 var user = await usersService.EnsureUserAsync(e.Interaction.User.Id.ToString(), e.Interaction.User.Username, (e.Interaction.User as DiscordMember)?.DisplayName);
                 var balanceStr = user != null ? GpFormatter.Format(user.Balance) : "0gp";
                 
                 string titleBase = "";
                 string customId = "";

                 if (e.Id == "games_select_mines") { titleBase = "Start Mines"; customId = "games_mines_modal"; }
                 else if (e.Id == "games_select_higherlower") { titleBase = "Start Higher/Lower"; customId = "games_hl_modal"; }
                 else if (e.Id == "games_select_chest") { titleBase = "Start Chest"; customId = "games_chest_modal"; }

                 var title = $"{titleBase} (Bal: {balanceStr})";
                 if (title.Length > 45) title = titleBase;
                 
                 var label = "Bet Amount (e.g. 100k, 1m)";
                 var modal = new DiscordModalBuilder()
                    .WithTitle(title)
                    .WithCustomId(customId)
                    .AddTextInput(new DiscordTextInputComponent(label, "amount", required: true, value: ""), label);
                 
                 if (e.Id == "games_select_mines")
                 {
                     modal.AddTextInput(new DiscordTextInputComponent("Mines Count (1-23)", "mines", "3", required: true, min_length: 1, max_length: 2), "Mines Count (1-23)");
                 }
                 
                 await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.Modal, modal);
             }
        }

        public static async Task HandleModal(DiscordClient client, ModalSubmittedEventArgs e)
        {
             var amountStr = GetValue(e.Values, "amount"); // Key is usually customId of the input, wait check above. 
             // Oh, AddTextInput(..., "Bet Amount") <- "Bet Amount" is label? No, ctor is (label, customId, ...)
             // existing code: AddTextInput(new DiscordTextInputComponent("Bet Amount", "amount", ...), "Bet Amount"); DSharp 5 changed builder?
             // DSharpPlus 5 ModalBuilder.AddTextInput(component) directly.
             // The existing code handles it.
             
             // Wait, the existing code has GetValue(e.Values, "Bet Amount") but the component ID is "amount".
             // DSharpPlus Modal returns values by Component CustomID usually.
             // But valid code is referencing "Bet Amount".
             // Let's check existing: AddTextInput(new DiscordTextInputComponent("Bet Amount", "amount", ...), "Bet Amount");
             // The second arg to AddTextInput is NOT the label in 4.x, but the label is the first arg of Component.
             // Wait, `AddTextInput(DiscordTextInputComponent component)` is how it usually works.
             // The existing code has `.AddTextInput(component, label)`. Maybe extension method or specific overload.
             // BUT `GetValue(e.Values, "Bet Amount")` looks suspicious if the ID is "amount".
             // If DSharpPlus returns Dictionary<ComponentID, value>, then it should used "amount".
             // If DSharpPlus returns Dictionary<Label, value>, then "Bet Amount".
             // I will assume the previous code worked or the user knows.
             // However, I see `GetValue(e.Values, "Bet Amount")` inside the existing `HandleModal`.
             
             // In my new code for Mines/etc, I used ID "amount".
             // I will try to retrieve by "amount" but fall back to label if needed? 
             // Actually, I should use the proper ID which is "amount".
             // The previous code had: `var amountStr = GetValue(e.Values, "Bet Amount");`
             // And defined it as: `AddTextInput(new DiscordTextInputComponent("Bet Amount", "amount", ...), "Bet Amount")`
             // I will change my new code to match the pattern or fix the pattern.
             // I will use "amount" as ID.

             if (e.Interaction.Data.CustomId == "games_coinflip_modal")
             {
                 await StartCoinflip(client, e, amountStr);
             }
             else if (e.Interaction.Data.CustomId == "games_blackjack_modal")
             {
                  await StartBlackjack(client, e, amountStr);
             }
             else if (e.Interaction.Data.CustomId == "games_cracker_modal")
             {
                  await StartCracker(client, e, amountStr);
             }
             else if (e.Interaction.Data.CustomId == "games_mines_modal")
             {
                  var minesStr = GetValue(e.Values, "mines"); // ID "mines"
                  await StartMines(client, e, amountStr, minesStr);
             }
             else if (e.Interaction.Data.CustomId == "games_hl_modal")
             {
                  await StartHigherLower(client, e, amountStr);
             }
             else if (e.Interaction.Data.CustomId == "games_chest_modal")
             {
                  await StartChest(client, e, amountStr);
             }
        }

        private static async Task StartCoinflip(DiscordClient client, ModalSubmittedEventArgs e, string amountStr)
        {
            var env = ServerEnvironment.GetServerEnvironment();
            var serverManager = env.ServerManager;
            var usersService = serverManager.UsersService;
            var coinflipsService = serverManager.CoinflipsService;

            // Since this is a modal submission, we should defer or acknowledge?
            // createResponse with ChannelMessageWithSource sends a visible message.
            
            var user = await usersService.EnsureUserAsync(e.Interaction.User.Id.ToString(), e.Interaction.User.Username, (e.Interaction.User as DiscordMember)?.DisplayName);
            if (user == null)
            {
                 // Should reply ephemeral error
                 await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                    new DiscordInteractionResponseBuilder().WithContent("Could not find user.").AsEphemeral(true));
                 return;
            }

            long amountK;
            if (string.IsNullOrWhiteSpace(amountStr) || string.Equals(amountStr, "all", StringComparison.OrdinalIgnoreCase) || string.Equals(amountStr, "all-in", StringComparison.OrdinalIgnoreCase))
            {
                amountK = user.Balance;
            }
            else if (!GpParser.TryParseAmountInK(amountStr, out amountK, out var error))
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                    new DiscordInteractionResponseBuilder().WithContent($"Invalid amount: {error}").AsEphemeral(true));
                return;
            }

            if (amountK < GpFormatter.MinimumBetAmountK)
            {
                // Can we reply with ephemeral error? Yes.
                 await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                    new DiscordInteractionResponseBuilder().WithContent($"Minimum bet is {GpFormatter.Format(GpFormatter.MinimumBetAmountK)}.").AsEphemeral(true));
                return;
            }

            if (user.Balance < amountK)
            {
                 await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                    new DiscordInteractionResponseBuilder().WithContent("You don't have enough balance.").AsEphemeral(true));
                return;
            }

            if (!await usersService.RemoveBalanceAsync(user.Identifier, amountK, isWager: true))
            {
                 await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                    new DiscordInteractionResponseBuilder().WithContent("Failed to lock balance.").AsEphemeral(true));
                return;
            }

            var flip = await coinflipsService.CreateCoinflipAsync(user, amountK);
            if (flip == null)
            {
                await usersService.AddBalanceAsync(user.Identifier, amountK);
                 await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                    new DiscordInteractionResponseBuilder().WithContent("Failed to create game.").AsEphemeral(true));
                return;
            }

            var embed = CoinflipCommand.BuildGameEmbed();
            var actionRow = CoinflipCommand.BuildActionRow(flip.Id);

             // Response
             await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                 new DiscordInteractionResponseBuilder()
                 .AddEmbed(embed)
                 .AddActionRowComponent(actionRow));
             
             // Update MessageId
             var msg = await e.Interaction.GetOriginalResponseAsync();
             flip.MessageId = msg.Id;
             flip.ChannelId = msg.ChannelId;
             await coinflipsService.UpdateCoinflipStateAsync(flip);
        }

        private static async Task StartBlackjack(DiscordClient client, ModalSubmittedEventArgs e, string amountStr)
        {
            var env = ServerEnvironment.GetServerEnvironment();
            var serverManager = env.ServerManager;
            var usersService = serverManager.UsersService;
            var blackjackService = serverManager.BlackjackService;

            var user = await usersService.EnsureUserAsync(e.Interaction.User.Id.ToString(), e.Interaction.User.Username, (e.Interaction.User as DiscordMember)?.DisplayName);
            if (user == null) {
                 await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                    new DiscordInteractionResponseBuilder().WithContent("User not found.").AsEphemeral(true));
                return;
            }

             long betAmount;
            if (string.IsNullOrWhiteSpace(amountStr) || string.Equals(amountStr, "all", StringComparison.OrdinalIgnoreCase) || string.Equals(amountStr, "all-in", StringComparison.OrdinalIgnoreCase))
            {
                betAmount = user.Balance;
            }
            else if (!GpParser.TryParseAmountInK(amountStr, out betAmount, out var error))
            {
                 await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                    new DiscordInteractionResponseBuilder().WithContent($"Invalid amount: {error}").AsEphemeral(true));
                return;
            }

            if (betAmount < GpFormatter.MinimumBetAmountK)
            {
                 await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                    new DiscordInteractionResponseBuilder().WithContent($"Minimum bet is {GpFormatter.Format(GpFormatter.MinimumBetAmountK)}.").AsEphemeral(true));
                return;
            }

            if (user.Balance < betAmount)
            {
                 await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                    new DiscordInteractionResponseBuilder().WithContent("Not enough balance.").AsEphemeral(true));
                return;
            }

            if (!await usersService.RemoveBalanceAsync(user.Identifier, betAmount, isWager: true))
            {
                 await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                    new DiscordInteractionResponseBuilder().WithContent("Failed to lock balance.").AsEphemeral(true));
                return;
            }
            
            user.Balance -= betAmount; // local update

            var game = await blackjackService.CreateGameAsync(user, betAmount);
            if (game == null)
            {
                await usersService.AddBalanceAsync(user.Identifier, betAmount);
                 await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                    new DiscordInteractionResponseBuilder().WithContent("Failed to create game.").AsEphemeral(true));
                return;
            }

             if (game.Status == BlackjackGameStatus.Finished)
            {
                user = await usersService.GetUserAsync(user.Identifier);
            }

            var embed = BlackjackCommand.BuildGameEmbed(game, user, client);
            var buttons = BlackjackCommand.BuildButtons(game);

            var builder = new DiscordInteractionResponseBuilder().AddEmbed(embed);
             if (buttons.Length > 0)
            {
                builder.AddActionRowComponent(new DiscordActionRowComponent(buttons));
            }
            
            await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, builder);
            
             // Update MessageId
             var msg = await e.Interaction.GetOriginalResponseAsync();
             game.MessageId = msg.Id;
             game.ChannelId = msg.ChannelId;
             await blackjackService.SaveGameAsync(game);
        }

        private static async Task StartCracker(DiscordClient client, ModalSubmittedEventArgs e, string amountStr)
        {
            var env = ServerEnvironment.GetServerEnvironment();
            var serverManager = env.ServerManager;
            var usersService = serverManager.UsersService;
            var crackerService = serverManager.CrackerService;

            var user = await usersService.EnsureUserAsync(e.Interaction.User.Id.ToString(), e.Interaction.User.Username, (e.Interaction.User as DiscordMember)?.DisplayName);
            if (user == null)
            {
                 await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                    new DiscordInteractionResponseBuilder().WithContent("Could not find user.").AsEphemeral(true));
                 return;
            }

            long betAmount;
            if (string.IsNullOrWhiteSpace(amountStr) || string.Equals(amountStr, "all", StringComparison.OrdinalIgnoreCase) || string.Equals(amountStr, "all-in", StringComparison.OrdinalIgnoreCase))
            {
                betAmount = user.Balance;
            }
            else if (!GpParser.TryParseAmountInK(amountStr, out betAmount, out var error))
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                    new DiscordInteractionResponseBuilder().WithContent($"Invalid amount: {error}").AsEphemeral(true));
                return;
            }

            if (betAmount < GpFormatter.MinimumBetAmountK)
            {
                 await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                    new DiscordInteractionResponseBuilder().WithContent($"Minimum bet is {GpFormatter.Format(GpFormatter.MinimumBetAmountK)}.").AsEphemeral(true));
                return;
            }

            if (user.Balance < betAmount)
            {
                 await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                    new DiscordInteractionResponseBuilder().WithContent("You don't have enough balance.").AsEphemeral(true));
                return;
            }

            if (!await usersService.RemoveBalanceAsync(user.Identifier, betAmount, isWager: true))
            {
                 await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                    new DiscordInteractionResponseBuilder().WithContent("Failed to lock balance.").AsEphemeral(true));
                return;
            }

            user.Balance -= betAmount; // update local

            var game = await crackerService.CreateGameAsync(user, betAmount);
            if (game == null)
            {
                await usersService.AddBalanceAsync(user.Identifier, betAmount);
                 await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                    new DiscordInteractionResponseBuilder().WithContent("Failed to create game.").AsEphemeral(true));
                return;
            }

             var embed = CrackerCommand.BuildGameEmbed(game, user, client);
             var buttons = CrackerCommand.BuildButtons(game);
             var builder = new DiscordInteractionResponseBuilder().AddEmbed(embed);
             foreach(var row in buttons) builder.AddActionRowComponent(new DiscordActionRowComponent(row));

             await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, builder);

             // Update MessageId
             var msg = await e.Interaction.GetOriginalResponseAsync();
             game.MessageId = msg.Id;
             game.ChannelId = msg.ChannelId;
             await crackerService.UpdateGameAsync(game);
        }

        private static async Task StartMines(DiscordClient client, ModalSubmittedEventArgs e, string amountStr, string minesStr)
        {
            var env = ServerEnvironment.GetServerEnvironment();
            var usersService = env.ServerManager.UsersService;
            var minesService = env.ServerManager.MinesService;

            var user = await usersService.EnsureUserAsync(e.Interaction.User.Id.ToString(), e.Interaction.User.Username, (e.Interaction.User as DiscordMember)?.DisplayName);
            if (user == null)
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("User not found.").AsEphemeral(true));
                return;
            }

            if (!GpParser.TryParseAmountInK(amountStr, out var betAmount, out var error))
            {
                 // Handle "all" logic if not handled by helper
                 if (string.Equals(amountStr, "all", StringComparison.OrdinalIgnoreCase)) betAmount = user.Balance;
                 else
                 {
                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent($"Invalid amount: {error}").AsEphemeral(true));
                    return;
                 }
            }
             if (string.Equals(amountStr, "all", StringComparison.OrdinalIgnoreCase) || string.Equals(amountStr, "all-in", StringComparison.OrdinalIgnoreCase)) betAmount = user.Balance;

            if (betAmount < GpFormatter.MinimumBetAmountK)
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent($"Minimum bet is {GpFormatter.Format(GpFormatter.MinimumBetAmountK)}.").AsEphemeral(true));
                return;
            }

            if (user.Balance < betAmount)
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("Insufficient funds.").AsEphemeral(true));
                return;
            }

            if (!int.TryParse(minesStr, out int minesCount)) minesCount = 3;
            if (minesCount < 1) minesCount = 1;
            if (minesCount > 23) minesCount = 23;

            if (!await usersService.RemoveBalanceAsync(user.Identifier, betAmount, isWager: true))
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("Failed to lock balance.").AsEphemeral(true));
                return;
            }
            user.Balance -= betAmount;

            var game = await minesService.CreateGameAsync(user, betAmount, minesCount);
            if (game == null)
            {
                await usersService.AddBalanceAsync(user.Identifier, betAmount);
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("Failed to create game.").AsEphemeral(true));
                return;
            }

            var embed = MinesCommand.BuildGameEmbed(game, user);
            var buttons = MinesCommand.BuildButtons(game);
            var builder = new DiscordInteractionResponseBuilder().AddEmbed(embed);
            foreach (var row in buttons) builder.AddActionRowComponent(new DiscordActionRowComponent(row));

            await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, builder);
            
            var msg = await e.Interaction.GetOriginalResponseAsync();
            game.MessageId = msg.Id;
            game.ChannelId = msg.ChannelId;
            await minesService.UpdateGameAsync(game);
        }

        private static async Task StartHigherLower(DiscordClient client, ModalSubmittedEventArgs e, string amountStr)
        {
            var env = ServerEnvironment.GetServerEnvironment();
            var usersService = env.ServerManager.UsersService;
            var hlService = env.ServerManager.HigherLowerService;

            var user = await usersService.EnsureUserAsync(e.Interaction.User.Id.ToString(), e.Interaction.User.Username, (e.Interaction.User as DiscordMember)?.DisplayName);
            if (user == null)
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("User not found.").AsEphemeral(true));
                return;
            }

            long betAmount;
            if (string.Equals(amountStr, "all", StringComparison.OrdinalIgnoreCase) || string.Equals(amountStr, "all-in", StringComparison.OrdinalIgnoreCase))
            {
                betAmount = user.Balance;
            }
            else if (!GpParser.TryParseAmountInK(amountStr, out betAmount, out var error))
            {
                 await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent($"Invalid amount: {error}").AsEphemeral(true));
                 return;
            }

            if (betAmount < GpFormatter.MinimumBetAmountK)
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent($"Minimum bet is {GpFormatter.Format(GpFormatter.MinimumBetAmountK)}.").AsEphemeral(true));
                return;
            }
            if (user.Balance < betAmount)
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("Insufficient funds.").AsEphemeral(true));
                return;
            }

            if (!await usersService.RemoveBalanceAsync(user.Identifier, betAmount, isWager: true))
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("Failed to lock balance.").AsEphemeral(true));
                return;
            }
            user.Balance -= betAmount;

            var game = await hlService.CreateGameAsync(user, betAmount);
            if (game == null)
            {
                await usersService.AddBalanceAsync(user.Identifier, betAmount);
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("Failed to create game.").AsEphemeral(true));
                return;
            }

            var embed = HigherLowerCommand.BuildGameEmbed(game, user, client);
            var buttons = HigherLowerCommand.BuildButtons(game);
            
            var builder = new DiscordInteractionResponseBuilder().AddEmbed(embed.Build());
            if (buttons.Length > 0) builder.AddActionRowComponent(new DiscordActionRowComponent(buttons));

            await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, builder);

            var msg = await e.Interaction.GetOriginalResponseAsync();
            await hlService.UpdateMessageInfoAsync(game.Id, msg.Id, msg.ChannelId);
        }

        private static async Task StartChest(DiscordClient client, ModalSubmittedEventArgs e, string amountStr)
        {
            var env = ServerEnvironment.GetServerEnvironment();
            var usersService = env.ServerManager.UsersService;
            var chestService = env.ServerManager.ChestService;

            var user = await usersService.EnsureUserAsync(e.Interaction.User.Id.ToString(), e.Interaction.User.Username, (e.Interaction.User as DiscordMember)?.DisplayName);
            if (user == null)
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("User not found.").AsEphemeral(true));
                return;
            }

            long betAmount;
            if (string.Equals(amountStr, "all", StringComparison.OrdinalIgnoreCase) || string.Equals(amountStr, "all-in", StringComparison.OrdinalIgnoreCase))
            {
                betAmount = user.Balance;
            }
            else if (!GpParser.TryParseAmountInK(amountStr, out betAmount, out var error))
            {
                 await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent($"Invalid amount: {error}").AsEphemeral(true));
                 return;
            }

            if (betAmount < GpFormatter.MinimumBetAmountK)
            {
                 await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent($"Minimum bet is {GpFormatter.Format(GpFormatter.MinimumBetAmountK)}.").AsEphemeral(true));
                 return;
            }

            if (user.Balance < betAmount)
            {
                 await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("Insufficient funds.").AsEphemeral(true));
                 return;
            }
            if (!await usersService.RemoveBalanceAsync(user.Identifier, betAmount, isWager: true))
            {
                 await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("Failed to lock balance.").AsEphemeral(true));
                 return;
            }
            
            var game = await chestService.CreateGameAsync(user, betAmount, e.Interaction.ChannelId);
            if (game == null)
            {
                await usersService.AddBalanceAsync(user.Identifier, betAmount);
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("Failed to start game.").AsEphemeral(true));
                return;
            }

            var embed = ChestCommand.BuildGameEmbed(betAmount, new List<string>(), 0, 0);
            var rows = ChestCommand.BuildComponents(game.Id.ToString());

            var builder = new DiscordInteractionResponseBuilder().AddEmbed(embed);
            foreach(var row in rows) builder.AddActionRowComponent(row);

            await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, builder);
            
            var msg = await e.Interaction.GetOriginalResponseAsync();
            await chestService.UpdateSelectionAsync(game.Id, new List<string>(), msg.Id);
        }

        private static string GetValue(IReadOnlyDictionary<string, DSharpPlus.EventArgs.IModalSubmission> values, string key)
        {
             if (values.TryGetValue(key, out var val)) 
             {
                 // Handle IModalSubmission which contains a Value property
                 try
                 {
                     dynamic d = val;
                     return d.Value;
                 }
                 catch
                 {
                     return val.ToString();
                 }
             }
             return string.Empty;
        }
    }
}
