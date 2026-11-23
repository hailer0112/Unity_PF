using UnityEngine;
using UnityEngine.Serialization;
namespace Storybook
{
    public sealed class CrabSoldierAI : MonsterCombatAI
    {
        public enum State { Idle, Chase, Windup, Slam, Recovery, Dead }
        [SerializeField, Min(.1f), FormerlySerializedAs("attackRange")] float attackStartRange = 1.3f;

        const int WarningSegments = 64;
        [Header("Attack Circle (meters)")]
        [SerializeField, Min(.1f)] float hitCenterOffset = .70f;
        [SerializeField, Min(.1f)] float hitRadius = .65f;

        [SerializeField, Min(0)] float attackDamage=15;
        [SerializeField] State state;
        CrabAttackVisual visual;
        LineRenderer warning;
        ChargedAttackWarning chargedWarning;
        float previousElapsed;
        Vector3 attackCenter, attackDirection;
        float attackRadius;
        bool attemptedHit;
        public State CurrentState => state;
        public Vector3 LockedDirection => attackDirection;
        protected override void Awake()
        {
            base.Awake();
            visual=GetComponentInChildren<CrabAttackVisual>();
            var go=new GameObject("Claw Slam Warning");go.transform.SetParent(transform,false);warning=go.AddComponent<LineRenderer>();
            warning.useWorldSpace=true;warning.loop=true;warning.positionCount=WarningSegments;warning.startWidth=warning.endWidth=.025f;
            warning.sharedMaterial=GetComponentInChildren<SpriteRenderer>().sharedMaterial;warning.startColor=warning.endColor=new Color(1,.25f,.05f,.85f);
            warning.enabled=false;warning.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            chargedWarning=GetComponent<ChargedAttackWarning>();if(chargedWarning==null)chargedWarning=gameObject.AddComponent<ChargedAttackWarning>();chargedWarning.BindOutline(warning);
        }
        protected override void ResetBehavior(bool dead)
        {
            state = dead ? State.Dead : State.Idle;
            CancelAttack();
        }
        void CancelAttack(){if(warning!=null)warning.enabled=false;if(chargedWarning!=null)chargedWarning.Hide();if(visual!=null)visual.End();}
        protected override void TickBehavior()
        {
            if(state==State.Windup || state==State.Slam || state==State.Recovery){TickAttack();return;}
            if(chargedWarning.IsVisible)CancelAttack();
            Vector3 delta = PlanarOffset(target.transform.position);
            float distance = delta.magnitude;
            if (!TracksTarget(distance))
            {
                state = State.Idle;
                return;
            }
            if(distance<=attackStartRange && !Occluded())
            {
                state=State.Idle;
                if(AttackReady)BeginAttack(delta);
                return;
            }
            state=State.Chase;
            float clearance = (controller.radius + controller.skinWidth) *
                Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
            Vector3 waypoint = PalaceCover.NextPoint(transform.position, target.transform.position, clearance);
            Vector3 step = PlanarOffset(waypoint);
            MoveOnGround(step.normalized, step.magnitude);
        }
        void BeginAttack(Vector3 delta)
        {
            attackDirection=delta.sqrMagnitude>.0001f?delta.normalized:Vector3.forward;
            attackRadius = hitRadius;
            attackCenter = transform.position + attackDirection * hitCenterOffset;
            BeginAttackTiming();previousElapsed=0;attemptedHit=false;state=State.Windup;
            visual.Begin(attackDirection);DrawWarning();
        }
        void DrawWarning()
        {
            Vector3 groundCenter = attackCenter + Vector3.up * .025f;
            for (int i = 0; i < WarningSegments; i++)
            {
                float angle = i * (2f * Mathf.PI / WarningSegments);
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                warning.SetPosition(i, groundCenter + radial * attackRadius);
            }
            warning.enabled = true;
            chargedWarning.BeginCircle(groundCenter, attackRadius);
        }
        void TickAttack()
        {
            float elapsed=AttackElapsed,contact=windupTime,end=windupTime+activeTime;
            visual.Pose(elapsed,windupTime,activeTime,recoveryTime);
            chargedWarning.SetProgress(elapsed<contact?Mathf.Min(elapsed/Mathf.Max(.0001f,contact),.99999f):1f);
            // Contact is aligned to the claw reaching the floor. Catch skipped frames once.
            if(elapsed>=contact && previousElapsed<end && !attemptedHit)TryHit();
            AttackPhase phase = AttackPhaseAt(elapsed);
            if(phase == AttackPhase.Windup)state=State.Windup;
            else if(phase == AttackPhase.Active)state=State.Slam;
            else if(phase == AttackPhase.Recovery){state=State.Recovery;warning.enabled=false;chargedWarning.Hide();}
            else{CancelAttack();state=State.Idle;}
            previousElapsed=elapsed;
        }
        bool Occluded()
        {
            Vector3 start=transform.position+Vector3.up*.35f,to=target.transform.position+Vector3.up*.35f-start;
            foreach(var hit in Physics.RaycastAll(start,to.normalized,to.magnitude,~0,QueryTriggerInteraction.Ignore))
                if(!hit.collider.transform.IsChildOf(transform) && !hit.collider.transform.IsChildOf(target.transform))return true;
            return false;
        }
        void TryHit()
        {
            // The same ground footprint drives the warning and the damage check.
            // Checking the target root prevents its capsule rim from extending the range.
            Vector3 offset = Vector3.ProjectOnPlane(target.transform.position - attackCenter, Vector3.up);
            if (offset.sqrMagnitude > attackRadius * attackRadius) return;

            foreach (var collider in Physics.OverlapCapsule(attackCenter + Vector3.up * .35f,
                attackCenter + Vector3.up * .45f, attackRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                if(collider.GetComponentInParent<PlayerHealth>()!=target)continue;
                ApplyHit();
                break;
            }
        }

        void ApplyHit()
        {
            attemptedHit = true;
            if (!target.IsDead && !Occluded()) target.TryTakeDamage(attackDamage);
        }
    }
}
