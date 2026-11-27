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
        [SerializeField] GroundShadow.Style groundShadow = GroundShadow.Style.Character;
        [Header("Story Fragments")]
        [SerializeField, Min(0)] int minimumFragments;
        [SerializeField, Min(0)] int maximumFragments;
        PlayerHealth fragmentRecipient, killRecipient;
        EnemyTimeStopController timeStop;

        internal bool IsTimeStopped => timeStop != null && timeStop.IsStopped;
        internal double ActionTime => timeStop != null ? timeStop.ActionTime : Time.timeAsDouble;
        internal double UnscaledActionTime => timeStop != null ? timeStop.UnscaledActionTime : Time.unscaledTimeAsDouble;
        internal bool CanTimeStop => CanTakeDamage && (timeStop == null || timeStop.CanStart);

        internal bool TryTimeStop(float duration, float immunity, Sprite icon)
        {
            if (!CanTimeStop || duration <= 0f) return false;
            if (timeStop == null) timeStop = gameObject.AddComponent<EnemyTimeStopController>();
            timeStop.Begin(duration, immunity, icon);
            return true;
        }

        void OnDisable()
        {
            if (timeStop != null) timeStop.Cancel();
        }

        void OnValidate()
        {
            minimumFragments = Mathf.Max(0, minimumFragments);
            maximumFragments = Mathf.Max(minimumFragments, maximumFragments);
        }

        internal void SetFragmentRecipient(PlayerHealth recipient) => fragmentRecipient = recipient;
        internal void SetKillRecipient(PlayerHealth recipient) => killRecipient = recipient;

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
        bool crownTriggered;
        TurtleGuardAI guard;
        MonsterCombatAI combatAI;

        void Awake()
        {
            currentHP = maxHP;
            TryGetComponent(out guard);
            TryGetComponent(out combatAI);
            GroundShadow.Attach(transform, groundShadow);
        }

        public override void ReceiveDamage(float damage, Vector3 source)
        {
            ApplyDamage(DirectedDamage(damage, source), source);
        }
        public override void ReceiveDamage(float damage) => ApplyDamage(damage, null);

        float DirectedDamage(float damage, Vector3 source) => guard != null ? guard.GuardDamage(damage, source) : damage;

        internal bool ReceivePlayerAttack(float damage, Vector3 source, FairyTaleRelicInventory relics)
        {
            var crown = !crownTriggered && Mathf.Approximately(currentHP, maxHP) ? relics?.Find<BrokenCrownRelic>() : null;
            float crownBonus = crown != null ? crown.DamageBonus : 0f;
            float total = damage * (1f + (relics?.AttackBonus ?? 0f) + crownBonus);
            return ApplyDamage(DirectedDamage(total, source), source, crownBonus > 0f ? crown : null);
        }

        internal void ReceiveSkillDamage(float damage, Vector3 source, SkillHit skill, Vector3? contact = null,
            ClearingCamera camera = null, FairyTaleRelicInventory relics = null)
        {
            if (!ReceivePlayerAttack(damage, source, relics) || (combatAI == null && skill != SkillHit.CloneSlash)) return;
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

        bool ApplyDamage(float damage, Vector3? source, BrokenCrownRelic crown = null)
        {
            if (!CanTakeDamage || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return false;
            float remaining = Mathf.Max(0, currentHP - damage);
            if (remaining >= currentHP) return false;

            if (source.HasValue && combatAI != null) combatAI.TrackDamageDirection(source.Value);
            currentHP = remaining;
            if (crown != null)
            {
                crownTriggered = true;
                crown.ShowImpact(this);
            }

            if (!IsDead) Damaged?.Invoke();
            else FinishDeath(MonsterDeathCause.PlayerKilled);
            return true;
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
            if (timeStop != null) timeStop.Cancel();
            crownTriggered = false;
            DeathCause = cause;
            var recipient = fragmentRecipient;
            fragmentRecipient = null;
            if (cause == MonsterDeathCause.PlayerKilled)
            {
                recipient?.AwardStoryFragments(minimumFragments, maximumFragments);
                (killRecipient != null ? killRecipient : recipient)?.RecordMonsterKill();
            }
            killRecipient = null;
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
