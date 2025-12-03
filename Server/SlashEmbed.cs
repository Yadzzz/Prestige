using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using System.Threading.Tasks;

namespace Server
{
    public class SlashEmbed : ApplicationCommandModule
    {
        [SlashCommand("embedtest", "Sends an embed with buttons")]
        public async Task EmbedTest(InteractionContext ctx)
        {
            var embed = new DiscordEmbedBuilder()
                .WithTitle("Embed with Buttons")
                .WithDescription("Click a button below:")
                .WithColor(DiscordColor.Blurple);

            await ctx.CreateResponseAsync(
                new DiscordInteractionResponseBuilder()
                    .AddEmbed(embed)
                    .AddComponents(
                        new DiscordButtonComponent(
                            ButtonStyle.Success,
                            "btn_ok",
                            "OK",
                            emoji: new DiscordComponentEmoji("✅")
                        ),
                        new DiscordButtonComponent(
                            ButtonStyle.Danger,
                            "btn_cancel",
                            "Cancel",
                            emoji: new DiscordComponentEmoji("❌")
                        )
                    )
            );
        }

    }
}
