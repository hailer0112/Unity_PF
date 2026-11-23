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

        bool TrySpendSkillMana(HudSkill skill, float cost)
        {
            if (manaHealth != null && manaHealth.IsDead) return false;
            cost = Mathf.Max(0f, cost);
            if (CurrentMP < cost)
            {
                insufficientManaUntil[(int)skill] = Time.unscaledTimeAsDouble + ManaFeedbackDuration;
                return false;
            }

            CurrentMP -= cost;
            insufficientManaUntil[(int)skill] = 0d;
            return true;
        }

        internal bool HasInsufficientManaFeedback(HudSkill skill) =>
            (int)skill < insufficientManaUntil.Length &&
            Time.unscaledTimeAsDouble < insufficientManaUntil[(int)skill];

        static bool DealsBasicDamage(AttackTarget target, float damage, Vector3 source)
        {
            var monster = target as MonsterHealth;
            float before = monster != null ? monster.CurrentHP : 0f;
            target.ReceiveDamage(damage, source);
            return damage > 0f && !float.IsNaN(damage) && !float.IsInfinity(damage) &&
                (monster == null || monster.CurrentHP < before);
        }
    }
}
