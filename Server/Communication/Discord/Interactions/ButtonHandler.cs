using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace Server.Communication.Discord.Interactions
{
    public class ButtonHandler
    {
        public static async Task HandleButtons(DiscordClient client, ComponentInteractionCreateEventArgs e)
        {
            if (e.Id != "btn_ok" && e.Id != "btn_cancel")
                return;

            var builder = new DiscordInteractionResponseBuilder()
                .AddComponents(
                    new DiscordButtonComponent(ButtonStyle.Success, "btn_ok", "OK", disabled: true, emoji: new DiscordComponentEmoji("✅")),
                    new DiscordButtonComponent(ButtonStyle.Danger, "btn_cancel", "Cancel", disabled: true, emoji: new DiscordComponentEmoji("❌"))
                );

            if (e.Message.Embeds.Count > 0)
            {
                builder.AddEmbed(e.Message.Embeds[0]);
            }

            await e.Interaction.CreateResponseAsync(
                InteractionResponseType.UpdateMessage,
                builder
            );

            var text = e.Id == "btn_ok"
                ? "You clicked **OK** ✅"
                : "You clicked **Cancel** ❌";

            await e.Interaction.CreateFollowupMessageAsync(
                new DiscordFollowupMessageBuilder().WithContent(text)
            );
        }
    }
}
