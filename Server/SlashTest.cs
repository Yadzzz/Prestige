using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.SlashCommands;

namespace Server
{
    public class SlashTest : ApplicationCommandModule
    {
        private static int _counter = 0;

        [SlashCommand("test", "Simple test command")]
        public async Task Test(InteractionContext ctx)
        {
            var count = Interlocked.Increment(ref _counter);

            Console.WriteLine($"[TEST] Command used {count} times.");

            await ctx.CreateResponseAsync($"Test command works! 🚀 (Used {count} times since bot started)");
        }
    }
}
