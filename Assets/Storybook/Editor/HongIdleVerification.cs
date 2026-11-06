using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace Storybook.Editor
{
    [InitializeOnLoad]
    public static class HongIdleVerification
    {
        static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static HongGildong player;
        static HongGildongIdleVisual idle;
        static SpriteRenderer body;
        static int stage,checks;
        static float next;
        static Vector3 rootPosition,bodyScale;
        static object Get(object target,string name)=>target.GetType().GetField(name,Flags).GetValue(target);
        static void Set(object target,string name,object value)=>target.GetType().GetField(name,Flags).SetValue(target,value);
        static void Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Flags).Invoke(target,args);
        static void Check(bool ok,string message){if(!ok)throw new Exception("HONG_IDLE_TEST_FAIL: "+message);checks++;Debug.Log("HONG_IDLE_CHECK_PASS: "+message);}
        static HongIdleVerification()
        {
            EditorApplication.playModeStateChanged+=State;
            EditorApplication.delayCall+=()=>{if(!File.Exists(".utmp/test-hong-idle") || EditorApplication.isPlayingOrWillChangePlaymode)return;File.Delete(".utmp/test-hong-idle");SessionState.SetBool("HongIdleTest",true);EditorApplication.EnterPlaymode();};
        }
        static void State(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("HongIdleTest",false))return;SessionState.SetBool("HongIdleTest",false);
            foreach(var ai in UnityEngine.Object.FindObjectsByType<CrabSoldierAI>(FindObjectsSortMode.None))ai.enabled=false;
            player=UnityEngine.Object.FindFirstObjectByType<HongGildong>();idle=player.GetComponentInChildren<HongGildongIdleVisual>();body=(SpriteRenderer)Get(player,"body");
            bodyScale=body.transform.localScale;rootPosition=player.transform.position;player.PrepareForAction();stage=0;next=Time.time+.15f;EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            if(Time.time<next)return;
            try
            {
                if(stage==0)
                {
                    Check(idle.IsPlaying && !body.enabled,"Stationary player uses idle visual surface");
                    Check(Mathf.Abs((float)Get(idle,"loopDuration")-1.5f)<.001f,"Idle loop is 1.5 seconds");
                    Call(idle,"ApplyPose",0f);var start=(Vector3[])((Mesh)Get(idle,"mesh")).vertices.Clone();
                    Call(idle,"ApplyPose",.5f);var peak=((Mesh)Get(idle,"mesh")).vertices;var normalized=(Vector2[])Get(idle,"normalized");bool feet=true,upper=false;
                    for(int i=0;i<start.Length;i++){if(normalized[i].y<=.2f && Vector3.Distance(start[i],peak[i])>.00001f)feet=false;if(normalized[i].y>.5f && Vector3.Distance(start[i],peak[i])>.001f)upper=true;}
                    Check(feet && upper,"Feet fixed while upper-body pixels breathe");
                    Call(idle,"ApplyPose",1f);var end=((Mesh)Get(idle,"mesh")).vertices;bool loop=true;
                    for(int i=0;i<start.Length;i++)if(Vector3.Distance(start[i],end[i])>.00001f)loop=false;
                    Check(loop,"Loop endpoints are continuous");
                    Check(Vector3.ProjectOnPlane(player.transform.position-rootPosition,Vector3.up).magnitude<.001f && body.transform.localScale==bodyScale,"Idle does not move player root or resize body");
                    Capture();
                    Call(player,"SetDestination",player.transform.position+Vector3.right);Call(idle,"LateUpdate");
                    Check(!idle.IsPlaying && body.enabled,"Right-click destination immediately stops idle");stage=1;next=Time.time+.2f;
                }
                else if(stage==1)
                {
                    Check(!idle.IsPlaying && player.HasMoveDestination,"Idle stays off while actual movement proceeds");
                    var camera=(Camera)Get(player,"fieldCamera");Call(player,"BeginSwing",(Vector2)camera.WorldToScreenPoint(player.transform.position+Vector3.forward));Call(idle,"LateUpdate");
                    Check(player.Attacking && !idle.IsPlaying && body.enabled,"Attack retains original sprite rendering");stage=2;next=Time.time+.5f;
                }
                else if(stage==2)
                {
                    Check(!player.Attacking && idle.IsPlaying,"Idle resumes after attack recovery");
                    var camera=(Camera)Get(player,"fieldCamera");Call(player,"BeginDash",(Vector2)camera.WorldToScreenPoint(player.transform.position+Vector3.right));Call(idle,"LateUpdate");
                    Check(player.IsDashing && !idle.IsPlaying && body.enabled,"Dash disables idle without changing dash sprite");stage=3;next=Time.time+.35f;
                }
                else if(stage==3)
                {
                    Check(!player.IsDashing && idle.IsPlaying,"Idle resumes after dash stops");
                    Set(player,"back",true);Set(player,"left",true);stage=4;next=Time.time+.15f;
                }
                else
                {
                    Check(idle.IsPlaying && Get(idle,"source")==Get(player,"idleBack") && body.flipX,"Back-facing and mirrored idle use original back sprite");
                    var cc=player.GetComponent<CharacterController>();Check(Mathf.Abs(cc.height-.72f)<.001f && Mathf.Abs(cc.radius-.17f)<.001f,"Player collider dimensions unchanged");
                    player.enabled=false;Call(idle,"LateUpdate");Check(!idle.IsPlaying && body.enabled,"Disabled or dead player releases idle rendering");
                    Debug.Log("HONG_IDLE_TEST_PASSED: "+checks);EditorApplication.update-=Tick;EditorApplication.ExitPlaymode();
                }
            }
            catch(Exception ex){Debug.LogException(ex);EditorApplication.update-=Tick;EditorApplication.ExitPlaymode();}
        }
        static void Capture()
        {
            var camera=(Camera)Get(player,"fieldCamera");var rt=new RenderTexture(1280,720,24);var old=camera.targetTexture;var active=RenderTexture.active;
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
            File.WriteAllBytes("C:/Users/Admin/Documents/ChatGPT/Unity 포트폴리오/hong-idle.png",image.EncodeToPNG());camera.targetTexture=old;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}
