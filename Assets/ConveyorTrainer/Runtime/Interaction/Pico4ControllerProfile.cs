#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
#endif

namespace ConveyorTrainer
{
#if UNITY_EDITOR
    [OpenXRFeature(UiName = "PICO 4 Controller Profile", Company = "Conveyor Trainer",
        BuildTargetGroups = new[] { BuildTargetGroup.Standalone },
        OpenxrExtensionStrings = "XR_BD_controller_interaction",
        Version = "1.0.0", Category = FeatureCategory.Interaction,
        FeatureId = "conveyortrainer.input.pico4")]
#endif
    public sealed class Pico4ControllerProfile : ThumbstickControllerProfile
    {
        protected override string ProfilePath => "/interaction_profiles/bytedance/pico4_controller";
        protected override string ControllerName => "PICO 4 Controller OpenXR";
        protected override string LayoutName => "TrainerPico4Controller";
        protected override string Manufacturer => "ByteDance";
        protected override string RequiredExtension => "XR_BD_controller_interaction";
    }
}
