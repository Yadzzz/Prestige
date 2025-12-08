using System;
using System.Collections.Generic;

namespace Server.Client.Races
{
    public enum RaceStatus
    {
        Pending,
        Active,
        Finished,
        Cancelled
    }

    public class RacePrize
    {
        public int Rank { get; set; }
        public string Prize { get; set; } = string.Empty; // e.g. "100M" or "Item Name"
    }

    public class Race
    {
        public int Id { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public RaceStatus Status { get; set; }
        
        // Serialized JSON of List<RacePrize>
        public string PrizeDistributionJson { get; set; } = "[]";
        
        public ulong ChannelId { get; set; }
        public ulong MessageId { get; set; }

        public List<RacePrize> GetPrizes()
        {
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<RacePrize>>(PrizeDistributionJson) ?? new List<RacePrize>();
            }
            catch
            {
                return new List<RacePrize>();
            }
        }

        public void SetPrizes(List<RacePrize> prizes)
        {
            PrizeDistributionJson = System.Text.Json.JsonSerializer.Serialize(prizes);
        }
    }
}
