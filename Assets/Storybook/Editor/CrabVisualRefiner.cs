using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Storybook.Editor
{
    [InitializeOnLoad]
    public static class CrabVisualRefiner
    {
        const string Folder="Assets/Storybook/Enemies/CrabSoldier";
        static CrabVisualRefiner(){EditorApplication.delayCall+=Run;}
        static void Shape(Transform root,string name,Vector3 position,Vector3 scale,float angle=0)
        {var t=root.Find(name);t.localPosition=position;t.localScale=scale;t.localRotation=Quaternion.Euler(0,0,angle);}
        static Mesh Finger(string name, bool mirror)
        {
            // Curved tapered jaw: deliberately open inner contour, not two rectangular posts.
            Vector2[] outline={new Vector2(-.25f,-.15f),new Vector2(-.38f,.10f),new Vector2(-.34f,.40f),new Vector2(-.16f,.62f),new Vector2(.04f,.68f),new Vector2(-.08f,.43f),new Vector2(-.13f,.18f),new Vector2(.02f,-.06f)};
            var v=new Vector3[outline.Length*2];var tri=new System.Collections.Generic.List<int>();
            for(int i=0;i<outline.Length;i++){float x=outline[i].x*(mirror?-1:1);v[i]=new Vector3(x,outline[i].y,-.13f);v[i+outline.Length]=new Vector3(x,outline[i].y,.13f);}
            // Polygon is triangulated through Unity's editor utility for a concave curved jaw.
            // Use individual triangles around a centre lying inside the thick jaw contour.
            int[] face={0,1,7,1,6,7,1,2,6,2,5,6,2,3,5,3,4,5};
            foreach(int i in face)tri.Add(i);
            for(int i=0;i<face.Length;i+=3)tri.AddRange(new[]{face[i+2]+8,face[i+1]+8,face[i]+8});
            for(int i=0;i<8;i++){int j=(i+1)%8;tri.AddRange(new[]{i,j,j+8,i,j+8,i+8});}
            if(mirror)for(int i=0;i<tri.Count;i+=3){int a=tri[i];tri[i]=tri[i+2];tri[i+2]=a;}
            var mesh=new Mesh{name=name};mesh.vertices=v;mesh.triangles=tri.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
            return mesh;
        }
        static void Run()
        {
            if(!File.Exists(".utmp/refine-crab-request"))return;
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Delete(".utmp/refine-crab-request");
            try{Refine();}catch(Exception ex){Debug.LogException(ex);}
        }
        static void Refine()
        {
            string path=Folder+"/CorruptedCrabSoldier.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var v=root.transform.Find("Visual");
                Shape(v,"Carapace",new Vector3(0,.92f,.04f),new Vector3(1.12f,.58f,.65f));
                Shape(v,"Chest Armor",new Vector3(0,.79f,-.25f),new Vector3(.57f,.31f,.10f));
                Shape(v,"Officer Belt",new Vector3(0,.59f,-.04f),new Vector3(.65f,.075f,.48f));
                var shell=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/DarkTealShell.mat");
                var gold=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/AgedGold.mat");
                shell.color=new Color(.06f,.34f,.35f);
                for(int side=-1;side<=1;side+=2)
                {
                    Shape(v,"Shoulder "+side,new Vector3(side*.52f,.89f,.03f),new Vector3(.28f,.22f,.28f));
                    Shape(v,"Arm "+side,new Vector3(side*.67f,.75f,-.03f),new Vector3(.19f,.27f,.19f),side*52);
                    Shape(v,"Claw Palm "+side,new Vector3(side*.92f,.79f,-.11f),new Vector3(.43f,.39f,.34f));
                    for(int jaw=0;jaw<2;jaw++)
                    {
                        string name=jaw==0?"Pincer A ":"Pincer B ";var t=v.Find(name+side);
                        var mesh=Finger("CurvedClaw_"+side+"_"+jaw,jaw==1);
                        string mp=Folder+"/"+mesh.name+".asset";
                        var old=AssetDatabase.LoadAssetAtPath<Mesh>(mp);
                        if(old!=null){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);mesh=old;}else AssetDatabase.CreateAsset(mesh,mp);
                        t.GetComponent<MeshFilter>().sharedMesh=mesh;t.localPosition=new Vector3(side*.92f, .96f,-.11f);t.localScale=new Vector3(.67f,.67f,1);t.localRotation=Quaternion.Euler(0,0,-side*18+(jaw==0?18:-18));
                    }
                    Shape(v,"Eye Stalk "+side,new Vector3(side*.27f,1.24f,-.13f),new Vector3(.085f,.16f,.085f),-side*12);
                    Shape(v,"Red Eye "+side,new Vector3(side*.30f,1.42f,-.16f),new Vector3(.15f,.12f,.12f));
                    // Shell rim teeth make the broad crab silhouette visible from the top-down camera.
                    for(int i=0;i<3;i++)
                    {
                        string name="Shell Rim Tooth "+side+"_"+i;if(v.Find(name)!=null)continue;
                        var tooth=GameObject.CreatePrimitive(PrimitiveType.Cube);tooth.name=name;tooth.transform.SetParent(v,false);
                        tooth.transform.localPosition=new Vector3(side*(.49f+i*.035f),1.08f-i*.13f,.06f);
                        tooth.transform.localScale=new Vector3(.16f,.12f,.24f);tooth.transform.localRotation=Quaternion.Euler(0,0,side*35);
                        UnityEngine.Object.DestroyImmediate(tooth.GetComponent<Collider>());tooth.GetComponent<Renderer>().sharedMaterial=shell;
                    }
                }
                var hit=new SerializedObject(root.GetComponent<MonsterHitVisual>());var a=hit.FindProperty("flashRenderers");
                var rr=v.GetComponentsInChildren<Renderer>().Where(r=>!r.name.StartsWith("Red Eye")).ToArray();a.arraySize=rr.Length;for(int i=0;i<rr.Length;i++)a.GetArrayElementAtIndex(i).objectReferenceValue=rr[i];hit.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root,path);AssetDatabase.SaveAssets();
                Preview(root);
                Debug.Log("CRAB_VISUAL_REFINED: curved open claws, wide shell, eye stalks; health untouched");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        static void Preview(GameObject prefab)
        {
            var scene=EditorSceneManager.NewPreviewScene();
            var model=UnityEngine.Object.Instantiate(prefab);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(model,scene);
            var cameraGo=new GameObject("PreviewCamera");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraGo,scene);
            var camera=cameraGo.AddComponent<Camera>();camera.scene=scene;camera.transform.position=new Vector3(2.5f,2.6f,-5);camera.transform.LookAt(new Vector3(0,.75f,0));camera.orthographic=true;camera.orthographicSize=1.35f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.075f,.09f,.12f);
            for(int i=0;i<2;i++){var g=new GameObject("PreviewLight");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(g,scene);var light=g.AddComponent<Light>();light.type=LightType.Directional;light.intensity=i==0?1.6f:.7f;g.transform.rotation=Quaternion.Euler(35,i==0?140:220,0);}
            var rt=new RenderTexture(768,768,24);camera.targetTexture=rt;camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
            var image=new Texture2D(768,768,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,768,768),0,0);image.Apply();
            File.WriteAllBytes("C:/Users/Admin/Documents/ChatGPT/Unity 포트폴리오/crab-preview.png",image.EncodeToPNG());RenderTexture.active=previous;camera.targetTexture=null;
            UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
