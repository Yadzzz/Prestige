using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public enum AppEnvironment
    {
        Test,
        PrestigeBets,
        OceanStakes
    }

    public class ServerConfiguration
    {
        // ---------------------------------------------------------
        // SWITCH ENVIRONMENT HERE
        // ---------------------------------------------------------
        public static AppEnvironment Environment = AppEnvironment.Test;
        // ---------------------------------------------------------

        //Database
        public static string ConnectionString => Environment switch
        {
            AppEnvironment.Test => "Server=127.0.0.1;Database=prestige_bets;Uid=root;Pwd=Yadz1042!;",
            AppEnvironment.PrestigeBets => "Server=127.0.0.1;Database=prestige_bets;Uid=root;Pwd=Yadz1042!;",
            AppEnvironment.OceanStakes => "Server=127.0.0.1;Database=ocean_stakes;Uid=root;Pwd=Yadz1042!;",
            _ => throw new NotImplementedException()
        };

        //Network
        public const int MaxSocketConnection = 1000;
    }
}
