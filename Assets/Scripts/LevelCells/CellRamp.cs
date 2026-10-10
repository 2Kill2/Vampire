using UnityEngine;

namespace Bloodbound
{
    public sealed class CellRamp : MonoBehaviour
    {
        [Min(1f)] public float width = 3f;
        [Min(1f)] public float run = 4f;
        [Min(0.5f)] public float rise = 2f;
        [HideInInspector] public Mesh generatedMesh;
    }
}
