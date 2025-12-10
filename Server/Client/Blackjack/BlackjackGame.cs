using System;
using System.Collections.Generic;

namespace Server.Client.Blackjack
{
    public class BlackjackGame
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Identifier { get; set; }
        public long BetAmount { get; set; }
        public BlackjackGameStatus Status { get; set; }
        public List<Card> DeckState { get; set; } = new List<Card>();
        public BlackjackHand DealerHand { get; set; } = new BlackjackHand();
        public List<BlackjackHand> PlayerHands { get; set; } = new List<BlackjackHand>();
        public int CurrentHandIndex { get; set; }
        public bool InsuranceTaken { get; set; }
        public bool InsuranceDeclined { get; set; }
        public ulong? MessageId { get; set; }
        public ulong? ChannelId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public BlackjackHand GetCurrentHand()
        {
            if (CurrentHandIndex >= 0 && CurrentHandIndex < PlayerHands.Count)
                return PlayerHands[CurrentHandIndex];
            return null;
        }

        public bool AllHandsFinished()
        {
            foreach (var hand in PlayerHands)
            {
                if (!hand.IsStanding && !hand.IsBusted)
                    return false;
            }
            return true;
        }
    }

    public enum BlackjackGameStatus
    {
        Active = 0,
        Finished = 1
    }
}
