using UnityEngine;

namespace ConveyorTrainer
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Washer : MonoBehaviour
    {
        [SerializeField] private Renderer visual;
        [SerializeField] private Material metalMaterial;
        [SerializeField] private Material plasticMaterial;
        private ConveyorLine line;
        public Rigidbody Body { get; private set; }
        public bool Metallic { get; private set; }
        public bool Detected { get; set; }
        public bool Consumed { get; private set; }
        public int BornRevision { get; private set; }
        public float ExpiresAt { get; private set; }
        private void Awake() { Body = GetComponent<Rigidbody>(); }
        public void Configure(Renderer renderer, Material metal, Material plastic)
        { visual = renderer; metalMaterial = metal; plasticMaterial = plastic; }
        public void Initialize(ConveyorLine conveyor, bool metallic, int revision)
        {
            line = conveyor; Metallic = metallic; BornRevision = revision;
            ExpiresAt = Time.time + 25f;
            visual.sharedMaterial = metallic ? metalMaterial : plasticMaterial;
        }
        public void Consume(bool metalDestination, bool validDestination = true)
        {
            if (Consumed) return;
            Consumed = true;
            line.Collect(this, metalDestination, validDestination);
            Destroy(gameObject);
        }
    }
}
