using System;
using System.Linq;
using Discord.WebSocket;

namespace Server.Infrastructure.Discord
{
    public static class DiscordExtensions
    {
        public static bool IsStaff(this SocketGuildUser? member)
        {
            if (member == null) return false;

            return member.Roles.Any(r =>
                r.Id == DiscordIds.StaffRoleId ||
                string.Equals(r.Name, "Developer", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.Name, "Staff", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.Name, "Admin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.Name, "Moderator", StringComparison.OrdinalIgnoreCase));
        }
    }
}
