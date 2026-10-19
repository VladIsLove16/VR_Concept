using ConveyorTrainer.Domain;
using UnityEngine;

namespace ConveyorTrainer
{
    public sealed class ItemSocket : MonoBehaviour
    {
        [SerializeField] private Location location;
        [SerializeField] private Transform anchor;
        [SerializeField, Min(0.05f)] private float captureRadius = 0.35f;
        public Location Location => location;
        public Transform Anchor => anchor;
        public bool Contains(Vector3 position) => (anchor.position - position).sqrMagnitude <= captureRadius * captureRadius;
        public void Configure(Location destination, Transform pose, float radius = 0.35f)
        { location = destination; anchor = pose; captureRadius = radius; }
        private void OnDrawGizmosSelected()
        { if (anchor != null) { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(anchor.position, captureRadius); } }
    }
}
