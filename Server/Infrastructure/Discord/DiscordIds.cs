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

        // Cracker Emojis
        public static ulong CrackerBlueEmojiId => ConfigService.Current.Discord.CrackerBlueEmojiId;
        public static ulong CrackerGreenEmojiId => ConfigService.Current.Discord.CrackerGreenEmojiId;
        public static ulong CrackerPurpleEmojiId => ConfigService.Current.Discord.CrackerPurpleEmojiId;
        public static ulong CrackerRedEmojiId => ConfigService.Current.Discord.CrackerRedEmojiId;
        public static ulong CrackerWhiteEmojiId => ConfigService.Current.Discord.CrackerWhiteEmojiId;
        public static ulong CrackerYellowEmojiId => ConfigService.Current.Discord.CrackerYellowEmojiId;
        public static ulong CrackerPullEmojiId => ConfigService.Current.Discord.CrackerPullEmojiId;
        public static ulong CrackerRmEmojiId => ConfigService.Current.Discord.CrackerRmEmojiId;
        public static ulong CrackerHalfEmojiId => ConfigService.Current.Discord.CrackerHalfEmojiId;
        public static ulong CrackerMaxEmojiId => ConfigService.Current.Discord.CrackerMaxEmojiId;
        public static ulong CrackerX2EmojiId => ConfigService.Current.Discord.CrackerX2EmojiId;

        // Higher/Lower Emojis
        public static ulong HigherLowerHigherEmojiId => ConfigService.Current.Discord.HigherLowerHigherEmojiId;
        public static ulong HigherLowerLowerEmojiId => ConfigService.Current.Discord.HigherLowerLowerEmojiId;

        // Coinflip result emojis
        public static ulong CoinflipGoldEmojiId => ConfigService.Current.Discord.CoinflipGoldEmojiId; // win
        public static ulong CoinflipSilverEmojiId => ConfigService.Current.Discord.CoinflipSilverEmojiId; // loss

        // Mines Emojis
        public static ulong MinesPurpleGemEmojiId => ConfigService.Current.Discord.MinesPurpleGemEmojiId;
        public static ulong MinesGreenGemEmojiId => ConfigService.Current.Discord.MinesGreenGemEmojiId;
        public static ulong MinesRedGemEmojiId => ConfigService.Current.Discord.MinesRedGemEmojiId;
        public static ulong MinesBombEmojiId => ConfigService.Current.Discord.MinesBombEmojiId;
        public static ulong MinesRematchEmojiId => ConfigService.Current.Discord.MinesRematchEmojiId;
        public static ulong MinesCashoutEmojiId => ConfigService.Current.Discord.MinesCashoutEmojiId;
        // Blackjack Emojis
        public static ulong BlackjackHitEmojiId => ConfigService.Current.Discord.BlackjackHitEmojiId;
        public static ulong BlackjackStandEmojiId => ConfigService.Current.Discord.BlackjackStandEmojiId;
        public static ulong BlackjackDoubleEmojiId => ConfigService.Current.Discord.BlackjackDoubleEmojiId;
        public static ulong BlackjackSplitEmojiId => ConfigService.Current.Discord.BlackjackSplitEmojiId;
        public static ulong BlackjackBacksideEmojiId => ConfigService.Current.Discord.BlackjackBacksideEmojiId;
        public static ulong BlackjackSpadesEmojiId => ConfigService.Current.Discord.BlackjackSpadesEmojiId;
        public static ulong BlackjackDiamondsEmojiId => ConfigService.Current.Discord.BlackjackDiamondsEmojiId;
        public static ulong BlackjackHeartsEmojiId => ConfigService.Current.Discord.BlackjackHeartsEmojiId;
        public static ulong BlackjackClubsEmojiId => ConfigService.Current.Discord.BlackjackClubsEmojiId;

        // Blackjack Rematch Emojis
        public static ulong BlackjackRmEmojiId => ConfigService.Current.Discord.BlackjackRmEmojiId;
        public static ulong BlackjackRmHalfEmojiId => ConfigService.Current.Discord.BlackjackRmHalfEmojiId;
        public static ulong BlackjackRmX2EmojiId => ConfigService.Current.Discord.BlackjackRmX2EmojiId;

        // Balance
        public static ulong BalanceSheetEmojiId => ConfigService.Current.Discord.BalanceSheetEmojiId;
        public static ulong WithdrawEmojiId => ConfigService.Current.Discord.WithdrawEmojiId;
        public static ulong DepositEmojiId => ConfigService.Current.Discord.DepositEmojiId;
        public static ulong WalletEmojiId => ConfigService.Current.Discord.WalletEmojiId;
        public static ulong BuyEmojiId => ConfigService.Current.Discord.BuyEmojiId;

        // Chest Emojis
        public static ulong ChestZaryteEmojiId => ConfigService.Current.Discord.ChestZaryteEmojiId;
        public static ulong ChestTbowEmojiId => ConfigService.Current.Discord.ChestTbowEmojiId;
        public static ulong ChestShadowEmojiId => ConfigService.Current.Discord.ChestShadowEmojiId;
        public static ulong ChestTorvaLegsEmojiId => ConfigService.Current.Discord.ChestTorvaLegsEmojiId;
        public static ulong ChestTorvaBodyEmojiId => ConfigService.Current.Discord.ChestTorvaBodyEmojiId;
        public static ulong ChestTorvaHelmEmojiId => ConfigService.Current.Discord.ChestTorvaHelmEmojiId;
        public static ulong ChestScytheEmojiId => ConfigService.Current.Discord.ChestScytheEmojiId;
        public static ulong ChestElysianEmojiId => ConfigService.Current.Discord.ChestElysianEmojiId;
        public static ulong ChestPickaxeEmojiId => ConfigService.Current.Discord.ChestPickaxeEmojiId;
    }
}
