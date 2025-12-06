using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Server.Infrastructure.Connection;
using Server.Infrastructure.Database;
using Server.Infrastructure.Logger;
using Server.Infrastructure.Discord;
using Server.Client.Users;
using Server.Client.Transactions;
using Server.Client.Stakes;
using Server.Client.Audit;
using Server.Client.Coinflips;
using Server.Client.LiveFeed;

namespace Server.Infrastructure
{
    public class ServerManager
    {
        public ConnectionManager ConnectionManager { get; set; }
        public DatabaseManager DatabaseManager { get; set; }
        public LoggerManager LoggerManager { get; set; }
        public DiscordBotHost DiscordBotHost { get; set; }
        public UsersService UsersService { get; set; }
        public TransactionsService TransactionsService { get; set; }
        public StakesService StakesService { get; set; }
        public CoinflipsService CoinflipsService { get; set; }
        public LogsService LogsService { get; set; }
        public LiveFeedService LiveFeedService { get; set; }

        public ServerManager()
        {
            this.ConnectionManager = new ConnectionManager();
            this.DatabaseManager = new DatabaseManager();
            this.LoggerManager = new LoggerManager(new LoggerConfiguration
            {
                ConsoleLoggerEnabled = true,
                FileLoggerEnabled = true,
                DatabaseLoggerEnabled = false,
            });
            this.UsersService = new UsersService(this.DatabaseManager);
            this.TransactionsService = new TransactionsService(this.DatabaseManager, this.UsersService);
            this.StakesService = new StakesService(this.DatabaseManager);
            this.CoinflipsService = new CoinflipsService(this.DatabaseManager);
            this.LogsService = new LogsService(this.DatabaseManager);

            var discordOptions = new DiscordOptions
            {
                Token = DiscordIds.BotToken,
                GuildId = Discord.DiscordIds.GuildId,
                Intents = DSharpPlus.DiscordIntents.All
            };

            this.DiscordBotHost = new DiscordBotHost(discordOptions, this.UsersService);
            this.LiveFeedService = new LiveFeedService(this.DiscordBotHost);

            Console.WriteLine("ServerManager Initialized ->");
        }
    }
}
