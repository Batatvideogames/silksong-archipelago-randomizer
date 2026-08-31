using System;

namespace SilksongRandomizer
{
    public enum RandomizationMode
    {
        Vanilla = 0,
        Anywhere = 1,
        Shuffle = 2,
    }

    public enum ItemType
    {
        Unknown,
        Skill,
        Spell,
        Crest,
        Tool,
        Flea,
        CrestSlot,
        MaskShard,
        SpoolFragment,
        SilkHeart,
        Bellway,
        Ventrica,
        Currency,
        Upgrade,
        Map,
        Pin,
        Relic,
        Trap,
        Boss,
        BellShrine,
        Quest,
        NeedleUpgrade,
        Event,
        Resource,
        SimpleKey,
        Melody,
        MemoryLocket,
        Craftmetal,
        Mossberry,
        PollipHeart,
        Silkeater,
        MajorKey,
        ToolPouch,
        LoreTablet,
        PaleOil,
        InnateAbility,
    }

    public class Item
    {
        public readonly string Name;
        public readonly ItemType Type;
        public readonly Action Receive;
        public readonly bool Repeatable;

        public Item(string name, ItemType type, Action receive, bool repeatable = false)
        {
            Name = ItemSet.GetCanonicalItemName(name);
            Type = type;
            Receive = receive;
            Repeatable = repeatable;
        }
    }
}
