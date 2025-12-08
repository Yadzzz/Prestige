using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Server.Client.Stakes;
using Server.Client.Users;
using Server.Client.Utils;
using Server.Infrastructure;
using Server.Infrastructure.Discord;

namespace Server.Communication.Discord.Commands
{
    public class StakeCommand : BaseCommandModule
    {
        private static readonly TimeSpan RateLimitInterval = TimeSpan.FromSeconds(1);
        [Command("stake")]
        [Aliases("s")]
        public async Task Stake(CommandContext ctx, string amount)
        {
            if (!await DiscordChannelPermissionService.EnforceStakeChannelAsync(ctx))
            {
                return;
            }

            if (RateLimiter.IsRateLimited(ctx.User.Id, "stake", RateLimitInterval))
            {
                await ctx.RespondAsync("You're doing that too fast. Please wait a moment.");
                return;
            }

            var env = ServerEnvironment.GetServerEnvironment();
            var serverManager = env.ServerManager;
            var usersService = serverManager.UsersService;
            var stakesService = serverManager.StakesService;

            var user = await usersService.EnsureUserAsync(ctx.User.Id.ToString(), ctx.User.Username, ctx.Member.DisplayName);
            if (user == null)
                return;

            if (!GpParser.TryParseAmountInK(amount, out var amountK))
            {
                await ctx.RespondAsync("Invalid amount. Examples: `!stake 100`, `!stake 0.5`, `!stake 1b`, `!stake 1000m`.");
                return;
            }

            if (amountK < GpFormatter.MinimumBetAmountK)
            {
                await ctx.RespondAsync($"Minimum stake is {GpFormatter.Format(GpFormatter.MinimumBetAmountK)}.");
                return;
            }

            if (user.Balance < amountK)
            {
                await ctx.RespondAsync("You don't have enough balance for this stake.");
                return;
            }

            // Lock stake amount up-front so it can't be reused while pending
            var balanceLocked = await usersService.RemoveBalanceAsync(user.Identifier, amountK);
            if (!balanceLocked)
            {
                await ctx.RespondAsync("Failed to lock balance for this stake. Please try again.");
                return;
            }

            var stake = await stakesService.CreateStakeAsync(user, amountK);
            if (stake == null)
            {
                // rollback locked balance on failure
                await usersService.AddBalanceAsync(user.Identifier, amountK);

                await ctx.RespondAsync("Failed to create stake. Please try again later.");
                serverManager.LogsService.Log(
                    source: nameof(StakeCommand),
                    level: "Error",
                    userIdentifier: user.Identifier,
                    action: "CreateStakeFailed",
                    message: $"Failed to create stake for {user.Identifier} amountK={amountK}",
                    exception: null);
                return;
            }

            serverManager.LogsService.Log(
                source: nameof(StakeCommand),
                level: "Info",
                userIdentifier: user.Identifier,
                action: "StakeCreated",
                message: $"Stake created id={stake.Id} user={user.Identifier} amountK={stake.AmountK} balanceBefore={user.Balance}",
                exception: null,
                metadataJson: $"{{\"referenceId\":{stake.Id},\"kind\":\"Stake\",\"amountK\":{stake.AmountK},\"balanceBefore\":{user.Balance}}}");

            var prettyAmount = GpFormatter.Format(stake.AmountK);
            var remainingK = user.Balance - stake.AmountK;
            var remainingPretty = GpFormatter.Format(remainingK);

            var embed = new DiscordEmbedBuilder()
                .WithTitle("⚔️ Stake Request")
                .WithDescription("Your stake was sent.")
                .AddField("Amount", prettyAmount, true)
                .AddField("Remaining", remainingPretty, true)
                .AddField("Member", ctx.Member.DisplayName, true)
                .WithColor(DiscordColor.Gold)
                .WithThumbnail("https://i.imgur.com/e45uYPm.gif")
                .WithFooter(ServerConfiguration.ServerName)
                .WithTimestamp(DateTimeOffset.UtcNow);

            // User cancel button is temporarily disabled; keep code for future use.
            // var userCancelButton = new DiscordButtonComponent(ButtonStyle.Secondary, $"stake_usercancel_{stake.Id}", "Cancel", emoji: new DiscordComponentEmoji("❌"));

            var userMessage = await ctx.RespondAsync(new DiscordMessageBuilder()
                .AddEmbed(embed));

            var staffChannel = await ctx.Client.GetChannelAsync(DiscordIds.StakeStaffChannelId);

            var staffEmbed = new DiscordEmbedBuilder()
                .WithTitle("New Stake Request ⏳")
                .WithDescription($"User: {ctx.Member.DisplayName} ({user.Identifier})\nAmount: **{prettyAmount}**\nStake ID: `{stake.Id}`\nStatus: **PENDING**")
                .WithColor(DiscordColor.Orange)
                .WithTimestamp(DateTimeOffset.UtcNow);

            var winButton = new DiscordButtonComponent(ButtonStyle.Success, $"stake_win_{stake.Id}", "Win", emoji: new DiscordComponentEmoji("🏆"));
            //var cancelButton = new DiscordButtonComponent(ButtonStyle.Secondary, $"stake_cancel_{stake.Id}", "Cancel", emoji: new DiscordComponentEmoji("❌"));
            var loseButton = new DiscordButtonComponent(ButtonStyle.Danger, $"stake_lose_{stake.Id}", "Lose", emoji: new DiscordComponentEmoji("❌"));

            var staffMessage = await staffChannel.SendMessageAsync(new DiscordMessageBuilder()
                .WithContent($"<@&{DiscordIds.StaffRoleId}>")
                .AddEmbed(staffEmbed)
                .AddComponents(winButton, /*cancelButton,*/ loseButton));

            stakesService.UpdateStakeMessages(stake.Id, userMessage.Id, userMessage.Channel.Id, staffMessage.Id, staffMessage.Channel.Id);
        }
    }
}
