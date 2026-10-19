using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace ConveyorTrainer
{
    public sealed class SafeTeleportationArea : TeleportationArea
    {
        [SerializeField] private LayerMask obstacles = 1 << 8;
        public bool HasClearance(Vector3 point) => !Physics.CheckCapsule(point + Vector3.up * 0.25f,
            point + Vector3.up * 1.55f, 0.22f, obstacles, QueryTriggerInteraction.Ignore);
        protected override bool GenerateTeleportRequest(IXRInteractor interactor, RaycastHit hit, ref TeleportRequest request)
            => HasClearance(hit.point) && base.GenerateTeleportRequest(interactor, hit, ref request);
    }
}
