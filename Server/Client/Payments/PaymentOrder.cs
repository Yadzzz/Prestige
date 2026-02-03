using System;

namespace Server.Client.Payments
{
    public class PaymentOrder
    {
        public int Id { get; set; }
        public string OrderId { get; set; }
        public string UserId { get; set; }
        public string ChannelId { get; set; }
        public double AmountM { get; set; }
        public double PriceUsd { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}