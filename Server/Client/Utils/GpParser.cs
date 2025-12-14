using System;
using System.Globalization;

namespace Server.Client.Utils
{
    public static class GpParser
    {
        // Parses user inputs into K (thousands) units.
        // Semantics:
        //   - Plain numbers are treated as millions:  "1" => 1M => 1000K, "1000" => 1000M => 1,000,000K
        //   - Trailing 'm' is also millions:         "1m" => 1M => 1000K, "1000m" => 1000M => 1,000,000K
        //   - Trailing 'b' is billions:              "1b" => 1B => 1,000,000K, "1.5b" => 1.5B => 1,500,000K
        // Returns true on success; amountK is the value in thousands.
        public static bool TryParseAmountInK(string input, out long amountK)
        {
            return TryParseAmountInK(input, out amountK, out _);
        }

        public static bool TryParseAmountInK(string input, out long amountK, out string error)
        {
            amountK = 0;
            error = null;

            if (string.IsNullOrWhiteSpace(input))
            {
                error = "Amount cannot be empty.";
                return false;
            }

            if (input.Contains(","))
            {
                error = "Commas are not allowed. Please use dots (.) for decimals.";
                return false;
            }

            input = input.Trim().ToLowerInvariant();

            decimal multiplier;

            if (input.EndsWith("b"))
            {
                multiplier = 1_000_000m; // 1B = 1,000,000K
                input = input[..^1];
            }
            else if (input.EndsWith("m"))
            {
                multiplier = 1_000m; // 1M = 1,000K
                input = input[..^1];
            }
            else
            {
                // No suffix -> interpret as millions for users
                multiplier = 1_000m; // 1M = 1,000K
            }

            if (!decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out var baseValue))
            {
                error = "Invalid number format.";
                return false;
            }

            if (baseValue <= 0)
            {
                error = "Amount must be positive.";
                return false;
            }

            var resultK = baseValue * multiplier;

            amountK = (long)Math.Round(resultK, MidpointRounding.AwayFromZero);
            
            if (amountK <= 0)
            {
                error = "Amount is too small.";
                return false;
            }

            return true;
        }
    }
}
