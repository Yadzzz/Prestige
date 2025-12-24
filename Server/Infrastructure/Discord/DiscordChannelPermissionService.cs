using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;

namespace Server.Infrastructure.Discord
{
    public static class DiscordChannelPermissionService
    {
        public static bool IsInDepositTicketChannel(CommandContext ctx)
        {
            return IsInAnyCategory(ctx.Channel, DiscordIds.DepositTicketCategoryIds);
        }

        public static bool IsInWithdrawTicketChannel(CommandContext ctx)
        {
            return IsInAnyCategory(ctx.Channel, DiscordIds.WithdrawTicketCategoryIds);
        }

        public static bool IsInCoinflipTicketChannel(CommandContext ctx)
        {
            return IsInAnyCategory(ctx.Channel, DiscordIds.CoinflipTicketCategoryIds);
        }

        public static bool IsInStakeTicketChannel(CommandContext ctx)
        {
            return IsInAnyCategory(ctx.Channel, DiscordIds.StakeTicketCategoryIds);
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

        private static bool IsStaff(CommandContext ctx)
        {
            return ctx.Member.IsStaff();
        }

        public static async Task<bool> EnforceDepositChannelAsync(CommandContext ctx)
        {
            if (IsStaff(ctx) || IsInDepositTicketChannel(ctx))
                return true;

            await ctx.RespondAsync("You can only use this command inside a deposit ticket channel.");
            return false;
        }

        public static async Task<bool> EnforceWithdrawChannelAsync(CommandContext ctx)
        {
            if (IsStaff(ctx) || IsInWithdrawTicketChannel(ctx))
                return true;

            await ctx.RespondAsync("You can only use this command inside a withdrawal ticket channel.");
            return false;
        }

        public static async Task<bool> EnforceCoinflipChannelAsync(CommandContext ctx)
        {
            if (IsStaff(ctx) || IsInCoinflipTicketChannel(ctx))
                return true;

            await ctx.RespondAsync("You can only use this command inside a games channel.");
            return false;
        }

        public static async Task<bool> EnforceBlackjackChannelAsync(CommandContext ctx)
        {
            if (IsStaff(ctx) || IsInCoinflipTicketChannel(ctx))
                return true;

            await ctx.RespondAsync("You can only use this command inside a games channel.");
            return false;
        }

        public static async Task<bool> EnforceMinesChannelAsync(CommandContext ctx)
        {
            if (IsStaff(ctx) || IsInCoinflipTicketChannel(ctx))
                return true;

            await ctx.RespondAsync("You can only use this command inside a games channel.");
            return false;
        }

        public static async Task<bool> EnforceStakeChannelAsync(CommandContext ctx)
        {
            if (IsStaff(ctx) || IsInStakeTicketChannel(ctx))
                return true;

            await ctx.RespondAsync("You can only use this command inside a games channel.");
            return false;
        }
    }
}
