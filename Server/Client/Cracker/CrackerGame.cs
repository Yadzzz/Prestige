using System;
using System.Collections.Generic;

namespace Server.Client.Cracker
{
    public class CrackerGame
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Identifier { get; set; }
        public long BetAmount { get; set; }
        public CrackerGameStatus Status { get; set; }
        
        // Selected hats (Red, Yellow, Green, Blue, Purple, White)
        public HashSet<string> SelectedHats { get; set; } = new HashSet<string>();
        
        public string ResultHat { get; set; } // The hat that was pulled
        public decimal Multiplier { get; set; }
        public long Payout { get; set; }
        
        public ulong? MessageId { get; set; }
        public ulong? ChannelId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public enum CrackerGameStatus
    {
        Active = 0,
        Finished = 1,
        Cancelled = 2
    }
}
