using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
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

        public static async Task HandleComponent(DiscordClient client, ComponentInteractionCreateEventArgs e)
        {
            if (e.Id == "race_config_menu")
            {
                var selected = e.Values[0];
                if (selected == "race_duration")
                {
                    var modal = new DiscordInteractionResponseBuilder()
                        .WithTitle("Set Race Duration")
                        .WithCustomId("race_duration_modal")
                        .AddComponents(new TextInputComponent("Duration (Days)", "duration", "1"));
                    
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.Modal, modal);
                }
                else if (selected == "race_winners")
                {
                    var modal = new DiscordInteractionResponseBuilder()
                        .WithTitle("Set Winners Count")
                        .WithCustomId("race_winners_modal")
                        .AddComponents(new TextInputComponent("Number of Winners", "winners", "3"));
                    
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.Modal, modal);
                }
                else if (selected == "race_prizes")
                {
                    var modal = new DiscordInteractionResponseBuilder()
                        .WithTitle("Set Prizes")
                        .WithCustomId("race_prizes_modal")
                        .AddComponents(new TextInputComponent("Prizes (Rank:Prize, one per line)", "prizes", "1:100M\n2:50M\n3:25M", style: TextInputStyle.Paragraph));
                    
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.Modal, modal);
                }
                else if (selected == "race_start")
                {
                    if (!_pendingConfigs.TryGetValue(e.User.Id, out var config))
                    {
                        config = new RaceConfigBuilder(); // Default
                    }

                    var env = ServerEnvironment.GetServerEnvironment();
                    try
                    {
                        var endTime = DateTime.UtcNow.AddDays(config.DurationDays);
                        
                        // Use the specific race channel ID
                        var raceChannelId = DiscordIds.RaceChannelId;
                        var raceChannel = await client.GetChannelAsync(raceChannelId);

                        var race = await env.ServerManager.RaceService.CreateRaceAsync(endTime, config.Prizes, raceChannelId);
                        
                        await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, 
                            new DiscordInteractionResponseBuilder().WithContent($"Race started! ID: {race.Id} in <#{raceChannelId}>"));
                        
                        // Post initial leaderboard
                        var leaderboardEmbed = new DiscordEmbedBuilder()
                            .WithTitle("🏆 Race Leaderboard 🏆")
                            .WithDescription("Race is active! Wager to climb the ranks.")
                            .WithColor(DiscordColor.Gold)
                            .WithFooter($"Ends at {endTime:g}");
                            
                        var msg = await raceChannel.SendMessageAsync(leaderboardEmbed);
                        await env.ServerManager.RaceService.SetMessageIdAsync(msg.Id);
                        
                        _pendingConfigs.Remove(e.User.Id);
                    }
                    catch (Exception ex)
                    {
                        await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, 
                            new DiscordInteractionResponseBuilder().WithContent($"Error: {ex.Message}").AsEphemeral(true));
                    }
                }
            }
        }

        public static async Task HandleModal(DiscordClient client, ModalSubmitEventArgs e)
        {
            if (!_pendingConfigs.TryGetValue(e.Interaction.User.Id, out var config))
            {
                config = new RaceConfigBuilder();
                _pendingConfigs[e.Interaction.User.Id] = config;
            }

            if (e.Interaction.Data.CustomId == "race_duration_modal")
            {
                if (int.TryParse(e.Values["duration"], out var days))
                {
                    config.DurationDays = days;
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, 
                        new DiscordInteractionResponseBuilder().WithContent($"Duration set to {days} days.").AsEphemeral(true));
                }
            }
            else if (e.Interaction.Data.CustomId == "race_winners_modal")
            {
                if (int.TryParse(e.Values["winners"], out var winners))
                {
                    config.WinnersCount = winners;
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, 
                        new DiscordInteractionResponseBuilder().WithContent($"Winners set to {winners}.").AsEphemeral(true));
                }
            }
            else if (e.Interaction.Data.CustomId == "race_prizes_modal")
            {
                var text = e.Values["prizes"];
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
                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, 
                    new DiscordInteractionResponseBuilder().WithContent($"Prizes updated ({config.Prizes.Count} entries).").AsEphemeral(true));
            }
        }
    }
}
