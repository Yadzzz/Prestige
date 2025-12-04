using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using Server.Client.Users;
using System;
using System.Collections.Generic;
using System.Text;

namespace Server.Communication.Discord.Commands
{
    public class TestCommand : BaseCommandModule
    {
        private static int _counter = 0;

        [Command("test")]
        public async Task Test(CommandContext ctx)
        {
            if (ctx.User.IsBot || (ctx.User.IsSystem.HasValue && ctx.User.IsSystem.Value))
            {
                await ctx.RespondAsync("Bots and system users cannot use this command.");
                return;
            }

            var user = await UsersFactory.EnsureUserAsync(ctx.User.Id.ToString(), ctx.User.Username, ctx.Member.DisplayName);
            if (user == null) return;

            var count = Interlocked.Increment(ref _counter);

            Console.WriteLine($"[TEST] Command used {count} times. Triggered by {user.DisplayName}, {user.Identifier}");

            await ctx.RespondAsync($"Test command works! 🚀 (Used {count} times since bot started). Triggered by {user.DisplayName}, {user.Identifier}");
        }
    }
}
