using System;
using System.Linq;
using DSharpPlus.Entities;

namespace Server.Infrastructure.Discord
{
    public static class DiscordExtensions
    {
        public static bool IsStaff(this DiscordMember? member)
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
