using System;
using System.Collections.Generic;

namespace Storybook
{
    public sealed class FairyTaleRelicInventory
    {
        public const int Capacity = 6;
        readonly PlayerHealth owner;
        readonly List<FairyTaleRelic> owned = new(Capacity);

        internal FairyTaleRelicInventory(PlayerHealth owner)
        {
            this.owner = owner;
            Owned = owned.AsReadOnly();
        }

        public IReadOnlyList<FairyTaleRelic> Owned { get; }
        public bool IsFull => owned.Count >= Capacity;
        internal static string FullPrompt => $"유물 보유 한도에 도달했습니다. ({Capacity}/{Capacity})";

        public float AttackBonus
        {
            get
            {
                float bonus = 0f;
                foreach (var relic in owned) bonus += relic.GetAttackBonus(owner);
                return bonus;
            }
        }

        internal decimal ManaCostReduction
        {
            get
            {
                decimal reduction = 0m;
                foreach (var relic in owned) reduction += relic.ManaCostReduction;
                return reduction;
            }
        }

        internal T Find<T>() where T : FairyTaleRelic
        {
            foreach (var relic in owned)
                if (relic is T found) return found;
            return null;
        }

        internal T Consume<T>() where T : FairyTaleRelic
        {
            var relic = Find<T>();
            return relic != null && owned.Remove(relic) ? relic : null;
        }

        public bool Contains(string id)
        {
            foreach (var relic in owned)
                if (relic.Id == id) return true;
            return false;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal void ClearForTesting()
        {
            bool hadSeed = Find<PrimordialSeedRelic>() != null;
            owned.Clear();
            if (hadSeed) owner.ClearSeedGrowthForTesting();
        }
#endif

        public bool TryAcquire(FairyTaleRelic relic)
        {
            if (owner == null || owner.IsDead || relic == null || string.IsNullOrEmpty(relic.Id) ||
                IsFull || Contains(relic.Id)) return false;

            owned.Add(relic);
            try
            {
                relic.OnAcquired(owner);
                return true;
            }
            catch
            {
                owned.Remove(relic);
                throw;
            }
        }

        internal void NotifyBasicAttackHit(MonsterHealth target)
        {
            if (target.IsDead) return;
            foreach (var relic in owned) relic.OnBasicAttackHit(owner, target);
        }

        public FairyTaleRelic AcquireRandom(IEnumerable<FairyTaleRelic> catalog, Random random)
        {
            var relic = SelectRandom(catalog, random);
            return TryAcquire(relic) ? relic : null;
        }

        internal FairyTaleRelic SelectRandom(IEnumerable<FairyTaleRelic> catalog, Random random)
        {
            if (catalog == null || IsFull) return null;
            var available = new Dictionary<string, FairyTaleRelic>(StringComparer.Ordinal);
            foreach (var relic in catalog)
                if (relic != null && !string.IsNullOrEmpty(relic.Id) && !Contains(relic.Id))
                    available.TryAdd(relic.Id, relic);

            if (available.Count == 0) return null;
            int index = random.Next(available.Count);
            foreach (var relic in available.Values)
                if (index-- == 0) return relic;
            return null;
        }
    }
}
