using System.Collections.Generic;
using UnityEngine;

namespace Bloodbound
{
    [System.Flags]
    public enum CellDoorSides
    {
        None = 0,
        North = 1,
        East = 2,
        South = 4,
        West = 8
    }

    [ExecuteAlways]
    public sealed class LevelCell : MonoBehaviour
    {
        private static readonly List<LevelCell> activeCells = new List<LevelCell>();

        [Min(2f)] public float width = 20f;
        [Min(2f)] public float depth = 20f;
        [Min(3f)] public float height = 8f;
        [Min(0.1f)] public float wallThickness = 0.5f;
        [Min(1f)] public float wallHeight = 5f;
        [Min(0.8f)] public float doorwayWidth = 3f;
        public CellDoorSides doorways;
        public Transform cameraAnchor;
        [Min(0f)] public float cameraTransitionSeconds = 0.8f;
        public Mesh generatedFloor;
        public Material floorMaterial;
        public Material wallMaterial;
        [HideInInspector] public string editorId;

        public static IReadOnlyList<LevelCell> ActiveCells => activeCells;

        // Registers this room so the camera rig can find active cells without scanning every frame.
        private void OnEnable()
        {
            if (!activeCells.Contains(this))
                activeCells.Add(this);
        }

        // Removes this room from the runtime camera lookup when it is disabled or unloaded.
        private void OnDisable()
        {
            activeCells.Remove(this);
        }

        // Tests whether a world position lies inside this room's local rectangular volume.
        public bool ContainsWorldPoint(Vector3 worldPoint)
        {
            Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
            return Mathf.Abs(localPoint.x) <= width * 0.5f
                && Mathf.Abs(localPoint.z) <= depth * 0.5f
                && localPoint.y >= -2f
                && localPoint.y <= height;
        }

        // Draws the room bounds and camera target in the Scene view for easy editing.
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.35f, 0.78f, 0.82f, 0.9f);
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth));
            Gizmos.matrix = previousMatrix;

            if (cameraAnchor != null)
            {
                Gizmos.color = new Color(0.95f, 0.72f, 0.28f, 1f);
                Vector3 lookTarget = transform.TransformPoint(new Vector3(0f, height * 0.25f, 0f));
                Gizmos.DrawSphere(cameraAnchor.position, 0.18f);
                Gizmos.DrawLine(cameraAnchor.position, lookTarget);
            }
        }
    }
}
