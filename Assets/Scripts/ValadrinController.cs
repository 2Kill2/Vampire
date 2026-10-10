using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bloodbound
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ValadrinController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 4.2f;
        [SerializeField] private float rotationSpeed = 720f;
        [SerializeField] private float gravity = -22f;
        [SerializeField] private float dodgeSpeed = 10f;
        [SerializeField] private float dodgeDuration = 0.22f;

        [Header("Combat")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float staminaRecovery = 24f;
        [SerializeField] private float attackCost = 22f;
        [SerializeField] private float attackDamage = 28f;
        [SerializeField] private float attackReach = 1.7f;
        [SerializeField] private float attackCooldown = 0.5f;

        private CharacterController characterController;
        private Vector3 verticalVelocity;
        private Vector3 dodgeDirection;
        private float currentHealth;
        private float currentStamina;
        private float attackReadyAt;
        private bool isDodging;
        private EnemyController lockedTarget;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public float CurrentStamina => currentStamina;
        public float MaxStamina => maxStamina;
        public EnemyController LockedTarget => lockedTarget;
        public bool IsDefeated => currentHealth <= 0f;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            currentHealth = maxHealth;
            currentStamina = maxStamina;
        }

        private void Update()
        {
            if (IsDefeated)
            {
                if (Input.GetKeyDown(KeyCode.R))
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Tab))
                ToggleTargetLock();

            if (Input.GetKeyDown(KeyCode.J) || Input.GetMouseButtonDown(0) || Input.GetButtonDown("Fire1"))
                TryAttack();

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetButtonDown("Fire2"))
                TryDodge();

            currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRecovery * Time.deltaTime);
            MoveCharacter();
            FaceDesiredDirection();
        }

        private void MoveCharacter()
        {
            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            input = Vector2.ClampMagnitude(input, 1f);

            Transform cameraTransform = Camera.main != null ? Camera.main.transform : transform;
            Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            Vector3 move = (forward * input.y + right * input.x).normalized;

            if (isDodging)
                return;

            if (move.sqrMagnitude > 0.001f)
                characterController.Move(move * moveSpeed * Time.deltaTime);

            if (characterController.isGrounded && verticalVelocity.y < 0f)
                verticalVelocity.y = -2f;
            verticalVelocity.y += gravity * Time.deltaTime;
            characterController.Move(verticalVelocity * Time.deltaTime);
        }

        private void FaceDesiredDirection()
        {
            Vector3 direction = Vector3.zero;
            if (lockedTarget != null && lockedTarget.IsAlive)
                direction = lockedTarget.transform.position - transform.position;
            else
            {
                Transform cameraTransform = Camera.main != null ? Camera.main.transform : transform;
                Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
                direction = forward * Input.GetAxisRaw("Vertical") + right * Input.GetAxisRaw("Horizontal");
            }

            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
                return;
            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        private void TryAttack()
        {
            if (Time.time < attackReadyAt || currentStamina < attackCost)
                return;

            currentStamina -= attackCost;
            attackReadyAt = Time.time + attackCooldown;
            Vector3 origin = transform.position + Vector3.up * 1f + transform.forward * 0.7f;
            Collider[] hits = Physics.OverlapSphere(origin, attackReach);
            foreach (Collider hit in hits)
            {
                EnemyController enemy = hit.GetComponentInParent<EnemyController>();
                if (enemy != null && enemy.IsAlive)
                    enemy.TakeDamage(attackDamage);
            }
        }

        private void TryDodge()
        {
            if (isDodging || currentStamina < 28f)
                return;

            currentStamina -= 28f;
            Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            Transform cameraTransform = Camera.main != null ? Camera.main.transform : transform;
            Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            dodgeDirection = input.sqrMagnitude > 0.01f ? (forward * input.z + right * input.x).normalized : transform.forward;
            StartCoroutine(DodgeRoutine());
        }

        private IEnumerator DodgeRoutine()
        {
            isDodging = true;
            float endTime = Time.time + dodgeDuration;
            while (Time.time < endTime)
            {
                characterController.Move(dodgeDirection * dodgeSpeed * Time.deltaTime);
                yield return null;
            }
            isDodging = false;
        }

        private void ToggleTargetLock()
        {
            if (lockedTarget != null)
            {
                lockedTarget = null;
                return;
            }

            EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
            float closestDistance = float.MaxValue;
            foreach (EnemyController enemy in enemies)
            {
                if (!enemy.IsAlive)
                    continue;
                float distance = Vector3.Distance(transform.position, enemy.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    lockedTarget = enemy;
                }
            }
        }

        public void TakeDamage(float amount)
        {
            if (IsDefeated)
                return;
            currentHealth = Mathf.Max(0f, currentHealth - amount);
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(18, 18, 300, 112), "VALADRIN");
            DrawBar(new Rect(32, 48, 270, 18), currentHealth / maxHealth, new Color(0.62f, 0.12f, 0.2f), "Health");
            DrawBar(new Rect(32, 78, 270, 18), currentStamina / maxStamina, new Color(0.76f, 0.63f, 0.2f), "Stamina");

            GUI.Box(new Rect(18, Screen.height - 112, 380, 92), "CONTROLS");
            GUI.Label(new Rect(32, Screen.height - 84, 350, 20), "Move: WASD / left stick    Attack: J / left mouse");
            GUI.Label(new Rect(32, Screen.height - 62, 350, 20), "Dodge: Space / left Shift    Target lock: Tab");

            if (lockedTarget != null && lockedTarget.IsAlive)
            {
                float ratio = lockedTarget.CurrentHealth / lockedTarget.MaxHealth;
                DrawBar(new Rect(Screen.width * 0.5f - 145, 34, 290, 18), ratio, new Color(0.55f, 0.08f, 0.13f), "Vampire Thrall");
            }
            if (IsDefeated)
                GUI.Box(new Rect(Screen.width * 0.5f - 170, Screen.height * 0.5f - 35, 340, 70), "VALADRIN HAS FALLEN\nPress R to restart the encounter");
            else if (FindFirstObjectByType<EnemyController>() == null || !FindFirstObjectByType<EnemyController>().IsAlive)
                GUI.Box(new Rect(Screen.width * 0.5f - 160, 22, 320, 44), "ENCOUNTER CLEARED  |  Press R to restart");
        }

        private static void DrawBar(Rect rect, float ratio, Color fill, string label)
        {
            GUI.color = new Color(0.08f, 0.08f, 0.1f, 0.92f);
            GUI.Box(rect, GUIContent.none);
            GUI.color = fill;
            GUI.DrawTexture(new Rect(rect.x + 2, rect.y + 2, (rect.width - 4) * Mathf.Clamp01(ratio), rect.height - 4), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 7, rect.y - 1, rect.width - 10, rect.height), label);
        }
    }
}
