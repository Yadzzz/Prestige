using Discord;
using Discord.Interactions;
using System.Threading.Tasks;

namespace Server.Communication.Discord.Commands
{
    public class PingSlashCommand : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("ping", "Simple health check command.")]
        public async Task PingAsync()
        {
            var embed = new EmbedBuilder()
                .WithTitle("Pong")
                .WithDescription($"{ServerConfiguration.ShortName} bot is online and responding.")
                .WithColor(Color.Blue);

            await RespondAsync(embed: embed.Build());
        }
    }
}
