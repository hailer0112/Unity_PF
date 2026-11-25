using System;
using UnityEngine;

namespace Storybook
{
    public sealed partial class HongGildong
    {
        [Header("Mana")]
        [SerializeField, Min(1f)] float maxMP = 100f;
        [SerializeField, Min(0f)] float startingMP = 100f;
        [SerializeField, Min(0f)] float manaPerBasicAttack = 5f;
        [SerializeField, Min(0f)] float azureSlashManaCost = 15f;
        [SerializeField, Min(0f)] float cloneSlashManaCost = 25f;
        [SerializeField, Min(0f)] float windstepManaCost = 20f;
        [SerializeField, Min(0f)] float swordTyphoonManaCost = 40f;

        [SerializeField, Range(0f, 1f)] float manaCostReductionCap = .50f;

        const float ManaFeedbackDuration = .65f;
        readonly double[] insufficientManaUntil = new double[(int)HudSkill.Dash];
        PlayerHealth manaHealth;
        bool restoredManaThisSwing;

        public float MaxMP => Mathf.Max(1f, maxMP);
        public float CurrentMP { get; private set; }

        void InitializeMana()
        {
            TryGetComponent(out manaHealth);
            CurrentMP = Mathf.Clamp(startingMP, 0f, MaxMP);
        }

        internal void RefillMana() => CurrentMP = MaxMP;

        internal float RestoreMana(float amount)
        {
            float before = CurrentMP;
            CurrentMP = Mathf.Clamp(CurrentMP + amount, 0f, MaxMP);
            return CurrentMP - before;
        }

        void RestoreManaOnBasicHit()
        {
            if (restoredManaThisSwing || (manaHealth != null && manaHealth.IsDead)) return;
            restoredManaThisSwing = true;
            RestoreMana(manaPerBasicAttack);
        }

        internal decimal SkillManaCostReduction =>
            Math.Min((decimal)Mathf.Clamp01(manaCostReductionCap), AttackRelics?.ManaCostReduction ?? 0m);

        internal float GetSkillManaCost(HudSkill skill)
        {
            float baseCost = Mathf.Max(0f, skill switch
            {
                HudSkill.AzureSlash => azureSlashManaCost,
                HudSkill.CloneSlash => cloneSlashManaCost,
                HudSkill.Windstep => windstepManaCost,
                HudSkill.SwordTyphoon => swordTyphoonManaCost,
                _ => 0f
            });
            decimal reduction = SkillManaCostReduction;
            if (reduction <= 0m || baseCost == 0f) return baseCost;
            if (reduction == 1m) return 0f;

            // Decimal arithmetic keeps exact integer results such as 40 × 0.8 at 32.
            if ((double)baseCost >= (double)decimal.MaxValue) return Mathf.Ceil(baseCost * (float)(1m - reduction));
            return (float)Math.Ceiling((decimal)baseCost * (1m - reduction));
        }

        bool TrySpendSkillMana(HudSkill skill, out float spentMana)
        {
            spentMana = 0f;
            if (manaHealth != null && manaHealth.IsDead) return false;
            float cost = GetSkillManaCost(skill);
            if (CurrentMP < cost)
            {
                insufficientManaUntil[(int)skill] = Time.unscaledTimeAsDouble + ManaFeedbackDuration;
                return false;
            }

            float before = CurrentMP;
            CurrentMP -= cost;
            spentMana = before - CurrentMP;
            insufficientManaUntil[(int)skill] = 0d;
            return true;
        }

        internal bool HasInsufficientManaFeedback(HudSkill skill) =>
            (int)skill < insufficientManaUntil.Length &&
            Time.unscaledTimeAsDouble < insufficientManaUntil[(int)skill] && CurrentMP < GetSkillManaCost(skill);

    }
}
