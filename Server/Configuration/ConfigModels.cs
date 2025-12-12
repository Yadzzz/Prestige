using System.Collections.Generic;

namespace Server.Configuration
{
    public class AppConfig
    {
        public string ActiveProfile { get; set; }
        public Dictionary<string, EnvironmentConfig> Environments { get; set; }
    }

    public class EnvironmentConfig
    {
        public string ServerName { get; set; }
        public DatabaseConfig Database { get; set; }
        public DiscordConfig Discord { get; set; }
    }

    public class DatabaseConfig
    {
        public string ConnectionString { get; set; }
    }

    public class DiscordConfig
    {
        public string BotToken { get; set; }
        public ulong GuildId { get; set; }
        public ulong StaffRoleId { get; set; }
        public DiscordChannels Channels { get; set; }
        public DiscordTicketCategories TicketCategories { get; set; }
        public DiscordEmojis Emojis { get; set; }
    }

    public class DiscordChannels
    {
        public ulong DepositStaffChannelId { get; set; }
        public ulong WithdrawStaffChannelId { get; set; }
        public ulong StakeStaffChannelId { get; set; }
        public ulong LiveFeedChannelId { get; set; }
        public ulong RaceChannelId { get; set; }
    }

    public class DiscordTicketCategories
    {
        public ulong[] DepositIds { get; set; }
        public ulong[] WithdrawIds { get; set; }
        public ulong[] CoinflipIds { get; set; }
        public ulong[] StakeIds { get; set; }
    }

    public class DiscordEmojis
    {
        public CoinflipEmojis Coinflip { get; set; }
        public LiveFeedEmojis LiveFeed { get; set; }
        public BalanceEmojis Balance { get; set; }
        public BlackjackCardEmojis BlackjackCards { get; set; }
    }

    public class BlackjackCardEmojis
    {
        public SuitEmojis Clubs { get; set; }
        public SuitEmojis Diamonds { get; set; }
        public SuitEmojis Hearts { get; set; }
        public SuitEmojis Spades { get; set; }
    }

    public class SuitEmojis
    {
        public ulong Two { get; set; }
        public ulong Three { get; set; }
        public ulong Four { get; set; }
        public ulong Five { get; set; }
        public ulong Six { get; set; }
        public ulong Seven { get; set; }
        public ulong Eight { get; set; }
        public ulong Nine { get; set; }
        public ulong Ten { get; set; }
        public ulong Jack { get; set; }
        public ulong Queen { get; set; }
        public ulong King { get; set; }
        public ulong Ace { get; set; }
    }

    public class CoinflipEmojis
    {
        public ulong Heads { get; set; }
        public ulong Tails { get; set; }
        public ulong Rm { get; set; }
        public ulong Half { get; set; }
        public ulong Max { get; set; }
        public ulong X2 { get; set; }
        public ulong Exit { get; set; }
        public ulong Gold { get; set; }
        public ulong Silver { get; set; }
    }

    public class LiveFeedEmojis
    {
        public ulong Ddp { get; set; }
        public ulong Wpray { get; set; }
        public ulong BigWinMvpp { get; set; }
    }

    public class BalanceEmojis
    {
        public ulong Sheet { get; set; }
        public ulong Withdraw { get; set; }
        public ulong Deposit { get; set; }
        public ulong Wallet { get; set; }
        public ulong Buy { get; set; }
    }
}
