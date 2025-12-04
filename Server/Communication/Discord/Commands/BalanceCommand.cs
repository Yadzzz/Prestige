using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Server.Client.Users;
using Server.Client.Transactions;
using Server.Infrastructure;
using Server.Client.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Server.Communication.Discord.Commands
{
    public class BalanceCommand : BaseCommandModule
    {
        [Command("balance")]
        [Aliases("bal", "wallet", "money", "gp")]
        public async Task Balance(CommandContext ctx)
        {
            var env = ServerEnvironment.GetServerEnvironment();
            var usersService = env.ServerManager.UsersService;
            var transactionsService = env.ServerManager.TransactionsService;

            var user = await usersService.EnsureUserAsync(ctx.User.Id.ToString(), ctx.User.Username, ctx.Member.DisplayName);
            if (user == null)
                return;

            var formatted = GpFormatter.Format(user.Balance);

            // Build embed
            var embed = new DiscordEmbedBuilder()
                .WithTitle("Balance")
                .WithDescription($"{ctx.Member.DisplayName}, you have **{formatted}** in your wallet.")
                .WithColor(DiscordColor.Gold)
                .WithThumbnail("https://i.imgur.com/DHXgtn5.gif")
                .WithFooter($"Prestige Bets")
                //.WithFooter($"Prestige Bets • {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm}");
                .WithTimestamp(System.DateTimeOffset.UtcNow);

            // Buttons row 1
            var row1 = new[]
            {
            new DiscordButtonComponent(ButtonStyle.Primary, "bal_deposit", "Deposit", emoji: new DiscordComponentEmoji("💳")),
            new DiscordButtonComponent(ButtonStyle.Primary, "bal_withdraw", "Withdraw", emoji: new DiscordComponentEmoji("🏧")),
            //new DiscordButtonComponent(ButtonStyle.Success, "bal_buy", "Buy", emoji: new DiscordComponentEmoji("🛒"))
        };

            // Buttons row 2
        //    var row2 = new[]
        //    {
        //    new DiscordButtonComponent(ButtonStyle.Secondary, "bal_profile", "Profile", emoji: new DiscordComponentEmoji("👤")),
        //    new DiscordButtonComponent(ButtonStyle.Secondary, "bal_history", "History", emoji: new DiscordComponentEmoji("📜")),
        //    new DiscordButtonComponent(ButtonStyle.Secondary, "bal_games", "Games", emoji: new DiscordComponentEmoji("🎲"))
        //};

            // Send embed + buttons
            await ctx.RespondAsync(new DiscordMessageBuilder()
                .AddEmbed(embed)
                .AddComponents(row1)
                //.AddComponents(row2)
            );
        }
    }
}
