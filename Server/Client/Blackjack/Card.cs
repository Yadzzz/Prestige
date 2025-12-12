using Discord.WebSocket;
using Server.Infrastructure.Configuration;

namespace Server.Client.Blackjack
{
    public class Card
    {
        public string Suit { get; set; }
        public string Rank { get; set; }

        public Card(string suit, string rank)
        {
            Suit = suit;
            Rank = rank;
        }

        public int GetValue(int currentTotal)
        {
            if (Rank == "A")
            {
                // Ace: 11 if it doesn't bust, otherwise 1
                return (currentTotal + 11 > 21) ? 1 : 11;
            }
            else if (Rank == "K" || Rank == "Q" || Rank == "J")
            {
                return 10;
            }
            else
            {
                return int.Parse(Rank);
            }
        }

        public string GetEmoji(DiscordSocketClient client = null)
        {
            // Try to get configured card emoji
            var config = ConfigService.Current?.Discord?.BlackjackCards;
            if (config != null)
            {
                SuitConfig suitConfig = Suit switch
                {
                    "Clubs" => config.Clubs,
                    "Diamonds" => config.Diamonds,
                    "Hearts" => config.Hearts,
                    "Spades" => config.Spades,
                    _ => null
                };

                if (suitConfig != null)
                {
                    ulong emojiId = Rank switch
                    {
                        "2" => suitConfig.Two,
                        "3" => suitConfig.Three,
                        "4" => suitConfig.Four,
                        "5" => suitConfig.Five,
                        "6" => suitConfig.Six,
                        "7" => suitConfig.Seven,
                        "8" => suitConfig.Eight,
                        "9" => suitConfig.Nine,
                        "10" => suitConfig.Ten,
                        "J" => suitConfig.Jack,
                        "Q" => suitConfig.Queen,
                        "K" => suitConfig.King,
                        "A" => suitConfig.Ace,
                        _ => 0
                    };

                    if (emojiId > 0)
                    {
                        return $"<:card:{emojiId}>";
                    }
                }
            }

            var suitEmoji = Suit switch
            {
                "Hearts" => "♥️",
                "Diamonds" => "♦️",
                "Clubs" => "♣️",
                "Spades" => "♠️",
                _ => ""
            };

            return $"{suitEmoji} {Rank}";
        }

        public override string ToString()
        {
            return $"{Rank} of {Suit}";
        }
    }
}
