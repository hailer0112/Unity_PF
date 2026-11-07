using UnityEngine;
using UnityEngine.Serialization;
namespace Storybook
{
    [RequireComponent(typeof(MonsterHealth),typeof(CharacterController))]
    public sealed class CrabSoldierAI : MonoBehaviour
    {
        public enum State { Idle, Chase, Windup, Slam, Recovery, Dead }
        [SerializeField] PlayerHealth target;
        [SerializeField, Min(0)] float moveSpeed = 1.6f;
        [SerializeField, Min(0)] float detectionRange = 4.5f;
        [SerializeField, Min(0)] float loseTargetRange = 6.5f;
        [SerializeField, Min(.1f), FormerlySerializedAs("attackRange")] float attackStartRange = 1.3f;

        [Header("Attack Hitbox Size (meters)")]
        [SerializeField, Min(.1f), InspectorName("Length")] float hitLength = 1.35f;
        [SerializeField, Min(.1f), InspectorName("Width")] float hitWidth = .825f;

        [Header("Attack Timing")]
        [SerializeField, Min(.01f)] float windupTime=.6f,activeTime=.15f,recoveryTime=.7f;
        [SerializeField, Min(0)] float attackDamage=15,attackInterval=2;
        [SerializeField] State state;
        MonsterHealth health;
        CharacterController controller;
        CrabAttackVisual visual;
        LineRenderer warning;
        ChargedAttackWarning chargedWarning;
        float attackStarted,nextAttack,previousElapsed;
        Vector3 attackOrigin, attackDirection, attackRight;
        Vector2 attackSize;
        bool hasDetectedTarget;
        bool attemptedHit;
        public State CurrentState => state;
        public Vector3 LockedDirection => attackDirection;
        void Awake()
        {
            health=GetComponent<MonsterHealth>();controller=GetComponent<CharacterController>();visual=GetComponentInChildren<CrabAttackVisual>();
            var go=new GameObject("Claw Slam Warning");go.transform.SetParent(transform,false);warning=go.AddComponent<LineRenderer>();
            warning.useWorldSpace=true;warning.loop=true;warning.positionCount=4;warning.startWidth=warning.endWidth=.025f;
            warning.sharedMaterial=GetComponentInChildren<SpriteRenderer>().sharedMaterial;warning.startColor=warning.endColor=new Color(1,.25f,.05f,.85f);
            warning.enabled=false;warning.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            chargedWarning=GetComponent<ChargedAttackWarning>();if(chargedWarning==null)chargedWarning=gameObject.AddComponent<ChargedAttackWarning>();chargedWarning.BindOutline(warning);
        }
        void Start(){if(target==null)target=FindFirstObjectByType<PlayerHealth>();}
        void OnEnable(){health.Died+=Die;}
        void OnDisable(){health.Died-=Die;CancelAttack();}
        void Die(){state=State.Dead;CancelAttack();}
        void CancelAttack(){if(warning!=null)warning.enabled=false;if(chargedWarning!=null)chargedWarning.Hide();if(visual!=null)visual.End();}
        void Update()
        {
            if(health.IsDead){if(state!=State.Dead)Die();return;}
            if(target==null || !target.isActiveAndEnabled || target.IsDead){CancelAttack();hasDetectedTarget=false;state=State.Idle;return;}
            if(state==State.Windup || state==State.Slam || state==State.Recovery){TickAttack();return;}
            if(chargedWarning.IsVisible)CancelAttack();
            Vector3 delta=Vector3.ProjectOnPlane(target.transform.position-transform.position,Vector3.up);float distance=delta.magnitude;
            // Keep awareness during cooldown and attack recovery as well as movement.
            float trackingRange = hasDetectedTarget ? Mathf.Max(detectionRange, loseTargetRange) : detectionRange;
            hasDetectedTarget = distance <= trackingRange;
            if (!hasDetectedTarget)
            {
                state = State.Idle;
                return;
            }
            if(distance<=attackStartRange)
            {
                state=State.Idle;
                if(Time.time>=nextAttack && !Occluded())BeginAttack(delta);
                return;
            }
            state=State.Chase;
            controller.Move(Vector3.ClampMagnitude(delta,moveSpeed*Time.deltaTime)+Vector3.down*(2*Time.deltaTime));
        }
        void BeginAttack(Vector3 delta)
        {
            attackDirection=delta.sqrMagnitude>.0001f?delta.normalized:Vector3.forward;
            attackOrigin = transform.position;
            attackRight = Vector3.Cross(Vector3.up, attackDirection);
            attackSize = new Vector2(hitWidth, hitLength);
            attackStarted=Time.time;nextAttack=attackStarted+attackInterval;previousElapsed=0;attemptedHit=false;state=State.Windup;
            visual.Begin();DrawWarning();
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
            float elapsed=Time.time-attackStarted,contact=windupTime,end=windupTime+activeTime;
            visual.Pose(elapsed,windupTime,activeTime,recoveryTime);
            chargedWarning.SetProgress(elapsed<contact?Mathf.Min(elapsed/Mathf.Max(.0001f,contact),.99999f):1f);
            // Contact is aligned to the temporary claw reaching the floor. Catch skipped frames once.
            if(elapsed>=contact && previousElapsed<end && !attemptedHit)TryHit();
            if(elapsed<windupTime)state=State.Windup;
            else if(elapsed<end)state=State.Slam;
            else if(elapsed<end+recoveryTime){state=State.Recovery;warning.enabled=false;chargedWarning.Hide();}
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
            // CharacterController movement can omit an ignored player body from
            // the overlap query. Its ground position already passed the exact footprint.
            if (target.TryGetComponent(out CharacterController playerBody) && playerBody.enabled &&
                Physics.GetIgnoreCollision(controller, playerBody))
            {
                Bounds body = playerBody.bounds;
                if (body.max.y >= center.y - .6f && body.min.y <= center.y + .6f)
                    ApplyHit();
                return;
            }

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
