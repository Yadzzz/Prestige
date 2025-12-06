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
                // Always show two decimal places and use '.' as decimal separator
                return millions.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "M";
            }
    }
}
