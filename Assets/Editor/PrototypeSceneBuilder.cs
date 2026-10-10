using System.IO;
using Bloodbound;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bloodbound.Editor
{
    public static class PrototypeSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/BloodboundPrototype.unity";
        private const string MaterialPath = "Assets/Materials";

        [MenuItem("Bloodbound/Build Prototype Scene")]
        public static void BuildPrototype()
        {
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory(MaterialPath);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Material floorMat = CreateMaterial("Ash Stone", new Color(0.16f, 0.17f, 0.19f));
            Material wallMat = CreateMaterial("Weathered Slate", new Color(0.10f, 0.11f, 0.14f));
            Material trimMat = CreateMaterial("Old Brass", new Color(0.38f, 0.27f, 0.12f));
            Material playerMat = CreateMaterial("Valadrin", new Color(0.36f, 0.08f, 0.16f));
            Material enemyMat = CreateMaterial("Thrall Armor", new Color(0.28f, 0.31f, 0.35f));

            CreateBlock("Floor", new Vector3(0f, -0.3f, 0f), new Vector3(22f, 0.6f, 22f), floorMat);
            CreateBlock("North Wall", new Vector3(0f, 2.4f, 11f), new Vector3(22f, 5f, 0.6f), wallMat);
            CreateBlock("East Wall", new Vector3(11f, 2.4f, 0f), new Vector3(0.6f, 5f, 22f), wallMat);
            CreateBlock("West Wall", new Vector3(-11f, 2.4f, 0f), new Vector3(0.6f, 5f, 22f), wallMat);
            CreateBlock("South Wall", new Vector3(0f, 2.4f, -11f), new Vector3(22f, 5f, 0.6f), wallMat);

            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    CreateBlock("Stone Pier", new Vector3(x * 8.5f, 2.2f, z * 8.5f), new Vector3(0.8f, 4.4f, 0.8f), trimMat);
                }
            }

            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Valadrin";
            player.transform.position = new Vector3(0f, 1.05f, 5f);
            player.transform.localScale = new Vector3(0.82f, 1f, 0.82f);
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            player.GetComponent<MeshRenderer>().sharedMaterial = playerMat;
            CharacterController characterController = player.AddComponent<CharacterController>();
            characterController.height = 1.9f;
            characterController.radius = 0.4f;
            characterController.center = new Vector3(0f, 0.95f, 0f);
            player.AddComponent<ValadrinController>();

            GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemy.name = "Vampire Thrall";
            enemy.transform.position = new Vector3(0f, 1.05f, -3.5f);
            enemy.transform.localScale = new Vector3(0.92f, 1.05f, 0.92f);
            enemy.GetComponent<MeshRenderer>().sharedMaterial = enemyMat;
            enemy.AddComponent<EnemyController>();

            CreateBlock("Raised Altar", new Vector3(0f, 0.28f, -8f), new Vector3(4f, 0.55f, 1.4f), trimMat);

            GameObject cameraObject = new GameObject("Fixed Room Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 10.5f, -13f);
            cameraObject.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.2f, 1f), Vector3.up);
            camera.fieldOfView = 46f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.backgroundColor = new Color(0.035f, 0.04f, 0.055f);
            camera.clearFlags = CameraClearFlags.SolidColor;

            GameObject keyLight = new GameObject("Moonlight");
            Light directional = keyLight.AddComponent<Light>();
            directional.type = LightType.Directional;
            directional.color = new Color(0.57f, 0.65f, 0.82f);
            directional.intensity = 0.75f;
            keyLight.transform.rotation = Quaternion.Euler(38f, -28f, 0f);

            GameObject poolLight = new GameObject("Altar Candlelight");
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

        private static GameObject CreateBlock(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.position = position;
            block.transform.localScale = scale;
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
            return block;
        }
    }
}
