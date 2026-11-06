using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Storybook.Editor
{
    [InitializeOnLoad]
    public static class CrabAISetup
    {
        static CrabAISetup(){EditorApplication.delayCall+=Requested;}
        static void Requested()
        {
            if(!File.Exists(".utmp/setup-crab-ai") || EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Delete(".utmp/setup-crab-ai");try{Build();}catch(Exception ex){Debug.LogException(ex);}
        }
        static void Build()
        {
            var scene=EditorSceneManager.GetActiveScene();if(scene.isDirty)throw new Exception("Save current scene first; unsaved work preserved.");
            var player=UnityEngine.Object.FindFirstObjectByType<HongGildong>();
            if(player.GetComponent<PlayerHealth>()==null)player.gameObject.AddComponent<PlayerHealth>();
            string path="Assets/Storybook/Enemies/CrabSoldier/CorruptedCrabSoldier.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var capsule=root.GetComponent<CapsuleCollider>();
                var cc=root.GetComponent<CharacterController>();if(cc==null)cc=root.AddComponent<CharacterController>();
                cc.height=capsule.height;cc.radius=capsule.radius;cc.center=capsule.center;cc.skinWidth=.025f;cc.stepOffset=.1f;cc.minMoveDistance=0;
                capsule.enabled=false;
                var body=root.transform.Find("SpriteVisual");if(body.GetComponent<CrabAttackVisual>()==null)body.gameObject.AddComponent<CrabAttackVisual>();
                if(root.GetComponent<CrabSoldierAI>()==null)root.AddComponent<CrabSoldierAI>();
                PrefabUtility.SaveAsPrefabAsset(root,path);AssetDatabase.SaveAssets();
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("CRAB_AI_SETUP_OK: CharacterController + AI + temporary claw pose; minimal player HP100; prefab and scene saved");
        }
    }
}
