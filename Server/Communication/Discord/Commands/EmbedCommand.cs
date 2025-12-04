using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Server.Communication.Discord.Commands
{
    public class EmbedCommand : BaseCommandModule
    {
        [Command("embedtest")]
        public async Task EmbedTest(CommandContext ctx)
        {
            var embed = new DiscordEmbedBuilder()
                .WithTitle("Embed with Buttons")
                .WithDescription("Click a button below:")
                .WithColor(DiscordColor.Blurple);

            await ctx.RespondAsync(
                new DiscordMessageBuilder()
                    .AddEmbed(embed)
                    .AddComponents(
                        new DiscordButtonComponent(ButtonStyle.Success, "btn_ok", "OK", emoji: new DiscordComponentEmoji("✅")),
                        new DiscordButtonComponent(ButtonStyle.Danger, "btn_cancel", "Cancel", emoji: new DiscordComponentEmoji("❌"))
                    )
            );
        }
    }
}
