using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.EventArgs;
using DSharpPlus.Entities;
using Server.Infrastructure.Discord;

namespace Server.Communication.Discord.Interactions
{
    public class ModalHandler
    {
        public static async Task HandleModals(DiscordClient client, ModalSubmittedEventArgs e)
        {
            var id = e.Interaction.Data.CustomId;

            if (id.StartsWith("race_", StringComparison.OrdinalIgnoreCase))
            {
                await RaceInteractionHandler.HandleModal(client, e);
                return;
            }

            if (id.StartsWith("ref_", StringComparison.OrdinalIgnoreCase))
            {
                await ReferralInteractionHandler.HandleModal(client, e);
                return;
            }

            if (id == "broadcast_modal")
            {
                DiscordMember? member = null;
                if (e.Interaction.Guild is not null)
                {
                    member = await e.Interaction.Guild.GetMemberAsync(e.Interaction.User.Id);
                }

                if (member is null || !member.IsStaff())
                {
                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                        new DiscordInteractionResponseBuilder().WithContent("Unauthorized.").AsEphemeral(true));
                    return;
                }

                var title = GetValue(e.Values, "Announcement Title");
                var message = GetValue(e.Values, "Message Content");
                var imageUrl = GetValue(e.Values, "Image URL");

                var env = ServerEnvironment.GetServerEnvironment();
                // Send acknowledgment first so interaction doesn't fail
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                    new DiscordInteractionResponseBuilder().WithContent("Broadcasting...").AsEphemeral(true));

                await env.ServerManager.BroadcastService.BroadcastAsync(e.Interaction.Channel, title, message, imageUrl);
                return;
            }
        }

        private static string GetValue(System.Collections.Generic.IReadOnlyDictionary<string, DSharpPlus.EventArgs.IModalSubmission> values, string key)
        {
            if (values.TryGetValue(key, out var val)) 
            {
                // val is IModalSubmission
                // Assuming it has a Value property based on common patterns if not string directly
                try 
                {
                    dynamic d = val;
                    return d.Value.ToString();
                }
                catch
                {
                    return val?.ToString() ?? string.Empty;
                }
            }
            return string.Empty;
        }
    }
}
