using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
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

        public static async Task HandleComponent(DiscordClient client, ComponentInteractionCreatedEventArgs e)
        {
            // Security: Ensure the user interacting is Staff
            var member = e.Guild != null ? await e.Guild.GetMemberAsync(e.User.Id) : null;
            if (!member.IsStaff())
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("You are not authorized to configure races.")
                        .AsEphemeral(true));
                return;
            }

            if (e.Id == "race_config_menu")
            {
                var selected = e.Values[0];
                if (selected == "race_duration")
                {
                    var modal = new DiscordModalBuilder()
                        .WithTitle("Set Race Duration")
                        .WithCustomId("race_duration_modal")
                        .AddTextInput(new DiscordTextInputComponent("Duration (Days)", "duration", "1"), "Duration (Days)");

                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.Modal, modal);
                }
                else if (selected == "race_winners")
                {
                    var modal = new DiscordModalBuilder()
                        .WithTitle("Set Winners Count")
                        .WithCustomId("race_winners_modal")
                        .AddTextInput(new DiscordTextInputComponent("Number of Winners", "winners", "3"), "Number of Winners");

                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.Modal, modal);
                }
                else if (selected == "race_prizes")
                {
                    var modal = new DiscordModalBuilder()
                        .WithTitle("Set Prizes")
                        .WithCustomId("race_prizes_modal")
                        .AddTextInput(new DiscordTextInputComponent("Prizes (Rank:Prize, one per line)", "prizes", "1:100M\n2:50M\n3:25M", style: DiscordTextInputStyle.Paragraph), "Prizes (Rank:Prize, one per line)");

                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.Modal, modal);
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
                        
                        await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                            new DiscordInteractionResponseBuilder().WithContent($"Race started! ID: {race.Id} in <#{raceChannelId}>"));
                        
                        // Post initial leaderboard
                        var leaderboardEmbed = new DiscordEmbedBuilder()
                            .WithTitle("🏆 Race Leaderboard 🏆")
                            .WithDescription("Race is active! Wager to climb the ranks.")
                            .WithColor(DiscordColor.Gold)
                            .WithThumbnail("https://i.imgur.com/Axcs6YE.gif")
                            .WithTimestamp(DateTimeOffset.UtcNow)
                            .WithFooter($"Ends at {endTime:g}");
                            
                        var msg = await raceChannel.SendMessageAsync(leaderboardEmbed);
                        await env.ServerManager.RaceService.SetMessageIdAsync(msg.Id);
                        
                        _pendingConfigs.Remove(e.User.Id);
                    }
                    catch (Exception ex)
                    {
                        await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                            new DiscordInteractionResponseBuilder().WithContent($"Error: {ex.Message}").AsEphemeral(true));
                    }
                }
            }
        }

        public static async Task HandleModal(DiscordClient client, ModalSubmittedEventArgs e)
        {
            // Security: Ensure the user interacting is Staff
            var member = e.Interaction.Guild != null ? await e.Interaction.Guild.GetMemberAsync(e.Interaction.User.Id) : null;
            if (!member.IsStaff())
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("You are not authorized to configure races.")
                        .AsEphemeral(true));
                return;
            }

            if (!_pendingConfigs.TryGetValue(e.Interaction.User.Id, out var config))
            {
                config = new RaceConfigBuilder();
                _pendingConfigs[e.Interaction.User.Id] = config;
            }

            if (e.Interaction.Data.CustomId == "race_duration_modal")
            {
                if (int.TryParse(GetModalValue(e, "duration"), out var days))
                {
                    config.DurationDays = days;
                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                        new DiscordInteractionResponseBuilder().WithContent($"Duration set to {days} days.").AsEphemeral(true));
                }
            }
            else if (e.Interaction.Data.CustomId == "race_winners_modal")
            {
                if (int.TryParse(GetModalValue(e, "winners"), out var winners))
                {
                    config.WinnersCount = winners;
                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                        new DiscordInteractionResponseBuilder().WithContent($"Winners set to {winners}.").AsEphemeral(true));
                }
            }
            else if (e.Interaction.Data.CustomId == "race_prizes_modal")
            {
                var text = GetModalValue(e, "prizes");
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
                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                        new DiscordInteractionResponseBuilder().WithContent($"Prizes updated ({config.Prizes.Count} entries).").AsEphemeral(true));
                }
            }
        }

        private static string? GetModalValue(ModalSubmittedEventArgs e, string customId)
        {
            foreach (var component in e.Interaction.Data.Components)
            {
                if (component is DiscordActionRowComponent row)
                {
                    foreach (var inner in row.Components)
                    {
                        if (inner is DiscordTextInputComponent text && text.CustomId == customId)
                        {
                            return text.Value;
                        }
                    }
                }
            }
            return null;
        }
    }
}
