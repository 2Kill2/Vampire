using UnityEngine;

namespace Bloodbound
{
    public sealed class LevelCellPit : MonoBehaviour
    {
        [Min(0.5f)] public float pitWidth = 2f;
        [Min(0.5f)] public float pitDepth = 2f;

        // Draws the opening footprint in the Scene view while the floor mesh is rebuilt.
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.9f, 0.15f, 0.18f, 0.9f);
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(pitWidth, 0.05f, pitDepth));
            Gizmos.matrix = previousMatrix;
        }
    }
}
