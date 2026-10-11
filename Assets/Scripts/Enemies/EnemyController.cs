using UnityEngine;

namespace Bloodbound
{
    public sealed class EnemyController : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 90f;
        [SerializeField] private float moveSpeed = 1.8f;
        [SerializeField] private float attackRange = 1.65f;
        [SerializeField] private float attackDamage = 12f;
        [SerializeField] private float attackInterval = 1.65f;
        [SerializeField] private float vulnerableDuration = 20f;
        [SerializeField] private float claimFlashDuration = 0.7f;

        private float currentHealth;
        private float nextAttackAt;
        private Transform player;
        private Renderer bodyRenderer;
        private Color baseColor;
        private bool hasBeenClaimed;
        private float vulnerableUntil;
        private Coroutine claimFlash;

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
            else if (Time.time >= nextAttackAt)
            {
                nextAttackAt = Time.time + attackInterval;
                ValadrinController target = player.GetComponent<ValadrinController>();
                if (target != null)
                    target.TakeDamage(attackDamage);
            }
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
