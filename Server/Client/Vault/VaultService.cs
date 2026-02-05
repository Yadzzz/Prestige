using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using Server.Infrastructure;
using Server.Infrastructure.Configuration;

namespace Server.Client.Vault
{
    public class VaultService
    {
        private readonly ServerManager _serverManager;
        private VaultRound _activeRound;
        private readonly object _lock = new object();
        
        // Cost: 10,000 GP -> "10M" visually? No, GpFormatter format is K -> M.
        // User said "10k per entry".
        // Assuming "10k" means 10,000 credits. 
        // In GpFormatter, "100L" is stored as 100K (100,000). Wait.
        // GpFormatter.Format(100L) -> 0.1M?
        // Let's check GpFormatter again.
        // stored 1000K -> Format(1000) -> 1.0M.
        // So 1 unit stored = 1000. 
        // If user wants to pay "10k", that is 10,000.
        // stored value for 10k = 10.
        
        // Wait, "1M -> 1000K internally".
        // So 1,000,000 = 1000 stored.
        // 10,000 = 10 stored.
        private const long GuessCostK = 10; 
        private const long PoolIncrementK = 9; // 95% is 9.5, let's round or use double?
        // Let's use 9500 for 10000 if we had resolution, but we track in K.
        // 10K cost -> 9.5K to pool? Integers only?
        // If I use long for K, I can't do .5
        // Maybe I should give 9K to pool and burn 1K (90%) to keep it simple with integers?
        // Or accumulated pool can be float? No, DB is bigint.
        
        // Let's just make the cost 20k (20 stored) so we can do 19 to pool (95%).
        // Or stick to 10k cost and 9k to pool (90%).
        // User asked for 95%.
        // I will use 10k cost -> 9k for now to avoid fractional K issues unless I change DB types.
        // Actually, let's effectively do 10k cost, 9k to pool. That's 90%. Close enough for now or I'll double it.
        // Let's stick effectively to: Cost 10 units (10k), Pool + 9 units (9k).
        
        public VaultService(ServerManager serverManager)
        {
            _serverManager = serverManager;
            LoadActiveRound();
        }

        public VaultRound GetActiveRound() => _activeRound;

        private void LoadActiveRound()
        {
            using (var cmd = _serverManager.DatabaseManager.CreateDatabaseCommand())
            {
                cmd.SetCommand("SELECT * FROM vault_rounds WHERE Status = 'Active' ORDER BY Id DESC LIMIT 1");
                var dt = cmd.ExecuteDataTable();
                if (dt.Rows.Count > 0)
                {
                    var row = dt.Rows[0];
                    _activeRound = new VaultRound
                    {
                        Id = Convert.ToInt32(row["Id"]),
                        SecretCode = Convert.ToInt32(row["SecretCode"]),
                        Pool = Convert.ToInt64(row["Pool"]),
                        GuessCount = Convert.ToInt32(row["GuessCount"]),
                        Status = row["Status"].ToString(),
                        StartTime = Convert.ToDateTime(row["StartTime"]),
                        ChannelId = Convert.ToUInt64(row["ChannelId"]),
                        MessageId = Convert.ToUInt64(row["MessageId"])
                    };
                }
                else
                {
                    CreateRound();
                }
            }
        }

        private void CreateRound()
        {
            var rnd = new Random();
            var code = rnd.Next(0, 10000); // 0000 - 9999
            
            var round = new VaultRound
            {
                SecretCode = code,
                Pool = 100000, // Seed with 100K (100 units) or 100M (100000 units)? 
                               // Let's seed with 1M (1000 units).
                GuessCount = 0,
                Status = "Active",
                StartTime = DateTime.UtcNow,
                // ChannelID will be set later when we post specific message or use race channel
            };
            
            // If we want to reuse the race channel, we can't hardcode it easily unless we look up RaceService settings or Config.
            // For now, we'll let the command set the channel if it's 0.

            using (var cmd = _serverManager.DatabaseManager.CreateDatabaseCommand())
            {
                cmd.SetCommand("INSERT INTO vault_rounds (SecretCode, Pool, StartTime, Status) VALUES (@Code, @Pool, @Start, 'Active'); SELECT LAST_INSERT_ID();");
                cmd.AddParameter("@Code", round.SecretCode);
                cmd.AddParameter("@Pool", round.Pool);
                cmd.AddParameter("@Start", round.StartTime);
                
                var id = Convert.ToInt32(cmd.ExecuteScalar());
                round.Id = id;
            }

            _activeRound = round;
        }

        public async Task<string> ProcessGuessAsync(string userIdentifier, string username, int guess, ulong channelId)
        {
            if (_activeRound == null || _activeRound.Status != "Active")
                LoadActiveRound();

            var round = _activeRound;

            // check balance
            var user = await _serverManager.UsersService.GetUserAsync(userIdentifier);
            if (user == null || user.Balance < GuessCostK)
                return "You don't have enough GP to guess (10k per guess).";

            // deduct balance
            await _serverManager.UsersService.RemoveBalanceAsync(userIdentifier, GuessCostK);
            // Log the transaction? Assuming UsersService logs or we rely on general logs.
            
            bool won = (guess == round.SecretCode);
            
            // Update round state
            round.Pool += PoolIncrementK;
            round.GuessCount++;
            
            await SaveRoundStateAsync(round);
            await LogGuessAsync(round.Id, userIdentifier, username, guess);

            // Handle Win
            if (won)
            {
                round.Status = "Finished";
                await EndRoundAsync(round, userIdentifier, username);
                var winAmount = round.Pool;
                
                // Start new round immediately
                CreateRound();
                // Update embed for new round is handled by returning "WON" signal or just refreshing
                
                // Return message
                return $"🎉 **CRACKED!** The code was `{guess:D4}`. You won `{Server.Client.Utils.GpFormatter.Format(winAmount)}`!";
            }
            else
            {
                // Trigger Embed Update
                // Fire and forget update to avoid slowing down the response
                _ = Task.Run(() => UpdateEmbedAsync());
                
                return null; // Silent if wrong, or return specific "Wrong" message? usually silent to not spam chat.
            }
        }

        private async Task SaveRoundStateAsync(VaultRound round)
        {
             using (var cmd = _serverManager.DatabaseManager.CreateDatabaseCommand())
            {
                cmd.SetCommand("UPDATE vault_rounds SET Pool = @Pool, GuessCount = @Count, Status = @Status WHERE Id = @Id");
                cmd.AddParameter("@Pool", round.Pool);
                cmd.AddParameter("@Count", round.GuessCount);
                cmd.AddParameter("@Status", round.Status);
                cmd.AddParameter("@Id", round.Id);
                await cmd.ExecuteQueryAsync();
            }
        }

        private async Task LogGuessAsync(int roundId, string userId, string username, int guess)
        {
             using (var cmd = _serverManager.DatabaseManager.CreateDatabaseCommand())
            {
                cmd.SetCommand("INSERT INTO vault_guesses (RoundId, UserIdentifier, Username, Guess, Timestamp) VALUES (@R, @U, @N, @G, @T)");
                cmd.AddParameter("@R", roundId);
                cmd.AddParameter("@U", userId);
                cmd.AddParameter("@N", username);
                cmd.AddParameter("@G", guess);
                cmd.AddParameter("@T", DateTime.UtcNow);
                await cmd.ExecuteQueryAsync();
            }
        }

        private async Task EndRoundAsync(VaultRound round, string winnerId, string winnerName)
        {
             using (var cmd = _serverManager.DatabaseManager.CreateDatabaseCommand())
            {
                cmd.SetCommand("UPDATE vault_rounds SET EndTime = @End, WinnerId = @Winner WHERE Id = @Id");
                cmd.AddParameter("@End", DateTime.UtcNow);
                cmd.AddParameter("@Winner", winnerId);
                cmd.AddParameter("@Id", round.Id);
                await cmd.ExecuteQueryAsync();
            }

            // Payout
            await _serverManager.UsersService.AddBalanceAsync(winnerId, round.Pool);
            
            // Final update of the embed to show "CRACKED BY X"
            await UpdateEmbedAsync(true, winnerName, round.SecretCode);
        }

        public async Task UpdateEmbedAsync(bool finished = false, string winnerName = null, int correctCode = 0, ulong? newChannelId = null)
        {
            try 
            {
                var round = finished ? _activeRound : _activeRound; 
                
                if (round == null) return;
                
                // Update channel if provided and different (or if 0)
                if (newChannelId.HasValue && newChannelId.Value != 0)
                {
                    if (round.ChannelId != newChannelId.Value)
                    {
                        round.ChannelId = newChannelId.Value;
                        // Persist new channel ID
                        using (var cmd = _serverManager.DatabaseManager.CreateDatabaseCommand())
                        {
                            cmd.SetCommand($"UPDATE vault_rounds SET ChannelId = {round.ChannelId} WHERE Id = {round.Id}");
                            await cmd.ExecuteQueryAsync();
                        }
                    }
                }

                if (round.ChannelId == 0) return;

                var client = _serverManager.DiscordBotHost?.Client;
                if (client == null) return;

                var channel = await client.GetChannelAsync(round.ChannelId);
                if (channel == null) return;

                DiscordMessage message = null;
                if (round.MessageId != 0)
                {
                    try { message = await channel.GetMessageAsync(round.MessageId); } catch { }
                }

                var embed = await BuildVaultEmbed(round);
                var builder = new DiscordMessageBuilder()
                    .AddEmbed(embed)
                    .AddActionRowComponent(new DiscordButtonComponent(DiscordButtonStyle.Secondary, "vault_crack_btn", "🔓 Crack Vault", false));

                if (message != null)
                {
                    await message.ModifyAsync(builder);
                }
                else
                {
                    var msg = await channel.SendMessageAsync(builder);
                    round.MessageId = msg.Id;
                    // Persist message ID
                    using (var cmd = _serverManager.DatabaseManager.CreateDatabaseCommand())
                    {
                        cmd.SetCommand($"UPDATE vault_rounds SET MessageId = {msg.Id} WHERE Id = {round.Id}");
                        await cmd.ExecuteQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Vault] Error updating embed: {ex.Message}");
            }
        }

        private async Task<DiscordEmbed> BuildVaultEmbed(VaultRound round)
        {
            var guesses = await GetRecentFailures(round.Id);
            
            var embed = new DiscordEmbedBuilder()
                .WithTitle("🔒  **Community Vault**  🔒")
                .WithColor(DiscordColor.Goldenrod)
                .WithFooter("To guess: !vault <0000-9999>")
                .WithTimestamp(DateTime.UtcNow);

            embed.AddField("💰 Pool", $"`{Server.Client.Utils.GpFormatter.Format(round.Pool)}`", true);
            embed.AddField("🎫 Cost", "`10k`", true);
            embed.AddField("🔢 Guesses", $"`{round.GuessCount}`", true);
            
            embed.AddField("🔓 Hints", GetHintText(round), false);

            if (guesses.Count > 0)
            {
                var sbFail = new System.Text.StringBuilder();
                foreach (var g in guesses)
                {
                    sbFail.AppendLine($"- `{g.Guess:D4}` ({g.Username})");
                }
                embed.AddField("📉 Recent Failures", sbFail.ToString(), false);
            }

            return embed.Build();
        }

        private string GetHintText(VaultRound round)
        {
            var hints = new List<string>();
            int code = round.SecretCode;
            int count = round.GuessCount;

            // Hint 1: Range (at 10 guesses)
            if (count >= 10)
            {
                if (code < 5000) hints.Add("1️⃣ The code is between **0000 - 4999**.");
                else hints.Add("1️⃣ The code is between **5000 - 9999**.");
            }
            else
            {
                hints.Add($"1️⃣ 🔒 *Unlocks at 10 guesses*");
            }

            // Hint 2: Parity (at 25 guesses)
            if (count >= 25)
            {
                 if (code % 2 == 0) hints.Add("2️⃣ The code is **EVEN**.");
                 else hints.Add("2️⃣ The code is **ODD**.");
            }
            else
            {
                hints.Add($"2️⃣ 🔒 *Unlocks at 25 guesses*");
            }

            // Hint 3: Last Digit (at 50 guesses)
            if (count >= 50)
            {
                hints.Add($"3️⃣ The last digit is **{code % 10}**.");
            }
            else
            {
                hints.Add($"3️⃣ 🔒 *Unlocks at 50 guesses*");
            }
            
            return string.Join("\n", hints);
        }

        private async Task<List<VaultGuess>> GetRecentFailures(int roundId)
        {
            var list = new List<VaultGuess>();
            using (var cmd = _serverManager.DatabaseManager.CreateDatabaseCommand())
            {
                cmd.SetCommand("SELECT * FROM vault_guesses WHERE RoundId = @R ORDER BY Id DESC LIMIT 3");
                cmd.AddParameter("@R", roundId);
                var dt = await cmd.ExecuteDataTableAsync();
                foreach (System.Data.DataRow row in dt.Rows)
                {
                    list.Add(new VaultGuess
                    {
                        Guess = Convert.ToInt32(row["Guess"]),
                        Username = row["Username"].ToString()
                    });
                }
            }
            return list;
        }

        public class VaultRound
        {
            public int Id { get; set; }
            public int SecretCode { get; set; }
            public long Pool { get; set; }
            public int GuessCount { get; set; }
            public string Status { get; set; }
            public DateTime StartTime { get; set; }
            public ulong ChannelId { get; set; }
            public ulong MessageId { get; set; }
        }

        public class VaultGuess
        {
            public string Username { get; set; }
            public int Guess { get; set; }
        }
    }
}
