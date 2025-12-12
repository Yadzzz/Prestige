using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;

namespace Server.Infrastructure.Discord
{
    public static class DiscordChannelPermissionService
    {
        public static bool IsInDepositTicketChannel(SocketCommandContext ctx)
        {
            return IsInAnyCategory(ctx.Channel, DiscordIds.DepositTicketCategoryIds);
        }

        public static bool IsInWithdrawTicketChannel(SocketCommandContext ctx)
        {
            return IsInAnyCategory(ctx.Channel, DiscordIds.WithdrawTicketCategoryIds);
        }

        public static bool IsInCoinflipTicketChannel(SocketCommandContext ctx)
        {
            return IsInAnyCategory(ctx.Channel, DiscordIds.CoinflipTicketCategoryIds);
        }

        public static bool IsInStakeTicketChannel(SocketCommandContext ctx)
        {
            return IsInAnyCategory(ctx.Channel, DiscordIds.StakeTicketCategoryIds);
        }

        private static bool IsInAnyCategory(ISocketMessageChannel channel, ulong[] categoryIds)
        {
            if (channel is not SocketTextChannel textChannel || !textChannel.CategoryId.HasValue || categoryIds == null || categoryIds.Length == 0)
                return false;

            var parentId = textChannel.CategoryId.Value;
            foreach (var id in categoryIds)
            {
                if (parentId == id)
                    return true;
            }

            return false;
        }

        private static bool IsStaff(SocketCommandContext ctx)
        {
            return (ctx.User as SocketGuildUser).IsStaff();
        }

        public static async Task<bool> EnforceDepositChannelAsync(SocketCommandContext ctx)
        {
            if (IsStaff(ctx) || IsInDepositTicketChannel(ctx))
                return true;

            await ctx.Channel.SendMessageAsync("You can only use this command inside a deposit ticket channel.");
            return false;
        }

        public static async Task<bool> EnforceWithdrawChannelAsync(SocketCommandContext ctx)
        {
            if (IsStaff(ctx) || IsInWithdrawTicketChannel(ctx))
                return true;

            await ctx.Channel.SendMessageAsync("You can only use this command inside a withdrawal ticket channel.");
            return false;
        }

        public static async Task<bool> EnforceCoinflipChannelAsync(SocketCommandContext ctx)
        {
            if (IsStaff(ctx) || IsInCoinflipTicketChannel(ctx))
                return true;

            await ctx.Channel.SendMessageAsync("You can only use this command inside a games channel.");
            return false;
        }

        public static async Task<bool> EnforceBlackjackChannelAsync(SocketCommandContext ctx)
        {
            if (IsStaff(ctx) || IsInCoinflipTicketChannel(ctx))
                return true;

            await ctx.Channel.SendMessageAsync("You can only use this command inside a games channel.");
            return false;
        }

        public static async Task<bool> EnforceStakeChannelAsync(SocketCommandContext ctx)
        {
            if (IsStaff(ctx) || IsInStakeTicketChannel(ctx))
                return true;

            await ctx.Channel.SendMessageAsync("You can only use this command inside a games channel.");
            return false;
        }
    }
}
