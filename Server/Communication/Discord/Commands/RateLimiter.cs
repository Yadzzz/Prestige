using System;
using System.Collections.Concurrent;

namespace Server.Communication.Discord.Commands
{
    internal static class RateLimiter
    {
        private static readonly ConcurrentDictionary<string, DateTime> LastUsed = new();

        public static bool IsRateLimited(ulong userId, string key, TimeSpan interval)
        {
            var now = DateTime.UtcNow;
            var compositeKey = $"{userId}:{key}";

            if (LastUsed.TryGetValue(compositeKey, out var last) && (now - last) < interval)
            {
                return true;
            }

            LastUsed[compositeKey] = now;
            return false;
        }
    }
}
