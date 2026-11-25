using System.Globalization;
using UnityEngine;

namespace Storybook
{
    internal enum WishKind { Life, Mana, Time }

    [CreateAssetMenu(menuName = "Storybook/Relics/Wish Devouring Eye")]
    public sealed class WishDevouringEyeRelic : FairyTaleRelic
    {
        [SerializeField] string effectName;
        [SerializeField, Range(0f, 1f)] float triggerChance = .20f;
        [SerializeField, Min(0f)] float lifeWeight = 1f, manaWeight = 1f, timeWeight = 1f;
        [SerializeField, Range(0f, 1f)] float healFraction = .10f, refundFraction = 1f;
        [SerializeField] Sprite lifeEye, manaEye, timeEye;
        [SerializeField] ParticleSystem recoverySparks;
        [SerializeField, Min(.05f)] float visualDuration = .5f;
        [SerializeField, Min(.01f)] float visualHeightRatio = .58f;
        [SerializeField, Min(0f)] float headGap = .10f;

        internal float HealFraction => Mathf.Clamp01(healFraction);
        internal float RefundFraction => Mathf.Clamp01(refundFraction);
        internal float VisualDuration => Mathf.Max(.05f, visualDuration);
        internal float VisualHeightRatio => Mathf.Max(.01f, visualHeightRatio);
        internal float HeadGap => Mathf.Max(0f, headGap);
        internal ParticleSystem RecoverySparks => recoverySparks;
        float TriggerChance => Mathf.Clamp01(triggerChance);
        double LifeWeight => Mathf.Max(0f, lifeWeight);
        double ManaWeight => Mathf.Max(0f, manaWeight);
        double TimeWeight => Mathf.Max(0f, timeWeight);
        double TotalWeight => LifeWeight + ManaWeight + TimeWeight;

        public override string Description => base.Description?
            .Replace("{chance}", Percent(TotalWeight > 0d ? TriggerChance : 0f))
            .Replace("{heal}", Percent(HealFraction))
            .Replace("{refund}", RefundFraction == 1f ? "전량" : Percent(RefundFraction));

        internal override string DescribeActiveEffect(PlayerHealth player)
        {
            double total = TotalWeight;
            return total > 0d
                ? $"{effectName}\n소원 선택 비율   생명 {Percent(LifeWeight / total)} · 마력 {Percent(ManaWeight / total)} · 시간 {Percent(TimeWeight / total)}"
                : effectName;
        }

        static string Percent(double value) => value.ToString("0.##%", CultureInfo.InvariantCulture);

        internal WishKind? RollWish()
        {
            double total = TotalWeight;
            float chance = TriggerChance;
            if (total <= 0d || chance <= 0f || (chance < 1f && Random.value >= chance)) return null;

            double roll = (double)Random.value * total;
            if (roll < LifeWeight) return WishKind.Life;
            if (roll < LifeWeight + ManaWeight) return WishKind.Mana;

            // Random.value can be exactly 1, so select the last enabled wish at the upper endpoint.
            if (TimeWeight > 0d) return WishKind.Time;
            return ManaWeight > 0d ? WishKind.Mana : WishKind.Life;
        }

        internal Sprite GetEye(WishKind wish) => wish switch
        {
            WishKind.Life => lifeEye,
            WishKind.Mana => manaEye,
            _ => timeEye
        };
    }
}
