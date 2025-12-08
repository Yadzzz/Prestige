using System;

namespace Server.Infrastructure.Discord
{
    /// <summary>
    /// Central place for Discord-related IDs so they are easy to manage.
    /// </summary>
    public static class DiscordIds
    {
        //Token
        public static string BotToken => ServerConfiguration.Environment switch
        {
            AppEnvironment.Test => "",
            AppEnvironment.PrestigeBets => "", // TODO: Add Token
            AppEnvironment.OceanStakes => "", // TODO: Add Token
            _ => throw new NotImplementedException()
        };

        // Guild
        public static ulong GuildId => ServerConfiguration.Environment switch
        {
            AppEnvironment.Test => 1445556867307929612UL,
            AppEnvironment.PrestigeBets => 1430501306472075307UL,
            AppEnvironment.OceanStakes => 1096096457167216752UL,
            _ => 0
        };

        public static string ServerName => ServerConfiguration.Environment switch
        {
            AppEnvironment.Test => "Prestige Bot Test",
            AppEnvironment.PrestigeBets => "Prestige Bot",
            AppEnvironment.OceanStakes => "Bank",
            _ => "Unknown"
        };

        // Roles
        public static ulong StaffRoleId => ServerConfiguration.Environment switch
        {
            AppEnvironment.Test => 1446166541933613127UL,
            AppEnvironment.PrestigeBets => 1430501306816270341UL,
            AppEnvironment.OceanStakes => 1096096457217544364UL,
            _ => 0
        };

        // Channels
        public static ulong DepositStaffChannelId => ServerConfiguration.Environment switch
        {
            AppEnvironment.Test => 1446212804737503494UL,
            AppEnvironment.PrestigeBets => 1430501307034107987UL,
            AppEnvironment.OceanStakes => 1192733274942996530UL,
            _ => 0
        };

        public static ulong WithdrawStaffChannelId => ServerConfiguration.Environment switch
        {
            AppEnvironment.Test => 1446212852388991118UL,
            AppEnvironment.PrestigeBets => 1430501307034107988UL,
            AppEnvironment.OceanStakes => 1394990154233548872UL,
            _ => 0
        };

        public static ulong StakeStaffChannelId => ServerConfiguration.Environment switch
        {
            AppEnvironment.Test => 1446157757106491404UL,
            AppEnvironment.PrestigeBets => 1430501307034107989UL,
            AppEnvironment.OceanStakes => 1096096459063046188UL,
            _ => 0
        };

        public static ulong LiveFeedChannelId => ServerConfiguration.Environment switch
        {
            AppEnvironment.Test => 1446877400318349352UL,
            AppEnvironment.PrestigeBets => 1430501307629703266UL,
            AppEnvironment.OceanStakes => 1096096457695703127UL,
            _ => 0
        };

        public static ulong RaceChannelId => ServerConfiguration.Environment switch
        {
            AppEnvironment.Test => 1447522659759030324UL,
            AppEnvironment.PrestigeBets => 0, // TODO: Add RaceChannelId
            AppEnvironment.OceanStakes => 0, // TODO: Add RaceChannelId
            _ => 0
        };

        // Ticket categories (single or multi)
        public static ulong[] DepositTicketCategoryIds => ServerConfiguration.Environment switch
        {
            AppEnvironment.Test => new[] { 1446849201269899306UL },
            AppEnvironment.PrestigeBets => new[] { 1430501307193495609UL, 1430501307491287110UL, 1430501307034107995UL, 1430501307193495609UL, 1430501307491287110UL, 1430501307491287116UL, 1430501307491287118UL },
            AppEnvironment.OceanStakes => new[] { 1096096459063046192UL, 1179586576674726010UL },
            _ => Array.Empty<ulong>()
        };

        public static ulong[] WithdrawTicketCategoryIds => ServerConfiguration.Environment switch
        {
            AppEnvironment.Test => new[] { 1446849201269899306UL },
            AppEnvironment.PrestigeBets => new[] { 1430501307491287110UL, 1430501307193495609UL, 1430501307034107995UL, 1430501307193495609UL, 1430501307491287110UL, 1430501307491287116UL, 1430501307491287118UL },
            AppEnvironment.OceanStakes => new[] { 1096096459063046192UL, 1179586576674726010UL },
            _ => Array.Empty<ulong>()
        };

        // Multi-category commands (can be extended with more IDs)
        public static ulong[] CoinflipTicketCategoryIds => ServerConfiguration.Environment switch
        {
            AppEnvironment.Test => new[] { 1446849201269899306UL, 1446962395053559859UL },
            AppEnvironment.PrestigeBets => new[] { 1430501307772571733UL, 1430501307034107995UL, 1430501307193495609UL, 1430501307491287110UL, 1430501307491287116UL, 1430501307491287118UL },
            AppEnvironment.OceanStakes => new[] { 1096096458157072526UL, 1096096459063046192UL, 1179586576674726010UL },
            _ => Array.Empty<ulong>()
        };

        public static ulong[] StakeTicketCategoryIds => ServerConfiguration.Environment switch
        {
            AppEnvironment.Test => new[] { 1446849201269899306UL, 1446962395053559859UL },
            AppEnvironment.PrestigeBets => new[] { 1430501307772571733UL, 1430501307034107995UL, 1430501307193495609UL, 1430501307491287110UL, 1430501307491287116UL, 1430501307491287118UL },
            AppEnvironment.OceanStakes => new[] { 1096096458157072526UL, 1096096459063046192UL, 1179586576674726010UL },
            _ => Array.Empty<ulong>()
        };

        // Coinflip custom emoji IDs
        public static ulong CoinflipHeadsEmojiId => ServerConfiguration.Environment switch
        {
            AppEnvironment.Test => 1446593042705285312UL,
            AppEnvironment.PrestigeBets => 1446593382506958888UL,
            AppEnvironment.OceanStakes => 1446933554029265099UL,
            _ => 0
        };

        public static ulong CoinflipTailsEmojiId => ServerConfiguration.Environment switch
        {
            AppEnvironment.Test => 1446593047977660457UL,
            AppEnvironment.PrestigeBets => 1446593388081058054UL,
            AppEnvironment.OceanStakes => 1446933565219672084UL,
            _ => 0
        };

        public static ulong CoinflipRmEmojiId => ServerConfiguration.Environment switch
        {
            AppEnvironment.Test => 1446593046409117780UL,
            AppEnvironment.PrestigeBets => 1446593386156003368UL,
            AppEnvironment.OceanStakes => 1446933562208161932UL,
            _ => 0
        };

        public static ulong CoinflipHalfEmojiId => ServerConfiguration.Environment switch
        {
            AppEnvironment.Test => 1446593040889413702UL,
            AppEnvironment.PrestigeBets => 1446593380946677981UL,
            AppEnvironment.OceanStakes => 1446933552947269818UL,
            _ => 0
        };

        public static ulong CoinflipMaxEmojiId => ServerConfiguration.Environment switch
        {
            AppEnvironment.Test => 1446593044219560111UL,
            AppEnvironment.PrestigeBets => 1446593383920439457UL,
            AppEnvironment.OceanStakes => 1446933557305147422UL,
            _ => 0
        };

        public static ulong CoinflipX2EmojiId => ServerConfiguration.Environment switch
        {
            AppEnvironment.Test => 1446655759575416942UL,
            AppEnvironment.PrestigeBets => 1446656711615320155UL,
            AppEnvironment.OceanStakes => 1446933569149866106UL,
            _ => 0
        };

        public static ulong CoinflipExitEmojiId => ServerConfiguration.Environment switch
        {
            AppEnvironment.Test => 1446867079578914816UL,
            AppEnvironment.PrestigeBets => 1446867519318003732UL,
            AppEnvironment.OceanStakes => 1446933550032224276UL,
            _ => 0
        };

        // Live feed icon emoji(s)
        public static ulong DdpEmojiId => ServerConfiguration.Environment switch
        {
            AppEnvironment.Test => 1446887716305244280UL,
            AppEnvironment.PrestigeBets => 1446892417260322887UL,
            AppEnvironment.OceanStakes => 1446933544839544915UL,
            _ => 0
        };

        public static ulong WprayEmojiId => ServerConfiguration.Environment switch
        {
            AppEnvironment.Test => 1446888742651433091UL,
            AppEnvironment.PrestigeBets => 1446892401078698194UL,
            AppEnvironment.OceanStakes => 1446933567774130467UL,
            _ => 0
        };

        // Big win emoji (MVPP)
        public static ulong BigWinMvppEmojiId => ServerConfiguration.Environment switch
        {
            AppEnvironment.Test => 1446889114514493530UL,
            AppEnvironment.PrestigeBets => 1446892382531489792UL,
            AppEnvironment.OceanStakes => 1446933558617833552UL,
            _ => 0
        };

        // Coinflip result emojis
        public static ulong CoinflipGoldEmojiId => ServerConfiguration.Environment switch
        {
            AppEnvironment.Test => 1446889072537894994UL,
            AppEnvironment.PrestigeBets => 1446892366316306585UL,
            AppEnvironment.OceanStakes => 1446933551348977695UL,
            _ => 0
        };

        public static ulong CoinflipSilverEmojiId => ServerConfiguration.Environment switch
        {
            AppEnvironment.Test => 1446889057874346124UL,
            AppEnvironment.PrestigeBets => 1446892440001974424UL,
            AppEnvironment.OceanStakes => 1446933563424637029UL,
            _ => 0
        };

        // Balance
        public static ulong BalanceSheetEmojiId => ServerConfiguration.Environment switch
        {
            AppEnvironment.Test => 1447330999812755557UL,
            _ => 1447330999812755557UL // Default to Test if missing
        };

        public static ulong WithdrawEmojiId => ServerConfiguration.Environment switch
        {
            AppEnvironment.Test => 1447330230237659287UL,
            _ => 1447330230237659287UL // Default to Test if missing
        };

        public static ulong DepositEmojiId => ServerConfiguration.Environment switch
        {
            AppEnvironment.Test => 1447330227360502013UL,
            _ => 1447330227360502013UL // Default to Test if missing
        };

        public static ulong WalletEmojiId => ServerConfiguration.Environment switch
        {
            AppEnvironment.Test => 1447335171387949127UL,
            _ => 1447335171387949127UL // Default to Test if missing
        };

        public static ulong BuyEmojiId => ServerConfiguration.Environment switch
        {
            AppEnvironment.Test => 1447583989178302485UL,
            _ => 1447583989178302485UL // Default to Test if missing
        };
    }
}
