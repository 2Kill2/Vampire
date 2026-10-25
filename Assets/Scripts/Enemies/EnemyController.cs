using UnityEngine;
using System.Collections;

namespace Bloodbound
{
    public enum EnemyType
    {
        SwordCarrier,
        SpearCarrier,
        BowCarrier
    }

    public sealed class EnemyController : MonoBehaviour
    {
        [Header("Enemy Type")]
        [Tooltip("Sword Carrier is implemented. Spear and Bow Carrier are selectable placeholders for later work.")]
        [SerializeField] private EnemyType enemyType = EnemyType.SwordCarrier;
        [SerializeField] private float maxHealth = 90f;
        [SerializeField] private float moveSpeed = 1.8f;
        [SerializeField] private float attackRange = 1.85f;
        [SerializeField] private float attackDamage = 12f;
        [SerializeField] private float attackInterval = 1.65f;
        [SerializeField] private float vulnerableDuration = 20f;
        [SerializeField] private float claimFlashDuration = 0.7f;
        [Header("Sword Carrier Attack")]
        [SerializeField, Min(0.05f)] private float swordSwingDuration = 0.48f;
        [SerializeField, Range(0f, 1f)] private float swordHitStart = 0.35f;
        [SerializeField, Range(0f, 1f)] private float swordHitEnd = 0.68f;
        [SerializeField] private float swordLength = 1.2f;
        [SerializeField] private float swordWidth = 0.22f;
        [SerializeField] private float swordThickness = 0.16f;

        private float currentHealth;
        private float nextAttackAt;
        private Transform player;
        private Renderer bodyRenderer;
        private Color baseColor;
        private bool hasBeenClaimed;
        private float vulnerableUntil;
        private Coroutine claimFlash;
        private Coroutine swordAttackRoutine;
        private Transform swordPivot;
        private BoxCollider swordHitbox;
        private Renderer swordRenderer;
        private bool isAttacking;

        private static readonly Color SwordIdleColor = new Color(0.58f, 0.58f, 0.64f);
        private static readonly Color SwordActiveColor = new Color(1f, 0.12f, 0.08f);

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsAlive => currentHealth > 0f;
        public bool CanBeClaimed => !IsAlive && !hasBeenClaimed && Time.time <= vulnerableUntil;

        // Initializes enemy health, caches its renderer, and finds Valadrin.
        private void Awake()
        {
            currentHealth = maxHealth;
            bodyRenderer = GetComponentInChildren<Renderer>();
            if (bodyRenderer != null)
                baseColor = bodyRenderer.material.color;
            if (enemyType == EnemyType.SwordCarrier)
                CreateSword();
            ValadrinController valadrin = FindAnyObjectByType<ValadrinController>();
            if (valadrin != null)
                player = valadrin.transform;
        }

        // Pursues Valadrin until in range, then attacks on a timed interval.
        private void Update()
        {
            if (!IsAlive || player == null)
                return;

            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;
            if (distance > attackRange)
            {
                Vector3 direction = toPlayer.normalized;
                transform.position += direction * moveSpeed * Time.deltaTime;
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
            else if (Time.time >= nextAttackAt && !isAttacking && enemyType == EnemyType.SwordCarrier)
            {
                nextAttackAt = Time.time + attackInterval;
                ValadrinController target = player.GetComponent<ValadrinController>();
                if (target != null)
                {
                    transform.rotation = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
                    swordAttackRoutine = StartCoroutine(SwordAttack(target));
                }
            }
        }

        private void CreateSword()
        {
            GameObject pivotObject = new GameObject("Enemy Sword Pivot");
            swordPivot = pivotObject.transform;
            swordPivot.SetParent(transform, false);
            // The player controller's center is near the root; keep the blade at chest height
            // instead of the head/top of the CharacterController capsule.
            swordPivot.localPosition = new Vector3(0f, 0.45f, 0.36f);

            GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade.name = "Enemy Sword Hitbox";
            blade.transform.SetParent(swordPivot, false);
            blade.transform.localPosition = new Vector3(0f, 0f, swordLength * 0.5f);
            blade.transform.localScale = new Vector3(swordWidth, swordThickness, swordLength);
            swordHitbox = blade.GetComponent<BoxCollider>();
            swordHitbox.enabled = false;
            swordRenderer = blade.GetComponent<Renderer>();
            swordRenderer.material.color = SwordIdleColor;
        }

        private IEnumerator SwordAttack(ValadrinController target)
        {
            isAttacking = true;
            float elapsed = 0f;
            bool hasHit = false;
            while (elapsed < swordSwingDuration && IsAlive && target != null && !target.IsDefeated)
            {
                float normalizedTime = Mathf.Clamp01(elapsed / swordSwingDuration);
                float swing = Mathf.SmoothStep(0f, 1f, normalizedTime);
                swordPivot.localRotation = Quaternion.Euler(0f, Mathf.Lerp(-65f, 65f, swing), 0f);
                bool active = normalizedTime >= swordHitStart && normalizedTime <= swordHitEnd;
                swordHitbox.enabled = active;
                swordRenderer.material.color = active ? SwordActiveColor : SwordIdleColor;

                if (active && !hasHit)
                {
                    Vector3 center = swordHitbox.transform.TransformPoint(swordHitbox.center);
                    Vector3 halfExtents = Vector3.Scale(swordHitbox.size * 0.5f, swordHitbox.transform.lossyScale);
                    foreach (Collider overlap in Physics.OverlapBox(center, halfExtents, swordHitbox.transform.rotation))
                    {
                        ValadrinController hitPlayer = overlap.GetComponentInParent<ValadrinController>();
                        if (hitPlayer == target)
                        {
                            target.TakeDamage(attackDamage);
                            hasHit = true;
                            break;
                        }
                    }
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (swordHitbox != null) swordHitbox.enabled = false;
            if (swordRenderer != null) swordRenderer.material.color = SwordIdleColor;
            if (swordPivot != null) swordPivot.localRotation = Quaternion.identity;
            isAttacking = false;
            swordAttackRoutine = null;
        }

        // Reduces enemy health, flashes on hit, and deactivates the enemy when defeated.
        public void TakeDamage(float amount)
        {
            if (!IsAlive)
                return;
            currentHealth = Mathf.Max(0f, currentHealth - amount);
            if (bodyRenderer != null)
                StartCoroutine(HitFlash());
            if (currentHealth <= 0f)
            {
                if (swordAttackRoutine != null)
                {
                    StopCoroutine(swordAttackRoutine);
                    swordAttackRoutine = null;
                    isAttacking = false;
                    swordHitbox.enabled = false;
                    swordRenderer.material.color = SwordIdleColor;
                    swordPivot.localRotation = Quaternion.identity;
                }
                hasBeenClaimed = false;
                vulnerableUntil = Time.time + vulnerableDuration;
                if (bodyRenderer != null)
                    bodyRenderer.material.color = new Color(0.18f, 0.06f, 0.09f);
            }
        }

        public bool TryClaim()
        {
            if (!CanBeClaimed)
                return false;
            hasBeenClaimed = true;
            if (claimFlash != null) StopCoroutine(claimFlash);
            claimFlash = StartCoroutine(ClaimFlash());
            return true;
        }

        private System.Collections.IEnumerator ClaimFlash()
        {
            if (bodyRenderer == null) yield break;
            bodyRenderer.material.color = new Color(0.65f, 0.05f, 0.2f);
            yield return new WaitForSeconds(claimFlashDuration);
            if (bodyRenderer != null) bodyRenderer.material.color = Color.black;
            claimFlash = null;
        }

        // Briefly tints the enemy red before restoring its original material color.
        private System.Collections.IEnumerator HitFlash()
        {
            bodyRenderer.material.color = new Color(1f, 0.45f, 0.45f);
            yield return new WaitForSeconds(0.12f);
            if (bodyRenderer != null && IsAlive)
                bodyRenderer.material.color = baseColor;
        }
    }
}
