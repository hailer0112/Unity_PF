using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
namespace Storybook.Editor
{
    [InitializeOnLoad]
    public static class CrabAIVerification
    {
        static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        static CrabSoldierAI ai;
        static PlayerHealth player;
        static GameObject wall;
        static int stage,checks;
        static float deadline;
        static object Get(object obj,string field)=>obj.GetType().GetField(field,Flags).GetValue(obj);
        static void Set(object obj,string field,object value)=>obj.GetType().GetField(field,Flags).SetValue(obj,value);
        static void Call(object obj,string method,params object[] args)=>obj.GetType().GetMethod(method,Flags).Invoke(obj,args);
        static void Check(bool ok,string text){if(!ok)throw new Exception("CRAB_AI_TEST_FAIL: "+text);checks++;Debug.Log("CRAB_AI_CHECK_PASS: "+text);}
        static CrabAIVerification()
        {
            EditorApplication.playModeStateChanged+=State;
            EditorApplication.delayCall+=()=>{if(!File.Exists(".utmp/test-crab-ai") || EditorApplication.isPlayingOrWillChangePlaymode)return;File.Delete(".utmp/test-crab-ai");SessionState.SetBool("CrabAITest",true);EditorApplication.EnterPlaymode();};
        }
        static void State(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("CrabAITest",false))return;
            SessionState.SetBool("CrabAITest",false);checks=0;
            try
            {
                foreach(var existing in UnityEngine.Object.FindObjectsByType<CrabSoldierAI>(FindObjectsSortMode.None))existing.enabled=false;
                player=UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();player.GetComponent<CharacterController>().enabled=false;player.transform.position=new Vector3(0,0,3);player.GetComponent<CharacterController>().enabled=true;
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Storybook/Enemies/CrabSoldier/CorruptedCrabSoldier.prefab");
                ai=UnityEngine.Object.Instantiate(prefab,Vector3.zero,Quaternion.identity).GetComponent<CrabSoldierAI>();Set(ai,"target",player);
                Check(player.CurrentHP==100,"Minimal player health starts at 100");
                stage=0;deadline=Time.time+.25f;EditorApplication.update+=Tick;
            }
            catch(Exception ex){Fail(ex);}
        }
        static void Fail(Exception ex){Debug.LogException(ex);EditorApplication.update-=Tick;EditorApplication.ExitPlaymode();}
        static void Place(Transform target,Vector3 position)
        {
            var cc=target.GetComponent<CharacterController>();cc.enabled=false;target.position=position;cc.enabled=true;Physics.SyncTransforms();
        }
        static void Begin(Vector3 direction)
        {
            Place(ai.transform,Vector3.zero);Place(player.transform,direction*1.2f);Call(ai,"BeginAttack",direction);
        }
        static void At(float seconds){Set(ai,"attackStarted",Time.time-seconds);Call(ai,"TickAttack");}
        static void Tick()
        {
            if(Time.time<deadline)return;
            try
            {
                if(stage==0)
                {
                    Check(ai.transform.position.z>.2f && ai.CurrentState==CrabSoldierAI.State.Chase,"Detection leads to real chase movement");
                    Check((bool)Get(ai.GetComponentInChildren<CrabSpriteAnimation>(),"wasWalking"),"Chase triggers existing Walk frames");
                    wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="AI Test Wall";wall.transform.position=new Vector3(0,.5f,1.3f);wall.transform.localScale=new Vector3(2,1,.15f);Physics.SyncTransforms();
                    stage=1;deadline=Time.time+.65f;
                }
                else
                {
                    Check(ai.transform.position.z<1.12f,"CharacterController stops at wall");
                    UnityEngine.Object.DestroyImmediate(wall);Physics.SyncTransforms();ai.enabled=false;
                    Begin(Vector3.forward);Vector3 locked=ai.LockedDirection;
                    Check(ai.CurrentState==CrabSoldierAI.State.Windup && ((LineRenderer)Get(ai,"warning")).enabled,"Windup shows grounded warning");
                    Check(Mathf.Abs((float)Get(ai,"nextAttack")-(float)Get(ai,"attackStarted")-2)<.001f,"Interval is measured from attack start");
                    VerifyAttachment();At(.5f);Check(player.CurrentHP==100,"No damage during windup");Capture("crab-ai-windup.png");
                    Place(player.transform,Vector3.right*1.2f);At(.67f);
                    Check(ai.LockedDirection==locked && player.CurrentHP==100,"Moving outside locked attack zone avoids damage");
                    Begin(Vector3.forward);
                    var hong=player.GetComponent<HongGildong>();var camera=UnityEngine.Object.FindFirstObjectByType<ClearingCamera>().GetComponent<Camera>();
                    Call(hong,"BeginDash",(Vector2)camera.WorldToScreenPoint(player.transform.position+Vector3.right));
                    Check(hong.IsDashing && hong.IsInvulnerable,"Actual dash activation sets invulnerability");
                    At(.67f);Check(player.CurrentHP==100,"Dash invulnerability rejects claw hit");
                    Set(hong,"dashTimeLeft",0f);Set(hong,"invulnerableUntil",0d);hong.PrepareForAction();
                    Begin(Vector3.forward);At(.59f);Check(player.CurrentHP==100,"Downstroke before floor contact cannot damage");
                    At(.67f);Check(player.CurrentHP==85,"Floor contact deals exactly 15 damage");
                    At(.69f);Check(player.CurrentHP==85,"Same attack cannot deal duplicate damage");
                    At(.9f);Vector3 stopped=ai.transform.position;Check(ai.CurrentState==CrabSoldierAI.State.Recovery,"Contact transitions into recovery");
                    At(1.1f);Check(ai.transform.position==stopped,"Recovery does not move enemy");
                    At(1.46f);Check(ai.CurrentState==CrabSoldierAI.State.Idle,"Recovery ends and releases attack state");
                    Begin(Vector3.forward);ai.enabled=true;
                    var health=ai.GetComponent<MonsterHealth>();health.ReceiveDamage(25);
                    Check(health.CurrentHP==75 && ai.CurrentState==CrabSoldierAI.State.Windup && ai.GetComponentInChildren<EnemyHealthBar>().IsVisible,"Existing hit and HP bar work without cancelling windup");
                    health.ReceiveDamage(100);
                    Check(ai.CurrentState==CrabSoldierAI.State.Dead && !((LineRenderer)Get(ai,"warning")).enabled,"Death cancels warning and attack immediately");
                    Check(!ai.GetComponent<CharacterController>().enabled,"Dead enemy collision is disabled");
                    float before=player.CurrentHP;Call(ai,"Update");Check(player.CurrentHP==before,"Dead enemy cannot leave damage behind");
                    var second=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Storybook/Enemies/CrabSoldier/CorruptedCrabSoldier.prefab"),Vector3.zero,Quaternion.identity).GetComponent<CrabSoldierAI>();
                    Set(second,"target",player);Call(second,"BeginAttack",Vector3.forward);player.TryTakeDamage(1000);Call(second,"Update");
                    Check(player.IsDead && !hong.enabled,"Player death stops player controls");
                    Check(second.CurrentState==CrabSoldierAI.State.Idle && !((LineRenderer)Get(second,"warning")).enabled,"Player death cancels enemy pursuit and attack");
                    Check(!player.TryTakeDamage(15),"Dead player rejects additional damage");
                    Debug.Log("CRAB_AI_TEST_PASSED: "+checks);EditorApplication.update-=Tick;EditorApplication.ExitPlaymode();
                }
            }
            catch(Exception ex){Fail(ex);}
        }
        static void VerifyAttachment()
        {
            var visual=ai.GetComponentInChildren<CrabAttackVisual>();
            var directionalAnimator = visual.GetComponent<Animator>();
            if (directionalAnimator != null)
            {
                var renderer = visual.GetComponent<SpriteRenderer>();
                Call(visual, "Pose", .59f, .6f, .15f, .7f);
                var raise = renderer.sprite;
                Check(renderer.enabled && raise.name.EndsWith("_Raise"), "Directional windup uses a full-body raised claw frame");
                Call(visual, "Pose", .6f, .6f, .15f, .7f);
                Check(renderer.enabled && renderer.sprite.name.EndsWith("_Impact") &&
                    raise.texture == renderer.sprite.texture, "Directional contact uses a full-body frame from the same atlas");
                Check(Get(visual, "shoulderJoint") == null, "Directional attack does not split the shoulder into temporary sprites");
                return;
            }

            var joint=(Transform)Get(visual,"shoulderJoint");var pieces=(SpriteRenderer[])Get(visual,"pieces");var cap=(SpriteRenderer)Get(visual,"shoulderCover");
            Vector3 anchor=joint.localPosition,offset=pieces[3].transform.localPosition,scale=pieces[3].transform.localScale;
            Check(pieces[3].transform.parent==joint,"Moving claw is parented to fixed shoulder joint");
            var sourceBody=(SpriteRenderer)Get(visual,"body");
            Check((pieces[3].sprite.rect.y-sourceBody.sprite.rect.y)/sourceBody.sprite.rect.height<.2f,"Claw crop includes lower pincer tips instead of leaving them on the torso");
            Check(cap.sortingOrder>pieces[3].sortingOrder,"Small shoulder cap renders over moving claw seam");
            Check(pieces[3].sortingOrder>pieces[0].sortingOrder && pieces[3].sortingOrder>pieces[2].sortingOrder,"Attack claw remains in front of torso instead of being clipped behind it");
            Check(cap.sprite.rect.width/pieces[3].sprite.rect.width<.5f,"Joint cap covers only a small proximal region");
            Check(visual.GetComponentInParent<UnityEngine.Rendering.SortingGroup>()==null,"No parent Sorting Group overrides attack part ordering");
            foreach(float elapsed in new[]{0f,.15f,.3f,.59f,.6f,.74f,.9f,1.1f,1.45f})
            {
                Call(visual,"Pose",elapsed,.6f,.15f,.7f);
                Check(joint.localPosition==anchor && pieces[3].transform.localPosition==offset && pieces[3].transform.localScale==scale,"Joint and arm attachment remain fixed at "+elapsed);
                if(elapsed==.59f){Capture("crab-arm-windup.png");CaptureClose("crab-arm-windup-close.png");}
                if(elapsed==.6f){Capture("crab-arm-impact.png");CaptureClose("crab-arm-impact-close.png");}
                if(elapsed==1.1f){Capture("crab-arm-recovery.png");CaptureClose("crab-arm-recovery-close.png");}
            }
            Check(Quaternion.Angle(joint.localRotation,Quaternion.identity)<.01f,"Shoulder rotation returns to rest after recovery");
        }
        static void CaptureClose(string filename)
        {
            var follow=UnityEngine.Object.FindFirstObjectByType<ClearingCamera>();var camera=follow.GetComponent<Camera>();
            var position=camera.transform.position;float size=camera.orthographicSize;bool wasEnabled=follow.enabled;
            var rt=new RenderTexture(640,640,24);var old=camera.targetTexture;var active=RenderTexture.active;
            try
            {
                follow.enabled=false;camera.orthographicSize=.85f;
                camera.transform.position=ai.transform.position+camera.transform.up*.5f-camera.transform.forward*10;
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                var image=new Texture2D(640,640,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,640,640),0,0);image.Apply();
                File.WriteAllBytes("C:/Users/Admin/Documents/ChatGPT/Unity 포트폴리오/"+filename,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
            }
            finally{camera.targetTexture=old;RenderTexture.active=active;camera.transform.position=position;camera.orthographicSize=size;follow.enabled=wasEnabled;UnityEngine.Object.DestroyImmediate(rt);}
        }
        static void Capture(string filename)
        {
            var camera=UnityEngine.Object.FindFirstObjectByType<ClearingCamera>().GetComponent<Camera>();var rt=new RenderTexture(1280,720,24);var old=camera.targetTexture;var active=RenderTexture.active;
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
            File.WriteAllBytes("C:/Users/Admin/Documents/ChatGPT/Unity 포트폴리오/"+filename,image.EncodeToPNG());camera.targetTexture=old;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}
