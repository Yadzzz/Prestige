using System;

namespace Server.Client.Races
{
    public class RaceParticipant
    {
        public int RaceId { get; set; }
        public string UserIdentifier { get; set; }
        public long TotalWagered { get; set; }
        public string Username { get; set; } // Cached for display
    }
}
