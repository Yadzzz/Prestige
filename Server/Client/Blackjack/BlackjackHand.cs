using DSharpPlus;
using System.Collections.Generic;
using System.Linq;
using Server.Infrastructure.Discord;

namespace Server.Client.Blackjack
{
    public class BlackjackHand
    {
        public List<Card> Cards { get; set; } = new List<Card>();
        public long BetAmount { get; set; }
        public bool IsStanding { get; set; }
        public bool IsDoubled { get; set; }
        public bool IsBusted { get; set; }

        public int GetTotal()
        {
            int total = 0;
            int aceCount = 0;

            foreach (var card in Cards)
            {
                if (card.Rank == "A")
                {
                    aceCount++;
                    total += 11;
                }
                else if (card.Rank == "K" || card.Rank == "Q" || card.Rank == "J")
                {
                    total += 10;
                }
                else
                {
                    total += int.Parse(card.Rank);
                }
            }

            // Adjust for aces
            while (total > 21 && aceCount > 0)
            {
                total -= 10;
                aceCount--;
            }

            return total;
        }

        public bool IsBlackjack()
        {
            return Cards.Count == 2 && GetTotal() == 21;
        }

        public bool CanSplit()
        {
            if (Cards.Count != 2 || IsDoubled)
                return false;

            // Check if both cards have same rank
            var rank1 = Cards[0].Rank;
            var rank2 = Cards[1].Rank;

            // For face cards, treat them as equal
            if ((rank1 == "K" || rank1 == "Q" || rank1 == "J") &&
                (rank2 == "K" || rank2 == "Q" || rank2 == "J"))
                return true;

            return rank1 == rank2;
        }

        public bool CanDouble()
        {
            return Cards.Count == 2 && !IsDoubled;
        }

        public string GetHandDisplay(DiscordClient client, bool hideHoleCard = false)
        {
            if (hideHoleCard && Cards.Count > 1)
            {
                // Show first card, hide second (hole card)
                var backsideId = DiscordIds.BlackjackBacksideEmojiId;
                string backsideEmoji = "🂠";
                
                if (backsideId > 0)
                {
                     if (client != null)
                     {
                         try { backsideEmoji = DSharpPlus.Entities.DiscordEmoji.FromGuildEmote(client, backsideId).ToString(); }
                         catch { backsideEmoji = $"<:back:{backsideId}>"; }
                     }
                     else
                     {
                         backsideEmoji = $"<:back:{backsideId}>";
                     }
                }

                return $"{Cards[0].GetEmoji(client)} {backsideEmoji}";
            }

            return string.Join(" ", Cards.Select(c => c.GetEmoji(client)));
        }
    }
}
