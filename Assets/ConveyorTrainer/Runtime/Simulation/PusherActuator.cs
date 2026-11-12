using System;
using UnityEngine;

namespace ConveyorTrainer
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PusherActuator : MonoBehaviour
    {
        [Serializable]
        public struct MotionVisual
        {
            public Transform group;
            public MeshRenderer rod;
        }

        [SerializeField] private float travel = 1.05f;
        [SerializeField] private float speed = 3.2f;
        private Rigidbody body;
        private Collider headCollider;
        private Vector3 home;
        private Transform movingPart;
        private Vector3 movingPartHome;
        private Transform rod;
        private Vector3 rodHome;
        private Vector3 rodScale;
        private float rodLength;
        private int phase;

        public bool Moving => phase != 0;

        public void ConfigureStroke(float distance, float strokeSpeed)
        {
            travel = distance;
            speed = strokeSpeed;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            headCollider = GetComponentInChildren<Collider>();
            home = body.position;
            ResetStroke();
        }

        public void BindMovingPart(MotionVisual part)
        {
            if (movingPart == part.group) return;
            ResetStroke();
            movingPart = part.group;
            if (movingPart != null) movingPartHome = movingPart.localPosition;
            rod = part.rod != null ? part.rod.transform : null;
            if (rod == null) return;
            rodHome = rod.localPosition;
            rodScale = rod.localScale;
            rodLength = Mathf.Max(0.01f, part.rod.localBounds.size.z * Mathf.Abs(rod.lossyScale.z));
        }

        public bool TryStroke()
        {
            if (phase != 0) return false;
            phase = 1;
            if (headCollider != null) headCollider.enabled = true;
            return true;
        }

        public void ResetStroke()
        {
            phase = 0;
            if (body != null) body.position = home;
            if (headCollider != null) headCollider.enabled = false;
            ApplyVisualOffset(Vector3.zero);
        }

        private void FixedUpdate()
        {
            if (phase == 0) return;
            var goal = phase == 1 ? home + transform.forward * travel : home;
            var next = Vector3.MoveTowards(body.position, goal, speed * Time.fixedDeltaTime);
            body.MovePosition(next);
            ApplyVisualOffset(next - home);
            if ((next - goal).sqrMagnitude >= 0.00001f) return;
            phase = phase == 1 ? 2 : 0;
            if (phase == 0 && headCollider != null) headCollider.enabled = false;
        }

        private void ApplyVisualOffset(Vector3 offset)
        {
            if (movingPart != null)
                movingPart.localPosition = movingPartHome + movingPart.parent.InverseTransformVector(offset);
            if (rod == null) return;
            // Keep the rear end inside the housing while the front follows the head.
            rod.localPosition = rodHome - rod.parent.InverseTransformVector(offset * 0.5f);
            rod.localScale = new Vector3(rodScale.x, rodScale.y, rodScale.z * (1f + offset.magnitude / rodLength));
        }
    }
}
