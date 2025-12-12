using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Server.Infrastructure;
using Server.Infrastructure.Database;
using Server.Infrastructure.Discord;
using Server.Infrastructure.Configuration;

namespace Server.Client.Races
{
    public class RaceService
    {
        private readonly ServerManager _serverManager;
        private Race _activeRace;
        private readonly ConcurrentDictionary<string, RaceParticipant> _activeParticipants;
        private readonly Timer _flushTimer;
        private bool _isDirty;

        public RaceService(ServerManager serverManager)
        {
            _serverManager = serverManager;
            _activeParticipants = new ConcurrentDictionary<string, RaceParticipant>();
            
            // Load active race from DB if exists
            LoadActiveRace();

            // Flush to DB and update Discord every 60 seconds
            _flushTimer = new Timer(async _ => await FlushAndBroadcastAsync(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        public Race GetActiveRace()
        {
            return _activeRace;
        }

        public async Task RegisterWagerAsync(string userIdentifier, string username, long amount)
        {
            var race = _activeRace;
            if (race == null || race.Status != RaceStatus.Active)
                return;

            if (DateTime.UtcNow > race.EndTime)
            {
                // Race ended; do not accept new wagers. 
                // The background timer will handle closing the race.
                return;
            }

            // Optimization: If user is already participating, they have already passed the staff check.
            // This avoids repeated API calls for every wager.
            if (!_activeParticipants.ContainsKey(userIdentifier))
            {
                // Check if user is staff
                try
                {
                    var client = _serverManager.DiscordBotHost.Client;
                    var guildId = ConfigService.Current.Discord.GuildId;
                    if (client != null && guildId != 0)
                    {
                        var guild = client.GetGuild(guildId);
                        if (guild != null)
                        {
                            if (ulong.TryParse(userIdentifier, out var userId))
                            {
                                var member = guild.GetUser(userId);
                                if (member != null && member.IsStaff())
                                {
                                    return;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _serverManager.LoggerManager.LogError($"[RaceService] Failed to check staff status for {userIdentifier}: {ex.Message}");
                }
            }

            _activeParticipants.AddOrUpdate(userIdentifier,
                // Add new
                id => new RaceParticipant
                {
                    RaceId = race.Id,
                    UserIdentifier = id,
                    Username = username,
                    TotalWagered = amount
                },
                // Update existing
                (id, existing) =>
                {
                    existing.TotalWagered += amount;
                    // Update username in case it changed (optional, but good for display)
                    existing.Username = username; 
                    return existing;
                });

            _isDirty = true;
        }

        public async Task<Race> CreateRaceAsync(DateTime endTime, List<RacePrize> prizes, ulong channelId)
        {
            if (_activeRace != null && _activeRace.Status == RaceStatus.Active)
            {
                throw new InvalidOperationException("A race is already active.");
            }

            var race = new Race
            {
                StartTime = DateTime.UtcNow,
                EndTime = endTime,
                Status = RaceStatus.Active,
                ChannelId = channelId,
                PrizeDistributionJson = System.Text.Json.JsonSerializer.Serialize(prizes)
            };

            // Save to DB to get ID
            using (var cmd = _serverManager.DatabaseManager.CreateDatabaseCommand())
            {
                cmd.SetCommand("INSERT INTO races (StartTime, EndTime, Status, PrizeDistributionJson, ChannelId, MessageId) VALUES (@Start, @End, @Status, @Prizes, @Channel, 0); SELECT LAST_INSERT_ID();");
                cmd.AddParameter("@Start", race.StartTime);
                cmd.AddParameter("@End", race.EndTime);
                cmd.AddParameter("@Status", race.Status.ToString());
                cmd.AddParameter("@Prizes", race.PrizeDistributionJson);
                cmd.AddParameter("@Channel", race.ChannelId);
                
                var result = await cmd.ExecuteScalarAsync();
                race.Id = Convert.ToInt32(result);
            }

            _activeRace = race;
            _activeParticipants.Clear();
            return race;
        }

        public Race CreateRace(DateTime endTime, List<RacePrize> prizes, ulong channelId)
        {
            return CreateRaceAsync(endTime, prizes, channelId).GetAwaiter().GetResult();
        }

        public async Task EndRaceAsync()
        {
            if (_activeRace == null) return;

            // Force the race to end immediately.
            // FlushAndBroadcastAsync handles the logic of updating Discord, DB, and clearing memory.
            _activeRace.EndTime = DateTime.UtcNow.AddSeconds(-1);
            await FlushAndBroadcastAsync();
        }

        public void EndRace()
        {
            EndRaceAsync().GetAwaiter().GetResult();
        }

        private void LoadActiveRace()
        {
            using (var cmd = _serverManager.DatabaseManager.CreateDatabaseCommand())
            {
                cmd.SetCommand("SELECT * FROM races WHERE Status = 'Active' ORDER BY Id DESC LIMIT 1");
                var dt = cmd.ExecuteDataTable();
                if (dt.Rows.Count > 0)
                {
                    var row = dt.Rows[0];
                    _activeRace = new Race
                    {
                        Id = Convert.ToInt32(row["Id"]),
                        StartTime = Convert.ToDateTime(row["StartTime"]),
                        EndTime = Convert.ToDateTime(row["EndTime"]),
                        Status = (RaceStatus)Enum.Parse(typeof(RaceStatus), row["Status"].ToString()),
                        PrizeDistributionJson = row["PrizeDistributionJson"].ToString(),
                        ChannelId = Convert.ToUInt64(row["ChannelId"]),
                        MessageId = Convert.ToUInt64(row["MessageId"])
                    };

                    // Load participants
                    LoadParticipants(_activeRace.Id);
                }
            }
        }

        private void LoadParticipants(int raceId)
        {
            using (var cmd = _serverManager.DatabaseManager.CreateDatabaseCommand())
            {
                cmd.SetCommand("SELECT * FROM race_participants WHERE RaceId = @RaceId");
                cmd.AddParameter("@RaceId", raceId);
                var dt = cmd.ExecuteDataTable();
                foreach (System.Data.DataRow row in dt.Rows)
                {
                    var p = new RaceParticipant
                    {
                        RaceId = raceId,
                        UserIdentifier = row["UserIdentifier"].ToString(),
                        TotalWagered = Convert.ToInt64(row["TotalWagered"]),
                        Username = row["Username"].ToString()
                    };
                    _activeParticipants.TryAdd(p.UserIdentifier, p);
                }
            }
        }

        private async Task FlushAndBroadcastAsync()
        {
            try
            {
                var race = _activeRace;
                if (race == null) return;

                bool isEnding = race.Status == RaceStatus.Active && DateTime.UtcNow > race.EndTime;

                if (!_isDirty && !isEnding) return;

                _isDirty = false;

                // 1. Flush to DB (Bulk)
                var participants = _activeParticipants.Values.ToList();
                if (participants.Count > 0)
                {
                    using (var cmd = _serverManager.DatabaseManager.CreateDatabaseCommand())
                    {
                        var sb = new System.Text.StringBuilder();
                        sb.Append("INSERT INTO race_participants (RaceId, UserIdentifier, TotalWagered, Username) VALUES ");

                        for (int i = 0; i < participants.Count; i++)
                        {
                            if (i > 0) sb.Append(",");
                            sb.Append($"(@r{i}, @u{i}, @w{i}, @n{i})");

                            cmd.AddParameter($"@r{i}", participants[i].RaceId);
                            cmd.AddParameter($"@u{i}", participants[i].UserIdentifier);
                            cmd.AddParameter($"@w{i}", participants[i].TotalWagered);
                            cmd.AddParameter($"@n{i}", participants[i].Username);
                        }

                        sb.Append(" ON DUPLICATE KEY UPDATE TotalWagered = VALUES(TotalWagered), Username = VALUES(Username)");

                        cmd.SetCommand(sb.ToString());
                        await cmd.ExecuteQueryAsync();
                    }
                }

                // 2. Handle Ending & Prize Distribution
                List<string>? distributionLog = null;
                if (isEnding)
                {
                    race.Status = RaceStatus.Finished;
                    using (var cmd = _serverManager.DatabaseManager.CreateDatabaseCommand())
                    {
                        cmd.SetCommand("UPDATE races SET Status = @Status WHERE Id = @Id");
                        cmd.AddParameter("@Status", race.Status.ToString());
                        cmd.AddParameter("@Id", race.Id);
                        await cmd.ExecuteQueryAsync();
                    }

                    // Distribute Prizes
                    distributionLog = new List<string>();
                    var winners = GetTopParticipants(50);
                    var prizes = race.GetPrizes();
                    var usersService = _serverManager.UsersService;

                    foreach (var prize in prizes)
                    {
                        int index = prize.Rank - 1;
                        if (index >= 0 && index < winners.Count)
                        {
                            var winner = winners[index];
                            if (Server.Client.Utils.GpParser.TryParseAmountInK(prize.Prize, out long amountK))
                            {
                                await usersService.AddBalanceAsync(winner.UserIdentifier, amountK);
                                distributionLog.Add($"**#{prize.Rank}** {winner.Username}: `{prize.Prize}` ✅");

                                _serverManager.LogsService.Log(
                                    source: nameof(RaceService),
                                    level: "Info",
                                    userIdentifier: winner.UserIdentifier,
                                    action: "RacePrize",
                                    message: $"Won rank {prize.Rank} in race {race.Id}. Prize: {prize.Prize} ({amountK}k)",
                                    exception: null);
                            }
                        }
                    }
                }

                // 3. Update Discord Message
                if (race.ChannelId != 0 && race.MessageId != 0)
                {
                    try
                    {
                        var client = _serverManager.DiscordBotHost?.Client;
                        if (client != null)
                        {
                            var channel = client.GetChannel(race.ChannelId) as IMessageChannel;
                            if (channel != null)
                            {
                                var embed = BuildRaceEmbed(race, GetTopParticipants(10), isEnding, distributionLog);

                                try
                                {
                                    var message = await channel.GetMessageAsync(race.MessageId) as IUserMessage;
                                    if (message != null)
                                    {
                                        await message.ModifyAsync(msg => msg.Embed = embed);
                                    }
                                    else
                                    {
                                        // Message not found or not a user message
                                        var newMsg = await channel.SendMessageAsync(embed: embed);
                                        await SetMessageIdAsync(newMsg.Id);
                                    }
                                }
                                catch (Discord.Net.HttpException ex) when (ex.HttpCode == System.Net.HttpStatusCode.NotFound)
                                {
                                    var newMsg = await channel.SendMessageAsync(embed: embed);
                                    await SetMessageIdAsync(newMsg.Id);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _serverManager.LoggerManager.LogError($"Failed to update race leaderboard: {ex}");
                    }
                }

                // 4. Cleanup
                if (isEnding)
                {
                    if (_activeRace == race)
                    {
                        _activeRace = null!;
                        _activeParticipants.Clear();
                    }
                }
            }
            catch (Exception ex)
            {
                _serverManager.LoggerManager.LogError($"[RaceService] Critical error in FlushAndBroadcastAsync: {ex}");
            }
        }

        private Embed BuildRaceEmbed(Race race, List<RaceParticipant> topParticipants, bool isEnding, List<string>? distributionLog = null)
        {
            var totalWagered = _activeParticipants.Values.Sum(p => p.TotalWagered);
            var totalWageredFormatted = Server.Client.Utils.GpFormatter.Format(totalWagered);
            
            var sb = new System.Text.StringBuilder();
            if (topParticipants.Count == 0)
            {
                sb.AppendLine("No wagers yet. Be the first!");
            }
            else
            {
                int rank = 1;
                foreach (var p in topParticipants)
                {
                    string medal = rank switch
                    {
                        1 => "🥇",
                        2 => "🥈",
                        3 => "🥉",
                        4 => "4️⃣",
                        5 => "5️⃣",
                        6 => "6️⃣",
                        7 => "7️⃣",
                        8 => "8️⃣",
                        9 => "9️⃣",
                        10 => "🔟",
                        _ => $"#{rank}"
                    };
                    sb.AppendLine($"{medal} **{p.Username}** — `{Server.Client.Utils.GpFormatter.Format(p.TotalWagered)}`");
                    rank++;
                }
            }

            var prizes = race.GetPrizes();
            var prizeDesc = string.Join("\n", prizes.Select(x => $"`#{x.Rank}` {x.Prize}"));
            if (string.IsNullOrEmpty(prizeDesc)) prizeDesc = "None";

            var endTimestamp = new DateTimeOffset(race.EndTime).ToUnixTimeSeconds();
            var timeString = isEnding ? "Ended" : $"<t:{endTimestamp}:R>";

            var embed = new EmbedBuilder()
                .WithTitle(isEnding ? "🏁  **RACE ENDED**  🏁" : "🏁  **ACTIVE RACE**  🏁")
                .WithDescription($"Ends: {timeString}\nTotal Wagered: `{totalWageredFormatted}`")
                .WithColor(isEnding ? Color.DarkGrey : Color.Gold)
                .WithThumbnailUrl("https://i.imgur.com/e45uYPm.gif")
                .AddField("🏆 Leaderboard", sb.ToString(), false);

            if (isEnding && distributionLog != null && distributionLog.Count > 0)
            {
                embed.AddField("🎁 Winners Paid", string.Join("\n", distributionLog), false);
            }
            else
            {
                embed.AddField("🎁 Prizes", prizeDesc, false);
            }

            embed.WithFooter($"Race ID: {race.Id} • {ServerConfiguration.ShortName}", null)
                .WithCurrentTimestamp();

            if (!isEnding)
            {
                embed.AddField("\u200b", "-# *Updates every 30 seconds*", false);
            }

            return embed.Build();
        }
        
        public List<RaceParticipant> GetTopParticipants(int count)
        {
            return _activeParticipants.Values
                .OrderByDescending(p => p.TotalWagered)
                .Take(count)
                .ToList();
        }

        public async Task SetMessageIdAsync(ulong messageId)
        {
            if (_activeRace == null) return;
            _activeRace.MessageId = messageId;
            
            using (var cmd = _serverManager.DatabaseManager.CreateDatabaseCommand())
            {
                cmd.SetCommand("UPDATE races SET MessageId = @MsgId WHERE Id = @Id");
                cmd.AddParameter("@MsgId", messageId);
                cmd.AddParameter("@Id", _activeRace.Id);
                await cmd.ExecuteQueryAsync();
            }
        }

        public void SetMessageId(ulong messageId)
        {
            SetMessageIdAsync(messageId).GetAwaiter().GetResult();
        }

        public async Task StopAsync()
        {
            _flushTimer?.Change(Timeout.Infinite, 0);
            await FlushAndBroadcastAsync();
        }
    }
}
