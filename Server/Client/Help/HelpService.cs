using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using Server.Infrastructure;
using Server.Infrastructure.Discord;

namespace Server.Client.Help
{
    public static class HelpService
    {
        public static DiscordEmbedBuilder BuildHelpEmbed(DiscordMember member)
        {
            var isStaff = member.IsStaff();
            var embed = new DiscordEmbedBuilder()
                .WithTitle("📜 Command List")
                .WithColor(DiscordColor.Blurple)
                .WithFooter(ServerConfiguration.ServerName)
                .WithTimestamp(DateTime.UtcNow);

            // General Commands (Everyone)
            string generalCommands = 
                "**Games**\n" +
                "`!games` - Open Game Menu\n" +
                "`!cf <amount>` - Play Coinflip (e.g. `!cf 100m`)\n" +
                "`!bj <amount>` - Play Blackjack\n" +
                "`!hl <amount>` - Play Higher/Lower\n" +
                "`!mines <amount> [mines]` - Play Mines (default 3 mines)\n" +
                "`!cr <amount>` - Play Cracker\n" +
                "`!s <amount>` - Play Stake\n" +
                "`!cancel` - Cancel pending game sessions\n" +
                "\n" +
                "**Economy**\n" +
                "`!bal` - Check your wallet balance\n" +
                "`!d <amount>` - Request a deposit\n" +
                "`!w <amount>` - Request a withdrawal\n" +
                "`!code <code>` - Redeem a referral code";

            embed.AddField("General", generalCommands, false);

            // Staff Commands (Hidden for normal users)
            if (isStaff)
            {
                embed.AddField("\u200b", "\u200b", false);

                string staffCommands =
                    "**Economy Management**\n" +
                    "`!add <amount> <user>` - Add credits to user\n" +
                    "`!remove <amount> <user>` - Remove credits from user\n" +
                    "`!gift <amount> <user>` - Gift credits (logged as gift)\n" +
                    "`!wagerlock <user> <amount>` - Add wager lock to user\n" +
                    "`!buy <amount>` - Process a manual purchase\n" + 
                    "\n" +
                    "**Game Hosting & Events**\n" +
                    "`!referralcode` - Create/Edit referral codes\n" +
                    "`!chest <amount>` - Host a Chest game\n" +
                    "`!vaultsetup` / `!vaultstop` - Manage Vault game\n" +
                    "`!racecreate` / `!raceend` - Manage Races\n" +
                    "`/broadcast` - Send server announcement (Slash command)";
                    
                embed.AddField("🛡️ Staff Only", staffCommands, false);
            }

            return embed;
        }
    }
}
