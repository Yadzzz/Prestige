using System;
using System.Collections.Generic;
using System.Text;

namespace Server.Client.Utils
{
    public static class GpFormatter
    {
            // Minimum allowed bet/stake for games (0.01M -> 10K internally)
            public const long MinimumBetAmountK = 10L;

            // stored is in thousands (K); format as millions (M)
            public static string Format(long storedK)
            {
                decimal millions = storedK / 1000.0m;
                // Truncate to 2 decimal places so we don't show more than the user actually has
                decimal truncated = Math.Floor(millions * 100) / 100;
                return truncated.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "M";
            }
    }
}
