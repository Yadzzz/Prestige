using System;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Server.Client.Payments;
using Server.Client.Users;
using Server.Infrastructure;
using Server.Infrastructure.Discord;

namespace Server.Communication.Discord.Commands
{
    public class BuyCommand : BaseCommandModule
    {
        [Command("buy")]
        public async Task Buy(CommandContext ctx, double amount = 0)
        {
            if (amount <= 0)
            {
                await ctx.RespondAsync("Please specify a valid amount to buy (e.g., `!buy 10` for $10).");
                return;
            }

            var env = ServerEnvironment.GetServerEnvironment();
            var serverManager = env.ServerManager;
            var usersService = serverManager.UsersService;
            var paymentsService = serverManager.NowPaymentsService;

            if (paymentsService == null)
            {
                await ctx.RespondAsync("Payments are currently disabled (API Key missing).");
                return;
            }

            var user = await usersService.EnsureUserAsync(ctx.User.Id.ToString(), ctx.User.Username, ctx.Member.DisplayName);
            if (user == null) return;

            var orderId = $"BUY-{user.Id}-{DateTime.UtcNow.Ticks}";
            var description = $"Purchase of ${amount} credits for {user.DisplayName}";

            // Create Invoice (better for user choice of crypto) or Payment (specific crypto)
            // Let's use Invoice to give them a link where they can choose currency
            var invoice = await paymentsService.CreateInvoiceAsync(amount, "USD", orderId, description);

            if (invoice == null)
            {
                await ctx.RespondAsync("Failed to create payment invoice. Please try again later.");
                return;
            }

            var embed = new DiscordEmbedBuilder()
                .WithTitle("🛒 Purchase Credits")
                .WithDescription($"You are purchasing **${amount}** worth of credits.")
                .AddField("Invoice URL", $"[Click here to pay]({invoice.invoice_url})")
                .WithColor(DiscordColor.Blurple)
                .WithFooter($"Order ID: {orderId}")
                .WithTimestamp(DateTime.UtcNow);

            // If we had a QR code URL, we could add it as image
            // NowPayments invoice URL page has the QR code.
            
            await ctx.RespondAsync(embed: embed);
        }
    }
}
