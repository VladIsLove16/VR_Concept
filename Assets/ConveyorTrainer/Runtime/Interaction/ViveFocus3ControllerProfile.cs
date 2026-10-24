#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
#endif

namespace ConveyorTrainer
{
#if UNITY_EDITOR
    [OpenXRFeature(UiName = "VIVE Focus 3 Controller Profile", Company = "Conveyor Trainer",
        BuildTargetGroups = new[] { BuildTargetGroup.Standalone },
        OpenxrExtensionStrings = "XR_HTC_vive_focus3_controller_interaction",
        Version = "1.0.0", Category = FeatureCategory.Interaction,
        FeatureId = "conveyortrainer.input.vivefocus3")]
#endif
    public sealed class ViveFocus3ControllerProfile : ThumbstickControllerProfile
    {
        protected override string ProfilePath => "/interaction_profiles/htc/vive_focus3_controller";
        protected override string ControllerName => "VIVE Focus 3 Controller OpenXR";
        protected override string LayoutName => "TrainerViveFocus3Controller";
        protected override string Manufacturer => "HTC";
        protected override string RequiredExtension => "XR_HTC_vive_focus3_controller_interaction";
    }
}
