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
        internal bool HasDefeatPresentation { get; private set; }
        internal bool HasSkillHitFeedback => combatAI != null;
        TurtleGuardAI guard;
        MonsterCombatAI combatAI;

        void Awake()
        {
            currentHP = maxHP;
            TryGetComponent(out guard);
            TryGetComponent(out combatAI);
        }

        public override void ReceiveDamage(float damage, Vector3 source)
        {
            ApplyDamage(guard != null ? guard.GuardDamage(damage, source) : damage, source);
        }
        public override void ReceiveDamage(float damage) => ApplyDamage(damage, null);

        internal void ReceiveSkillDamage(float damage, Vector3 source, SkillHit skill, Vector3? contact = null,
            ClearingCamera camera = null)
        {
            float before = currentHP;
            ReceiveDamage(damage, source);
            if (currentHP >= before || (combatAI == null && skill != SkillHit.CloneSlash)) return;
            if (!IsDead) combatAI?.ReactToSkillHit(source, skill, contact);
            ShowCombatShake(camera, skill);
        }

        internal void ShowCombatShake(ClearingCamera camera, SkillHit? skill = null)
        {
            if (camera == null) return;
            if (HasDefeatPresentation || skill == null || skill == SkillHit.AzureSlash)
                camera.RequestCombatShake(HasDefeatPresentation, skill == SkillHit.AzureSlash);
            else if (skill == SkillHit.CloneSlash)
                camera.RequestCloneSlashShake();
        }

        void ApplyDamage(float damage, Vector3? source)
        {
            if (!CanTakeDamage || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return;
            if (source.HasValue && combatAI != null) combatAI.TrackDamageDirection(source.Value);
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
            float visualDuration = 0f;
            if (cause == MonsterDeathCause.PlayerKilled && combatAI != null)
                visualDuration = combatAI.ShowDefeat();
            HasDefeatPresentation = visualDuration > 0f;
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            Defeated?.Invoke(cause);
            Died?.Invoke();
            if (despawnOnDeath) StartCoroutine(DisableAfterDeath(Mathf.Max(deathDelay, visualDuration)));
        }
        IEnumerator DisableAfterDeath(float delay)
        {
            yield return new WaitForSeconds(delay);
            gameObject.SetActive(false);
        }
    }
}
