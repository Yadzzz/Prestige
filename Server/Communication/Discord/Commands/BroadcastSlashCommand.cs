using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;
using System.ComponentModel;
using Server.Infrastructure;
using Server.Infrastructure.Configuration;
using Server.Infrastructure.Discord;

namespace Server.Communication.Discord.Commands
{
    public class BroadcastSlashCommand
    {
        [Command("broadcast")]
        [Description("Send a server-wide broadcast message.")]
        public async Task BroadcastAsync(CommandContext ctx)
        {
            if (ctx.Member == null || !ctx.Member.IsStaff())
            {
                await ctx.RespondAsync("You do not have permission to use this command.");
                return;
            }

            if (ctx is SlashCommandContext slashCtx)
            {
                var modal = new DiscordModalBuilder()
                    .WithTitle("Create Broadcast")
                    .WithCustomId("broadcast_modal")
                    .AddTextInput(new DiscordTextInputComponent("Announcement Title", "title", "", required: true, max_length: 256), "Announcement Title")
                    .AddTextInput(new DiscordTextInputComponent("Message Content", "message", "", required: true, style: DiscordTextInputStyle.Paragraph), "Message Content")
                    .AddTextInput(new DiscordTextInputComponent("Image URL", "image_url", "", required: false), "Image URL");

                await slashCtx.Interaction.CreateResponseAsync(DiscordInteractionResponseType.Modal, modal);
            }
            else
            {
                await ctx.RespondAsync("This command must be used as a slash command.");
            }
        }
    }
}
