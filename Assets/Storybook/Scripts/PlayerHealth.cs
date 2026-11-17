using System;
using UnityEngine;
namespace Storybook
{
    [RequireComponent(typeof(HongGildong))]
    public sealed class PlayerHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] float maxHP = 100;
        [SerializeField] float currentHP;
        HongGildong player;
        public float CurrentHP => currentHP;
        public float MaxHP => maxHP;
        public bool IsDead => currentHP <= 0;
        public int MonsterKillCount { get; private set; }
        readonly System.Random fragmentRandom = new(Guid.NewGuid().GetHashCode());
        public long StoryFragments { get; private set; }
        internal event Action<int> StoryFragmentsGained;
        public event Action Damaged, Died;
        internal bool ChapterClearProtected { get; set; }
        void Awake() { player=GetComponent<HongGildong>();currentHP=maxHP; }
        internal void RecordMonsterKill() => MonsterKillCount++;

        internal void AwardStoryFragments(int minimum, int maximum)
        {
            minimum = Mathf.Max(0, minimum);
            maximum = Mathf.Max(minimum, maximum);
            // Random.Next excludes its upper bound. Handle the largest int without overflow.
            int amount = maximum < int.MaxValue ? fragmentRandom.Next(minimum, maximum + 1)
                : minimum > 0 ? fragmentRandom.Next(minimum - 1, maximum) + 1
                : fragmentRandom.Next(1 << 30) * 2 + fragmentRandom.Next(2);
            if (amount == 0) return;

            StoryFragments += amount;
            StoryFragmentsGained?.Invoke(amount);
        }

        public bool TryHeal(float amount)
        {
            if (!isActiveAndEnabled || IsDead || currentHP >= maxHP || amount <= 0f ||
                float.IsNaN(amount) || float.IsInfinity(amount)) return false;
            currentHP = Mathf.Min(maxHP, currentHP + amount);
            return true;
        }

        public bool TryTakeDamage(float damage)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (TestInvulnerable || PalaceDeveloperMenu.BlocksGameplay) return false;
#endif
            if(!isActiveAndEnabled || IsDead || ChapterClearProtected || player.IsInvulnerable || damage<=0 || float.IsNaN(damage) || float.IsInfinity(damage))return false;
            currentHP=Mathf.Max(0,currentHP-damage);
            if(IsDead){player.enabled=false;Died?.Invoke();}else Damaged?.Invoke();
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal bool TestInvulnerable { get; set; }

        internal void SetOneHPForTesting()
        {
            if (!IsDead) currentHP = 1f;
        }

        internal void ClearFragmentsForTesting() => StoryFragments = 0;
#endif
    }
}
