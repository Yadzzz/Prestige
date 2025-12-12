using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Server.Client.Stakes;
using Server.Client.Users;
using Server.Client.Utils;
using Server.Infrastructure;
using Server.Infrastructure.Discord;

namespace Server.Communication.Discord.Commands
{
    public class StakeCommand : ModuleBase<SocketCommandContext>
    {
        private static readonly TimeSpan RateLimitInterval = TimeSpan.FromSeconds(1);

        [Command("stake")]
        [Alias("s")]
        public async Task Stake(string amount = null)
        {
            if (!await DiscordChannelPermissionService.EnforceStakeChannelAsync(Context))
            {
                return;
            }

            if (RateLimiter.IsRateLimited(Context.User.Id, "stake", RateLimitInterval))
            {
                await ReplyAsync("You're doing that too fast. Please wait a moment.");
                return;
            }

            if (string.IsNullOrWhiteSpace(amount))
            {
                await ReplyAsync("Please specify an amount. Usage: `!stake <amount>` (e.g. `!stake 100m`).");
                return;
            }

            var env = ServerEnvironment.GetServerEnvironment();
            var serverManager = env.ServerManager;
            var usersService = serverManager.UsersService;
            var stakesService = serverManager.StakesService;

            var displayName = (Context.User as SocketGuildUser)?.DisplayName ?? Context.User.Username;
            var user = await usersService.EnsureUserAsync(Context.User.Id.ToString(), Context.User.Username, displayName);
            if (user == null)
                return;

            if (!GpParser.TryParseAmountInK(amount, out var amountK))
            {
                await ReplyAsync("Invalid amount. Examples: `!stake 100`, `!stake 0.5`, `!stake 1b`, `!stake 1000m`.");
                return;
            }

            if (amountK < GpFormatter.MinimumBetAmountK)
            {
                await ReplyAsync($"Minimum stake is {GpFormatter.Format(GpFormatter.MinimumBetAmountK)}.");
                return;
            }

            if (user.Balance < amountK)
            {
                await ReplyAsync("You don't have enough balance for this stake.");
                return;
            }

            // Lock stake amount up-front so it can't be reused while pending
            var balanceLocked = await usersService.RemoveBalanceAsync(user.Identifier, amountK);
            if (!balanceLocked)
            {
                await ReplyAsync("Failed to lock balance for this stake. Please try again.");
                return;
            }

            var stake = await stakesService.CreateStakeAsync(user, amountK);
            if (stake == null)
            {
                // rollback locked balance on failure
                await usersService.AddBalanceAsync(user.Identifier, amountK);

                await ReplyAsync("Failed to create stake. Please try again later.");
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

            var embed = new EmbedBuilder()
                .WithTitle("⚔️ Stake Request")
                .WithDescription("Your stake was sent.")
                .AddField("Amount", $"`{prettyAmount}`", true)
                .AddField("Remaining", $"`{remainingPretty}`", true)
                .AddField("Member", displayName, true)
                .WithColor(Color.Gold)
                .WithThumbnailUrl("https://i.imgur.com/e45uYPm.gif")
                .WithFooter(ServerConfiguration.ServerName)
                .WithCurrentTimestamp();

            // User cancel button is temporarily disabled; keep code for future use.
            // var userCancelButton = new ButtonBuilder("Cancel", $"stake_usercancel_{stake.Id}", ButtonStyle.Secondary, emote: new Emoji("❌"));

            var userMessage = await ReplyAsync(embed: embed.Build());

            var staffChannel = await Context.Client.GetChannelAsync(DiscordIds.StakeStaffChannelId) as IMessageChannel;

            if (staffChannel != null)
            {
                var staffEmbed = new EmbedBuilder()
                    .WithTitle("New Stake Request ⏳")
                    .WithDescription($"User: {displayName} ({user.Identifier})\nAmount: `{prettyAmount}`\nStake ID: `{stake.Id}`\nStatus: **PENDING**")
                    .WithColor(Color.Orange)
                    .WithCurrentTimestamp();

                var builder = new ComponentBuilder()
                    .WithButton("Win", $"stake_win_{stake.Id}", ButtonStyle.Success, emote: new Emoji("🏆"))
                    .WithButton("Cancel", $"stake_cancel_{stake.Id}", ButtonStyle.Secondary, emote: new Emoji("❌"))
                    .WithButton("Lose", $"stake_lose_{stake.Id}", ButtonStyle.Danger, emote: new Emoji("❌"));

                var staffMessage = await staffChannel.SendMessageAsync(
                    text: $"<@&{DiscordIds.StaffRoleId}>",
                    embed: staffEmbed.Build(),
                    components: builder.Build());

                await stakesService.UpdateStakeMessagesAsync(stake.Id, userMessage.Id, userMessage.Channel.Id, staffMessage.Id, staffMessage.Channel.Id);
            }
        }
    }
}
