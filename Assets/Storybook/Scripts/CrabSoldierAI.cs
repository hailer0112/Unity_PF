using UnityEngine;
using UnityEngine.Serialization;
namespace Storybook
{
    public sealed class CrabSoldierAI : MonsterCombatAI
    {
        public enum State { Idle, Chase, Windup, Slam, Recovery, Dead }
        [SerializeField, Min(.1f), FormerlySerializedAs("attackRange")] float attackStartRange = 1.3f;

        [Header("Attack Hitbox Size (meters)")]
        [SerializeField, Min(.1f), InspectorName("Length")] float hitLength = 1.35f;
        [SerializeField, Min(.1f), InspectorName("Width")] float hitWidth = .825f;

        [SerializeField, Min(0)] float attackDamage=15;
        [SerializeField] State state;
        CrabAttackVisual visual;
        LineRenderer warning;
        ChargedAttackWarning chargedWarning;
        float previousElapsed;
        Vector3 attackOrigin, attackDirection, attackRight;
        Vector2 attackSize;
        bool attemptedHit;
        public State CurrentState => state;
        public Vector3 LockedDirection => attackDirection;
        protected override void Awake()
        {
            base.Awake();
            visual=GetComponentInChildren<CrabAttackVisual>();
            var go=new GameObject("Claw Slam Warning");go.transform.SetParent(transform,false);warning=go.AddComponent<LineRenderer>();
            warning.useWorldSpace=true;warning.loop=true;warning.positionCount=4;warning.startWidth=warning.endWidth=.025f;
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
            attackOrigin = transform.position;
            attackRight = Vector3.Cross(Vector3.up, attackDirection);
            attackSize = new Vector2(hitWidth, hitLength);
            BeginAttackTiming();previousElapsed=0;attemptedHit=false;state=State.Windup;
            visual.Begin(attackDirection);DrawWarning();
        }
        void DrawWarning()
        {
            Vector3 side = attackRight * (attackSize.x * .5f);
            Vector3 origin = attackOrigin + Vector3.up * .025f;
            Vector3 end = origin + attackDirection * attackSize.y;
            warning.SetPositions(new[] { origin - side, origin + side, end + side, end - side });
            warning.enabled = true;
            chargedWarning.Begin(origin, attackDirection, attackSize.x, attackSize.y);
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
            Vector3 offset = target.transform.position - attackOrigin;
            float forward = Vector3.Dot(offset, attackDirection);
            float sideways = Mathf.Abs(Vector3.Dot(offset, attackRight));
            if (forward < 0f || forward > attackSize.y || sideways > attackSize.x * .5f) return;

            Vector3 center = attackOrigin + Vector3.up * .4f + attackDirection * (attackSize.y * .5f);
            foreach(var collider in Physics.OverlapBox(center,new Vector3(attackSize.x * .5f, .6f, attackSize.y * .5f),Quaternion.LookRotation(attackDirection),~0,QueryTriggerInteraction.Ignore))
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
