using UnityEngine;

namespace ConveyorTrainer
{
    public sealed class SensorZone : MonoBehaviour
    {
        [SerializeField] private ConveyorLine line;
        public void Configure(ConveyorLine conveyor) { line = conveyor; }
        private void OnTriggerEnter(Collider other)
        { if (other.attachedRigidbody != null && other.attachedRigidbody.TryGetComponent<Washer>(out var washer)) line.Detect(washer); }
    }
}
