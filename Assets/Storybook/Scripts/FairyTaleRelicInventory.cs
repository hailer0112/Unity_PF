using System;
using System.Collections.Generic;

namespace Storybook
{
    public sealed class FairyTaleRelicInventory
    {
        readonly PlayerHealth owner;
        readonly Dictionary<string, FairyTaleRelic> owned = new(StringComparer.Ordinal);

        internal FairyTaleRelicInventory(PlayerHealth owner) => this.owner = owner;

        public IReadOnlyCollection<FairyTaleRelic> Owned => owned.Values;

        public bool TryAcquire(FairyTaleRelic relic)
        {
            if (owner == null || owner.IsDead || relic == null || string.IsNullOrEmpty(relic.Id) ||
                !owned.TryAdd(relic.Id, relic)) return false;

            relic.OnAcquired(owner);
            return true;
        }

        public FairyTaleRelic AcquireRandom(IEnumerable<FairyTaleRelic> catalog, Random random)
        {
            var relic = SelectRandom(catalog, random);
            return TryAcquire(relic) ? relic : null;
        }

        internal FairyTaleRelic SelectRandom(IEnumerable<FairyTaleRelic> catalog, Random random)
        {
            if (catalog == null) return null;
            var available = new Dictionary<string, FairyTaleRelic>(StringComparer.Ordinal);
            foreach (var relic in catalog)
                if (relic != null && !string.IsNullOrEmpty(relic.Id) && !owned.ContainsKey(relic.Id))
                    available.TryAdd(relic.Id, relic);

            if (available.Count == 0) return null;
            int index = random.Next(available.Count);
            foreach (var relic in available.Values)
                if (index-- == 0) return relic;
            return null;
        }
    }
}
