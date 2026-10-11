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

        [Header("Blood Claim")]
        [SerializeField] private float maxBlood = 100f;
        [SerializeField] private float bloodPerClaim = 60f;
        [SerializeField] private float claimRange = 2.2f;
        [SerializeField] private float powerCost = 40f;
        [SerializeField] private float powerDamage = 62f;
        [SerializeField] private float claimDuration = 0.65f;
        [SerializeField] private float claimInterruptDamage = 0.01f;
        [SerializeField] private float powerRadius = 1.85f;
        [Header("Blood to Health Conversion")]
        [SerializeField, Min(0f)] private float bloodDrainPerSecond = 2f;
        [SerializeField, Min(0f)] private float healthPerBloodUnit = 0.5f;
        [Header("Target Reticle")]
        [SerializeField] private Sprite targetReticle;
        [SerializeField] private Vector2 reticleSize = new Vector2(48f, 48f);
        [SerializeField] private Vector3 targetReticleOffset = new Vector3(0f, 1.2f, 0f);

        private CharacterController characterController;
        private Vector3 verticalVelocity;
        private Vector3 dodgeDirection;
        private float currentHealth;
        private float currentStamina;
        private float attackReadyAt;
        private float currentBlood;
        private float powerReadyAt;
        private bool isDodging;
        private EnemyController lockedTarget;
        private global::ValadrinControls controls;
        private EnemyController claimingEnemy;
        private float claimCompletesAt;
        private string bloodMessage;
        private float bloodMessageUntil;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public float CurrentStamina => currentStamina;
        public float MaxStamina => maxStamina;
        public float CurrentBlood => currentBlood;
        public float MaxBlood => maxBlood;
        public EnemyController LockedTarget => lockedTarget;
        public bool IsDefeated => currentHealth <= 0f;

        // Creates Valadrin's runtime state and constructs the generated input wrapper.
        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            currentHealth = maxHealth;
            currentStamina = maxStamina;
            currentBlood = 0f;

            controls = new global::ValadrinControls();
        }

        // Enables the generated gameplay action map while this component is active.
        private void OnEnable()
        {
            if (controls != null)
                controls.Enable();
        }

        // Disables the gameplay action map when Valadrin is inactive.
        private void OnDisable()
        {
            if (controls != null)
                controls.Disable();
        }

        // Releases the generated input asset when this controller is destroyed.
        private void OnDestroy()
        {
            controls?.Dispose();
        }

        // Reads gameplay actions and advances stamina, movement, and combat each frame.
        private void Update()
        {
            if (claimingEnemy != null && (!claimingEnemy.CanBeClaimed || Vector3.Distance(transform.position, claimingEnemy.transform.position) > claimRange))
                CancelClaim("Blood Claim interrupted");
            if (claimingEnemy != null && Time.time >= claimCompletesAt)
                CompleteClaim();
            if (controls.Gameplay.Restart.WasPressedThisFrame())
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                return;
            }

            if (IsDefeated)
                return;

            if (lockedTarget != null && (!lockedTarget.IsAlive || !lockedTarget.gameObject.activeInHierarchy))
                lockedTarget = null;
            ConvertBloodToHealth();

            if (controls.Gameplay.LockOn.WasPressedThisFrame())
                ToggleTargetLock();

            if (controls.Gameplay.Attack.WasPressedThisFrame())
                TryAttack();

            if (controls.Gameplay.Claim.WasPressedThisFrame())
                TryClaimBlood();

            if (controls.Gameplay.BloodPower.WasPressedThisFrame())
                TryBloodPower();

            if (controls.Gameplay.Dodge.WasPressedThisFrame())
                TryDodge();

            currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRecovery * Time.deltaTime);
            MoveCharacter();
            FaceDesiredDirection();
        }

        private void ConvertBloodToHealth()
        {
            if (currentBlood <= 0f || currentHealth >= maxHealth || bloodDrainPerSecond <= 0f || healthPerBloodUnit <= 0f)
                return;
            float bloodToSpend = Mathf.Min(currentBlood, bloodDrainPerSecond * Time.deltaTime,
                (maxHealth - currentHealth) / healthPerBloodUnit);
            currentBlood -= bloodToSpend;
            currentHealth = Mathf.Min(maxHealth, currentHealth + bloodToSpend * healthPerBloodUnit);
        }

        // Converts camera-relative movement input into CharacterController motion and gravity.
        private void MoveCharacter()
        {
            Vector2 input = controls.Gameplay.Move.ReadValue<Vector2>();
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

        // Rotates Valadrin toward her locked target or current movement direction.
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
                Vector2 input = controls.Gameplay.Move.ReadValue<Vector2>();
                direction = forward * input.y + right * input.x;
            }

            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
                return;
            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // Spends stamina and damages living enemies in Valadrin's forward attack area.
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

        private void TryClaimBlood()
        {
            if (claimingEnemy != null || IsDefeated) return;
            EnemyController closest = null;
            float closestDistance = claimRange;
            foreach (EnemyController enemy in FindObjectsByType<EnemyController>())
            {
                if (!enemy.CanBeClaimed)
                    continue;
                float distance = Vector3.Distance(transform.position, enemy.transform.position);
                if (distance <= closestDistance)
                {
                    closest = enemy;
                    closestDistance = distance;
                }
            }

            if (closest != null) { claimingEnemy = closest; claimCompletesAt = Time.time + claimDuration; ShowBloodMessage("Claiming Blood..."); }
        }

        private void CompleteClaim()
        {
            EnemyController target = claimingEnemy; claimingEnemy = null;
            if (target == null || !target.CanBeClaimed || Vector3.Distance(transform.position, target.transform.position) > claimRange || !target.TryClaim()) { ShowBloodMessage("Blood Claim failed"); return; }
            currentBlood = Mathf.Min(maxBlood, currentBlood + bloodPerClaim);
            ShowBloodMessage("Blood claimed  +" + bloodPerClaim.ToString("0"));
        }

        private void CancelClaim(string message) { claimingEnemy = null; ShowBloodMessage(message); }
        private void ShowBloodMessage(string message) { bloodMessage = message; bloodMessageUntil = Time.time + 1.5f; }

        private void TryBloodPower()
        {
            if (Time.time < powerReadyAt || currentBlood < powerCost)
                return;

            currentBlood -= powerCost;
            powerReadyAt = Time.time + 0.8f;
            Vector3 origin = transform.position + Vector3.up + transform.forward * 0.8f;
            foreach (Collider hit in Physics.OverlapSphere(origin, powerRadius))
            {
                EnemyController enemy = hit.GetComponentInParent<EnemyController>();
                if (enemy != null && enemy.IsAlive)
                    enemy.TakeDamage(powerDamage);
            }
        }

        // Spends stamina and starts a short directional dodge using the current move input.
        private void TryDodge()
        {
            if (isDodging || currentStamina < 28f)
                return;

            currentStamina -= 28f;
            Vector2 input = controls.Gameplay.Move.ReadValue<Vector2>();
            Transform cameraTransform = Camera.main != null ? Camera.main.transform : transform;
            Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            dodgeDirection = input.sqrMagnitude > 0.01f ? (forward * input.y + right * input.x).normalized : transform.forward;
            StartCoroutine(DodgeRoutine());
        }

        // Moves Valadrin quickly in the chosen dodge direction for the dodge duration.
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

        // Toggles lock-on to the nearest living enemy in the scene.
        private void ToggleTargetLock()
        {
            if (lockedTarget != null)
            {
                lockedTarget = null;
                return;
            }

            EnemyController[] enemies = FindObjectsByType<EnemyController>();
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

        // Applies incoming damage and clamps Valadrin's health at zero.
        public void TakeDamage(float amount)
        {
            if (IsDefeated)
                return;
            if (claimingEnemy != null && amount >= claimInterruptDamage) CancelClaim("Blood Claim interrupted by damage");
            currentHealth = Mathf.Max(0f, currentHealth - amount);
        }

        // Draws the prototype health, stamina, target, and encounter status interface.
        private void OnGUI()
        {
            GUI.Box(new Rect(18, 18, 300, 136), "VALADRIN");
            DrawBar(new Rect(32, 48, 270, 18), currentHealth / maxHealth, new Color(0.62f, 0.12f, 0.2f), "Health");
            DrawBar(new Rect(32, 78, 270, 18), currentStamina / maxStamina, new Color(0.76f, 0.63f, 0.2f), "Stamina");
            DrawBar(new Rect(32, 108, 270, 18), currentBlood / maxBlood, new Color(0.55f, 0.08f, 0.18f), "Blood");

            GUI.Box(new Rect(18, Screen.height - 140, 440, 120), "CONTROLS");
            GUI.Label(new Rect(32, Screen.height - 112, 420, 20), "Move: WASD / left stick    Attack: J / left mouse / X");
            GUI.Label(new Rect(32, Screen.height - 90, 420, 20), "Dodge: Space / Shift / A    Target lock: Tab / R3");
            GUI.Label(new Rect(32, Screen.height - 68, 420, 20), "Claim: E / Y near fallen thrall    Blood Power: Q / RB");
            if (Time.time < bloodMessageUntil) GUI.Box(new Rect(Screen.width * 0.5f - 180, Screen.height * 0.7f, 360, 42), bloodMessage);

            if (lockedTarget != null && lockedTarget.IsAlive)
            {
                float ratio = lockedTarget.CurrentHealth / lockedTarget.MaxHealth;
                DrawBar(new Rect(Screen.width * 0.5f - 145, 34, 290, 18), ratio, new Color(0.55f, 0.08f, 0.13f), "Vampire Thrall");
                DrawTargetReticle();
            }
            if (IsDefeated)
                GUI.Box(new Rect(Screen.width * 0.5f - 170, Screen.height * 0.5f - 35, 340, 70), "VALADRIN HAS FALLEN\nPress R to restart the encounter");
            else
            {
                EnemyController enemy = FindAnyObjectByType<EnemyController>();
                if (enemy == null || !enemy.IsAlive)
                {
                    if (enemy != null && enemy.CanBeClaimed && Vector3.Distance(transform.position, enemy.transform.position) <= claimRange)
                        GUI.Box(new Rect(Screen.width * 0.5f - 180, Screen.height * 0.58f, 360, 46), "Press E / Y to claim Blood from the fallen thrall");
                    else if (enemy != null && enemy.CanBeClaimed)
                        GUI.Box(new Rect(Screen.width * 0.5f - 160, 22, 320, 44), "THRALL DEFEATED  |  Approach to claim Blood");
                    else
                        GUI.Box(new Rect(Screen.width * 0.5f - 160, 22, 320, 44), "ENCOUNTER CLEARED  |  Press R to restart");
                }
            }
        }

        private void DrawTargetReticle()
        {
            if (targetReticle == null || lockedTarget == null || !lockedTarget.IsAlive || Camera.main == null)
                return;
            Vector3 screenPosition = Camera.main.WorldToScreenPoint(lockedTarget.transform.position + targetReticleOffset);
            if (screenPosition.z <= 0f)
                return;
            Rect rect = new Rect(screenPosition.x - reticleSize.x * 0.5f,
                Screen.height - screenPosition.y - reticleSize.y * 0.5f, reticleSize.x, reticleSize.y);
            GUI.DrawTexture(rect, targetReticle.texture, ScaleMode.ScaleToFit, true);
        }

        // Draws a labeled background bar and a clamped colored fill for a normalized value.
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
