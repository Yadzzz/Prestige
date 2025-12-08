using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Server.Infrastructure;
using Server.Infrastructure.Database;

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
            _flushTimer = new Timer(async _ => await FlushAndBroadcastAsync(), null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
        }

        public Race GetActiveRace()
        {
            return _activeRace;
        }

        public void RegisterWager(string userIdentifier, string username, long amount)
        {
            if (_activeRace == null || _activeRace.Status != RaceStatus.Active)
                return;

            if (DateTime.UtcNow > _activeRace.EndTime)
            {
                EndRace();
                return;
            }

            _activeParticipants.AddOrUpdate(userIdentifier,
                // Add new
                id => new RaceParticipant
                {
                    RaceId = _activeRace.Id,
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

        public Race CreateRace(DateTime endTime, List<RacePrize> prizes, ulong channelId)
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
                
                race.Id = Convert.ToInt32(cmd.ExecuteScalar());
            }

            _activeRace = race;
            _activeParticipants.Clear();
            return race;
        }

        public void EndRace()
        {
            if (_activeRace == null) return;

            _activeRace.Status = RaceStatus.Finished;
            
            using (var cmd = _serverManager.DatabaseManager.CreateDatabaseCommand())
            {
                cmd.SetCommand("UPDATE races SET Status = @Status WHERE Id = @Id");
                cmd.AddParameter("@Status", _activeRace.Status.ToString());
                cmd.AddParameter("@Id", _activeRace.Id);
                cmd.ExecuteQuery();
            }

            // Final flush
            FlushAndBroadcastAsync().Wait();
            
            _activeRace = null;
            _activeParticipants.Clear();
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
            if (_activeRace == null || !_isDirty) return;

            _isDirty = false;

            // 1. Flush to DB
            var participants = _activeParticipants.Values.ToList();
            foreach (var p in participants)
            {
                using (var cmd = _serverManager.DatabaseManager.CreateDatabaseCommand())
                {
                    // Upsert
                    cmd.SetCommand(@"
                        INSERT INTO race_participants (RaceId, UserIdentifier, TotalWagered, Username) 
                        VALUES (@RaceId, @UserId, @Wagered, @Username)
                        ON DUPLICATE KEY UPDATE TotalWagered = @Wagered, Username = @Username");
                    
                    cmd.AddParameter("@RaceId", p.RaceId);
                    cmd.AddParameter("@UserId", p.UserIdentifier);
                    cmd.AddParameter("@Wagered", p.TotalWagered);
                    cmd.AddParameter("@Username", p.Username);
                    cmd.ExecuteQuery();
                }
            }

            // 2. Update Discord Message
            if (_activeRace.ChannelId == 0 || _activeRace.MessageId == 0) return;

            try
            {
                var client = _serverManager.DiscordBotHost?.Client;
                if (client == null) return;

                var channel = await client.GetChannelAsync(_activeRace.ChannelId);
                var message = await channel.GetMessageAsync(_activeRace.MessageId);

                var top = GetTopParticipants(10);
                var sb = new System.Text.StringBuilder();
                int rank = 1;
                foreach (var p in top)
                {
                    string medal = rank switch { 1 => "🥇", 2 => "🥈", 3 => "🥉", _ => $"#{rank}" };
                    sb.AppendLine($"{medal} **{p.Username}** - {Server.Client.Utils.GpFormatter.Format(p.TotalWagered)}");
                    rank++;
                }

                if (sb.Length == 0) sb.Append("No wagers yet.");

                var prizes = _activeRace.GetPrizes();
                var prizeDesc = string.Join("\n", prizes.Select(x => $"Rank {x.Rank}: {x.Prize}"));

                var embed = new DSharpPlus.Entities.DiscordEmbedBuilder()
                    .WithTitle("🏆 Race Leaderboard 🏆")
                    .WithDescription(sb.ToString())
                    .WithColor(DSharpPlus.Entities.DiscordColor.Gold)
                    .AddField("Prizes", string.IsNullOrEmpty(prizeDesc) ? "None" : prizeDesc)
                    .WithFooter($"Ends at {_activeRace.EndTime:g}");

                await message.ModifyAsync(embed: embed.Build());
            }
            catch (DSharpPlus.Exceptions.NotFoundException)
            {
                // Message was deleted; try to repost it
                try
                {
                    var client = _serverManager.DiscordBotHost?.Client;
                    if (client != null)
                    {
                        var channel = await client.GetChannelAsync(_activeRace.ChannelId);
                        
                        var top = GetTopParticipants(10);
                        var sb = new System.Text.StringBuilder();
                        int rank = 1;
                        foreach (var p in top)
                        {
                            string medal = rank switch { 1 => "🥇", 2 => "🥈", 3 => "🥉", _ => $"#{rank}" };
                            sb.AppendLine($"{medal} **{p.Username}** - {Server.Client.Utils.GpFormatter.Format(p.TotalWagered)}");
                            rank++;
                        }
                        if (sb.Length == 0) sb.Append("No wagers yet.");

                        var prizes = _activeRace.GetPrizes();
                        var prizeDesc = string.Join("\n", prizes.Select(x => $"Rank {x.Rank}: {x.Prize}"));

                        var embed = new DSharpPlus.Entities.DiscordEmbedBuilder()
                            .WithTitle("🏆 Race Leaderboard 🏆")
                            .WithDescription(sb.ToString())
                            .WithColor(DSharpPlus.Entities.DiscordColor.Gold)
                            .AddField("Prizes", string.IsNullOrEmpty(prizeDesc) ? "None" : prizeDesc)
                            .WithFooter($"Ends at {_activeRace.EndTime:g}");

                        var newMsg = await channel.SendMessageAsync(embed.Build());
                        SetMessageId(newMsg.Id);
                    }
                }
                catch { /* Ignore if we can't repost */ }
            }
            catch (Exception ex)
            {
                _serverManager.LoggerManager.LogError($"Failed to update race leaderboard: {ex}");
            }
        }
        
        public List<RaceParticipant> GetTopParticipants(int count)
        {
            return _activeParticipants.Values
                .OrderByDescending(p => p.TotalWagered)
                .Take(count)
                .ToList();
        }

        public void SetMessageId(ulong messageId)
        {
            if (_activeRace == null) return;
            _activeRace.MessageId = messageId;
            
            using (var cmd = _serverManager.DatabaseManager.CreateDatabaseCommand())
            {
                cmd.SetCommand("UPDATE races SET MessageId = @MsgId WHERE Id = @Id");
                cmd.AddParameter("@MsgId", messageId);
                cmd.AddParameter("@Id", _activeRace.Id);
                cmd.ExecuteQuery();
            }
        }
    }
}
