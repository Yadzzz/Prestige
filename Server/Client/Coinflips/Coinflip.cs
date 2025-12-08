namespace Server.Client.Coinflips
{
    public enum CoinflipStatus
    {
        Pending = 0,
        Finished = 1,
        Cancelled = 2
    }

    public class Coinflip
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Identifier { get; set; }
        public long AmountK { get; set; }
        public bool? ChoseHeads { get; set; }
        public bool? ResultHeads { get; set; }
        public CoinflipStatus Status { get; set; }
        public ulong? MessageId { get; set; }
        public ulong? ChannelId { get; set; }
        public System.DateTime CreatedAt { get; set; }
        public System.DateTime UpdatedAt { get; set; }
    }
}
