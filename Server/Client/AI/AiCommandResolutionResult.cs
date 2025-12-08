namespace Server.Client.AI
{
    public class AiCommandResolutionResult
    {
        public bool IsMatch { get; set; }
        public string Command { get; set; }
        public string Args { get; set; }
        public double Confidence { get; set; }
        public string Suggestion { get; set; }
    }
}
