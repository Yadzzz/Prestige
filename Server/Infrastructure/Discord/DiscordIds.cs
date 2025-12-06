namespace Server.Infrastructure.Discord
{
    /// <summary>
    /// Central place for Discord-related IDs so they are easy to manage.
    /// </summary>
    public static class DiscordIds
    {
        //Token
        public const string BotToken = "";

        // FOR TEST ENVIRONMENT
        // Guild
        public const ulong GuildId = 1445556867307929612UL;

        // Roles
        public const ulong StaffRoleId = 1446166541933613127UL;

        // Channels
        public const ulong DepositStaffChannelId = 1446212804737503494UL;
        public const ulong WithdrawStaffChannelId = 1446212852388991118UL;
        public const ulong StakeStaffChannelId = 1446157757106491404UL;

        // Coinflip custom emoji IDs
        public const ulong CoinflipHeadsEmojiId = 1446593042705285312UL;
        public const ulong CoinflipTailsEmojiId = 1446593047977660457UL;
        public const ulong CoinflipRmEmojiId    = 1446593046409117780UL;
        public const ulong CoinflipHalfEmojiId  = 1446593040889413702UL;
        public const ulong CoinflipMaxEmojiId   = 1446593044219560111UL;
        public const ulong CoinflipX2EmojiId    = 1446655759575416942UL;





        // FOR PRODUCTION ENVIRONMENT
        // Guild
        //public const ulong GuildId = 1430501306472075307;

        //// Roles
        //public const ulong StaffRoleId = 1430501306816270341;

        //// Channels
        //public const ulong DepositStaffChannelId = 1430501307034107987;
        //public const ulong WithdrawStaffChannelId = 1430501307034107988;
        //public const ulong StakeStaffChannelId = 1430501307034107989;

        //public const ulong CoinflipHeadsEmojiId = 1446593382506958888UL;
        //public const ulong CoinflipTailsEmojiId = 1446593388081058054UL;
        //public const ulong CoinflipRmEmojiId = 1446593386156003368UL;
        //public const ulong CoinflipHalfEmojiId = 1446593380946677981UL;
        //public const ulong CoinflipMaxEmojiId = 1446593383920439457UL;
        //public const ulong CoinflipX2EmojiId = 1446656711615320155UL;
    }
}
