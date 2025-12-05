using System;

namespace Server.Client.Stakes
{
    public enum StakeStatus
    {
        Pending = 0,
        Won = 1,
        Lost = 2,
        Cancelled = 3
    }

    public class Stake
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Identifier { get; set; }
        public long AmountK { get; set; }
        public long FeeK { get; set; }
        public StakeStatus Status { get; set; }
        public ulong? UserMessageId { get; set; }
        public ulong? UserChannelId { get; set; }
        public ulong? StaffMessageId { get; set; }
        public ulong? StaffChannelId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
