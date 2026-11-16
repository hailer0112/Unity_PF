using System;
using System.Collections;
using UnityEngine;
namespace Storybook
{
    public enum MonsterDeathCause { PlayerKilled, SelfDestruct }

    public sealed class MonsterHealth : AttackTarget
    {
        [SerializeField, Min(1)] float maxHP = 100;
        [SerializeField] float currentHP;
        [SerializeField, Min(0)] float deathDelay = .55f;
        [SerializeField] bool despawnOnDeath = true;
        [Header("Story Fragments")]
        [SerializeField, Min(0)] int minimumFragments;
        [SerializeField, Min(0)] int maximumFragments;
        PlayerHealth fragmentRecipient;

        void OnValidate()
        {
            minimumFragments = Mathf.Max(0, minimumFragments);
            maximumFragments = Mathf.Max(minimumFragments, maximumFragments);
        }

        internal void SetFragmentRecipient(PlayerHealth recipient) => fragmentRecipient = recipient;

        public float MaxHP => maxHP;
        public float CurrentHP => currentHP;
        public bool IsDead => currentHP <= 0;
        public override bool CanTakeDamage => base.CanTakeDamage && !IsDead;
        public event Action Damaged;
        public event Action Died;
        public event Action<MonsterDeathCause> Defeated;
        public MonsterDeathCause? DeathCause { get; private set; }
        TurtleGuardAI guard;

        void Awake()
        {
            currentHP = maxHP;
            TryGetComponent(out guard);
        }

        public override void ReceiveDamage(float damage, Vector3 source)
        {
            ReceiveDamage(guard != null ? guard.GuardDamage(damage, source) : damage);
        }
        public override void ReceiveDamage(float damage)
        {
            if (!CanTakeDamage || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return;
            currentHP = Mathf.Max(0, currentHP - damage);
            if (!IsDead) { Damaged?.Invoke(); return; }
            FinishDeath(MonsterDeathCause.PlayerKilled);
        }

        internal bool TrySelfDestruct()
        {
            if (!CanTakeDamage) return false;
            FinishDeath(MonsterDeathCause.SelfDestruct);
            return true;
        }

        void FinishDeath(MonsterDeathCause cause)
        {
            currentHP = 0f;
            DeathCause = cause;
            var recipient = fragmentRecipient;
            fragmentRecipient = null;
            if (cause == MonsterDeathCause.PlayerKilled && recipient != null)
                recipient.AwardStoryFragments(minimumFragments, maximumFragments);
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            Defeated?.Invoke(cause);
            Died?.Invoke();
            if (despawnOnDeath) StartCoroutine(DisableAfterDeath());
        }
        IEnumerator DisableAfterDeath()
        {
            yield return new WaitForSeconds(deathDelay);
            gameObject.SetActive(false);
        }
    }
}
