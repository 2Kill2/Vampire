using UnityEngine;

namespace Bloodbound
{
    public sealed class CellPitHazard : MonoBehaviour
    {
        // Defeats Valadrin when she drops into a pit trigger.
        private void OnTriggerEnter(Collider other)
        {
            ValadrinController player = other.GetComponentInParent<ValadrinController>();
            if (player != null)
                player.TakeDamage(player.MaxHealth * 2f);
        }
    }
}
