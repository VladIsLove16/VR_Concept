using UnityEngine.XR.Interaction.Toolkit;

namespace ConveyorTrainer.Tests
{
    public sealed class GripTestController : XRBaseController
    {
        public bool Grip { get; set; }

        protected override void UpdateInput(XRControllerState controllerState)
        {
            controllerState.ResetFrameDependentStates();
            controllerState.selectInteractionState.SetFrameState(Grip);
        }
    }
}
