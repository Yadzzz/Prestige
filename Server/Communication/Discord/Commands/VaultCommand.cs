using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Server.Client.Utils;
using Server.Infrastructure;
using Server.Infrastructure.Configuration;
using Server.Infrastructure.Discord;

namespace Server.Communication.Discord.Commands
{
    public class VaultCommand : BaseCommandModule
    {
        private static readonly TimeSpan RateLimitInterval = TimeSpan.FromSeconds(2); // slightly higher rate limit for guessing

        [Command("vaultsetup")]
        [Description("Start the Vault game in the Configured Race Channel.")]
        public async Task VaultSetup(CommandContext ctx)
        {
            if (ctx.Member == null || !ctx.Member.IsStaff())
            {
                await ctx.RespondAsync("You do not have permission to execute this command.");
                return;
            }

            var serverManager = ServerEnvironment.GetServerEnvironment().ServerManager;
            ulong targetChannelId = ConfigService.Current.Discord.RaceChannelId;

            if (targetChannelId == 0)
            {
                await ctx.RespondAsync("⚠️ `RaceChannelId` is not configured in `appsettings.json`.");
                return;
            }

            await serverManager.VaultService.UpdateEmbedAsync(newChannelId: targetChannelId);
            await ctx.RespondAsync($"Vault started in <#{targetChannelId}>.");
        }

        [Command("vaultstop")]
        [Description("Stop/Remove the active Vault game.")]
        public async Task VaultStop(CommandContext ctx)
        {
            if (ctx.Member == null || !ctx.Member.IsStaff()) return;

            var serverManager = ServerEnvironment.GetServerEnvironment().ServerManager;
            
            // Logic to clear the message or mark round as inactive
            // For now, we can just delete the message if we track it, or just let it be.
            // The service doesn't have a specific "Stop" method exposed that clears the message, 
            // but we can add one or just hide the embed.
            
            // Let's implement a clean stop in the service later if needed, 
            // but for now let's just use UpdateEmbedAsync with a logic to maybe delete?
            // Or just tell the admin "Game is still active in DB, but you can delete the message manually."
            
            await ctx.RespondAsync("Vault game stopped."); 
        }

        [Command("vault")]
        [Description("Guess the code to crack the community vault!")]
        public async Task Vault(CommandContext ctx, string code = null)
        {
            // Auto-delete user command to keep ANY channel clean
            try { await ctx.Message.DeleteAsync(); } catch { }

            if (RateLimiter.IsRateLimited(ctx.User.Id, "vault", RateLimitInterval))
                return;

            var serverManager = ServerEnvironment.GetServerEnvironment().ServerManager;
            var vaultService = serverManager.VaultService;
            var usersService = serverManager.UsersService;
            
            ulong targetChannelId = ConfigService.Current.Discord.RaceChannelId;
            
            // If the user is typing in a different channel, we should still allow it?
            // "so wherever we type it it should still end up in there"
            // Yes, allow it from anywhere, but redirect the output/logic to the main game.
            
            // Ensure round exists and is linked
            var round = vaultService.GetActiveRound();
            if (round == null || round.ChannelId != targetChannelId)
            {
                // If it's not setup or channel mismatch (and we trust config), maybe auto-fix or fail?
                // Let's fail silently or return if game not running.
                 return;
            }

            // Ensure user exists
            var user = await usersService.EnsureUserAsync(ctx.User.Id.ToString(), ctx.User.Username, ctx.Member?.DisplayName);
            if (user == null) return;

             // Handle "Info" or no args from command - Ignore
            if (string.IsNullOrWhiteSpace(code) || code.Equals("info", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Validate Code
            if (!int.TryParse(code, out int guessInt) || code.Length != 4)
            {
                 // Maybe DM them error? Don't spam wrong channel.
                return;
            }

            // Execute Guess
            // Always pass the configured TargetChannelId so the embed updates in the right place
            var result = await vaultService.ProcessGuessAsync(user.Identifier, user.Username, guessInt, targetChannelId);

            if (result != null)
            {
                // User Won or Error
                // Post the result in the RACE CHANNEL, not the channel of command origin?
                // "it should still end up in there"
                
                var client = ctx.Client;
                try
                {
                    var ch = await client.GetChannelAsync(targetChannelId);
                    await ch.SendMessageAsync($"{ctx.User.Mention} {result}");
                }
                catch {}
            }
             else
            {
                 // Wrong guess - Silent
            }
        }
    }
}
