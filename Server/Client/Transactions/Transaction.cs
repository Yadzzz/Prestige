using System;

namespace Server.Client.Transactions
{
    public enum TransactionType
    {
        Deposit = 0,
        Withdraw = 1
    }

    public enum TransactionStatus
    {
        Pending = 0,
        Accepted = 1,
        Cancelled = 2,
        Denied = 3
    }

    public class Transaction
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Identifier { get; set; }
        public long AmountK { get; set; }
        public TransactionType Type { get; set; }
        public TransactionStatus Status { get; set; }
        public int? StaffId { get; set; }
        public string StaffIdentifier { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string Notes { get; set; }
    }
}
