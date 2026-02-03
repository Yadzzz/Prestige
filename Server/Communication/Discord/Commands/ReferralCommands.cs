using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Server.Infrastructure;
using Server.Client.Users;
using System.Threading.Tasks;
using System;
using Server.Infrastructure.Discord;

namespace Server.Communication.Discord.Commands
{
    public class ReferralCommands : BaseCommandModule
    {
        [Command("referralcode")]
        [Description("Opens a menu to create or update a referral code.")]
        public async Task SetReferralCode(CommandContext ctx)
        {
            if (ctx.Member == null || !ctx.Member.IsStaff())
            {
                await ctx.RespondAsync("You do not have permission to use this command.");
                return;
            }

            var embed = new DiscordEmbedBuilder()
                .WithTitle("Referral Code Management")
                .WithDescription("Click the button below to create or update a referral code.")
                .WithColor(DiscordColor.Blurple);

            var button = new DiscordButtonComponent(DiscordButtonStyle.Primary, "ref_create", "Create/Edit Code", false, new DiscordComponentEmoji("📝"));

            await ctx.RespondAsync(new DiscordMessageBuilder()
                .AddEmbed(embed)
                .AddActionRowComponent(new DiscordActionRowComponent(new[] { button })));
        }

        // Kept for backward compatibility if needed, but hidden or aliased differently?
        // Or removed as per request to simplify. I'll comment it out or remove it to enforce the new way.
        /*
        [Command("referralcode_legacy")]
        public async Task SetReferralCodeLegacy(CommandContext ctx, DiscordUser referrer, long reward, long referrerReward, int uses, long wagerLock, bool newUsersOnly)
        {
            // ... implementation ...
        }
        */

        [Command("code")]
        [Description("Redeem a referral code.")]
        public async Task RedeemCode(CommandContext ctx, string code)
        {
            var env = ServerEnvironment.GetServerEnvironment();
            var referralService = env.ServerManager.ReferralService;
            var usersService = env.ServerManager.UsersService;

            // Ensure user exists in our DB
            var user = await usersService.EnsureUserAsync(ctx.User.Id.ToString(), ctx.User.Username, ctx.User is DiscordMember m ? m.DisplayName : ctx.User.Username);

            if (user == null)
            {
                await ctx.RespondAsync("Error retrieving user profile.");
                return;
            }

            // Fetch code details BEFORE redeeming to know what to display
            var refCode = await referralService.GetReferralCodeAsync(code);
            // The service checks this too, but we need the info for the embed.
            // If null, the service will return generic error string, so we let the service handle the check order?
            // Actually, if we want to show a nice embed with "You received X", we need to know X.
            // So let's rely on the result string for flow control, but if success, we use the prefetched info (if found).
            // Or better: fetch code -> check basic validity -> redeem -> show success embed.

            string result = await referralService.RedeemCodeAsync(code, user);

            if (result == "Success")
            {
                // Refresh user to get new balance/wager lock
                user = await usersService.GetUserAsync(user.Identifier);
                
                // If refCode is null here it means it was deleted mid-transaction? Unlikely.
                // We'll re-fetch or assume we can fetch it now.
                if (refCode == null) refCode = await referralService.GetReferralCodeAsync(code);

                // Light purple color
                var embed = new DiscordEmbedBuilder()
                    .WithColor(new DiscordColor(0xD8BFD8));
                    //.WithThumbnail("https://i.imgur.com/kS94hFm.png");

                var description = $"Congratulations :tada:\n" +
                                  $"You successfully used code **{code}**\n";

                if (refCode != null && refCode.RewardAmount > 0)
                {
                    description += $"and redeemed a **{Server.Client.Utils.GpFormatter.Format(refCode.RewardAmount)}** reward.";
                }
                else
                {
                    description += "and redeemed the code.";
                }

                description += $"\n\n💳 **New Balance:** `{Server.Client.Utils.GpFormatter.Format(user.Balance)}`";

                if (user.WagerLock > 0)
                {
                    description += $"\n🔒 **Total Wager Lock:** `{Server.Client.Utils.GpFormatter.Format(user.WagerLock)}`";
                }

                embed.WithDescription(description);

                await ctx.RespondAsync(embed);
            }
            else
            {
                await ctx.RespondAsync($"❌ {result}");
            }
        }

        [Command("referrallist")]
        [Description("Lists all active referral codes (Admin only).")]
        public async Task ListReviewCodes(CommandContext ctx)
        {
            if (ctx.Member == null || !ctx.Member.IsStaff())
            {
                await ctx.RespondAsync("You do not have permission to use this command.");
                return;
            }

            var env = ServerEnvironment.GetServerEnvironment();
            var service = env.ServerManager.ReferralService;
            var codes = await service.GetActiveReferralCodesAsync();

            if (codes == null || codes.Count == 0)
            {
                await ctx.RespondAsync("No active referral codes found.");
                return;
            }

            var embed = new DiscordEmbedBuilder()
                .WithTitle("Active Referral Codes")
                .WithColor(DiscordColor.Gold);

            var description = "";
            foreach (var code in codes)
            {
                var uses = code.MaxUses == -1 ? "∞" : code.MaxUses.ToString();
                var line = $"**{code.Code}**\nOwner: <@{code.OwnerIdentifier}> | Uses: {code.CurrentUses}/{uses} | Reward: {Server.Client.Utils.GpFormatter.Format(code.RewardAmount)}\n";
                
                if ((description + line).Length > 2048) 
                {
                    description += "... (list truncated)";
                    break;
                }
                description += line + "\n";
            }

            embed.WithDescription(description);
            await ctx.RespondAsync(embed);
        }

        [Command("referraldisable")]
        [Description("Disables/Deletes a referral code (Admin only).")]
        public async Task DisableReferralCode(CommandContext ctx, string code)
        {
            if (ctx.Member == null || !ctx.Member.IsStaff())
            {
                await ctx.RespondAsync("You do not have permission to use this command.");
                return;
            }

            var env = ServerEnvironment.GetServerEnvironment();
            var service = env.ServerManager.ReferralService;
            
            bool result = await service.DisableReferralCodeAsync(code);
            if (result)
            {
                await ctx.RespondAsync($"✅ Referral code **{code}** has been disabled.");
            }
            else
            {
                await ctx.RespondAsync($"❌ Could not find or disable referral code **{code}**.");
            }
        }
    }
}
