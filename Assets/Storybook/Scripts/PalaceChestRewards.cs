using System;
using UnityEngine;

namespace Storybook
{
    internal enum PalaceChestReward { Fragments, Health, Mana, Relic }

    internal readonly struct PalaceChestLoot
    {
        internal readonly PalaceChestReward Kind;
        internal readonly int Amount;
        internal readonly FairyTaleRelic Relic;

        internal PalaceChestLoot(PalaceChestReward kind, int amount = 0, FairyTaleRelic relic = null)
        {
            Kind = kind;
            Amount = amount;
            Relic = relic;
        }

        internal string Prompt => Kind switch
        {
            PalaceChestReward.Health => "생명의 구슬\n최대 HP의 15% 회복\nF - 사용",
            PalaceChestReward.Mana => "마력의 구슬\n최대 MP의 20% 회복\nF - 사용",
            PalaceChestReward.Relic => $"{Relic.DisplayName}\n{Relic.Description}\nF - 획득",
            _ => $"이야기 파편 꾸러미 · 파편 {Amount}개\nF - 획득"
        };
    }

    [Serializable]
    internal sealed class PalaceChestRewards
    {
        [SerializeField, Range(0, 100)] int fragmentsWeight = 60;
        [SerializeField, Range(0, 100)] int healthWeight = 20;
        [SerializeField, Range(0, 100)] int manaWeight = 15;
        [SerializeField, Range(0, 100)] int relicWeight = 5;
        [SerializeField] FairyTaleRelic[] relicCatalog = Array.Empty<FairyTaleRelic>();

        const int SubstituteFragments = 25;
        int TotalWeight => fragmentsWeight + healthWeight + manaWeight + relicWeight;

        internal PalaceChestReward Roll(System.Random random)
        {
            if (TotalWeight <= 0) return PalaceChestReward.Fragments;
            int value = random.Next(TotalWeight);
            if (value < fragmentsWeight) return PalaceChestReward.Fragments;
            if (value < fragmentsWeight + healthWeight) return PalaceChestReward.Health;
            if (value < fragmentsWeight + healthWeight + manaWeight) return PalaceChestReward.Mana;
            return PalaceChestReward.Relic;
        }

        internal string Grant(PalaceChestReward reward, PlayerHealth health, HongGildong player, System.Random random)
            => GrantLoot(CreateLoot(reward, health, random), health, player);

        internal PalaceChestLoot CreateLoot(PalaceChestReward reward, PlayerHealth health, System.Random random)
        {
            if (reward == PalaceChestReward.Fragments) return new(reward, random.Next(20, 31));
            if (reward != PalaceChestReward.Relic) return new(reward);
            var relic = health.Relics.SelectRandom(relicCatalog, random);
            return relic != null ? new(reward, relic: relic) : new(PalaceChestReward.Fragments, SubstituteFragments);
        }

        internal string GrantLoot(PalaceChestLoot loot, PlayerHealth health, HongGildong player)
        {
            switch (loot.Kind)
            {
                case PalaceChestReward.Fragments:
                    return GrantFragments(health, loot.Amount);
                case PalaceChestReward.Health:
                    float beforeHP = health.CurrentHP;
                    if (health.TryHeal((float)Math.Ceiling(health.MaxHP * 15d / 100d)))
                        return $"HP +{health.CurrentHP - beforeHP:0.##}";
                    break;
                case PalaceChestReward.Mana:
                    float recovered = player.RestoreMana((float)Math.Ceiling(player.MaxMP * 20d / 100d));
                    if (recovered > 0f) return $"MP +{recovered:0.##}";
                    break;
                case PalaceChestReward.Relic:
                    if (health.Relics.TryAcquire(loot.Relic)) return $"동화 유물 획득: {loot.Relic.DisplayName}";
                    break;
            }

            return GrantFragments(health, SubstituteFragments);
        }

        static string GrantFragments(PlayerHealth health, int amount)
        {
            health.AwardStoryFragments(amount, amount);
            return $"이야기 파편 +{amount}";
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal FairyTaleRelic FindRelic(string id) => Array.Find(relicCatalog, relic => relic != null && relic.Id == id);

        internal string DescribeOdds() =>
            $"Weights (Fragments / HP / MP / Relic): {fragmentsWeight}/{healthWeight}/{manaWeight}/{relicWeight}, total {TotalWeight}";
#endif
    }
}
