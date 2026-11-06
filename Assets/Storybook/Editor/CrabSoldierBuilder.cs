using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
namespace Storybook.Editor
{
    [InitializeOnLoad]
    public static class CrabSoldierBuilder
    {
        const string Folder = "Assets/Storybook/Enemies/CrabSoldier";
        static CrabSoldierBuilder() { EditorApplication.delayCall += RequestedBuild; }
        static void RequestedBuild()
        {
            if (!File.Exists(".utmp/build-crab-request")) return;
            File.Delete(".utmp/build-crab-request");
            try { Build(); } catch(Exception ex) { Debug.LogException(ex); }
        }
        static Material Mat(string name, Color color, bool emissive = false)
        {
            string path = Folder + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            bool urp = GraphicsSettings.currentRenderPipeline != null;
            var material = new Material(Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard"));
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (emissive) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", Color.red * 4); }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
        static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, float z = 0)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name;
            go.transform.SetParent(parent, false); go.transform.localPosition = pos;
            go.transform.localScale = scale; go.transform.localRotation = Quaternion.Euler(0,0,z);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }
        [MenuItem("Storybook/Build Crab Soldier")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play mode before building crab.");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != "Assets/Storybook/Scenes/FirstClearing.unity" || scene.isDirty)
                throw new Exception("Open saved FirstClearing scene before building. Unsaved work preserved.");
            if (GameObject.Find("Corrupted Crab Soldier") != null) { Debug.Log("CRAB_ALREADY_EXISTS"); return; }
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            var shell = Mat("DarkTealShell", new Color(.035f,.23f,.24f));
            var plate = Mat("DamagedJadeArmor", new Color(.12f,.37f,.32f));
            var robe = Mat("TornNavyUniform", new Color(.045f,.07f,.16f));
            var gold = Mat("AgedGold", new Color(.63f,.43f,.13f));
            var corruption = Mat("PurpleCorruption", new Color(.24f,.015f,.18f));
            var eye = Mat("RedEmissiveEyes", Color.red, true);
            var root = new GameObject("Corrupted Crab Soldier");
            var visual = new GameObject("Visual").transform; visual.SetParent(root.transform,false);
            Part(visual,"Carapace",PrimitiveType.Sphere,new Vector3(0,.87f,0),new Vector3(.85f,.64f,.52f),shell);
            Part(visual,"Chest Armor",PrimitiveType.Cube,new Vector3(0,.83f,-.22f),new Vector3(.56f,.42f,.1f),plate);
            Part(visual,"Officer Belt",PrimitiveType.Cube,new Vector3(0,.62f,-.05f),new Vector3(.65f,.09f,.49f),gold);
            for(int side=-1;side<=1;side+=2)
            {
                Part(visual,"Leg " + side,PrimitiveType.Capsule,new Vector3(side*.20f,.28f,0),new Vector3(.19f,.27f,.19f),shell);
                Part(visual,"Boot " + side,PrimitiveType.Cube,new Vector3(side*.20f,.075f,-.075f),new Vector3(.23f,.14f,.34f),robe);
                Part(visual,"Shoulder " + side,PrimitiveType.Sphere,new Vector3(side*.43f,.94f,0),new Vector3(.29f,.24f,.32f),plate);
                Part(visual,"Arm " + side,PrimitiveType.Capsule,new Vector3(side*.57f,.75f,-.03f),new Vector3(.17f,.23f,.18f),shell,side*35);
                Part(visual,"Claw Palm " + side,PrimitiveType.Sphere,new Vector3(side*.73f,.62f,-.10f),new Vector3(.35f,.32f,.29f),shell);
                Part(visual,"Pincer A " + side,PrimitiveType.Cube,new Vector3(side*.85f,.75f,-.1f),new Vector3(.13f,.30f,.16f),shell,-side*24);
                Part(visual,"Pincer B " + side,PrimitiveType.Cube,new Vector3(side*.64f,.77f,-.1f),new Vector3(.12f,.25f,.16f),shell,side*22);
                Part(visual,"Eye Stalk " + side,PrimitiveType.Capsule,new Vector3(side*.22f,1.20f,-.08f),new Vector3(.075f,.12f,.075f),shell,side*15);
                var eyes = Part(visual,"Red Eye " + side,PrimitiveType.Sphere,new Vector3(side*.25f,1.31f,-.12f),Vector3.one*.105f,eye);
                var light = eyes.AddComponent<Light>(); light.type=LightType.Point; light.color=Color.red; light.range=.35f; light.intensity=.5f;
                Part(visual,"Broken Epaulet " + side,PrimitiveType.Cube,new Vector3(side*.44f,1.02f,-.10f),new Vector3(.23f,.04f,.2f),gold,side*20);
            }
            // Individual uneven pointed strips form a visibly torn lower uniform hem.
            var mesh = new Mesh { name="RaggedUniformHem" };
            var vertices = new System.Collections.Generic.List<Vector3>(); var triangles = new System.Collections.Generic.List<int>();
            for(int i=0;i<7;i++)
            {
                float x=-.35f+i*.10f; int n=vertices.Count;
                vertices.Add(new Vector3(x,.64f,-.285f)); vertices.Add(new Vector3(x+.095f,.64f,-.285f));
                vertices.Add(new Vector3(x+.045f,i%2==0?.32f:.43f,-.30f));
                triangles.AddRange(new[]{n,n+1,n+2,n+2,n+1,n});
            }
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();
            AssetDatabase.CreateAsset(mesh,Folder+"/TornHem.asset");
            var cloth=new GameObject("Torn Uniform Hem",typeof(MeshFilter),typeof(MeshRenderer));cloth.transform.SetParent(visual,false);
            cloth.GetComponent<MeshFilter>().sharedMesh=mesh;cloth.GetComponent<Renderer>().sharedMaterial=robe;
            for(int i=0;i<4;i++) Part(visual,"Corruption Scar " + i,PrimitiveType.Cube,new Vector3(-.18f+i*.10f,.90f-i*.055f,-.285f),new Vector3(.055f,.16f,.025f),corruption,30);
            Part(visual,"Broken Armor Edge",PrimitiveType.Cube,new Vector3(.26f,.72f,-.29f),new Vector3(.08f,.18f,.045f),gold,35);
            var collider=root.AddComponent<CapsuleCollider>();collider.center=new Vector3(0,.66f,0);collider.height=1.35f;collider.radius=.40f;
            root.AddComponent<MonsterHealth>();
            var hit=root.AddComponent<MonsterHitVisual>();var so=new SerializedObject(hit);
            so.FindProperty("visualRoot").objectReferenceValue=visual;
            var renderers=visual.GetComponentsInChildren<Renderer>().Where(r=>r.sharedMaterial!=eye).ToArray();
            var array=so.FindProperty("flashRenderers");array.arraySize=renderers.Length;
            for(int i=0;i<renderers.Length;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=renderers[i];so.ApplyModifiedPropertiesWithoutUndo();
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/CorruptedCrabSoldier.prefab");UnityEngine.Object.DestroyImmediate(root);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);instance.transform.position=new Vector3(.6f,0,-.6f);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("CRAB_BUILD_OK: prefab and scene saved; pipeline="+(GraphicsSettings.currentRenderPipeline==null?"Built-in (Standard emission)":"URP"));
        }
    }
}
