using UnityEngine;

namespace Bloodbound
{
    [RequireComponent(typeof(Camera))]
    public sealed class CellCameraController : MonoBehaviour
    {
        [SerializeField] private Transform target;

        private Transform cameraTransform;
        private LevelCell activeCell;
        private Vector3 transitionStartPosition;
        private Quaternion transitionStartRotation;
        private float transitionDuration;
        private float transitionElapsed;

        // Finds Valadrin and caches the camera transform before room tracking begins.
        private void Awake()
        {
            cameraTransform = transform;
            if (target == null)
            {
                ValadrinController player = FindAnyObjectByType<ValadrinController>();
                if (player != null)
                    target = player.transform;
            }
        }

        // Switches to the anchor of the room Valadrin occupies and blends the camera pose.
        private void LateUpdate()
        {
            if (target == null)
                return;

            LevelCell destination = FindCellForTarget();
            if (destination != null && destination != activeCell && destination.cameraAnchor != null)
                BeginTransition(destination);

            if (activeCell == null || activeCell.cameraAnchor == null)
                return;

            if (transitionDuration <= 0f)
            {
                cameraTransform.SetPositionAndRotation(activeCell.cameraAnchor.position, activeCell.cameraAnchor.rotation);
                return;
            }

            transitionElapsed += Time.deltaTime;
            float blend = Mathf.Clamp01(transitionElapsed / transitionDuration);
            blend = blend * blend * (3f - 2f * blend);
            cameraTransform.SetPositionAndRotation(
                Vector3.Lerp(transitionStartPosition, activeCell.cameraAnchor.position, blend),
                Quaternion.Slerp(transitionStartRotation, activeCell.cameraAnchor.rotation, blend));
        }

        // Chooses the closest cell whose room bounds contain Valadrin.
        private LevelCell FindCellForTarget()
        {
            LevelCell closest = null;
            float closestDistance = float.MaxValue;
            foreach (LevelCell cell in LevelCell.ActiveCells)
            {
                if (cell == null || !cell.ContainsWorldPoint(target.position))
                    continue;

                float distance = (cell.transform.position - target.position).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closest = cell;
                    closestDistance = distance;
                }
            }

            return closest;
        }

        // Stores the current camera pose and starts blending toward the next room anchor.
        private void BeginTransition(LevelCell destination)
        {
            activeCell = destination;
            transitionStartPosition = cameraTransform.position;
            transitionStartRotation = cameraTransform.rotation;
            transitionDuration = destination.cameraTransitionSeconds;
            transitionElapsed = 0f;
        }
    }
}
