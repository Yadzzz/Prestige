using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using Server.Infrastructure;

namespace Server.Client.Broadcast
{
    public class BroadcastService
    {
        public BroadcastService()
        {
        }

        public async Task BroadcastAsync(DiscordChannel channel, string title, string message, string imageUrl = null)
        {
            if (channel == null) return;

            var embed = new DiscordEmbedBuilder()
                .WithTitle(title)
                .WithDescription(message)
                .WithColor(DiscordColor.Azure)
                .WithTimestamp(DateTime.UtcNow)
                .WithFooter("Broadcast System");

            if (!string.IsNullOrEmpty(imageUrl))
            {
                //embed.WithImageUrl(imageUrl);
                // Or Thumbnail if preferred, but broadcast usually implies big image
                embed.WithThumbnail(imageUrl); 
            }
            else
            {
                //embed.WithThumbnail("https://i.imgur.com/BTaePNv.jpeg");
            }

            var builder = new DiscordMessageBuilder()
                .WithContent("@everyone")
                .AddEmbed(embed)
                .WithAllowedMentions(Mentions.All); // Ensure it actually pings

            await channel.SendMessageAsync(builder);
        }
    }
}
