using ConveyorTrainer.Domain;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace ConveyorTrainer
{
    public sealed class MaintenanceTarget : XRSimpleInteractable
    {
        [SerializeField] private TrainingStation station;
        [SerializeField] private string targetId;
        [SerializeField] private string displayName;
        [SerializeField] private Operation operation;
        [SerializeField] private int index;
        [SerializeField] private Renderer visual;
        private MaterialPropertyBlock properties;
        public string TargetId => targetId;
        public Operation Operation => operation;
        public int Index => index;
        public bool Suggested { get; set; }
        public void Configure(TrainingStation owner, string id, string label, Operation action, int actionIndex, Renderer renderer)
        { station = owner; targetId = id; displayName = label; operation = action; index = actionIndex; visual = renderer; }
        public override bool IsSelectableBy(IXRSelectInteractor interactor) => station != null && station.Model != null && !station.Completed && base.IsSelectableBy(interactor);
        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            var hand = args.interactorObject.transform.GetComponentInParent<InteractorHand>();
            station.Execute(new Request(operation, hand != null ? hand.Hand : Hand.Right, index: index), targetId);
        }
        protected override void OnHoverEntered(HoverEnterEventArgs args) { base.OnHoverEntered(args); if (station != null) station.ShowHover(displayName); }
        protected override void OnHoverExited(HoverExitEventArgs args) { base.OnHoverExited(args); if (station != null && !isHovered) station.ShowHover(""); }
        private void Update()
        {
            if (visual == null || station == null || station.Model == null) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            var m = station.Model;
            var active = false;
            switch (operation)
            {
                case Operation.Power: active = m.Running; break;
                case Operation.Air: active = m.AirOpen; break;
                case Operation.Vent: active = !m.Pressure; break;
                case Operation.SensorCable: active = m.Cable; break;
                case Operation.SensorNut: active = m.NutTight(index); break;
                case Operation.PusherBolt: active = m.BoltTight(index); break;
                case Operation.PusherTube: active = m.TubeConnected(index); break;
            }
            var color = operation == Operation.Inspect ? new Color(0.16f, 0.38f, 0.55f) : active ? new Color(0.1f, 0.65f, 0.45f) : new Color(0.65f, 0.28f, 0.12f);
            if (isHovered || Suggested) color = Color.Lerp(color, Color.yellow, 0.65f);
            properties.SetColor("_BaseColor", color);
            visual.SetPropertyBlock(properties);
        }
    }
}
