using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FairyTaleRecovery.Editor
{
    public static class HongGildongBuilder
    {
        private const string Art = "Assets/_Game/Art/HongGildong";
        private const string Prefab = Art + "/HongGildongVisual.prefab";
        private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private static int meshIndex;

        [MenuItem("Tools/Fairy Tale Recovery/Install Hong Gildong")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Exit Play Mode before installing Hong Gildong.");
                return;
            }
            EnsureAssets();
            // Never save a live/dirty scene behind the user's back. The prefab is updated
            // independently, and scene edits remain undoable until the user saves.
            var players = Object.FindObjectsByType<PlayerMotor>();
            if (players.Length == 0)
            {
                Debug.LogWarning("Open MovementAimTest, then run Install Hong Gildong again. Visual prefab is ready.");
                return;
            }
            foreach (var player in players) Attach(player.gameObject);
            Debug.Log("Hong Gildong installed: original capsule/marker disabled, movement/aim unchanged. Save the scene to keep the replacement.");
        }

        public static void EnsureAssets()
        {
            Directory.CreateDirectory(Art + "/Meshes");
            Directory.CreateDirectory(Art + "/Materials");
            AssetDatabase.Refresh();
            materials.Clear(); meshIndex = 0;
            Mat("Indigo", "#192447"); Mat("Teal", "#287E82"); Mat("Ivory", "#EEE4D2");
            Mat("Straw", "#BC955D"); Mat("HatBand", "#5B3635"); Mat("Wrap", "#D7CCB8");
            Mat("Crimson", "#922B3C"); Mat("Skin", "#DDA77B"); Mat("SkinShadow", "#B67E5D");
            Mat("Hair", "#17202C"); Mat("Leather", "#4B352C"); Mat("Gold", "#C59B4B", 0.35f);
            Mat("Steel", "#B7D8E1", 0.8f); Mat("Edge", "#EDF4E9", 0.7f); Mat("Eye", "#151B25");
            GameObject root = new GameObject("HongGildongVisual");
            try
            {
                Build(root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, Prefab);
            }
            finally { Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
        }

        // Invoked only in an isolated verification project, never in the user's open editor.
        public static void BatchVerify()
        {
            try
            {
                EnsureAssets();
                UpdatePlayerPrefab();
                var scene=EditorSceneManager.OpenScene("Assets/_Game/Scenes/MovementAimTest.unity");
                var motor=Object.FindFirstObjectByType<PlayerMotor>();
                if(motor==null) throw new Exception("Missing existing PlayerMotor");
                Attach(motor.gameObject);
                EditorSceneManager.SaveScene(scene,"Assets/_Game/Scenes/HongGildongTest.unity");
                var visual=motor.GetComponentInChildren<HongGildongMotion>();
                if(visual==null || motor.GetComponent<PlayerAim>()==null || motor.GetComponent<PlayerInputReader>()==null)
                    throw new Exception("Lost player controls or visual rig");
                int meshes=visual.GetComponentsInChildren<MeshFilter>().Length;
                int triangles=0;
                foreach(var filter in visual.GetComponentsInChildren<MeshFilter>())
                {
                    if(filter.sharedMesh==null) throw new Exception("Missing persistent mesh");
                    triangles+=filter.sharedMesh.triangles.Length/3;
                    foreach(var vertex in filter.sharedMesh.vertices)
                        if(float.IsNaN(vertex.x)||float.IsNaN(vertex.y)||float.IsNaN(vertex.z))throw new Exception("Invalid mesh coordinates");
                }
                Directory.CreateDirectory("Verification");
                var camObject=new GameObject("PreviewCamera",typeof(Camera));
                var camera=camObject.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.38f;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.075f,.11f);
                var target=motor.transform.position+Vector3.up*.05f;
                // Existing floor and boundaries remain in the actual test scene.
                Render(camera,target,new Vector3(3.1f,2.1f,4.5f),"Verification/Warmup.png");
                for(int i=0;i<30;i++)visual.Animate(Vector3.zero,1f/60f);
                Quaternion idle=visual.leftThigh.localRotation;
                for(int i=0;i<17;i++)visual.Animate(Vector3.forward*5f,1f/60f);
                if(Quaternion.Angle(idle,visual.leftThigh.localRotation)<1f)throw new Exception("Walk pose did not change");
                Render(camera,target,new Vector3(3.1f,2.1f,4.5f),"Verification/Walk.png");
                for(int i=0;i<20;i++)visual.Animate(Vector3.right*5f,1f/60f);
                Render(camera,target,new Vector3(0f,6f,-.01f),"Verification/Top.png");
                for(int i=0;i<90;i++)visual.Animate(Vector3.zero,1f/60f);
                if(visual.MovementBlend>.001f)throw new Exception("Did not return to idle");
                if(Mathf.Abs(visual.hips.localPosition.y-.91f)>.02f)throw new Exception("Hip rest height changed");
                Render(camera,target,new Vector3(3.1f,2.1f,4.5f),"Verification/Front.png");
                Render(camera,target,new Vector3(-3.1f,2.0f,-4.5f),"Verification/Back.png");
                File.WriteAllText("Verification/Report.txt", "PASS: prefab meshes/materials persist; original input/motor/aim remain; walk changes leg pose; strafe evaluated; returns to idle.\nMeshes: "+meshes+"\nTriangles: "+triangles+"\nNo live keyboard/mouse or standalone build verification in this batch.\n");
                Object.DestroyImmediate(camObject);
                Debug.Log("HONG_GILDONG_VERIFICATION_PASS");
                EditorApplication.Exit(0);
            }
            catch(Exception exception){Debug.LogException(exception);EditorApplication.Exit(1);}
        }

        private static void Render(Camera camera,Vector3 target,Vector3 offset,string path)
        {
            camera.transform.position=target+offset;camera.transform.LookAt(target);
            RenderTexture texture=new RenderTexture(1000,1000,24);camera.targetTexture=texture;
            camera.Render();RenderTexture previous=RenderTexture.active;RenderTexture.active=texture;
            Texture2D image=new Texture2D(1000,1000,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0f,0f,1000f,1000f),0,0);image.Apply();
            File.WriteAllBytes(path,image.EncodeToPNG());RenderTexture.active=previous;camera.targetTexture=null;
            Object.DestroyImmediate(image);Object.DestroyImmediate(texture);
        }

        private static void Attach(GameObject player)
        {
            Undo.RegisterFullObjectHierarchyUndo(player, "Install Hong Gildong");
            Transform existing=player.transform.Find("HongGildongVisual");
            foreach (Transform child in player.transform)
            {
                if (child.name == "Visual" || child.name == "ForwardMarker")
                    child.gameObject.SetActive(false);
            }
            if(existing!=null){existing.gameObject.SetActive(true);EditorSceneManager.MarkSceneDirty(player.scene);return;}
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab), player.scene);
            Undo.RegisterCreatedObjectUndo(visual, "Install Hong Gildong");
            visual.transform.SetParent(player.transform, false);
            // The existing controller's pivot is one metre above the floor.
            var cc = player.GetComponent<CharacterController>();
            visual.transform.localPosition = Vector3.up * (cc != null ? cc.center.y - cc.height * 0.5f : -1f);
            EditorSceneManager.MarkSceneDirty(player.scene);
            Selection.activeGameObject = player;
        }

        private static void UpdatePlayerPrefab()
        {
            const string playerPath="Assets/_Game/Prefabs/Player.prefab";
            if(!File.Exists(playerPath))return;
            GameObject player=PrefabUtility.LoadPrefabContents(playerPath);
            try
            {
                foreach(Transform child in player.transform)
                    if(child.name=="Visual"||child.name=="ForwardMarker")child.gameObject.SetActive(false);
                if(player.transform.Find("HongGildongVisual")==null)
                {
                    GameObject visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),player.scene);
                    visual.transform.SetParent(player.transform,false);
                    var cc=player.GetComponent<CharacterController>();
                    visual.transform.localPosition=Vector3.up*(cc.center.y-cc.height*.5f);
                }
                PrefabUtility.SaveAsPrefabAsset(player,playerPath);
            }
            finally {PrefabUtility.UnloadPrefabContents(player);}
        }

        private static void Build(Transform root)
        {
            var motion = root.gameObject.AddComponent<HongGildongMotion>();
            Transform hips = Joint("Hips", root, new Vector3(0f, 0.91f, 0f)); motion.hips = hips;
            Shape("WaistTrousers", hips, new Vector3(0f, -0.10f, 0f), new Vector3(.27f,.12f,.17f), "Ivory");
            Transform chest = Joint("Chest", hips, new Vector3(0f,.14f,0f)); motion.chest = chest;
            Profile("TailoredCoat", chest, Vector3.zero,
                new[]{new Vector3(.24f,-.11f,.16f), new Vector3(.27f,.12f,.18f), new Vector3(.34f,.34f,.17f),new Vector3(.23f,.43f,.13f)}, "Indigo", 16);
            // Gold shoulder accents add a bold silhouette readable at a top-down angle.
            Strip("LeftShoulderTrim", chest, new Vector3(-.31f,.28f,.16f), new Vector3(-.15f,.38f,.19f), .032f, "Gold");
            Strip("RightShoulderTrim", chest, new Vector3(.31f,.28f,.16f), new Vector3(.15f,.38f,.19f), .032f, "Gold");
            // Two overlapping diagonal collars, made as thick cloth strips.
            Cloth("InnerShirt",chest,new[]{new Vector3(-.11f,.43f,.235f),new Vector3(.11f,.43f,.235f),new Vector3(0f,.23f,.235f)},"Ivory");
            Strip("InnerCrossCollar", chest, new Vector3(-.13f,.43f,.24f), new Vector3(0f,.23f,.24f), .065f, "Ivory");
            Strip("OuterCrossCollar", chest, new Vector3(.13f,.43f,.25f), new Vector3(-.16f,.025f,.25f), .075f, "Ivory");
            Strip("GoldenLapel", chest, new Vector3(.185f,.39f,.245f), new Vector3(-.12f,-.05f,.245f), .03f, "Gold");
            Shape("Sash", hips, new Vector3(0f,.02f,.005f), new Vector3(.29f,.065f,.193f), "Crimson");
            Shape("SashKnot", hips, new Vector3(-.16f,.02f,.204f), new Vector3(.07f,.05f,.045f), "Gold");
            Strip("BeltTailA", hips, new Vector3(-.16f,-.01f,.21f), new Vector3(-.24f,-.38f,.23f), .065f,"Crimson");
            Strip("BeltTailB", hips, new Vector3(-.11f,-.01f,.215f), new Vector3(-.04f,-.30f,.23f), .05f,"Crimson");
            for (int side = -1; side <= 1; side += 2)
            {
                string prefix = side < 0 ? "Left" : "Right";
                Transform thigh = Joint(prefix+"Thigh", hips, new Vector3(side*.155f,-.06f,0f));
                Profile(prefix+"Trouser", thigh, Vector3.zero, new[]{new Vector3(.13f,0f,.14f),new Vector3(.155f,-.19f,.145f),new Vector3(.10f,-.37f,.10f)},"Ivory",12);
                Transform shin = Joint(prefix+"Shin", thigh, new Vector3(0f,-.37f,0f));
                Profile(prefix+"Boot", shin, Vector3.zero,new[]{new Vector3(.10f,.015f,.10f),new Vector3(.102f,-.19f,.105f),new Vector3(.115f,-.37f,.12f)},"Wrap",12);
                Shape(prefix+"BootFoot",shin,new Vector3(0f,-.37f,.065f),new Vector3(.117f,.07f,.20f),"Leather");
                Shape(prefix+"Sole",shin,new Vector3(0f,-.422f,.06f),new Vector3(.12f,.025f,.203f),"Hair");
                Shape(prefix+"BootCuff",shin,new Vector3(0f,-.035f,0f),new Vector3(.109f,.035f,.11f),"Wrap");
                for(int band=0;band<3;band++) Strip(prefix+"Lace"+band,shin,new Vector3(-.065f,-.10f-band*.065f,.104f),new Vector3(.065f,-.13f-band*.065f,.104f),.014f,"Gold");
                Transform coat = Joint(prefix+"CoatTail",hips,new Vector3(side*.14f,-.035f,-.04f));
                Cloth(prefix+"SplitCoat",coat,new[]{new Vector3(side*-.05f,0f,.15f),new Vector3(side*.15f,0f,.05f),new Vector3(side*.22f,-.43f,.04f),new Vector3(side*-.03f,-.38f,.17f)},"Indigo");
                Strip(prefix+"CoatTrim",coat,new Vector3(side*.22f,-.43f,.045f),new Vector3(side*-.03f,-.38f,.175f),.035f,"Gold");
                Cloth(prefix+"BackCoat",coat,new[]{new Vector3(-side*.12f,0f,-.14f),new Vector3(side*.13f,0f,-.13f),new Vector3(side*.19f,-.43f,-.18f),new Vector3(-side*.115f,-.40f,-.20f)},"Indigo");
                Strip(prefix+"BackHem",coat,new Vector3(side*.19f,-.43f,-.181f),new Vector3(-side*.115f,-.40f,-.201f),.03f,"Gold");
                Transform arm=Joint(prefix+"UpperArm",chest,new Vector3(side*.29f,.29f,0f));
                Profile(prefix+"Sleeve",arm,Vector3.zero,new[]{new Vector3(.145f,.03f,.15f),new Vector3(.17f,-.14f,.145f),new Vector3(.12f,-.285f,.12f)},"Indigo",12);
                Transform forearm=Joint(prefix+"Forearm",arm,new Vector3(0f,-.28f,0f));
                Profile(prefix+"InnerSleeve",forearm,Vector3.zero,new[]{new Vector3(.113f,.02f,.112f),new Vector3(.09f,-.18f,.09f)},"Ivory",12);
                Shape(prefix+"Bracer",forearm,new Vector3(0f,-.17f,0f),new Vector3(.095f,.08f,.09f),"Wrap");
                Shape(prefix+"BracerEdge",forearm,new Vector3(0f,-.12f,0f),new Vector3(.10f,.015f,.095f),"Gold");
                Transform hand=Joint(prefix+"Hand",forearm,new Vector3(0f,-.285f,0f));
                Shape(prefix+"Palm",hand,Vector3.zero,new Vector3(.075f,.085f,.06f),"Skin");
                Shape(prefix+"Thumb",hand,new Vector3(-side*.065f,-.015f,.025f),new Vector3(.032f,.05f,.036f),"Skin");
                if(side<0){motion.leftThigh=thigh;motion.leftShin=shin;motion.leftArm=arm;motion.leftForearm=forearm;motion.leftCoat=coat;}
                else {motion.rightThigh=thigh;motion.rightShin=shin;motion.rightArm=arm;motion.rightForearm=forearm;motion.rightCoat=coat; Sword(hand);}
            }
            Transform neck=Joint("Neck",chest,new Vector3(0f,.43f,0f));
            Shape("NeckSkin",neck,new Vector3(0f,.045f,0f),new Vector3(.095f,.09f,.09f),"Skin");
            Transform head=Joint("Head",neck,new Vector3(0f,.16f,0f)); motion.head=head;
            Profile("Face",head,Vector3.zero,new[]{new Vector3(.105f,-.11f,.105f),new Vector3(.16f,-.04f,.145f),new Vector3(.177f,.09f,.151f),new Vector3(.16f,.22f,.135f),new Vector3(.09f,.27f,.08f)},"Skin",16);
            Shape("Nose",head,new Vector3(0f,.045f,.147f),new Vector3(.024f,.041f,.026f),"SkinShadow");
            for(int side=-1;side<=1;side+=2)
            {
                Shape("Ear"+side,head,new Vector3(side*.17f,.05f,0f),new Vector3(.035f,.067f,.035f),"Skin");
                Shape("EyeWhite"+side,head,new Vector3(side*.074f,.085f,.137f),new Vector3(.042f,.015f,.006f),"Ivory");
                Shape("Iris"+side,head,new Vector3(side*.07f,.084f,.144f),new Vector3(.014f,.015f,.004f),"Eye");
                Strip("Brow"+side,head,new Vector3(side*.025f,.127f,.153f),new Vector3(side*.12f,.145f,.134f),.023f,"Hair");
            }
            Strip("Mouth",head,new Vector3(-.044f,-.052f,.128f),new Vector3(.044f,-.052f,.128f),.009f,"SkinShadow");
            Profile("HairCap",head,new Vector3(0f,.01f,-.025f),new[]{new Vector3(.17f,.13f,.15f),new Vector3(.182f,.22f,.16f),new Vector3(.13f,.30f,.12f),new Vector3(.02f,.33f,.02f)},"Hair",16);
            Shape("TopKnot",head,new Vector3(0f,.34f,-.02f),new Vector3(.10f,.09f,.10f),"Hair");
            // Low-profile angular straw hat: readable from the raised game camera.
            Profile("StrawHatBrim",head,Vector3.zero,new[]{new Vector3(.20f,.355f,.18f),new Vector3(.33f,.365f,.30f),new Vector3(.355f,.385f,.325f),new Vector3(.31f,.400f,.288f)},"Straw",24);
            Profile("StrawHatCrown",head,Vector3.zero,new[]{new Vector3(.185f,.397f,.17f),new Vector3(.178f,.48f,.165f),new Vector3(.145f,.565f,.135f)},"Straw",20);
            Profile("StrawHatBand",head,Vector3.zero,new[]{new Vector3(.187f,.408f,.174f),new Vector3(.183f,.447f,.170f)},"HatBand",20);
            // Cloth head wrap beneath a broad straw hat, based on the costume reference.
            Profile("Headband",head,new Vector3(0f,0f,-.01f),new[]{new Vector3(.18f,.15f,.155f),new Vector3(.181f,.20f,.157f)},"Wrap",20);
            Shape("ForeheadSeal",head,new Vector3(0f,.177f,.154f),new Vector3(.035f,.025f,.012f),"Gold");
            // Asymmetric layered bangs keep the face visible from a three-quarter view.
            Cloth("BangLeft",head,new[]{new Vector3(-.15f,.255f,.105f),new Vector3(-.015f,.285f,.15f),new Vector3(-.09f,.13f,.17f)},"Hair");
            Cloth("BangRight",head,new[]{new Vector3(.01f,.29f,.145f),new Vector3(.15f,.25f,.11f),new Vector3(.145f,.075f,.10f)},"Hair");
            Transform pony=Joint("Ponytail",head,new Vector3(0f,.23f,-.14f));motion.ponytail=pony;
            Profile("TiedHair",pony,Vector3.zero,new[]{new Vector3(.07f,0f,.065f),new Vector3(.09f,-.14f,.07f),new Vector3(.055f,-.32f,.05f),new Vector3(.008f,-.40f,.01f)},"Hair",12);
            Transform scarf=Joint("HeadbandTails",head,new Vector3(.015f,.17f,-.17f));motion.scarf=scarf;
            Strip("RibbonA",scarf,Vector3.zero,new Vector3(-.12f,-.35f,-.08f),.055f,"Wrap");
            Strip("RibbonB",scarf,new Vector3(.025f,0f,0f),new Vector3(.15f,-.27f,-.10f),.04f,"Wrap");
            Transform sheath=Joint("Scabbard",hips,new Vector3(-.31f,.02f,-.09f));
            sheath.localRotation=Quaternion.Euler(-18f,0f,-18f);
            Profile("LacquerScabbard",sheath,Vector3.zero,new[]{new Vector3(.048f,.02f,.035f),new Vector3(.046f,-.55f,.033f),new Vector3(.02f,-.72f,.02f)},"Gold",8);
            Shape("ScabbardMouth",sheath,Vector3.zero,new Vector3(.06f,.035f,.045f),"Gold");
            Shape("ScabbardTip",sheath,new Vector3(0f,-.66f,0f),new Vector3(.037f,.06f,.03f),"Gold");
            // Agent insignia: a small closed-book clasp, not a borrowed character emblem.
            Shape("StorySeal",chest,new Vector3(-.21f,.23f,.167f),new Vector3(.047f,.055f,.02f),"Gold");
            Strip("StorySealSpine",chest,new Vector3(-.21f,.18f,.19f),new Vector3(-.21f,.28f,.19f),.01f,"Gold");
        }

        private static void Sword(Transform hand)
        {
            Transform sword=Joint("SwordSocket",hand,new Vector3(0f,-.015f,.02f));
            sword.localRotation=Quaternion.Euler(100f,0f,0f);
            Profile("WrappedGrip",sword,Vector3.zero,new[]{new Vector3(.035f,-.09f,.028f),new Vector3(.035f,.12f,.028f)},"Crimson",8);
            for(int i=0;i<5;i++)Shape("GripWrap"+i,sword,new Vector3(0f,-.07f+i*.04f,0f),new Vector3(.038f,.008f,.031f),"Gold");
            Shape("Pommel",sword,new Vector3(0f,-.12f,0f),new Vector3(.045f,.033f,.038f),"Gold");
            Shape("Guard",sword,new Vector3(0f,.14f,0f),new Vector3(.13f,.025f,.07f),"Gold");
            // Diamond-section blade with a taper and a visible polished central ridge.
            Profile("Blade",sword,new Vector3(0f,.17f,0f),new[]{new Vector3(.052f,0f,.017f),new Vector3(.046f,.56f,.015f),new Vector3(.001f,.75f,.001f)},"Steel",4);
            Strip("BladeHighlight",sword,new Vector3(0f,.18f,.019f),new Vector3(0f,.86f,.003f),.012f,"Edge");
        }

        private static Transform Joint(string name,Transform parent,Vector3 position)
        {
            var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=position;return t;
        }
        private static Material Mat(string name,string hex,float metallic=0f)
        {
            string path=Art+"/Materials/"+name+".mat";
            Material m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            ColorUtility.TryParseHtmlString(hex,out Color color);m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",metallic>0f?.55f:.23f);
            m.SetFloat("_Cull",(float)CullMode.Off);EditorUtility.SetDirty(m);materials[name]=m;return m;
        }
        private static void Shape(string name,Transform parent,Vector3 pos,Vector3 radius,string mat)
        {
            // Multi-ring custom ellipsoid rather than a Unity capsule/primitive.
            var rings=new Vector3[9];
            for(int j=0;j<rings.Length;j++){float a=-Mathf.PI*.5f+Mathf.PI*j/(rings.Length-1);rings[j]=new Vector3(Mathf.Max(.001f,Mathf.Cos(a))*radius.x,Mathf.Sin(a)*radius.y,Mathf.Max(.001f,Mathf.Cos(a))*radius.z);}
            Profile(name,parent,pos,rings,mat,12);
        }
        private static void Profile(string name,Transform parent,Vector3 pos,Vector3[] rings,string mat,int sides)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int j=0;j<rings.Length;j++)for(int i=0;i<sides;i++){float a=Mathf.PI*2f*i/sides;vertices.Add(new Vector3(Mathf.Sin(a)*rings[j].x,rings[j].y,Mathf.Cos(a)*rings[j].z));}
            bool up=rings[rings.Length-1].y>rings[0].y;
            for(int j=0;j<rings.Length-1;j++)for(int i=0;i<sides;i++){int a=j*sides+i,b=j*sides+(i+1)%sides,c=a+sides,d=b+sides;if(up){triangles.AddRange(new[]{a,c,b,b,c,d});}else{triangles.AddRange(new[]{a,b,c,b,d,c});}}
            int bottom=vertices.Count;vertices.Add(new Vector3(0f,rings[0].y,0f));int top=vertices.Count;vertices.Add(new Vector3(0f,rings[rings.Length-1].y,0f));
            for(int i=0;i<sides;i++){int next=(i+1)%sides;triangles.AddRange(up?new[]{bottom,i,next,top,(rings.Length-1)*sides+next,(rings.Length-1)*sides+i}:new[]{bottom,next,i,top,(rings.Length-1)*sides+i,(rings.Length-1)*sides+next});}
            // The ring travels from +Z towards +X; reverse for outward normals.
            for(int i=0;i<triangles.Count;i+=3){int swap=triangles[i+1];triangles[i+1]=triangles[i+2];triangles[i+2]=swap;}
            MeshPart(name,parent,pos,vertices.ToArray(),triangles.ToArray(),mat);
        }
        private static void Strip(string name,Transform parent,Vector3 a,Vector3 b,float width,string mat)
        {
            Vector3 across=Vector3.Cross((b-a).normalized,Vector3.forward).normalized*width*.5f;
            if(across.sqrMagnitude<.000001f)across=Vector3.right*width*.5f;
            Cloth(name,parent,new[]{a-across,a+across,b+across,b-across},mat);
        }
        private static void Cloth(string name,Transform parent,Vector3[] v,string mat)
        {
            int[] indices=v.Length==3?new[]{0,1,2}:new[]{0,1,2,0,2,3};
            MeshPart(name,parent,Vector3.zero,v,indices,mat);
        }
        private static void MeshPart(string name,Transform parent,Vector3 pos,Vector3[] vertices,int[] indices,string mat)
        {
            Mesh mesh=new Mesh{name=name};mesh.vertices=vertices;mesh.triangles=indices;mesh.RecalculateNormals();mesh.RecalculateBounds();
            string path=Art+"/Meshes/"+(meshIndex++).ToString("D3")+"_"+name+".asset";
            Mesh saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}
            else{EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);}
            var t=Joint(name,parent,pos);t.gameObject.AddComponent<MeshFilter>().sharedMesh=saved;
            var renderer=t.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=materials[mat];
        }
    }
}
