using System.Collections;
using System.Collections.Generic;
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
        [Header("Sword Swing Hitbox")]
        [SerializeField, Min(0.05f)] private float attackSwingDuration = 0.42f;
        [SerializeField, Range(0f, 1f)] private float attackHitStart = 0.28f;
        [SerializeField, Range(0f, 1f)] private float attackHitEnd = 0.72f;
        [SerializeField] private float swordHitboxLength = 1.15f;
        [SerializeField] private float swordHitboxWidth = 0.28f;
        [SerializeField] private float swordHitboxHeight = 0.18f;

        [Header("Blood Claim")]
        [SerializeField] private float maxBlood = 100f;
        [SerializeField] private float bloodPerClaim = 60f;
        [SerializeField] private float claimRange = 2.2f;
        [SerializeField] private float powerCost = 40f;
        [SerializeField] private float powerDamage = 62f;
        [SerializeField] private float claimDuration = 0.65f;
        [SerializeField] private float claimInterruptDamage = 0.01f;
        [SerializeField] private float powerRadius = 1.85f;
        [Header("Blood Power Swing Hitbox")]
        [SerializeField, Min(0.05f)] private float powerSwingDuration = 0.72f;
        [SerializeField, Range(0f, 1f)] private float powerHitStart = 0.3f;
        [SerializeField, Range(0f, 1f)] private float powerHitEnd = 0.82f;
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
        private Transform swordHitboxTransform;
        private BoxCollider swordHitboxCollider;
        private Renderer swordHitboxRenderer;
        private Transform powerHitboxTransform;
        private BoxCollider powerHitboxCollider;
        private Renderer powerHitboxRenderer;
        private bool isAttacking;
        private bool isUsingBloodPower;
        private readonly HashSet<EnemyController> hitEnemiesThisSwing = new HashSet<EnemyController>();

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
            CreateSwingHitboxes();
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
            if (swordHitboxCollider != null) swordHitboxCollider.enabled = false;
            if (powerHitboxCollider != null) powerHitboxCollider.enabled = false;
            if (powerHitboxTransform != null) powerHitboxTransform.gameObject.SetActive(false);
            if (swordHitboxRenderer != null) swordHitboxRenderer.material.color = new Color(0.58f, 0.58f, 0.64f, 0.75f);
            isAttacking = false;
            isUsingBloodPower = false;
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
            if (isAttacking || isUsingBloodPower || Time.time < attackReadyAt || currentStamina < attackCost)
                return;

            currentStamina -= attackCost;
            attackReadyAt = Time.time + attackCooldown;
            FaceAttackDirection();
            StartCoroutine(SwingRoutine(false));
        }

        private void FaceAttackDirection()
        {
            Vector3 direction = lockedTarget != null && lockedTarget.IsAlive
                ? lockedTarget.transform.position - transform.position
                : transform.forward;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private void CreateSwingHitboxes()
        {
            swordHitboxTransform = CreateSwingPivot("Sword Swing Pivot", new Vector3(0f, 0.95f, 0.38f));
            CreateHitboxVisual("Sword Hitbox", swordHitboxTransform, new Vector3(0f, 0f, swordHitboxLength * 0.5f),
                new Vector3(swordHitboxWidth, swordHitboxHeight, swordHitboxLength), new Color(0.58f, 0.58f, 0.64f, 0.75f),
                out swordHitboxCollider, out swordHitboxRenderer);
            swordHitboxTransform.gameObject.SetActive(true);

            float powerLength = Mathf.Max(0.35f, powerRadius);
            powerHitboxTransform = CreateSwingPivot("Blood Power Swing Pivot", new Vector3(0f, 0.95f, 0.4f));
            CreateHitboxVisual("Blood Power Hitbox", powerHitboxTransform, new Vector3(0f, 0f, powerLength * 0.5f),
                new Vector3(Mathf.Max(0.5f, powerRadius * 0.75f), Mathf.Max(0.5f, powerRadius * 0.75f), powerLength),
                new Color(0.35f, 0.08f, 0.12f, 0.55f), out powerHitboxCollider, out powerHitboxRenderer);
            powerHitboxTransform.gameObject.SetActive(false);
        }

        private Transform CreateSwingPivot(string objectName, Vector3 localPosition)
        {
            GameObject pivot = new GameObject(objectName);
            pivot.transform.SetParent(transform, false);
            pivot.transform.localPosition = localPosition;
            return pivot.transform;
        }

        private void CreateHitboxVisual(string objectName, Transform parent, Vector3 localPosition, Vector3 localScale,
            Color idleColor, out BoxCollider boxCollider, out Renderer visualRenderer)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = objectName;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localScale = localScale;
            boxCollider = visual.GetComponent<BoxCollider>();
            boxCollider.enabled = false;
            visualRenderer = visual.GetComponent<Renderer>();
            visualRenderer.material.color = idleColor;
        }

        private IEnumerator SwingRoutine(bool bloodPower)
        {
            isAttacking = !bloodPower;
            isUsingBloodPower = bloodPower;
            Transform hitbox = bloodPower ? powerHitboxTransform : swordHitboxTransform;
            BoxCollider box = bloodPower ? powerHitboxCollider : swordHitboxCollider;
            Renderer hitboxRenderer = bloodPower ? powerHitboxRenderer : swordHitboxRenderer;
            float duration = bloodPower ? powerSwingDuration : attackSwingDuration;
            float activeStart = bloodPower ? powerHitStart : attackHitStart;
            float activeEnd = bloodPower ? powerHitEnd : attackHitEnd;
            float damage = bloodPower ? powerDamage : attackDamage;
            Color activeColor = bloodPower ? new Color(1f, 0.08f, 0.18f, 0.8f) : new Color(1f, 0.12f, 0.08f, 0.8f);

            hitEnemiesThisSwing.Clear();
            hitbox.gameObject.SetActive(true);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float normalizedTime = Mathf.Clamp01(elapsed / duration);
                float swing = Mathf.SmoothStep(0f, 1f, normalizedTime);
                hitbox.localRotation = Quaternion.Euler(0f, Mathf.Lerp(-70f, 70f, swing), 0f);
                bool hitboxActive = normalizedTime >= activeStart && normalizedTime <= activeEnd;
                box.enabled = hitboxActive;
                hitboxRenderer.material.color = hitboxActive ? activeColor : (bloodPower ? new Color(0.35f, 0.08f, 0.12f, 0.55f) : new Color(0.58f, 0.58f, 0.64f, 0.75f));
                if (hitboxActive)
                    ApplySwingHitbox(box, damage);
                elapsed += Time.deltaTime;
                yield return null;
            }

            box.enabled = false;
            hitbox.localRotation = Quaternion.identity;
            if (bloodPower)
                hitbox.gameObject.SetActive(false);
            else
                hitboxRenderer.material.color = new Color(0.58f, 0.58f, 0.64f, 0.75f);
            isAttacking = false;
            isUsingBloodPower = false;
        }

        private void ApplySwingHitbox(BoxCollider box, float damage)
        {
            Vector3 center = box.transform.TransformPoint(box.center);
            Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, box.transform.lossyScale);
            Collider[] overlaps = Physics.OverlapBox(center, halfExtents, box.transform.rotation);
            foreach (Collider overlap in overlaps)
            {
                EnemyController enemy = overlap.GetComponentInParent<EnemyController>();
                if (enemy == null || !enemy.IsAlive || !hitEnemiesThisSwing.Add(enemy))
                    continue;
                enemy.TakeDamage(damage);
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
            if (isAttacking || isUsingBloodPower || Time.time < powerReadyAt || currentBlood < powerCost)
                return;

            currentBlood -= powerCost;
            powerReadyAt = Time.time + 0.8f;
            FaceAttackDirection();
            StartCoroutine(SwingRoutine(true));
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
