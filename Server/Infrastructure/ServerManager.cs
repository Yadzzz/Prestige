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
using Server.Client.Races;
using Server.Client.AI;
using Server.Client.Blackjack;
using Server.Client.Mines;
using Server.Client.HigherLower;
using Server.Client.Payments;
using Server.Client.Referrals;
using Server.Client.Cracker;
using Server.Infrastructure.Configuration;

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
        public BalanceAdjustmentsService BalanceAdjustmentsService { get; set; }
        public StakesService StakesService { get; set; }
        public CoinflipsService CoinflipsService { get; set; }
        public BlackjackService BlackjackService { get; set; }
        public MinesService MinesService { get; set; }
        public HigherLowerService HigherLowerService { get; set; }
        public LogsService LogsService { get; set; }
        public LiveFeedService LiveFeedService { get; set; }
        public RaceService RaceService { get; set; }
        public Server.Client.Chest.ChestService ChestService { get; set; }
        public AiCommandResolverService AiCommandResolverService { get; set; }
        public NowPaymentsService NowPaymentsService { get; set; }
        public PaymentOrdersService PaymentOrdersService { get; set; }
        public ReferralService ReferralService { get; set; }
        public CrackerService CrackerService { get; set; }

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
            this.ChestService = new Server.Client.Chest.ChestService(this.DatabaseManager);
            this.TransactionsService = new TransactionsService(this.DatabaseManager, this.UsersService);
            this.BalanceAdjustmentsService = new BalanceAdjustmentsService(this.DatabaseManager, this.UsersService);
            this.StakesService = new StakesService(this.DatabaseManager);
            this.CoinflipsService = new CoinflipsService(this.DatabaseManager);
            this.BlackjackService = new BlackjackService(this.DatabaseManager);
            this.MinesService = new MinesService(this.DatabaseManager, this.UsersService);
            this.HigherLowerService = new HigherLowerService(this.DatabaseManager);
            this.LogsService = new LogsService(this.DatabaseManager);
            this.PaymentOrdersService = new PaymentOrdersService(this.DatabaseManager);
            this.ReferralService = new ReferralService(this.DatabaseManager, this.UsersService);
            this.CrackerService = new CrackerService(this.DatabaseManager);
            this.AiCommandResolverService = new AiCommandResolverService("https://lively-butterfly-20a1.yadmarzan.workers.dev/");
            
            var paymentsApiKey = ConfigService.Current.Payments?.NowPaymentsApiKey;
            if (!string.IsNullOrEmpty(paymentsApiKey))
            {
                this.NowPaymentsService = new NowPaymentsService(paymentsApiKey);
            }
            else
            {
                Console.WriteLine("Warning: NowPayments API Key not found in configuration.");
            }

            var discordOptions = new DiscordOptions
            {
                Token = DiscordIds.BotToken,
                GuildId = Discord.DiscordIds.GuildId,
                Intents = DSharpPlus.DiscordIntents.All,
                MinimumLogLevel = Microsoft.Extensions.Logging.LogLevel.Information,
                TokenType = DSharpPlus.TokenType.Bot
            };

            this.DiscordBotHost = new DiscordBotHost(discordOptions, this);
            this.LiveFeedService = new LiveFeedService(this.DiscordBotHost);
            this.RaceService = new RaceService(this);

            Console.WriteLine("ServerManager Initialized ->");
        }

        public async Task StopAsync()
        {
            if (this.RaceService != null)
            {
                await this.RaceService.StopAsync();
            }
        }
    }
}
