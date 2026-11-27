using System.Globalization;
using UnityEngine;

namespace Storybook
{
    [CreateAssetMenu(menuName = "Storybook/Relics/Primordial Seed")]
    public sealed class PrimordialSeedRelic : FairyTaleRelic
    {
        [SerializeField, Min(1)] int killsPerGrowth = 10, maxGrowth = 10;
        [SerializeField, Min(0f)] float maxHealthPerGrowth = 3f, currentHealthPerGrowth = 3f;
        [SerializeField, Min(0f)] float attackBonusPerGrowth = .03f;
        [SerializeField] Sprite evolvedIcon, sprout;
        [SerializeField] Material lifeParticleMaterial;
        [SerializeField, Min(.1f)] float growthDuration = .7f, evolutionDuration = 1.2f;
        [SerializeField, Min(.01f)] float growthSize = .37f, evolutionSize = .51f;

        int KillsPerGrowth => Mathf.Max(1, killsPerGrowth);
        internal int MaxGrowth => Mathf.Max(1, maxGrowth);
        float MaxHealthPerGrowth => Mathf.Max(0f, maxHealthPerGrowth);
        internal float CurrentHealthPerGrowth => Mathf.Max(0f, currentHealthPerGrowth);
        float AttackBonusPerGrowth => Mathf.Max(0f, attackBonusPerGrowth);
        internal Sprite Sprout => sprout;
        internal Material LifeParticleMaterial => lifeParticleMaterial;
        internal float GrowthDuration => Mathf.Max(.1f, growthDuration);
        internal float EvolutionDuration => Mathf.Max(.1f, evolutionDuration);
        internal float GrowthSize => Mathf.Max(.01f, growthSize);
        internal float EvolutionSize => Mathf.Max(.01f, evolutionSize);

        internal int GrowthLevel(PlayerHealth player) => Mathf.Min(MaxGrowth, player.SeedKills / KillsPerGrowth);
        internal float GetMaxHealthBonus(PlayerHealth player) => GrowthLevel(player) * MaxHealthPerGrowth;
        protected internal override float GetAttackBonus(PlayerHealth player) => GrowthLevel(player) * AttackBonusPerGrowth;
        protected internal override void OnAcquired(PlayerHealth player) => player.BeginSeedGrowth();

        internal override Sprite GetOwnedIcon(PlayerHealth player) =>
            GrowthLevel(player) >= MaxGrowth && evolvedIcon != null ? evolvedIcon : Icon;

        public override string Description => base.Description?
            .Replace("{kills}", KillsPerGrowth.ToString(CultureInfo.InvariantCulture))
            .Replace("{maxHP}", Number(MaxHealthPerGrowth))
            .Replace("{currentHP}", Number(CurrentHealthPerGrowth))
            .Replace("{attack}", Percent(AttackBonusPerGrowth))
            .Replace("{cap}", MaxGrowth.ToString(CultureInfo.InvariantCulture));

        internal override string DescribeActiveEffect(PlayerHealth player)
        {
            int level = GrowthLevel(player);
            string progress = level >= MaxGrowth ? "최대 성장 완료" :
                $"다음 성장 진행도   {player.SeedKills % KillsPerGrowth} / {KillsPerGrowth}";
            return $"현재 성장 단계   {level} / {MaxGrowth}\n{progress}\n" +
                $"현재 최대 HP 증가   +{Number(GetMaxHealthBonus(player))}\n현재 공격력 증가   +{Percent(GetAttackBonus(player))}";
        }

        internal string GrowthNotice(bool evolved) => evolved ? "태초의 씨앗 — 최대 성장" :
            $"최대 HP +{Number(MaxHealthPerGrowth)}  ·  공격력 +{Percent(AttackBonusPerGrowth)}";

        static string Number(float value) => value.ToString("G", CultureInfo.InvariantCulture);
        static string Percent(float value) => value.ToString("0.##%", CultureInfo.InvariantCulture);
    }
}
