namespace Server.Infrastructure.Discord
{
    /// <summary>
    /// Central place for Discord-related IDs so they are easy to manage.
    /// </summary>
    public static class DiscordIds
    {
        // FOR TEST ENVIRONMENT
        // Guild
        //public const ulong GuildId = 1445556867307929612UL;

        //// Roles
        //public const ulong StaffRoleId = 1446166541933613127UL;

        //// Channels
        //public const ulong DepositStaffChannelId = 1446212804737503494UL;
        //public const ulong WithdrawStaffChannelId = 1446212852388991118UL;
        //public const ulong StakeStaffChannelId = 1446157757106491404UL;

        // FOR PRODUCTION ENVIRONMENT
        // Guild
        public const ulong GuildId = 1430501306472075307;

        // Roles
        public const ulong StaffRoleId = 1430501306816270341;

        // Channels
        public const ulong DepositStaffChannelId = 1430501307034107987;
        public const ulong WithdrawStaffChannelId = 1430501307034107988;
        public const ulong StakeStaffChannelId = 1430501307034107989;
    }
}
