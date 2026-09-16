using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Serialization;
using Archipelago.MultiClient.Net.Models;

namespace SilksongRandomizer
{
    [Serializable]
    public sealed class ReceivedItemReceipt
    {
        public int player;
        public long location;
        public long item;
        public int occurrence;
        public string name;

        [XmlIgnore]
        internal string SourceKey => player.ToString(CultureInfo.InvariantCulture) + ":" +
            location.ToString(CultureInfo.InvariantCulture);
        [XmlIgnore]
        internal string ItemKey => SourceKey + ":" + item.ToString(CultureInfo.InvariantCulture);
        [XmlIgnore]
        internal string Key => ItemKey + ":" + occurrence.ToString(CultureInfo.InvariantCulture);

        internal static List<ReceivedItemReceipt> FromServer(
            IReadOnlyList<ItemInfo> items, Func<ItemInfo, string> getName)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>();
            List<ReceivedItemReceipt> result = new List<ReceivedItemReceipt>(items.Count);
            foreach (ItemInfo item in items)
            {
                ReceivedItemReceipt receipt = new ReceivedItemReceipt
                {
                    player = item.Player.Slot,
                    location = item.LocationId,
                    item = item.ItemId,
                    name = getName(item),
                };
                counts.TryGetValue(receipt.ItemKey, out int count);
                receipt.occurrence = count + 1;
                counts[receipt.ItemKey] = receipt.occurrence;
                result.Add(receipt);
            }
            return result;
        }
    }

    public partial class SaveState
    {
        public bool receivedItemReceiptsInitialized;
        public List<ReceivedItemReceipt> receivedItemReceipts = new List<ReceivedItemReceipt>();
        public int receivedItemStreamIndex;
        private HashSet<string> receivedReceiptKeys;

        [XmlIgnore]
        internal int NextReceivedItemIndex => receivedItemReceiptsInitialized
            ? receivedItemStreamIndex : receivedItemIndex;

        internal bool HasReceivedReceipt(ReceivedItemReceipt receipt)
        {
            if (receipt == null || !receivedItemReceiptsInitialized)
            {
                return false;
            }
            if (receivedReceiptKeys == null)
            {
                receivedReceiptKeys = new HashSet<string>(receivedItemReceipts.Select(saved => saved.Key));
            }
            return receivedReceiptKeys.Contains(receipt.Key);
        }

        internal bool TrySynchronizeReceivedReceipts(IReadOnlyList<ReceivedItemReceipt> server)
        {
            if (server == null || receivedItemIndex < 0 || receivedItemHistory == null ||
                receivedItems == null)
            {
                return false;
            }
            List<ReceivedItemReceipt> saved = receivedItemReceiptsInitialized
                ? receivedItemReceipts : server.Take(receivedItemIndex).ToList();
            if (saved == null || saved.Count != receivedItemIndex ||
                receivedItemHistory.Count > receivedItemIndex ||
                (receivedItemReceiptsInitialized && receivedItemHistory.Count != receivedItemIndex))
            {
                return false;
            }
            HashSet<string> keys = new HashSet<string>();
            Dictionary<string, long> sources = new Dictionary<string, long>();
            Dictionary<string, int> counts = new Dictionary<string, int>();
            for (int index = 0; index < saved.Count; index++)
            {
                ReceivedItemReceipt receipt = saved[index];
                if (receipt == null || receipt.occurrence <= 0 ||
                    string.IsNullOrWhiteSpace(receipt.name) || !keys.Add(receipt.Key) ||
                    !receivedItems.Contains(ItemSet.GetCanonicalItemName(receipt.name)))
                {
                    return false;
                }
                counts.TryGetValue(receipt.ItemKey, out int previousCount);
                if (receipt.occurrence != previousCount + 1)
                {
                    return false;
                }
                counts[receipt.ItemKey] = receipt.occurrence;
                string previous = index < receivedItemHistory.Count ? receivedItemHistory[index] : null;
                if (!string.IsNullOrWhiteSpace(previous) && !string.Equals(
                    ItemSet.GetCanonicalItemName(previous), ItemSet.GetCanonicalItemName(receipt.name),
                    StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                if (receipt.location >= 0)
                {
                    if (sources.TryGetValue(receipt.SourceKey, out long item) && item != receipt.item)
                    {
                        return false;
                    }
                    sources[receipt.SourceKey] = receipt.item;
                }
            }
            foreach (ReceivedItemReceipt receipt in server)
            {
                if (receipt == null || receipt.occurrence <= 0 || string.IsNullOrWhiteSpace(receipt.name) ||
                    (receipt.location >= 0 && sources.TryGetValue(receipt.SourceKey, out long item) &&
                     item != receipt.item))
                {
                    return false;
                }
            }
            if (!receivedItemReceiptsInitialized)
            {
                if (!TrySynchronizeReceivedItemHistory(server.Select(receipt => receipt.name).ToArray()))
                {
                    return false;
                }
                receivedItemReceipts = saved;
                receivedItemReceiptsInitialized = true;
            }
            receivedReceiptKeys = keys;
            receivedItemStreamIndex = 0;
            return true;
        }

        internal bool CommitReceivedReceiptAtIndex(
            int index, string name, bool repeatable, ReceivedItemReceipt receipt)
        {
            if (receipt == null || !receivedItemReceiptsInitialized)
            {
                return CommitReceivedItemAtIndex(index, name, repeatable);
            }
            if (index < receivedItemStreamIndex)
            {
                return false;
            }
            if (index != receivedItemStreamIndex || receivedItemReceipts.Count != receivedItemIndex ||
                !string.Equals(ItemSet.GetCanonicalItemName(receipt.name),
                    ItemSet.GetCanonicalItemName(name), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Received AP receipt is out of order or inconsistent.");
            }
            bool newlyReceived = false;
            if (!HasReceivedReceipt(receipt))
            {
                newlyReceived = CommitReceivedItemAtIndex(receivedItemIndex, name, repeatable);
                receivedItemReceipts.Add(receipt);
                receivedReceiptKeys.Add(receipt.Key);
            }
            receivedItemStreamIndex++;
            return newlyReceived;
        }
    }
}
