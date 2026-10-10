using System.IO;
using Bloodbound;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bloodbound.Editor
{
    [InitializeOnLoad]
    public static class PrototypeSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/BloodboundPrototype.unity";
        private const string MaterialPath = "Assets/Materials";

        // Schedules the one-time conversion of the legacy runtime-built scene in the editor.
        static PrototypeSceneBuilder()
        {
            EditorApplication.delayCall += UpgradeLegacySceneIfNeeded;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        // Checks for the legacy scene after Unity finishes opening a scene.
        private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
        {
            UpgradeLegacySceneIfNeeded();
        }

        // Replaces only the original bootstrap-only scene with editable saved objects.
        private static void UpgradeLegacySceneIfNeeded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                return;

            GameObject[] roots = scene.GetRootGameObjects();
            if (roots.Length == 1 && roots[0].name == "Runtime Prototype Bootstrap")
                BuildScene();
        }

        [MenuItem("Bloodbound/Build Prototype Scene")]
        // Rebuilds and saves the prototype scene after confirming the replacement.
        public static void BuildPrototype()
        {
            if (File.Exists(ScenePath) && !Application.isBatchMode && !EditorUtility.DisplayDialog(
                    "Rebuild Bloodbound Prototype",
                    "This replaces the saved Bloodbound prototype scene. Any scene edits will be lost. Continue?",
                    "Rebuild Scene",
                    "Cancel"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            BuildScene();
        }

        // Creates the room, characters, camera, lighting, materials, and build-scene entry.
        private static void BuildScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory(MaterialPath);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Transform environment = CreateGroup("Environment");
            Transform characters = CreateGroup("Characters");
            Transform cameras = CreateGroup("Cameras");
            Transform lighting = CreateGroup("Lighting");

            Material floorMat = CreateMaterial("AshStone", new Color(0.16f, 0.17f, 0.19f));
            Material wallMat = CreateMaterial("WeatheredSlate", new Color(0.10f, 0.11f, 0.14f));
            Material trimMat = CreateMaterial("Old rass", new Color(0.38f, 0.27f, 0.12f));
            Material playerMat = CreateMaterial("Valadrin", new Color(0.36f, 0.08f, 0.16f));
            Material enemyMat = CreateMaterial("ThrallArmor", new Color(0.28f, 0.31f, 0.35f));

            CreateBlock("Floor", new Vector3(0f, -0.3f, 0f), new Vector3(22f, 0.6f, 22f), floorMat, environment);
            CreateBlock("North Wall", new Vector3(0f, 2.4f, 11f), new Vector3(22f, 5f, 0.6f), wallMat, environment);
            CreateBlock("East Wall", new Vector3(11f, 2.4f, 0f), new Vector3(0.6f, 5f, 22f), wallMat, environment);
            CreateBlock("West Wall", new Vector3(-11f, 2.4f, 0f), new Vector3(0.6f, 5f, 22f), wallMat, environment);
            CreateBlock("South Wall", new Vector3(0f, 2.4f, -11f), new Vector3(22f, 5f, 0.6f), wallMat, environment);

            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    CreateBlock("Stone Pier", new Vector3(x * 8.5f, 2.2f, z * 8.5f), new Vector3(0.8f, 4.4f, 0.8f), trimMat, environment);
                }
            }

            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Valadrin";
            player.transform.SetParent(characters, true);
            player.transform.position = new Vector3(0f, 1f, 5f);
            player.transform.localScale = new Vector3(0.82f, 1f, 0.82f);
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            player.GetComponent<MeshRenderer>().sharedMaterial = playerMat;
            CharacterController characterController = player.AddComponent<CharacterController>();
            characterController.height = 2f;
            characterController.radius = 0.4f;
            characterController.center = Vector3.zero;
            player.AddComponent<ValadrinController>();

            GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemy.name = "Vampire Thrall";
            enemy.transform.SetParent(characters, true);
            enemy.transform.position = new Vector3(0f, 1.05f, -3.5f);
            enemy.transform.localScale = new Vector3(0.92f, 1.05f, 0.92f);
            enemy.GetComponent<MeshRenderer>().sharedMaterial = enemyMat;
            enemy.AddComponent<EnemyController>();

            CreateBlock("Raised Altar", new Vector3(0f, 0.28f, -8f), new Vector3(4f, 0.55f, 1.4f), trimMat, environment);

            GameObject cameraObject = new GameObject("Fixed Room Camera");
            cameraObject.transform.SetParent(cameras, true);
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 10.5f, -13f);
            cameraObject.transform.LookAt(new Vector3(0f, 1f, 0f));
            camera.fieldOfView = 46f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.backgroundColor = new Color(0.035f, 0.04f, 0.055f);
            camera.clearFlags = CameraClearFlags.SolidColor;

            GameObject keyLight = new GameObject("Moonlight");
            keyLight.transform.SetParent(lighting, true);
            Light directional = keyLight.AddComponent<Light>();
            directional.type = LightType.Directional;
            directional.color = new Color(0.57f, 0.65f, 0.82f);
            directional.intensity = 0.75f;
            keyLight.transform.rotation = Quaternion.Euler(38f, -28f, 0f);

            GameObject poolLight = new GameObject("Altar Candlelight");
            poolLight.transform.SetParent(lighting, true);
            poolLight.transform.position = new Vector3(0f, 3.2f, -7.6f);
            Light point = poolLight.AddComponent<Light>();
            point.type = LightType.Point;
            point.color = new Color(0.74f, 0.30f, 0.18f);
            point.intensity = 7f;
            point.range = 9f;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.13f, 0.15f, 0.21f);
            RenderSettings.ambientEquatorColor = new Color(0.075f, 0.08f, 0.1f);
            RenderSettings.ambientGroundColor = new Color(0.025f, 0.025f, 0.03f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.055f, 0.06f, 0.08f);
            RenderSettings.fogDensity = 0.012f;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Bloodbound prototype scene built at " + ScenePath);
        }

        // Loads or creates a named project material and applies its display color.
        private static Material CreateMaterial(string name, Color color)
        {
            string path = MaterialPath + "/" + name.Replace(" ", "") + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.name = name;
            material.color = color;
            return material;
        }

        // Creates an identity transform to organize related scene objects in the hierarchy.
        private static Transform CreateGroup(string name)
        {
            return new GameObject(name).transform;
        }

        // Creates a scaled, material-assigned cube under the requested scene hierarchy group.
        private static GameObject CreateBlock(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, true);
            block.transform.position = position;
            block.transform.localScale = scale;
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
            return block;
        }
    }
}
