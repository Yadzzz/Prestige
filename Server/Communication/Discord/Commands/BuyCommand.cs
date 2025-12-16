using System;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Server.Client.Payments;
using Server.Client.Users;
using Server.Infrastructure;
using Server.Infrastructure.Configuration;
using Server.Infrastructure.Discord;

namespace Server.Communication.Discord.Commands
{
    public class BuyCommand : BaseCommandModule
    {
        [Command("buy")]
        public async Task Buy(CommandContext ctx, double amountM = 0)
        {
            if (!ctx.Member.IsStaff())
            {
                await ctx.RespondAsync("This command is currently restricted to staff only.");
                return;
            }

            if (amountM <= 0)
            {
                await ctx.RespondAsync("Please specify a valid amount of Millions (M) to buy (e.g., `!buy 10` for 10M).");
                return;
            }

            // Rate: Configured in appsettings.json (Default: 1M = $0.15)
            double ratePerM = ConfigService.Current.Payments?.UsdPerMillion ?? 0.15;
            double priceUsd = amountM * ratePerM;

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
            var description = $"Purchase of {amountM:N0}M credits for {user.DisplayName}";

            // Save order to DB
            await serverManager.PaymentOrdersService.CreateOrderAsync(orderId, user.Id.ToString(), ctx.Channel.Id.ToString(), amountM, priceUsd);

            // Create Invoice (better for user choice of crypto) or Payment (specific crypto)
            // Let's use Invoice to give them a link where they can choose currency
            var invoice = await paymentsService.CreateInvoiceAsync(priceUsd, "USD", orderId, description);

            if (invoice == null)
            {
                await ctx.RespondAsync("Failed to create payment invoice. Please try again later.");
                return;
            }

            var qrCodeUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=250x250&data={Uri.EscapeDataString(invoice.invoice_url)}";

            var embed = new DiscordEmbedBuilder()
                .WithTitle("🛒 Purchase Credits")
                .WithDescription($"You are purchasing **{amountM:N0}M** credits for **${priceUsd:N2}**.\n\n**Scan to Pay on Mobile:**")
                .AddField("Invoice URL", $"[Click here to pay]({invoice.invoice_url})")
                .WithThumbnail(qrCodeUrl)
                .WithColor(DiscordColor.Blurple)
                .WithFooter($"Order ID: {orderId}")
                .WithTimestamp(DateTime.UtcNow);
            
            await ctx.RespondAsync(embed: embed);
        }
    }
}
