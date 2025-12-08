using Server.Infrastructure.Configuration;

namespace Server.Infrastructure.Discord
{
    /// <summary>
    /// Central place for Discord-related IDs so they are easy to manage.
    /// </summary>
    public static class DiscordIds
    {
        //Token
        public static string BotToken => ConfigService.Current.Discord.BotToken;

        // Guild
        public static ulong GuildId => ConfigService.Current.Discord.GuildId;

        // Roles
        public static ulong StaffRoleId => ConfigService.Current.Discord.StaffRoleId;

        // Channels
        public static ulong DepositStaffChannelId => ConfigService.Current.Discord.DepositStaffChannelId;
        public static ulong WithdrawStaffChannelId => ConfigService.Current.Discord.WithdrawStaffChannelId;
        public static ulong StakeStaffChannelId => ConfigService.Current.Discord.StakeStaffChannelId;
        public static ulong LiveFeedChannelId => ConfigService.Current.Discord.LiveFeedChannelId;
        public static ulong RaceChannelId => ConfigService.Current.Discord.RaceChannelId;

        // Ticket categories (single or multi)
        public static ulong[] DepositTicketCategoryIds => ConfigService.Current.Discord.DepositTicketCategoryIds;
        public static ulong[] WithdrawTicketCategoryIds => ConfigService.Current.Discord.WithdrawTicketCategoryIds;

        // Multi-category commands (can be extended with more IDs)
        public static ulong[] CoinflipTicketCategoryIds => ConfigService.Current.Discord.CoinflipTicketCategoryIds;
        public static ulong[] StakeTicketCategoryIds => ConfigService.Current.Discord.StakeTicketCategoryIds;

        // Coinflip custom emoji IDs
        public static ulong CoinflipHeadsEmojiId => ConfigService.Current.Discord.CoinflipHeadsEmojiId;
        public static ulong CoinflipTailsEmojiId => ConfigService.Current.Discord.CoinflipTailsEmojiId;
        public static ulong CoinflipRmEmojiId => ConfigService.Current.Discord.CoinflipRmEmojiId;
        public static ulong CoinflipHalfEmojiId => ConfigService.Current.Discord.CoinflipHalfEmojiId;
        public static ulong CoinflipMaxEmojiId => ConfigService.Current.Discord.CoinflipMaxEmojiId;
        public static ulong CoinflipX2EmojiId => ConfigService.Current.Discord.CoinflipX2EmojiId;
        public static ulong CoinflipExitEmojiId => ConfigService.Current.Discord.CoinflipExitEmojiId;

        // Live feed icon emoji(s)
        public static ulong DdpEmojiId => ConfigService.Current.Discord.DdpEmojiId;
        public static ulong WprayEmojiId => ConfigService.Current.Discord.WprayEmojiId;

        // Big win emoji (MVPP)
        public static ulong BigWinMvppEmojiId => ConfigService.Current.Discord.BigWinMvppEmojiId;

        // Coinflip result emojis
        public static ulong CoinflipGoldEmojiId => ConfigService.Current.Discord.CoinflipGoldEmojiId; // win
        public static ulong CoinflipSilverEmojiId => ConfigService.Current.Discord.CoinflipSilverEmojiId; // loss

        // Balance
        public static ulong BalanceSheetEmojiId => ConfigService.Current.Discord.BalanceSheetEmojiId;
        public static ulong WithdrawEmojiId => ConfigService.Current.Discord.WithdrawEmojiId;
        public static ulong DepositEmojiId => ConfigService.Current.Discord.DepositEmojiId;
        public static ulong WalletEmojiId => ConfigService.Current.Discord.WalletEmojiId;
        public static ulong BuyEmojiId => ConfigService.Current.Discord.BuyEmojiId;
    }
}
