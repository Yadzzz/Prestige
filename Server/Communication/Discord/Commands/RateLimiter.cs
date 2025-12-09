using System;
using System.Collections.Concurrent;

namespace Server.Communication.Discord.Commands
{
    internal static class RateLimiter
    {
        private static readonly ConcurrentDictionary<string, DateTime> LastUsed = new();
        private static DateTime _lastCleanup = DateTime.UtcNow;
        private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

        public static bool IsRateLimited(ulong userId, string key, TimeSpan interval)
        {
            var now = DateTime.UtcNow;
            
            // Periodic cleanup (simple implementation)
            if (now - _lastCleanup > CleanupInterval)
            {
                Cleanup();
                _lastCleanup = now;
            }

            var compositeKey = $"{userId}:{key}";

            if (LastUsed.TryGetValue(compositeKey, out var last) && (now - last) < interval)
            {
                return true;
            }

            LastUsed[compositeKey] = now;
            return false;
        }

        private static void Cleanup()
        {
            // Remove entries older than 1 minute (or a safe threshold)
            var threshold = DateTime.UtcNow.AddMinutes(-1);
            foreach (var kvp in LastUsed)
            {
                if (kvp.Value < threshold)
                {
                    LastUsed.TryRemove(kvp.Key, out _);
                }
            }
        }
    }
}
