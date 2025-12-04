using System;
using System.Collections.Generic;
using System.Text;

namespace Server.Client.Utils
{
    public static class GpFormatter
    {
            // stored is in thousands (K); format as millions (M)
            public static string Format(long storedK)
            {
                decimal millions = storedK / 1000.0m;
                return millions.ToString("0.###") + "M";
            }
    }
}
