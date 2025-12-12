using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Server.Infrastructure.Discord;
using Server.Client.Races;

namespace Server.Communication.Discord.Interactions
{
    public static class RaceInteractionHandler
    {
        // Temporary storage for race configuration
        private static readonly Dictionary<ulong, RaceConfigBuilder> _pendingConfigs = new();

        private class RaceConfigBuilder
        {
            public int DurationDays { get; set; } = 1;
            public int WinnersCount { get; set; } = 3;
            public List<RacePrize> Prizes { get; set; } = new();
        }

        public static async Task HandleComponent(DiscordSocketClient client, SocketMessageComponent component)
        {
            // Security: Ensure the user interacting is Staff
            var guild = (component.Channel as SocketGuildChannel)?.Guild;
            var member = guild?.GetUser(component.User.Id);
            
            if (member == null || !member.IsStaff())
            {
                await component.RespondAsync("You are not authorized to configure races.", ephemeral: true);
                return;
            }

            if (component.Data.CustomId == "race_config_menu")
            {
                var selected = component.Data.Values.FirstOrDefault();
                if (selected == "race_duration")
                {
                    var mb = new ModalBuilder()
                        .WithTitle("Set Race Duration")
                        .WithCustomId("race_duration_modal")
                        .AddTextInput("Duration (Days)", "duration", placeholder: "1", value: "1");
                    
                    await component.RespondWithModalAsync(mb.Build());
                }
                else if (selected == "race_winners")
                {
                    var mb = new ModalBuilder()
                        .WithTitle("Set Winners Count")
                        .WithCustomId("race_winners_modal")
                        .AddTextInput("Number of Winners", "winners", placeholder: "3", value: "3");
                    
                    await component.RespondWithModalAsync(mb.Build());
                }
                else if (selected == "race_prizes")
                {
                    var mb = new ModalBuilder()
                        .WithTitle("Set Prizes")
                        .WithCustomId("race_prizes_modal")
                        .AddTextInput("Prizes (Rank:Prize, one per line)", "prizes", TextInputStyle.Paragraph, placeholder: "1:100M\n2:50M\n3:25M", value: "1:100M\n2:50M\n3:25M");
                    
                    await component.RespondWithModalAsync(mb.Build());
                }
                else if (selected == "race_start")
                {
                    if (!_pendingConfigs.TryGetValue(component.User.Id, out var config))
                    {
                        config = new RaceConfigBuilder(); // Default
                    }

                    var env = ServerEnvironment.GetServerEnvironment();
                    try
                    {
                        var endTime = DateTime.UtcNow.AddDays(config.DurationDays);
                        
                        // Use the specific race channel ID
                        var raceChannelId = DiscordIds.RaceChannelId;
                        var raceChannel = client.GetChannel(raceChannelId) as IMessageChannel;

                        if (raceChannel == null)
                        {
                             await component.RespondAsync($"Error: Race channel <#{raceChannelId}> not found.", ephemeral: true);
                             return;
                        }

                        var race = await env.ServerManager.RaceService.CreateRaceAsync(endTime, config.Prizes, raceChannelId);
                        
                        await component.RespondAsync($"Race started! ID: {race.Id} in <#{raceChannelId}>");
                        
                        // Post initial leaderboard
                        var leaderboardEmbed = new EmbedBuilder()
                            .WithTitle("🏆 Race Leaderboard 🏆")
                            .WithDescription("Race is active! Wager to climb the ranks.")
                            .WithColor(Color.Gold)
                            .WithFooter($"Ends at {endTime:g}");
                            
                        var msg = await raceChannel.SendMessageAsync(embed: leaderboardEmbed.Build());
                        await env.ServerManager.RaceService.SetMessageIdAsync(msg.Id);
                        
                        _pendingConfigs.Remove(component.User.Id);
                    }
                    catch (Exception ex)
                    {
                        await component.RespondAsync($"Error: {ex.Message}", ephemeral: true);
                    }
                }
            }
        }

        public static async Task HandleModal(DiscordSocketClient client, SocketModal modal)
        {
            // Security: Ensure the user interacting is Staff
            var guild = (modal.Channel as SocketGuildChannel)?.Guild;
            var member = guild?.GetUser(modal.User.Id);

            if (member == null || !member.IsStaff())
            {
                await modal.RespondAsync("You are not authorized to configure races.", ephemeral: true);
                return;
            }

            if (!_pendingConfigs.TryGetValue(modal.User.Id, out var config))
            {
                config = new RaceConfigBuilder();
                _pendingConfigs[modal.User.Id] = config;
            }

            if (modal.Data.CustomId == "race_duration_modal")
            {
                var value = modal.Data.Components.FirstOrDefault(x => x.CustomId == "duration")?.Value;
                if (int.TryParse(value, out var days))
                {
                    config.DurationDays = days;
                    await modal.RespondAsync($"Duration set to {days} days.", ephemeral: true);
                }
                else 
                {
                    await modal.RespondAsync("Invalid duration.", ephemeral: true);
                }
            }
            else if (modal.Data.CustomId == "race_winners_modal")
            {
                var value = modal.Data.Components.FirstOrDefault(x => x.CustomId == "winners")?.Value;
                if (int.TryParse(value, out var winners))
                {
                    config.WinnersCount = winners;
                    await modal.RespondAsync($"Winners set to {winners}.", ephemeral: true);
                }
                else
                {
                    await modal.RespondAsync("Invalid number.", ephemeral: true);
                }
            }
            else if (modal.Data.CustomId == "race_prizes_modal")
            {
                var text = modal.Data.Components.FirstOrDefault(x => x.CustomId == "prizes")?.Value;
                if (text != null)
                {
                    var lines = text.Split('\n');
                    config.Prizes.Clear();
                    foreach (var line in lines)
                    {
                        var parts = line.Split(':');
                        if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out var rank))
                        {
                            config.Prizes.Add(new RacePrize { Rank = rank, Prize = parts[1].Trim() });
                        }
                    }
                    await modal.RespondAsync($"Prizes updated ({config.Prizes.Count} entries).", ephemeral: true);
                }
            }
        }
    }
}
