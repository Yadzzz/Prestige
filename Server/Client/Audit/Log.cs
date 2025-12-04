using System;

namespace Server.Client.Audit
{
    public class Log
    {
        public int Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Source { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string? UserIdentifier { get; set; }
        public string? Action { get; set; }
        public string? Message { get; set; }
        public string? Exception { get; set; }
        public string? MetadataJson { get; set; }
    }
}
