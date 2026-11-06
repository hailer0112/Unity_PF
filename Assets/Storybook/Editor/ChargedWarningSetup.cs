using System;
using System.IO;
using UnityEngine;
using UnityEditor;
namespace Storybook.Editor
{
    [InitializeOnLoad]
    public static class ChargedWarningSetup
    {
        static ChargedWarningSetup(){EditorApplication.delayCall+=Run;}
        static void Run()
        {
            if(!File.Exists(".utmp/setup-charged-warning") || EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Delete(".utmp/setup-charged-warning");
            try
            {
                string materialPath="Assets/Storybook/UI/GroundWarningFill.mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(material==null){material=new Material(Shader.Find("Storybook/Ground Warning Fill"));AssetDatabase.CreateAsset(material,materialPath);}
                string path="Assets/Storybook/Enemies/CrabSoldier/CorruptedCrabSoldier.prefab";
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var warning=root.GetComponent<ChargedAttackWarning>();if(warning==null)warning=root.AddComponent<ChargedAttackWarning>();
                    var so=new SerializedObject(warning);so.FindProperty("fillMaterial").objectReferenceValue=material;so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root,path);AssetDatabase.SaveAssets();
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
                Debug.Log("CHARGED_WARNING_SETUP_OK: transparent fill material and crab component saved; no damage geometry changes");
            }
            catch(Exception ex){Debug.LogException(ex);}
        }
    }
}
