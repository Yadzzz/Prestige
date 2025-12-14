using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Server.Infrastructure.Configuration
{
    public class AppSettings
    {
        public string ActiveProfile { get; set; }
        public Dictionary<string, EnvironmentConfig> Profiles { get; set; }
    }

    public class EnvironmentConfig
    {
        public string ServerName { get; set; }
        public string ShortName { get; set; }
        public string ConnectionString { get; set; }
        public DiscordConfig Discord { get; set; }
    }

    public class DiscordConfig
    {
        public string BotToken { get; set; }
        public ulong GuildId { get; set; }
        public ulong StaffRoleId { get; set; }
        
        // Channels
        public ulong DepositStaffChannelId { get; set; }
        public ulong WithdrawStaffChannelId { get; set; }
        public ulong StakeStaffChannelId { get; set; }
        public ulong LiveFeedChannelId { get; set; }
        public ulong RaceChannelId { get; set; }

        // Ticket Categories
        public ulong[] DepositTicketCategoryIds { get; set; }
        public ulong[] WithdrawTicketCategoryIds { get; set; }
        public ulong[] CoinflipTicketCategoryIds { get; set; }
        public ulong[] StakeTicketCategoryIds { get; set; }

        // Emojis
        public ulong CoinflipHeadsEmojiId { get; set; }
        public ulong CoinflipTailsEmojiId { get; set; }
        public ulong CoinflipRmEmojiId { get; set; }
        public ulong CoinflipHalfEmojiId { get; set; }
        public ulong CoinflipMaxEmojiId { get; set; }
        public ulong CoinflipX2EmojiId { get; set; }
        public ulong CoinflipExitEmojiId { get; set; }

        public ulong DdpEmojiId { get; set; }
        public ulong WprayEmojiId { get; set; }
        public ulong BigWinMvppEmojiId { get; set; }
        public ulong CoinflipGoldEmojiId { get; set; }
        public ulong CoinflipSilverEmojiId { get; set; }

        // Balance Emojis
        public ulong BalanceSheetEmojiId { get; set; }
        public ulong WithdrawEmojiId { get; set; }
        public ulong DepositEmojiId { get; set; }
        public ulong WalletEmojiId { get; set; }
        public ulong BuyEmojiId { get; set; }

        // Blackjack Emojis
        public ulong BlackjackHitEmojiId { get; set; }
        public ulong BlackjackStandEmojiId { get; set; }
        public ulong BlackjackDoubleEmojiId { get; set; }
        public ulong BlackjackSplitEmojiId { get; set; }
        public ulong BlackjackBacksideEmojiId { get; set; }
        public ulong BlackjackSpadesEmojiId { get; set; }
        public ulong BlackjackDiamondsEmojiId { get; set; }
        public ulong BlackjackHeartsEmojiId { get; set; }
        public ulong BlackjackClubsEmojiId { get; set; }

        // Blackjack Rematch Emojis
        public ulong BlackjackRmEmojiId { get; set; }
        public ulong BlackjackRmHalfEmojiId { get; set; }
        public ulong BlackjackRmX2EmojiId { get; set; }

        // Higher/Lower Emojis
        public ulong HigherLowerHigherEmojiId { get; set; }
        public ulong HigherLowerLowerEmojiId { get; set; }

        public BlackjackCardConfig BlackjackCards { get; set; }
    }

    public class BlackjackCardConfig
    {
        public SuitConfig Clubs { get; set; }
        public SuitConfig Diamonds { get; set; }
        public SuitConfig Hearts { get; set; }
        public SuitConfig Spades { get; set; }
    }

    public class SuitConfig
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

    public static class ConfigService
    {
        public static EnvironmentConfig Current { get; private set; }
        private static AppSettings _appSettings;

        public static void Load(string filePath = "appsettings.json")
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Configuration file not found at {filePath}");
            }

            var json = File.ReadAllText(filePath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            _appSettings = JsonSerializer.Deserialize<AppSettings>(json, options);

            if (_appSettings == null || string.IsNullOrEmpty(_appSettings.ActiveProfile))
            {
                throw new System.Exception("Failed to load configuration or ActiveProfile is missing.");
            }

            if (!_appSettings.Profiles.ContainsKey(_appSettings.ActiveProfile))
            {
                throw new System.Exception($"Profile '{_appSettings.ActiveProfile}' not found in configuration.");
            }

            Current = _appSettings.Profiles[_appSettings.ActiveProfile];
            System.Console.WriteLine($"Loaded configuration for profile: {_appSettings.ActiveProfile}");
        }
    }
}
