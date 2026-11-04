using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FairyTaleRecovery.Editor
{
    public static class MovementTestSceneBuilder
    {
        private const string Root = "Assets/_Game";
        private const string ScenePath = Root + "/Scenes/MovementAimTest.unity";

        [MenuItem("Tools/Fairy Tale Recovery/Create Movement Test Scene")]
        public static void CreateScene()
        {
            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                return; // Never overwrite an existing test scene.
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Root + "/Scenes");
            Directory.CreateDirectory(Root + "/Prefabs");
            AssetDatabase.Refresh();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = Vector3.one * 3f;

            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 20f, 0f), Quaternion.Euler(90f, 0f, 0f));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 10f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;

            GameObject lightObject = new GameObject("Directional Light", typeof(Light));
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            lightObject.GetComponent<Light>().type = LightType.Directional;
            lightObject.GetComponent<Light>().intensity = 2f;

            GameObject player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 1.05f, 0f);
            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.skinWidth = 0.05f;
            controller.minMoveDistance = 0f;
            player.AddComponent<PlayerInputReader>();
            player.AddComponent<PlayerMotor>();
            PlayerAim aim = player.AddComponent<PlayerAim>();
            SerializedObject serializedAim = new SerializedObject(aim);
            serializedAim.FindProperty("aimCamera").objectReferenceValue = camera;
            serializedAim.ApplyModifiedPropertiesWithoutUndo();

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Visual";
            body.transform.SetParent(player.transform, false);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "ForwardMarker";
            marker.transform.SetParent(player.transform, false);
            marker.transform.localPosition = new Vector3(0f, 0.1f, 0.7f);
            marker.transform.localScale = new Vector3(0.2f, 0.2f, 0.6f);
            Object.DestroyImmediate(marker.GetComponent<Collider>());

            // Bound the test area inside the camera and keep the player on the floor.
            CreateWall("NorthWall", new Vector3(0f, 1f, 8f), new Vector3(16f, 2f, 0.5f));
            CreateWall("SouthWall", new Vector3(0f, 1f, -8f), new Vector3(16f, 2f, 0.5f));
            CreateWall("EastWall", new Vector3(8f, 1f, 0f), new Vector3(0.5f, 2f, 16f));
            CreateWall("WestWall", new Vector3(-8f, 1f, 0f), new Vector3(0.5f, 2f, 16f));
            string prefabPath = AssetDatabase.GenerateUniqueAssetPath(Root + "/Prefabs/Player.prefab");
            PrefabUtility.SaveAsPrefabAssetAndConnect(player, prefabPath, InteractionMode.AutomatedAction);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = player;
            Debug.Log("Movement test scene created. Press Play; WASD moves, mouse aims. Existing scenes were preserved.");
        }

        private static void CreateWall(string name, Vector3 position, Vector3 scale)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.position = position;
            wall.transform.localScale = scale;
        }
    }
}
