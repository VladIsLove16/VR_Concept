using System.Collections.Generic;
using ConveyorTrainer.Domain;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace ConveyorTrainer
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ServiceItem : XRGrabInteractable
    {
        [SerializeField] private TrainingStation station;
        [SerializeField] private ItemId itemId;
        [SerializeField] private string displayName;
        [SerializeField] private ItemSocket bench;
        [SerializeField] private ItemSocket mount;
        [SerializeField] private Renderer visual;
        [SerializeField] private bool preserveMaterialColor;
        [SerializeField] private Transform workTip;
        [SerializeField, Min(0.01f)] private float workRadius = 0.06f;
        [SerializeField] private LayerMask workLayers = 1 << 8;
        private readonly Collider[] workContacts = new Collider[16];
        private bool accepted;
        private Hand selectedHand;
        private bool releasing;
        private MaterialPropertyBlock properties;
        private bool appliedTint;
        private readonly List<XRBaseController> rejectedControllers = new List<XRBaseController>(2);
        public ItemId ItemId => itemId;
        public ItemSocket Bench => bench;
        public ItemSocket Mount => mount;
        public void ConfigureToolTip(Transform tip) => workTip = tip;
        public void Configure(TrainingStation owner, ItemId id, string label, ItemSocket benchSlot, ItemSocket equipmentSlot, Renderer renderer)
        { station = owner; itemId = id; displayName = label; bench = benchSlot; mount = equipmentSlot; visual = renderer; }
        public void UseModelVisual(Renderer renderer)
        {
            visual = renderer;
            preserveMaterialColor = true;
        }
        public override bool IsSelectableBy(IXRSelectInteractor interactor)
        {
            if (interactor is XRBaseControllerInteractor controllerInteractor &&
                controllerInteractor.xrController != null &&
                controllerInteractor.xrController.selectInteractionState.active &&
                rejectedControllers.Contains(controllerInteractor.xrController)) return false;
            return station != null && station.Model != null && !station.Completed && base.IsSelectableBy(interactor);
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            var hand = args.interactorObject.transform.GetComponentInParent<InteractorHand>();
            selectedHand = hand != null ? hand.Hand : Hand.Right;
            accepted = station.Execute(new Request(Operation.Grab, selectedHand, itemId), itemId.ToString()).Succeeded;
            if (!accepted)
            {
                // A held Grip may enter contact later, but one rejected press is still one attempt.
                if (args.interactorObject is XRBaseControllerInteractor controllerInteractor &&
                    controllerInteractor.xrController != null &&
                    controllerInteractor.xrController.selectInteractionState.active &&
                    !rejectedControllers.Contains(controllerInteractor.xrController))
                    rejectedControllers.Add(controllerInteractor.xrController);
                interactionManager.SelectExit(args.interactorObject, this);
            }
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);
            if (!accepted || releasing || station == null || station.Model == null) return;
            releasing = true;
            accepted = false;
            if (args.isCanceled)
                station.Model.CancelGrab(itemId);
            else
            {
                ItemSocket destination = null;
                if (mount != null && mount.Contains(transform.position)) destination = mount;
                else if (bench.Contains(transform.position)) destination = bench;
                if (destination == null)
                {
                    station.Execute(new Request(Operation.InvalidDrop, selectedHand, itemId), itemId.ToString());
                    station.Model.CancelGrab(itemId);
                }
                else if (!station.Execute(new Request(Operation.Place, selectedHand, itemId, destination: destination.Location), itemId.ToString()).Succeeded)
                    station.Model.CancelGrab(itemId);
            }
            ApplyPose();
            releasing = false;
        }

        protected override void OnActivated(ActivateEventArgs args)
        {
            base.OnActivated(args);
            if (!accepted || workTip == null || station == null || station.Model == null || station.Completed) return;

            var count = Physics.OverlapSphereNonAlloc(workTip.position, workRadius, workContacts, workLayers, QueryTriggerInteraction.Collide);
            MaintenanceTarget nearest = null;
            var nearestDistance = float.PositiveInfinity;
            for (var i = 0; i < count; i++)
            {
                var contact = workContacts[i];
                var target = contact.GetComponentInParent<MaintenanceTarget>();
                if (target == null || !target.IsFastener) continue;
                var distance = (contact.ClosestPoint(workTip.position) - workTip.position).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearest = target;
                nearestDistance = distance;
            }
            if (nearest == null)
            {
                station.ShowMessage("Поднесите рабочую часть инструмента к крепежу и нажмите Trigger.");
                return;
            }
            var result = nearest.ExecuteForHand(selectedHand);
            if (result.Succeeded && args.interactorObject is XRBaseControllerInteractor controllerInteractor)
                controllerInteractor.SendHapticImpulse(0.25f, 0.05f);
        }

        protected override void OnHoverEntered(HoverEnterEventArgs args) { base.OnHoverEntered(args); station?.ShowHover(displayName + " — захват"); }
        protected override void OnHoverExited(HoverExitEventArgs args) { base.OnHoverExited(args); if (!isHovered) station?.ShowHover(""); }
        private void LateUpdate()
        {
            for (var i = rejectedControllers.Count - 1; i >= 0; i--)
                if (rejectedControllers[i] == null || !rejectedControllers[i].selectInteractionState.active)
                    rejectedControllers.RemoveAt(i);
            if (station == null || station.Model == null) return;
            if (isSelected && accepted && station.Model.Held(selectedHand)?.Id != itemId)
            {
                accepted = false;
                interactionManager.SelectExit(firstInteractorSelecting, this);
            }
            if (!isSelected) ApplyPose();
            if (visual != null)
            {
                var item = station.Model.Item(itemId);
                if (preserveMaterialColor && item.Healthy && !isHovered)
                {
                    if (appliedTint) visual.SetPropertyBlock(null);
                    appliedTint = false;
                    return;
                }
                if (properties == null) properties = new MaterialPropertyBlock();
                var color = item.IsTool ? new Color(0.9f, 0.65f, 0.16f) : item.Healthy ? new Color(0.12f, 0.65f, 0.55f) : new Color(0.8f, 0.15f, 0.12f);
                if (preserveMaterialColor && item.Healthy) color = visual.sharedMaterial.color;
                if (isHovered) color = Color.Lerp(color, Color.white, 0.4f);
                properties.SetColor("_BaseColor", color);
                visual.SetPropertyBlock(properties);
                appliedTint = true;
            }
        }

        public void SynchronizePose() => ApplyPose();
        private void ApplyPose()
        {
            if (station == null || station.Model == null) return;
            var location = station.Model.Item(itemId).Location;
            var socket = location == Location.Bench ? bench : location == Location.SensorMount || location == Location.PusherMount ? mount : null;
            if (socket == null) return;
            var body = GetComponent<Rigidbody>();
            body.position = socket.Anchor.position;
            body.rotation = socket.Anchor.rotation;
            if (!body.isKinematic)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }
    }
}
