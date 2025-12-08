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
