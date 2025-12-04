using System;
using System.Collections.Generic;
using System.Text;

namespace Server.Client.Utils
{
    public static class GpFormatter
    {
        public static string Format(long stored)
        {
            // stored → thousands → millions
            decimal millions = stored / 1000.0m;

            return millions.ToString("0.###") + "M";
        }
    }
}
