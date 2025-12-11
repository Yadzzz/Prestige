using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Server.Communication.Discord.Commands
{
    internal static class RateLimiter
    {
        private static readonly ConcurrentDictionary<string, DateTime> LastUsed = new();
        private static DateTime _lastCleanup = DateTime.UtcNow;
        private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);
        private static int _isCleaning = 0;

        public static bool IsRateLimited(ulong userId)
        {
            return IsRateLimited(userId, "default", TimeSpan.FromSeconds(1));
        }

        public static bool IsRateLimited(ulong userId, string key, TimeSpan interval)
        {
            var now = DateTime.UtcNow;
            
            // Periodic cleanup (thread-safe implementation)
            if (now - _lastCleanup > CleanupInterval)
            {
                // Ensure only one thread runs cleanup
                if (Interlocked.CompareExchange(ref _isCleaning, 1, 0) == 0)
                {
                    try
                    {
                        Cleanup();
                        _lastCleanup = now;
                    }
                    finally
                    {
                        _isCleaning = 0;
                    }
                }
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
