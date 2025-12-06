using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;

namespace Server.Infrastructure.Discord
{
    public static class DiscordChannelPermissionService
    {
        public static bool IsInDepositTicketChannel(CommandContext ctx)
        {
            return IsInCategory(ctx.Channel, DiscordIds.DepositTicketCategoryId);
        }

        public static bool IsInWithdrawTicketChannel(CommandContext ctx)
        {
            return IsInCategory(ctx.Channel, DiscordIds.WithdrawTicketCategoryId);
        }

        public static bool IsInCoinflipTicketChannel(CommandContext ctx)
        {
            return IsInAnyCategory(ctx.Channel, DiscordIds.CoinflipTicketCategoryIds);
        }

        public static bool IsInStakeTicketChannel(CommandContext ctx)
        {
            return IsInAnyCategory(ctx.Channel, DiscordIds.StakeTicketCategoryIds);
        }

        private static bool IsInCategory(DiscordChannel channel, ulong categoryId)
        {
            // If the channel has a parent, compare against the required category.
            return channel.ParentId.HasValue && channel.ParentId.Value == categoryId;
        }

        private static bool IsInAnyCategory(DiscordChannel channel, ulong[] categoryIds)
        {
            if (!channel.ParentId.HasValue || categoryIds == null || categoryIds.Length == 0)
                return false;

            var parentId = channel.ParentId.Value;
            foreach (var id in categoryIds)
            {
                if (parentId == id)
                    return true;
            }

            return false;
        }

        public static async Task<bool> EnforceDepositChannelAsync(CommandContext ctx)
        {
            if (IsInDepositTicketChannel(ctx))
                return true;

            await ctx.RespondAsync("You can only use this command inside a deposit ticket channel.");
            return false;
        }

        public static async Task<bool> EnforceWithdrawChannelAsync(CommandContext ctx)
        {
            if (IsInWithdrawTicketChannel(ctx))
                return true;

            await ctx.RespondAsync("You can only use this command inside a withdrawal ticket channel.");
            return false;
        }

        public static async Task<bool> EnforceCoinflipChannelAsync(CommandContext ctx)
        {
            if (IsInCoinflipTicketChannel(ctx))
                return true;

            await ctx.RespondAsync("You can only use this command inside a coinflip ticket channel.");
            return false;
        }

        public static async Task<bool> EnforceStakeChannelAsync(CommandContext ctx)
        {
            if (IsInStakeTicketChannel(ctx))
                return true;

            await ctx.RespondAsync("You can only use this command inside a stake ticket channel.");
            return false;
        }
    }
}
