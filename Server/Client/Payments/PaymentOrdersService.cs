using Server.Infrastructure.Database;
using System;
using System.Data;
using System.Threading.Tasks;

namespace Server.Client.Payments
{
    public class PaymentOrdersService
    {
        private readonly DatabaseManager _databaseManager;

        public PaymentOrdersService(DatabaseManager databaseManager)
        {
            _databaseManager = databaseManager;
        }

        public async Task CreateOrderAsync(string orderId, string userId, string channelId, double amountM, double priceUsd)
        {
            using (var cmd = new DatabaseCommand())
            {
                cmd.SetCommand(@"INSERT INTO payment_orders (order_id, user_id, channel_id, amount_m, price_usd, status) 
                               VALUES (@OrderId, @UserId, @ChannelId, @AmountM, @PriceUsd, 'PENDING')");
                
                cmd.AddParameter("@OrderId", orderId);
                cmd.AddParameter("@UserId", userId);
                cmd.AddParameter("@ChannelId", channelId);
                cmd.AddParameter("@AmountM", amountM);
                cmd.AddParameter("@PriceUsd", priceUsd);

                await cmd.ExecuteQueryAsync();
            }
        }

        public async Task<PaymentOrder> GetOrderAsync(string orderId)
        {
            using (var cmd = new DatabaseCommand())
            {
                cmd.SetCommand("SELECT * FROM payment_orders WHERE order_id = @OrderId");
                cmd.AddParameter("@OrderId", orderId);

                var dt = await cmd.ExecuteDataTableAsync();
                if (dt.Rows.Count == 0) return null;

                var row = dt.Rows[0];
                return new PaymentOrder
                {
                    Id = Convert.ToInt32(row["id"]),
                    OrderId = row["order_id"].ToString(),
                    UserId = row["user_id"].ToString(),
                    ChannelId = row["channel_id"].ToString(),
                    AmountM = Convert.ToDouble(row["amount_m"]),
                    PriceUsd = Convert.ToDouble(row["price_usd"]),
                    Status = row["status"].ToString(),
                    CreatedAt = Convert.ToDateTime(row["created_at"]),
                    UpdatedAt = Convert.ToDateTime(row["updated_at"])
                };
            }
        }

        public async Task UpdateStatusAsync(string orderId, string status)
        {
            using (var cmd = new DatabaseCommand())
            {
                cmd.SetCommand("UPDATE payment_orders SET status = @Status WHERE order_id = @OrderId");
                cmd.AddParameter("@Status", status);
                cmd.AddParameter("@OrderId", orderId);

                await cmd.ExecuteQueryAsync();
            }
        }
    }
}