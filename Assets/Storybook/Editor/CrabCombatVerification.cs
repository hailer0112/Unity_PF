using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace Storybook.Editor
{
    [InitializeOnLoad]
    public static class CrabCombatVerification
    {
        static MonsterHealth test;
        static double deadline;
        static int checks;
        static HongGildong checkedPlayer;
        static Camera checkedCamera;
        static CrabSpriteAnimation checkedAnimation;
        static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static CrabCombatVerification()
        {
            EditorApplication.playModeStateChanged += State;
            EditorApplication.delayCall += () => {
                if (!File.Exists(".utmp/test-crab-request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
                File.Delete(".utmp/test-crab-request");SessionState.SetBool("CrabTest",true);EditorApplication.EnterPlaymode();
            };
        }
        static void Check(bool ok,string text) { if(!ok)throw new Exception("CRAB_TEST_FAIL: "+text);checks++;Debug.Log("CRAB_CHECK_PASS: "+text); }
        static void State(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("CrabTest",false))return;
            SessionState.SetBool("CrabTest",false);
            try
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Storybook/Enemies/CrabSoldier/CorruptedCrabSoldier.prefab");
                test=UnityEngine.Object.Instantiate(prefab).GetComponent<MonsterHealth>();
                var player=UnityEngine.Object.FindFirstObjectByType<HongGildong>();
                checkedPlayer=player;checkedCamera=UnityEngine.Object.FindFirstObjectByType<ClearingCamera>().GetComponent<Camera>();
                Check(checkedCamera.orthographic && Mathf.Abs(checkedCamera.orthographicSize-Mathf.Max(2.6f,3.9f/checkedCamera.aspect))<.02f,"Zoomed orthographic camera");
                Vector3 ground=player.transform.position+Vector3.right;ground.y=0;
                Ray ray=checkedCamera.ScreenPointToRay(checkedCamera.WorldToScreenPoint(ground));
                new Plane(Vector3.up,Vector3.zero).Raycast(ray,out float rayDistance);
                Check(Vector3.Distance(ray.GetPoint(rayDistance),ground)<.001f,"Mouse world projection roundtrip after camera tilt");
                checkedAnimation=UnityEngine.Object.FindObjectsByType<CrabSpriteAnimation>(FindObjectsSortMode.None)[0];
                var animationSettings=new SerializedObject(checkedAnimation);
                Check(animationSettings.FindProperty("idleFrames").arraySize==4 && animationSettings.FindProperty("walkFrames").arraySize==4,"Idle and walk each have four frames");
                animationSettings.FindProperty("previewWalk").boolValue=true;animationSettings.ApplyModifiedPropertiesWithoutUndo();
                test.transform.position=player.transform.position+Vector3.forward;
                Physics.SyncTransforms();
                Check(test.CurrentHP==100,"Initial HP 100");
                Check((float)typeof(HongGildong).GetField("attackDamage",Flags).GetValue(player)==25,"Player damage 25");
                typeof(HongGildong).GetField("slashDirection",Flags).SetValue(player,Vector3.forward);
                var hits=typeof(HongGildong).GetField("hitThisSwing",Flags).GetValue(player);
                var strike=typeof(HongGildong).GetMethod("StrikeTargets",Flags);
                for(int i=1;i<=4;i++)
                {
                    hits.GetType().GetMethod("Clear").Invoke(hits,null);
                    strike.Invoke(player,new object[]{-68f,68f});
                    strike.Invoke(player,new object[]{-68f,68f});
                    Check(test.CurrentHP==100-25*i,"Swing "+i+" damages exactly once");
                }
                Check(test.IsDead,"Fourth hit sets dead state");
                Check(!test.GetComponent<Collider>().enabled,"Death disables collider immediately");
                test.ReceiveDamage(25);Check(test.CurrentHP==0 && !test.CanTakeDamage,"Dead target ignores extra damage");
                player.transform.position+=Vector3.right;
                deadline=EditorApplication.timeSinceStartup+1.2;EditorApplication.update+=Wait;
            }
            catch(Exception ex) { Debug.LogException(ex);EditorApplication.ExitPlaymode(); }
        }
        static void Wait()
        {
            if(EditorApplication.timeSinceStartup<deadline)return;
            EditorApplication.update-=Wait;
            try { Check(test!=null && !test.gameObject.activeSelf,"Death delay disables enemy");Vector3 focus=new Vector3(checkedPlayer.transform.position.x,0,checkedPlayer.transform.position.z);
                Check(Vector3.Distance(checkedCamera.transform.position,focus+new Vector3(0,10,-8.141f))<.15f,"Camera follows player displacement");
                Debug.Log("CRAB_COMBAT_TEST_PASSED: "+checks); }
            catch(Exception ex) { Debug.LogException(ex); }
            EditorApplication.ExitPlaymode();
        }
    }
}
// Run readability regression suite.
