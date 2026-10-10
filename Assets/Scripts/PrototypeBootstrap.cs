using UnityEngine;

namespace Bloodbound
{
    public sealed class PrototypeBootstrap : MonoBehaviour
    {
        private void Start()
        {
            BuildRoom();
        }

        private static void BuildRoom()
        {
            Material floor = MakeMaterial("Ash Stone", new Color(0.16f, 0.17f, 0.19f));
            Material wall = MakeMaterial("Weathered Slate", new Color(0.10f, 0.11f, 0.14f));
            Material brass = MakeMaterial("Old Brass", new Color(0.38f, 0.27f, 0.12f));
            Material valadrin = MakeMaterial("Valadrin", new Color(0.36f, 0.08f, 0.16f));
            Material thrall = MakeMaterial("Thrall Armor", new Color(0.28f, 0.31f, 0.35f));

            Block("Floor", new Vector3(0f, -0.3f, 0f), new Vector3(22f, 0.6f, 22f), floor);
            Block("North Wall", new Vector3(0f, 2.4f, 11f), new Vector3(22f, 5f, 0.6f), wall);
            Block("East Wall", new Vector3(11f, 2.4f, 0f), new Vector3(0.6f, 5f, 22f), wall);
            Block("West Wall", new Vector3(-11f, 2.4f, 0f), new Vector3(0.6f, 5f, 22f), wall);
            Block("South Wall", new Vector3(0f, 2.4f, -11f), new Vector3(22f, 5f, 0.6f), wall);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    Block("Stone Pier", new Vector3(x * 8.5f, 2.2f, z * 8.5f), new Vector3(0.8f, 4.4f, 0.8f), brass);
            Block("Raised Altar", new Vector3(0f, 0.28f, -8f), new Vector3(4f, 0.55f, 1.4f), brass);

            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Valadrin";
            player.transform.position = new Vector3(0f, 1.05f, 5f);
            player.transform.localScale = new Vector3(0.82f, 1f, 0.82f);
            player.GetComponent<CapsuleCollider>().enabled = false;
            player.GetComponent<MeshRenderer>().sharedMaterial = valadrin;
            CharacterController character = player.AddComponent<CharacterController>();
            character.height = 1.9f;
            character.radius = 0.4f;
            character.center = new Vector3(0f, 0.95f, 0f);
            player.AddComponent<ValadrinController>();

            GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemy.name = "Vampire Thrall";
            enemy.transform.position = new Vector3(0f, 1.05f, -3.5f);
            enemy.transform.localScale = new Vector3(0.92f, 1.05f, 0.92f);
            enemy.GetComponent<MeshRenderer>().sharedMaterial = thrall;
            enemy.AddComponent<EnemyController>();

            GameObject cameraObject = new GameObject("Fixed Room Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 10.5f, -13f);
            cameraObject.transform.LookAt(new Vector3(0f, 1f, 0f));
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 46f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.backgroundColor = new Color(0.035f, 0.04f, 0.055f);
            camera.clearFlags = CameraClearFlags.SolidColor;

            GameObject moonlight = new GameObject("Moonlight");
            Light directional = moonlight.AddComponent<Light>();
            directional.type = LightType.Directional;
            directional.color = new Color(0.57f, 0.65f, 0.82f);
            directional.intensity = 0.75f;
            moonlight.transform.rotation = Quaternion.Euler(38f, -28f, 0f);

            GameObject candlelight = new GameObject("Altar Candlelight");
            candlelight.transform.position = new Vector3(0f, 3.2f, -7.6f);
            Light point = candlelight.AddComponent<Light>();
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
        }

        private static Material MakeMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Standard");
            Material material = new Material(shader) { name = name, color = color };
            return material;
        }

        private static GameObject Block(string name, Vector3 position, Vector3 scale, Material material)
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
