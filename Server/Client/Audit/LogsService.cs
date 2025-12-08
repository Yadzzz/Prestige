using System;
using Server.Infrastructure.Database;

namespace Server.Client.Audit
{
    public class LogsService
    {
        private readonly DatabaseManager _databaseManager;

        public LogsService(DatabaseManager databaseManager)
        {
            _databaseManager = databaseManager;
        }

        public async Task LogAsync(string source, string level, string? userIdentifier, string? action, string? message, string? exception, string? metadataJson = null)
        {
            try
            {
                using (var command = new DatabaseCommand())
                {
                    command.SetCommand("INSERT INTO logs (created_at, source, level, user_identifier, action, message, exception, metadata_json) VALUES (@created_at, @source, @level, @user_identifier, @action, @message, @exception, @metadata_json)");
                    command.AddParameter("created_at", DateTime.UtcNow);
                    command.AddParameter("source", source ?? string.Empty);
                    command.AddParameter("level", level ?? string.Empty);
                    command.AddParameter("user_identifier", (object?)userIdentifier ?? DBNull.Value);
                    command.AddParameter("action", (object?)action ?? DBNull.Value);
                    command.AddParameter("message", (object?)message ?? DBNull.Value);
                    command.AddParameter("exception", (object?)exception ?? DBNull.Value);
                    command.AddParameter("metadata_json", (object?)metadataJson ?? DBNull.Value);

                    await command.ExecuteQueryAsync();
                }
            }
            catch
            {
                // Intentionally swallow logging errors to avoid recursive failures
            }
        }

        public void Log(string source, string level, string? userIdentifier, string? action, string? message, string? exception, string? metadataJson = null)
        {
            LogAsync(source, level, userIdentifier, action, message, exception, metadataJson).GetAwaiter().GetResult();
        }
    }
}
