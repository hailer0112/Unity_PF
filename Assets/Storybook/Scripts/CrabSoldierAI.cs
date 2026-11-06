using UnityEngine;
namespace Storybook
{
    [RequireComponent(typeof(MonsterHealth),typeof(CharacterController))]
    public sealed class CrabSoldierAI : MonoBehaviour
    {
        public enum State { Idle, Chase, Windup, Slam, Recovery, Dead }
        [SerializeField] PlayerHealth target;
        [SerializeField, Min(0)] float detectionRange=8;
        [SerializeField, Min(.1f)] float attackRange=1.8f;
        [SerializeField, Min(0)] float moveSpeed=2.5f;
        [SerializeField, Min(.01f)] float windupTime=.6f,activeTime=.15f,recoveryTime=.7f;
        [SerializeField, Min(0)] float attackDamage=15,attackInterval=2;
        [SerializeField, Min(.1f)] float hitLength=1.8f,hitWidth=1.1f;
        [SerializeField] State state;
        MonsterHealth health;
        CharacterController controller;
        CrabAttackVisual visual;
        LineRenderer warning;
        ChargedAttackWarning chargedWarning;
        float attackStarted,nextAttack,previousElapsed;
        Vector3 attackDirection;
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
            if(target==null || !target.isActiveAndEnabled || target.IsDead){CancelAttack();state=State.Idle;return;}
            if(state==State.Windup || state==State.Slam || state==State.Recovery){TickAttack();return;}
            if(chargedWarning.IsVisible)CancelAttack();
            Vector3 delta=Vector3.ProjectOnPlane(target.transform.position-transform.position,Vector3.up);float distance=delta.magnitude;
            if(distance>detectionRange){state=State.Idle;return;}
            if(distance<=attackRange)
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
            attackStarted=Time.time;nextAttack=attackStarted+attackInterval;previousElapsed=0;attemptedHit=false;state=State.Windup;
            visual.Begin();DrawWarning();
        }
        void DrawWarning()
        {
            Vector3 side=Vector3.Cross(Vector3.up,attackDirection)*hitWidth*.5f;
            Vector3 origin=transform.position+Vector3.up*.025f,end=origin+attackDirection*hitLength;
            warning.SetPositions(new[]{origin-side,origin+side,end+side,end-side});warning.enabled=true;chargedWarning.Begin(origin,attackDirection,hitWidth,hitLength);
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
            Vector3 center=transform.position+Vector3.up*.4f+attackDirection*hitLength*.5f;
            foreach(var collider in Physics.OverlapBox(center,new Vector3(hitWidth*.5f,.6f,hitLength*.5f),Quaternion.LookRotation(attackDirection),~0,QueryTriggerInteraction.Ignore))
            {
                if(collider.GetComponentInParent<PlayerHealth>()!=target)continue;
                attemptedHit=true;
                if(!target.IsDead && !Occluded())target.TryTakeDamage(attackDamage);
                break;
            }
        }
    }
}
