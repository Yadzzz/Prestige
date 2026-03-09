using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace Server.Communication.Discord.Interactions
{
    public class ButtonHandler
    {
        public static async Task HandleButtons(DiscordClient client, ComponentInteractionCreatedEventArgs e)
        {
            // Transaction buttons
            if (e.Id.StartsWith("tx_", StringComparison.OrdinalIgnoreCase))
            {
                await TransactionButtonHandler.Handle(client, e);
                return;
            }

            // Balance buttons
            if (e.Id.StartsWith("bal_", StringComparison.OrdinalIgnoreCase))
            {
                await BalanceButtonHandler.Handle(client, e);
                return;
            }

            // Stake buttons
            if (e.Id.StartsWith("stake_", StringComparison.OrdinalIgnoreCase))
            {
                await StakeButtonHandler.Handle(client, e);
                return;
            }

            // Coinflip buttons
            if (e.Id.StartsWith("cf_", StringComparison.OrdinalIgnoreCase))
            {
                await CoinflipButtonHandler.Handle(client, e);
                return;
            }

            // Blackjack buttons
            if (e.Id.StartsWith("bj_", StringComparison.OrdinalIgnoreCase))
            {
                await BlackjackButtonHandler.Handle(client, e);
                return;
            }

            // Higher/Lower buttons
            if (e.Id.StartsWith("hl_", StringComparison.OrdinalIgnoreCase))
            {
                await HigherLowerButtonHandler.Handle(client, e);
                return;
            }
            // Games selection buttons
            if (e.Id.StartsWith("games_", StringComparison.OrdinalIgnoreCase))
            {
                await GamesInteractionHandler.HandleComponent(client, e);
                return;
            }
            // Chest buttons
            if (e.Id.StartsWith("chest_", StringComparison.OrdinalIgnoreCase))
            {
                await ChestButtonHandler.Handle(client, e);
                return;
            }

            // Race interactions
            if (e.Id.StartsWith("race_", StringComparison.OrdinalIgnoreCase))
            {
                await RaceInteractionHandler.HandleComponent(client, e);
                return;
            }

            // Referral interactions
            if (e.Id.StartsWith("ref_", StringComparison.OrdinalIgnoreCase))
            {
                await ReferralInteractionHandler.HandleComponent(client, e);
                return;
            }

            // Mines buttons
            if (e.Id.StartsWith("mines_", StringComparison.OrdinalIgnoreCase))
            {
                await MinesButtonHandler.Handle(client, e);
                return;
            }

            // Cracker buttons
            if (e.Id.StartsWith("cracker_", StringComparison.OrdinalIgnoreCase))
            {
                await CrackerButtonHandler.Handle(client, e);
                return;
            }

            // Vault buttons
            if (e.Id.StartsWith("vault_", StringComparison.OrdinalIgnoreCase))
            {
                await VaultInteractionHandler.HandleButton(client, e);
                return;
            }

            // Other button namespaces (game_, etc.) can be routed here later
        }
    }
}
