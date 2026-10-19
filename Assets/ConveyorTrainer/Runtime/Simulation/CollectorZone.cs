using UnityEngine;

namespace ConveyorTrainer
{
    public sealed class CollectorZone : MonoBehaviour
    {
        [SerializeField] private bool metalDestination;
        public void Configure(bool isMetalDestination) { metalDestination = isMetalDestination; }
        private void OnTriggerEnter(Collider other)
        { if (other.attachedRigidbody != null && other.attachedRigidbody.TryGetComponent<Washer>(out var washer)) washer.Consume(metalDestination); }
    }
}
