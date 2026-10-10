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

        private float currentHealth;
        private float nextAttackAt;
        private Transform player;
        private Renderer bodyRenderer;
        private Color baseColor;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsAlive => currentHealth > 0f;

        private void Awake()
        {
            currentHealth = maxHealth;
            bodyRenderer = GetComponentInChildren<Renderer>();
            if (bodyRenderer != null)
                baseColor = bodyRenderer.material.color;
            ValadrinController valadrin = FindFirstObjectByType<ValadrinController>();
            if (valadrin != null)
                player = valadrin.transform;
        }

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

        public void TakeDamage(float amount)
        {
            if (!IsAlive)
                return;
            currentHealth = Mathf.Max(0f, currentHealth - amount);
            if (bodyRenderer != null)
                StartCoroutine(HitFlash());
            if (currentHealth <= 0f)
                gameObject.SetActive(false);
        }

        private System.Collections.IEnumerator HitFlash()
        {
            bodyRenderer.material.color = new Color(1f, 0.45f, 0.45f);
            yield return new WaitForSeconds(0.12f);
            if (bodyRenderer != null && IsAlive)
                bodyRenderer.material.color = baseColor;
        }
    }
}
