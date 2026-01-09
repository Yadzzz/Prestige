using System.Collections.Generic;
using Server.Infrastructure.Discord;

namespace Server.Client.Chest
{
    public class ChestItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public long ValueK { get; set; } // Value in K (thousands)
        public string IconUrl { get; set; }
        public ulong EmojiId { get; set; } // Discord Custom Emoji ID

        public static readonly List<ChestItem> Items = new List<ChestItem>
        {
            new ChestItem { Id = "scythe_vitur", Name = "Scythe of Vitur", ValueK = 1_200_000, IconUrl = "https://oldschool.runescape.wiki/images/Scythe_of_vitur_detail.png", EmojiId = DiscordIds.ChestScytheEmojiId },
            new ChestItem { Id = "t_bow", Name = "Twisted Bow", ValueK = 1_600_000, IconUrl = "https://oldschool.runescape.wiki/images/Twisted_bow_detail.png", EmojiId = DiscordIds.ChestTbowEmojiId },
            new ChestItem { Id = "3rd_pick", Name = "3rd Age Pickaxe", ValueK = 2_100_000, IconUrl = "https://oldschool.runescape.wiki/images/3rd_age_pickaxe_detail.png", EmojiId = DiscordIds.ChestPickaxeEmojiId },
            new ChestItem { Id = "elysian", Name = "Elysian Spirit Shield", ValueK = 500_000, IconUrl = "https://oldschool.runescape.wiki/images/Elysian_spirit_shield_detail.png", EmojiId = DiscordIds.ChestElysianEmojiId },
            new ChestItem { Id = "shadow", Name = "Tumeken's Shadow", ValueK = 1_400_000, IconUrl = "https://oldschool.runescape.wiki/images/Tumeken%27s_shadow_%28uncharged%29_detail.png", EmojiId = DiscordIds.ChestShadowEmojiId },
            new ChestItem { Id = "zaryte", Name = "Zaryte Crossbow", ValueK = 330_000, IconUrl = "https://oldschool.runescape.wiki/images/Zaryte_crossbow_detail.png", EmojiId = DiscordIds.ChestZaryteEmojiId },
            new ChestItem { Id = "torva_helm", Name = "Torva Full Helm", ValueK = 400_000, IconUrl = "https://oldschool.runescape.wiki/images/Torva_full_helm_detail.png", EmojiId = DiscordIds.ChestTorvaHelmEmojiId },
            new ChestItem { Id = "torva_body", Name = "Torva Platebody", ValueK = 950_000, IconUrl = "https://oldschool.runescape.wiki/images/Torva_platebody_detail.png", EmojiId = DiscordIds.ChestTorvaBodyEmojiId },
            new ChestItem { Id = "torva_legs", Name = "Torva Platelegs", ValueK = 850_000, IconUrl = "https://oldschool.runescape.wiki/images/Torva_platelegs_detail.png", EmojiId = DiscordIds.ChestTorvaLegsEmojiId }
        };
    }
}
