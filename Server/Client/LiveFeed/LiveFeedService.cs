using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using Server.Client.Utils;
using Server.Infrastructure;
using Server.Infrastructure.Discord;

namespace Server.Client.LiveFeed
{
    /// <summary>
    /// Lightweight service to send fire-and-forget game activity messages
    /// to a dedicated live feed channel for staff.
    /// </summary>
    public class LiveFeedService
    {
        private readonly DiscordBotHost _discordBotHost;

        public LiveFeedService(DiscordBotHost discordBotHost)
        {
            _discordBotHost = discordBotHost;
        }

        private DiscordClient? TryGetClient()
        {
            try
            {
                return _discordBotHost?.Client;
            }
            catch
            {
                return null;
            }
        }

        public void PublishCoinflip(long amountK, bool win, bool resultHeads)
        {
            var client = TryGetClient();
            if (client == null)
                return;

            _ = Task.Run(async () =>
            {
                try
                {
                    var channel = await client.GetChannelAsync(DiscordIds.LiveFeedChannelId);
                    var amountPretty = GpFormatter.Format(amountK);
                    var sideText = resultHeads ? "Heads" : "Tails";

                    var isBigWin = win && amountK >= 1_000_000L; // >= 1B bet

                    string description;
                    DiscordColor color;

                    if (isBigWin)
                    {
                        description = $"{amountPretty} __**BIG WIN!**__ when the coin landed {sideText} <:{nameof(DiscordIds.BigWinMvppEmojiId)}:{DiscordIds.BigWinMvppEmojiId}>";
                        color = new DiscordColor("#AA66FF"); // purple for big wins
                    }
                    else if (win)
                    {
                        description = $"{amountPretty} **WIN** when the coin landed {sideText} <:{nameof(DiscordIds.CoinflipGoldEmojiId)}:{DiscordIds.CoinflipGoldEmojiId}>";
                        color = DiscordColor.SpringGreen;
                    }
                    else
                    {
                        description = $"{amountPretty} **LOST** when the coin landed {sideText} <:{nameof(DiscordIds.CoinflipSilverEmojiId)}:{DiscordIds.CoinflipSilverEmojiId}>";
                        color = DiscordColor.Red;
                    }

                    var embed = new DiscordEmbedBuilder()
                        .WithDescription(description)
                        .WithColor(color);

                    await channel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(embed));
                }
                catch
                {
                    // Swallow errors: live feed must not affect gameplay.
                }
            });
        }

        public void PublishStake(long amountK, bool win)
        {
            var client = TryGetClient();
            if (client == null)
                return;

            _ = Task.Run(async () =>
            {
                try
                {
                    var channel = await client.GetChannelAsync(DiscordIds.LiveFeedChannelId);
                    var amountPretty = GpFormatter.Format(amountK);

                    var isBigWin = win && amountK >= 1_000_000L; // >= 1B stake

                    string description;
                    DiscordColor color;

                    if (isBigWin)
                    {
                        // Big win is always a win, use MVPP emoji
                        description = $"{amountPretty} __**BIG WIN!**__ from a stake <:{nameof(DiscordIds.BigWinMvppEmojiId)}:{DiscordIds.BigWinMvppEmojiId}>";
                        color = new DiscordColor("#AA66FF"); // purple for big wins
                    }
                    else if (win)
                    {
                        // Wpray is the win emoji for stakes
                        description = $"{amountPretty} **WIN** from a stake <:{nameof(DiscordIds.WprayEmojiId)}:{DiscordIds.WprayEmojiId}>";
                        color = DiscordColor.SpringGreen;
                    }
                    else
                    {
                        // DdpEmojiId is used for stake losses
                        description = $"{amountPretty} **LOST** from a stake <:{nameof(DiscordIds.DdpEmojiId)}:{DiscordIds.DdpEmojiId}>";
                        color = DiscordColor.Red;
                    }

                    var embed = new DiscordEmbedBuilder()
                        .WithDescription(description)
                        .WithColor(color);

                    await channel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(embed));
                }
                catch
                {
                    // Swallow errors: live feed must not affect gameplay.
                }
            });
        }
    }
}
