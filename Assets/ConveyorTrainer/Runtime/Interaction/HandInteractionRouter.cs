using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace ConveyorTrainer
{
    // A held item owns this hand's Trigger. The other hand can still use UI and teleport.
    public sealed class HandInteractionRouter : MonoBehaviour
    {
        [SerializeField] private XRBaseInteractor nearGrab;
        [SerializeField] private XRBaseInteractor distanceGrab;
        [SerializeField] private XRRayInteractor workRay;
        [SerializeField] private XRInteractorLineVisual lineVisual;
        [SerializeField] private LineRenderer lineRenderer;
        private bool? occupied;

        public void Configure(XRBaseInteractor near, XRBaseInteractor distance, XRRayInteractor ray)
        {
            nearGrab = near;
            distanceGrab = distance;
            workRay = ray;
            lineVisual = ray.GetComponent<XRInteractorLineVisual>();
            lineRenderer = ray.GetComponent<LineRenderer>();
        }

        private void OnEnable()
        {
            Subscribe(nearGrab);
            Subscribe(distanceGrab);
            occupied = null;
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe(nearGrab);
            Unsubscribe(distanceGrab);
        }

        private void Subscribe(XRBaseInteractor interactor)
        {
            if (interactor == null) return;
            interactor.selectEntered.AddListener(OnGrabbed);
            interactor.selectExited.AddListener(OnReleased);
        }

        private void Unsubscribe(XRBaseInteractor interactor)
        {
            if (interactor == null) return;
            interactor.selectEntered.RemoveListener(OnGrabbed);
            interactor.selectExited.RemoveListener(OnReleased);
        }

        private void OnGrabbed(SelectEnterEventArgs args) => Refresh();
        private void OnReleased(SelectExitEventArgs args) => Refresh();
        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            if (workRay == null) return;
            var hasItem = nearGrab != null && nearGrab.hasSelection || distanceGrab != null && distanceGrab.hasSelection;
            if (occupied == hasItem) return;
            occupied = hasItem;
            workRay.enabled = !hasItem;
            if (lineVisual != null) lineVisual.enabled = !hasItem;
            if (lineRenderer != null) lineRenderer.enabled = !hasItem;
        }
    }
}
