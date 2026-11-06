using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
namespace Storybook.Editor
{
    [InitializeOnLoad]
    public static class EnemyHealthBarBuilder
    {
        const string Path="Assets/Storybook/UI/EnemyHealthBar.prefab";
        static EnemyHealthBarBuilder(){EditorApplication.delayCall+=Request;}
        static void Request()
        {
            if(!File.Exists(".utmp/build-enemy-bar") || EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Delete(".utmp/build-enemy-bar");try{Build();}catch(Exception ex){Debug.LogException(ex);}
        }
        static RectTransform Panel(string name, Transform parent, Color color, Vector2 min, Vector2 max, Vector2 inset)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            var rect=(RectTransform)go.transform;rect.SetParent(parent,false);rect.anchorMin=min;rect.anchorMax=max;
            rect.offsetMin=inset;rect.offsetMax=-inset;
            var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;return rect;
        }
        public static void Build()
        {
            Directory.CreateDirectory("Assets/Storybook/UI");AssetDatabase.Refresh();
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(Path);
            if(existing==null)
            {
                var root=new GameObject("Enemy Health Bar",typeof(RectTransform),typeof(Canvas),typeof(EnemyHealthBar));
                var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.sortingOrder=30;
                var border=Panel("Dark Border",root.transform,new Color(.035f,.025f,.03f,1),Vector2.zero,Vector2.one,Vector2.zero);
                var track=Panel("Empty Health",border,new Color(.16f,.055f,.065f,1),Vector2.zero,Vector2.one,new Vector2(2,1.5f));
                var fill=Panel("Health Fill",track,new Color(.75f,.08f,.1f,1),Vector2.zero,Vector2.one,Vector2.zero);fill.pivot=new Vector2(0,.5f);
                var so=new SerializedObject(root.GetComponent<EnemyHealthBar>());so.FindProperty("barCanvas").objectReferenceValue=canvas;so.FindProperty("fill").objectReferenceValue=fill;so.ApplyModifiedPropertiesWithoutUndo();
                canvas.enabled=false;existing=PrefabUtility.SaveAsPrefabAsset(root,Path);UnityEngine.Object.DestroyImmediate(root);
            }
            string monsterPath="Assets/Storybook/Enemies/CrabSoldier/CorruptedCrabSoldier.prefab";
            var monster=PrefabUtility.LoadPrefabContents(monsterPath);
            try
            {
                if(monster.GetComponentInChildren<EnemyHealthBar>(true)==null)PrefabUtility.InstantiatePrefab(existing,monster.transform);
                PrefabUtility.SaveAsPrefabAsset(monster,monsterPath);AssetDatabase.SaveAssets();
                Debug.Log("ENEMY_HP_BAR_BUILT: reusable world-space prefab linked to crab; HP code unchanged");
            }
            finally{PrefabUtility.UnloadPrefabContents(monster);}
        }
    }
}
