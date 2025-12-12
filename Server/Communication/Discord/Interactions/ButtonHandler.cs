using System;
using System.Threading.Tasks;
using Discord.WebSocket;

namespace Server.Communication.Discord.Interactions
{
    public class ButtonHandler
    {
        public static async Task HandleButtons(DiscordSocketClient client, SocketMessageComponent component)
        {
            var id = component.Data.CustomId;

            // Transaction buttons
            if (id.StartsWith("tx_", StringComparison.OrdinalIgnoreCase))
            {
                await TransactionButtonHandler.Handle(client, component);
                return;
            }

            // Balance buttons
            if (id.StartsWith("bal_", StringComparison.OrdinalIgnoreCase))
            {
                await BalanceButtonHandler.Handle(client, component);
                return;
            }

            // Stake buttons
            if (id.StartsWith("stake_", StringComparison.OrdinalIgnoreCase))
            {
                await StakeButtonHandler.Handle(client, component);
                return;
            }

            // Coinflip buttons
            if (id.StartsWith("cf_", StringComparison.OrdinalIgnoreCase))
            {
                await CoinflipButtonHandler.Handle(client, component);
                return;
            }

            // Blackjack buttons
            if (id.StartsWith("bj_", StringComparison.OrdinalIgnoreCase))
            {
                await BlackjackButtonHandler.Handle(client, component);
                return;
            }

            // Race interactions
            if (id.StartsWith("race_", StringComparison.OrdinalIgnoreCase))
            {
                await RaceInteractionHandler.HandleComponent(client, component);
                return;
            }

            // Other button namespaces (game_, etc.) can be routed here later
        }
    }
}
