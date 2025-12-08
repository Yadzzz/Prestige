using System;

namespace Server.Client.Transactions
{
    public class BalanceAdjustment
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserIdentifier { get; set; }
        public int? StaffId { get; set; }
        public string StaffIdentifier { get; set; }
        public BalanceAdjustmentType AdjustmentType { get; set; }
        public long AmountK { get; set; }
        public string Source { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Reason { get; set; }
    }
}
