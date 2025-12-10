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

        public string GetEmoji()
        {
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
