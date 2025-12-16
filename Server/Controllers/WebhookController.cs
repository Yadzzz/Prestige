using Microsoft.AspNetCore.Mvc;
using Server.Infrastructure;
using Server.Client.Payments;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Server.Infrastructure.Configuration;

namespace Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WebhookController : ControllerBase
    {
        private readonly ServerManager _serverManager;

        public WebhookController(ServerManager serverManager)
        {
            _serverManager = serverManager;
        }

        [HttpPost("nowpayments")]
        public async Task<IActionResult> NowPaymentsWebhook()
        {
            // 1. Read the request body
            string requestBody;
            using (var reader = new StreamReader(Request.Body))
            {
                requestBody = await reader.ReadToEndAsync();
            }

            // 2. Verify Signature (Security)
            if (!Request.Headers.TryGetValue("x-nowpayments-sig", out var signature))
            {
                return BadRequest("Missing signature");
            }

            var ipnSecret = ConfigService.Current.Payments?.IpnSecret;
            if (string.IsNullOrEmpty(ipnSecret))
            {
                Console.WriteLine("[Webhook] Error: IPN Secret is missing in configuration.");
                return StatusCode(500, "Server configuration error");
            }

            if (!VerifySignature(requestBody, signature, ipnSecret))
            {
                Console.WriteLine("[Webhook] Error: Invalid signature.");
                return BadRequest("Invalid signature");
            }

            // 3. Parse the IPN data
            try
            {
                var ipnData = JsonSerializer.Deserialize<NowPaymentsIpn>(requestBody);
                if (ipnData == null) return BadRequest("Invalid JSON");

                Console.WriteLine($"[Webhook] Received payment update: ID={ipnData.payment_id}, Status={ipnData.payment_status}, Order={ipnData.order_id}");

                // 4. Handle "finished" status (Payment confirmed)
                if (ipnData.payment_status == "finished")
                {
                    // Order ID format: BUY-{UserId}-{Ticks}
                    var parts = ipnData.order_id.Split('-');
                    if (parts.Length >= 2 && parts[0] == "BUY")
                    {
                        var userId = parts[1];
                        
                        // Calculate amount to credit (using pay_amount or price_amount depending on logic)
                        // Usually we credit the price_amount (USD value) requested
                        // But we should verify they paid enough. NowPayments handles that mostly.
                        
                        if (double.TryParse(ipnData.price_amount.ToString(), out double amountUsd))
                        {
                            // Rate: Configured in appsettings.json (Default: 1M = $0.15)
                            double ratePerM = ConfigService.Current.Payments?.UsdPerMillion ?? 0.15;
                            
                            // Credits (K) = (USD / Rate) * 1000
                            double millions = amountUsd / ratePerM;
                            long creditsToAddK = (long)(millions * 1000); 

                            var user = await _serverManager.UsersService.GetUserAsync(userId);
                            if (user != null)
                            {
                                await _serverManager.UsersService.AddBalanceAsync(user.Identifier, creditsToAddK);
                                
                                // Log it
                                await _serverManager.LogsService.LogAsync(
                                    "Webhook", "Info", user.Identifier, "PaymentReceived", 
                                    $"User bought {millions:N2}M (${amountUsd}) and received {creditsToAddK}K credits. PaymentId: {ipnData.payment_id}", null);

                                // Update Order Status and Notify User
                                var order = await _serverManager.PaymentOrdersService.GetOrderAsync(ipnData.order_id);
                                if (order != null)
                                {
                                    await _serverManager.PaymentOrdersService.UpdateStatusAsync(ipnData.order_id, "COMPLETED");

                                    if (ulong.TryParse(order.ChannelId, out ulong channelId))
                                    {
                                        try 
                                        {
                                            var channel = await _serverManager.DiscordBotHost.Client.GetChannelAsync(channelId);
                                            if (channel != null)
                                            {
                                                var embed = new DSharpPlus.Entities.DiscordEmbedBuilder()
                                                    .WithTitle("✅ Payment Successful")
                                                    .WithDescription($"<@{userId}>, your payment of **${amountUsd:N2}** has been received!\n**{creditsToAddK/1000.0:N2}M** credits have been added to your account.")
                                                    .WithColor(DSharpPlus.Entities.DiscordColor.Green)
                                                    .WithTimestamp(DateTime.UtcNow);
                                                
                                                await channel.SendMessageAsync(embed: embed);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"Failed to send payment notification: {ex.Message}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Webhook] Error processing: {ex.Message}");
                return StatusCode(500, "Internal Error");
            }
        }

        private bool VerifySignature(string requestBody, string signature, string secret)
        {
            using (var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret)))
            {
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(requestBody));
                var calculatedSignature = BitConverter.ToString(hash).Replace("-", "").ToLower();
                return calculatedSignature == signature;
            }
        }
    }

    public class NowPaymentsIpn
    {
        public object payment_id { get; set; } // Can be int or string
        public string payment_status { get; set; }
        public string pay_address { get; set; }
        public object price_amount { get; set; } // Can be string or double in JSON
        public string price_currency { get; set; }
        public object pay_amount { get; set; }
        public string pay_currency { get; set; }
        public string order_id { get; set; }
        public string order_description { get; set; }
    }
}
