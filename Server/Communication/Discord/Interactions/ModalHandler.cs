using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.EventArgs;
using DSharpPlus.Entities;

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
        }
    }
}
