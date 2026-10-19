using UnityEngine;

namespace ConveyorTrainer
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PusherActuator : MonoBehaviour
    {
        [SerializeField] private float travel = 1.05f;
        [SerializeField] private float speed = 3.2f;
        private Rigidbody body;
        private Vector3 home;
        private int phase;
        public bool Moving => phase != 0;
        private void Awake() { body = GetComponent<Rigidbody>(); home = body.position; }
        public bool TryStroke() { if (phase != 0) return false; phase = 1; return true; }
        public void ResetStroke()
        {
            phase = 0;
            if (body != null) body.position = home;
        }
        private void FixedUpdate()
        {
            if (phase == 0) return;
            var goal = phase == 1 ? home + Vector3.forward * travel : home;
            var next = Vector3.MoveTowards(body.position, goal, speed * Time.fixedDeltaTime);
            body.MovePosition(next);
            if ((next - goal).sqrMagnitude < 0.00001f) phase = phase == 1 ? 2 : 0;
        }
    }
}
