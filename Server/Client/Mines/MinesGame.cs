using System;
using System.Collections.Generic;

namespace Server.Client.Mines
{
    public class MinesGame
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Identifier { get; set; }
        public long BetAmount { get; set; }
        public int MinesCount { get; set; }
        public MinesGameStatus Status { get; set; }
        
        // Indices of mines (0-23)
        public List<int> MineLocations { get; set; } = new List<int>();
        
        // Indices of revealed tiles (0-23)
        public List<int> RevealedTiles { get; set; } = new List<int>();
        
        public ulong? MessageId { get; set; }
        public ulong? ChannelId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public bool IsMine(int tileIndex)
        {
            return MineLocations.Contains(tileIndex);
        }

        public bool IsRevealed(int tileIndex)
        {
            return RevealedTiles.Contains(tileIndex);
        }
    }

    public enum MinesGameStatus
    {
        Active = 0,
        CashedOut = 1,
        Lost = 2,
        Cancelled = 3
    }
}
