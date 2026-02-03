using Server.Infrastructure.Configuration;

namespace Server
{
    public class ServerConfiguration
    {
        public static string ServerName => ConfigService.Current.ServerName;
        public static string ShortName => ConfigService.Current.ShortName;

        //Database
        public static string ConnectionString => ConfigService.Current.ConnectionString;

        //Network
        public const int MaxSocketConnection = 1000;
    }
}
