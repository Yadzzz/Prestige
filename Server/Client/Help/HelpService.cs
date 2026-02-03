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
                "`!cf <amount>` - Play Coinflip\n" +
                "`!bj <amount>` - Play Blackjack\n" +
                "`!hl <amount>` - Play Higher/Lower\n" +
                "`!mines <amount>` - Play Mines\n" +
                "`!chest <amount>` - Open Chests\n" +
                "\n" +
                "**Economy**\n" +
                "`!bal` - Check Balance\n" +
                "`!deposit` - Information on how to deposit\n" +
                "`!withdraw <amount>` - Withdraw funds\n" +
                "`!code <code>` - Redeem a referral code\n" +
                "\n" +
                "**Utility**\n" +
                "`!ping` - Check bot latency\n" +
                "`!id` - Get your User ID";

            embed.AddField("General", generalCommands, false);

            // Staff Commands (Hidden for normal users)
            if (isStaff)
            {
                embed.AddField("\u200b", "\u200b", false);

                string staffCommands =
                    "**Management**\n" +
                    "`!referralcode` - Manage referral codes\n" +
                    "`/broadcast` - Send server-wide announcement\n" +
                    "`/race start` - Start a new wagering race\n" +
                    "`!add <amount> <user>` - Add (spawn) money to user\n" +
                    "`!remove <amount> <user>` - Remove money from user\n" +
                    "`!set <amount> <user>` - Set user balance exactly\n" +
                    "`!b <user>` - Check any user's balance";
                    
                embed.AddField("🛡️ Staff Only", staffCommands, false);
            }

            return embed;
        }
    }
}
