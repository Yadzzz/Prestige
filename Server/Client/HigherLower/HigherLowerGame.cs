using System;
using System.Collections.Generic;
using Server.Client.Blackjack;

namespace Server.Client.HigherLower
{
    public class HigherLowerGame
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Identifier { get; set; }
        public long BetAmount { get; set; }
        public decimal CurrentPayout { get; set; }
        public int CurrentRound { get; set; }
        public int MaxRounds { get; set; }
        public HigherLowerGameStatus Status { get; set; }
        public Card LastCard { get; set; }
        public List<Card> CardHistory { get; set; } = new List<Card>();
        public ulong? MessageId { get; set; }
        public ulong? ChannelId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public static int GetCardValue(Card card)
        {
            return card.Rank switch
            {
                "A" => 1,
                "2" => 2,
                "3" => 3,
                "4" => 4,
                "5" => 5,
                "6" => 6,
                "7" => 7,
                "8" => 8,
                "9" => 9,
                "10" => 10,
                "J" => 11,
                "Q" => 12,
                "K" => 13,
                _ => 0
            };
        }
    }

    public enum HigherLowerGameStatus
    {
        Active = 0,
        Won = 1,
        Lost = 2,
        CashedOut = 3
    }
}
