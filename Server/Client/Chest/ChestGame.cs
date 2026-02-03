using System;
using System.Collections.Generic;

namespace Server.Client.Chest
{
    public enum ChestGameStatus
    {
        Selection = 0,
        Pending = 1, // Ready to play (items selected, waiting for confirm) - actually we can just use Selection until Play is clicked
        Finished = 2,
        Cancelled = 3
    }

    public class ChestGame
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Identifier { get; set; }
        public long BetAmountK { get; set; }
        public string SelectedItemIds { get; set; } // Comma separated IDs
        public bool? Won { get; set; }
        public long PrizeValueK { get; set; }
        public ChestGameStatus Status { get; set; }
        public ulong? MessageId { get; set; }
        public ulong? ChannelId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Helper to get list
        public List<string> GetSelectedIds()
        {
            if (string.IsNullOrEmpty(SelectedItemIds)) return new List<string>();
            return new List<string>(SelectedItemIds.Split(',', StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
