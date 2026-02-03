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
                        .AddTextInput(new DiscordTextInputComponent("Duration (Days)", "duration", value: "1"), "Duration (Days)");

                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.Modal, modal);
                }
                else if (selected == "race_winners")
                {
                    var modal = new DiscordModalBuilder()
                        .WithTitle("Set Winners Count")
                        .WithCustomId("race_winners_modal")
                        .AddTextInput(new DiscordTextInputComponent("Number of Winners", "winners", value: "3"), "Number of Winners");

                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.Modal, modal);
                }
                else if (selected == "race_prizes")
                {
                    var modal = new DiscordModalBuilder()
                        .WithTitle("Set Prizes")
                        .WithCustomId("race_prizes_modal")
                        .AddTextInput(new DiscordTextInputComponent("Prizes (Rank:Prize, one per line)", "prizes", value: "1:100M\n2:50M\n3:25M", style: DiscordTextInputStyle.Paragraph), "Prizes (Rank:Prize, one per line)");

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
            try
            {
                // Security: Ensure the user interacting is Staff
                var member = e.Interaction.Guild != null ? await e.Interaction.Guild.GetMemberAsync(e.Interaction.User.Id) : null;
                if (member == null || !member.IsStaff())
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
                    var val = GetModalValue(e, "duration");
                    if (int.TryParse(val, out var days))
                    {
                        config.DurationDays = days;
                        await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                            new DiscordInteractionResponseBuilder().WithContent($"Duration set to {days} days.").AsEphemeral(true));
                    }
                    else
                    {
                        await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                            new DiscordInteractionResponseBuilder().WithContent($"Invalid duration value: '{val}'").AsEphemeral(true));
                    }
                }
                else if (e.Interaction.Data.CustomId == "race_winners_modal")
                {
                    var val = GetModalValue(e, "winners");
                    if (int.TryParse(val, out var winners))
                    {
                        config.WinnersCount = winners;
                        await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                            new DiscordInteractionResponseBuilder().WithContent($"Winners set to {winners}.").AsEphemeral(true));
                    }
                    else
                    {
                        await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                            new DiscordInteractionResponseBuilder().WithContent($"Invalid winners value: '{val}'").AsEphemeral(true));
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
                    else
                    {
                        await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                            new DiscordInteractionResponseBuilder().WithContent($"Invalid prizes value.").AsEphemeral(true));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HandleModal: {ex}");
                try
                {
                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                        new DiscordInteractionResponseBuilder().WithContent($"An error occurred: {ex.Message}").AsEphemeral(true));
                }
                catch { /* Ignore */ }
            }
        }

        private static string? GetModalValue(ModalSubmittedEventArgs e, string customId)
        {
            // In DSharpPlus nightly, e.Values keys seem to be the *Label* or *CustomId* depending on implementation.
            // The debug output showed: [DEBUG] e.Values Key: Duration (Days), Value: DSharpPlus.EventArgs.TextInputModalSubmission
            
            // We need to find the entry where the value (which is an object) corresponds to our customId, 
            // OR check if the key matches our customId (which it didn't in the debug output - it matched the Label "Duration (Days)").
            
            // Let's iterate through values and check their properties via reflection/dynamic since we know it's a TextInputModalSubmission
            foreach (var kvp in e.Values)
            {
                // The value is of type DSharpPlus.EventArgs.TextInputModalSubmission
                // We need to access its 'Value' property.
                try 
                {
                    // Check if this submission corresponds to the customId we are looking for.
                    // Since we can't easily check the CustomId of the submission object without casting (and we had issues with types),
                    // let's try to match by the Label if the key is indeed the label.
                    
                    // However, a more robust way is to check the CustomId if available on the object.
                    // Based on previous errors, we know we can't easily cast to IModalSubmission.
                    
                    // Let's try to use the key. The key in the debug output was "Duration (Days)".
                    // But we are passing "duration" as customId to this method.
                    
                    // If the key is the Label, we have a problem because we are looking up by CustomId ("duration").
                    // We should probably look up by the Label that corresponds to that CustomId.
                    
                    // Mapping:
                    // "duration" -> "Duration (Days)"
                    // "winners" -> "Number of Winners"
                    // "prizes" -> "Prizes (Rank:Prize, one per line)"
                    
                    string labelToLookFor = "";
                    if (customId == "duration") labelToLookFor = "Duration (Days)";
                    else if (customId == "winners") labelToLookFor = "Number of Winners";
                    else if (customId == "prizes") labelToLookFor = "Prizes (Rank:Prize, one per line)";
                    
                    if (kvp.Key == labelToLookFor)
                    {
                        // We found the entry by Label. Now get the value.
                        // The value is an object (TextInputModalSubmission). We need its Value property.
                        dynamic submission = kvp.Value;
                        return submission.Value;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error extracting value for {customId}: {ex.Message}");
                }
            }
            
            return null;
        }
    }
}
