namespace Server.Infrastructure.Discord
{
    /// <summary>
    /// Central place for Discord-related IDs so they are easy to manage.
    /// </summary>
    public static class DiscordIds
    {
        //Token
        public const string BotToken = "";




        /* ------------------------------------------------------------------------------------------------------------------------------ */

        // FOR TEST ENVIRONMENT
        // Guild
        public const ulong GuildId = 1445556867307929612UL;

        // Roles
        public const ulong StaffRoleId = 1446166541933613127UL;

        // Channels
        public const ulong DepositStaffChannelId = 1446212804737503494UL;
        public const ulong WithdrawStaffChannelId = 1446212852388991118UL;
        public const ulong StakeStaffChannelId = 1446157757106491404UL;
        public const ulong LiveFeedChannelId = 1446877400318349352UL;

        // Ticket categories (single or multi)
        public static readonly ulong[] DepositTicketCategoryIds =
        {
            1446849201269899306UL,
        };

        public static readonly ulong[] WithdrawTicketCategoryIds =
        {
            1446849201269899306UL,
        };

        // Multi-category commands (can be extended with more IDs)
        public static readonly ulong[] CoinflipTicketCategoryIds =
        {
            1446849201269899306UL,
            1446962395053559859UL
        };

        public static readonly ulong[] StakeTicketCategoryIds =
        {
            1446849201269899306UL,
            1446962395053559859UL
        };

        // Coinflip custom emoji IDs
        public const ulong CoinflipHeadsEmojiId = 1446593042705285312UL;
        public const ulong CoinflipTailsEmojiId = 1446593047977660457UL;
        public const ulong CoinflipRmEmojiId = 1446593046409117780UL;
        public const ulong CoinflipHalfEmojiId = 1446593040889413702UL;
        public const ulong CoinflipMaxEmojiId = 1446593044219560111UL;
        public const ulong CoinflipX2EmojiId = 1446655759575416942UL;
        public const ulong CoinflipExitEmojiId = 1446867079578914816UL;

        // Live feed icon emoji(s)
        public const ulong DdpEmojiId = 1446887716305244280UL;
        public const ulong WprayEmojiId = 1446888742651433091UL;

        // Big win emoji (MVPP)
        public const ulong BigWinMvppEmojiId = 1446889114514493530UL;

        // Coinflip result emojis
        public const ulong CoinflipGoldEmojiId = 1446889072537894994UL; // win
        public const ulong CoinflipSilverEmojiId = 1446889057874346124UL; // loss

        // Balance
        public const ulong BalanceSheetEmojiId = 1447330999812755557UL;
        public const ulong WithdrawEmojiId = 1447330230237659287UL;
        public const ulong DepositEmojiId = 1447330227360502013UL;
        public const ulong WalletEmojiId = 1447335171387949127UL;




        /* ------------------------------------------------------------------------------------------------------------------------------ */

        // FOR PRODUCTION ENVIRONMENT PRESTIGE BETS
        // Guild
        //public const ulong GuildId = 1430501306472075307;

        //// Roles
        //public const ulong StaffRoleId = 1430501306816270341;

        //// Channels
        //public const ulong DepositStaffChannelId = 1430501307034107987;
        //public const ulong WithdrawStaffChannelId = 1430501307034107988;
        //public const ulong StakeStaffChannelId = 1430501307034107989;
        //public const ulong LiveFeedChannelId = 1430501307629703266;

        ////Ticket categories
        //public const ulong DepositTicketCategoryId = 1430501307193495609UL;
        //public const ulong WithdrawTicketCategoryId = 1430501307491287110UL;

        //// Ticket categories (single or multi)
        //public static readonly ulong[] DepositTicketCategoryIds =
        //{
        //    1430501307193495609UL,
        //    1430501307491287110UL,
        //    1430501307034107995UL,
        //    1430501307193495609UL,
        //    1430501307491287110UL,
        //    1430501307491287116UL,
        //    1430501307491287118UL
        //};

        //public static readonly ulong[] WithdrawTicketCategoryIds =
        //{
        //    1430501307491287110UL,
        //    1430501307193495609UL,
        //    1430501307034107995UL,
        //    1430501307193495609UL,
        //    1430501307491287110UL,
        //    1430501307491287116UL,
        //    1430501307491287118UL
        //};

        ////Multi-category commands(can be extended with more IDs)
        //public static readonly ulong[] CoinflipTicketCategoryIds =
        //{
        //    1430501307772571733UL,
        //    1430501307034107995UL,
        //    1430501307193495609UL,
        //    1430501307491287110UL,
        //    1430501307491287116UL,
        //    1430501307491287118UL
        //};

        //public static readonly ulong[] StakeTicketCategoryIds =
        //{
        //    1430501307772571733UL,
        //    1430501307034107995UL,
        //    1430501307193495609UL,
        //    1430501307491287110UL,
        //    1430501307491287116UL,
        //    1430501307491287118UL
        //};

        //public const ulong CoinflipHeadsEmojiId = 1446593382506958888UL;
        //public const ulong CoinflipTailsEmojiId = 1446593388081058054UL;
        //public const ulong CoinflipRmEmojiId = 1446593386156003368UL;
        //public const ulong CoinflipHalfEmojiId = 1446593380946677981UL;
        //public const ulong CoinflipMaxEmojiId = 1446593383920439457UL;
        //public const ulong CoinflipX2EmojiId = 1446656711615320155UL;
        //public const ulong CoinflipExitEmojiId = 1446867519318003732UL;

        //// Live feed icon emoji(s)
        //public const ulong DdpEmojiId = 1446892417260322887;
        //public const ulong WprayEmojiId = 1446892401078698194;

        //// Big win emoji (MVPP)
        //public const ulong BigWinMvppEmojiId = 1446892382531489792;

        //// Coinflip result emojis
        //public const ulong CoinflipGoldEmojiId = 1446892366316306585; // win
        //public const ulong CoinflipSilverEmojiId = 1446892440001974424; // loss




        /* ------------------------------------------------------------------------------------------------------------------------------ */

        // FOR PRODUCTION ENVIRONMENT OCEAN STAKES
        //public const ulong GuildId = 1096096457167216752UL;

        //// Roles
        //public const ulong StaffRoleId = 1096096457217544364UL;

        //// Channels
        //public const ulong DepositStaffChannelId = 1192733274942996530UL;
        //public const ulong WithdrawStaffChannelId = 1394990154233548872UL;
        //public const ulong StakeStaffChannelId = 1096096459063046188UL;
        //public const ulong LiveFeedChannelId = 1096096457695703127UL;

        //// Ticket categories (single or multi)
        //public static readonly ulong[] DepositTicketCategoryIds =
        //{
        //    1096096459063046192UL,
        //    1179586576674726010UL
        //};

        //public static readonly ulong[] WithdrawTicketCategoryIds =
        //{
        //    1096096459063046192UL,
        //    1179586576674726010UL
        //};

        //// Multi-category commands (can be extended with more IDs)
        //public static readonly ulong[] CoinflipTicketCategoryIds =
        //{
        //    1096096458157072526UL,
        //    1096096459063046192UL,
        //    1179586576674726010UL
        //};

        //public static readonly ulong[] StakeTicketCategoryIds =
        //{
        //    1096096458157072526UL,
        //    1096096459063046192UL,
        //    1179586576674726010UL
        //};

        //// Coinflip custom emoji IDs
        //public const ulong CoinflipHeadsEmojiId = 1446933554029265099UL;
        //public const ulong CoinflipTailsEmojiId = 1446933565219672084UL;
        //public const ulong CoinflipRmEmojiId = 1446933562208161932UL;
        //public const ulong CoinflipHalfEmojiId = 1446933552947269818UL;
        //public const ulong CoinflipMaxEmojiId = 1446933557305147422UL;
        //public const ulong CoinflipX2EmojiId = 1446933569149866106UL;
        //public const ulong CoinflipExitEmojiId = 1446933550032224276UL;

        //// Live feed icon emoji(s)
        //public const ulong DdpEmojiId = 1446933544839544915UL;
        //public const ulong WprayEmojiId = 1446933567774130467UL;

        //// Big win emoji (MVPP)
        //public const ulong BigWinMvppEmojiId = 1446933558617833552UL;

        //// Coinflip result emojis
        //public const ulong CoinflipGoldEmojiId = 1446933551348977695UL; // win
        //public const ulong CoinflipSilverEmojiId = 1446933563424637029UL; // loss
    }
}
