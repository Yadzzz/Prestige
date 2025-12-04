using System;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus.SlashCommands;
using Server.Client.Users;

namespace Server.Communication.Discord.Commands
{
    public class SlashTest : ApplicationCommandModule
    {
        private static int _counter = 0;

        [SlashCommand("test", "Simple test command")]
        public async Task Test(InteractionContext ctx)
        {
            if (ctx.User.IsBot || (ctx.User.IsSystem.HasValue && ctx.User.IsSystem.Value))
            {
                await ctx.CreateResponseAsync("Bots and system users cannot use this command.");
                return;
            }
            
            var user = await UsersFactory.EnsureUserAsync(ctx.User.Id.ToString(), ctx.User.Username, ctx.Member.DisplayName);
            if (user == null) return;

            var count = Interlocked.Increment(ref _counter);

            Console.WriteLine($"[TEST] Command used {count} times. Triggered by {user.DisplayName}, {user.Identifier}");

            await ctx.CreateResponseAsync($"Test command works! 🚀 (Used {count} times since bot started). Triggered by {user.DisplayName}, {user.Identifier}");
        }
    }
}
