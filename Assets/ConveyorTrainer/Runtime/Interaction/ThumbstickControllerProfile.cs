using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace ConveyorTrainer
{
    // These controllers share the Touch control layout; their OpenXR profile paths differ.
    public abstract class ThumbstickControllerProfile : OpenXRInteractionFeature
    {
        protected abstract string ProfilePath { get; }
        protected abstract string ControllerName { get; }
        protected abstract string LayoutName { get; }
        protected abstract string Manufacturer { get; }
        protected abstract string RequiredExtension { get; }

        protected override bool OnInstanceCreate(ulong instance)
            => OpenXRRuntime.IsExtensionEnabled(RequiredExtension) && base.OnInstanceCreate(instance);

        protected override void RegisterDeviceLayout()
        {
            const string touchLayout = nameof(OculusTouchControllerProfile.OculusTouchController);
            if (!InputSystem.ListLayouts().Contains(touchLayout))
                InputSystem.RegisterLayout<OculusTouchControllerProfile.OculusTouchController>();
            InputSystem.RegisterLayout("{\"name\":\"" + LayoutName + "\",\"extend\":\"" + touchLayout + "\"}",
                name: LayoutName,
                matches: new InputDeviceMatcher().WithInterface("XRInput.*").WithProduct(ControllerName));
        }

        protected override void UnregisterDeviceLayout() => InputSystem.RemoveLayout(LayoutName);
        protected override string GetDeviceLayoutName() => LayoutName;

        protected override void RegisterActionMapsWithRuntime()
        {
            AddActionMap(new ActionMapConfig
            {
                name = LayoutName.ToLowerInvariant(),
                localizedName = ControllerName,
                desiredInteractionProfile = ProfilePath,
                manufacturer = Manufacturer,
                serialNumber = "",
                deviceInfos = new List<DeviceConfig>
                {
                    Device(UserPaths.leftHand, InputDeviceCharacteristics.Left),
                    Device(UserPaths.rightHand, InputDeviceCharacteristics.Right)
                },
                actions = new List<ActionConfig>
                {
                    Action("grip", ActionType.Axis1D, "/input/squeeze/value", "Grip"),
                    Action("gripPressed", ActionType.Binary, "/input/squeeze/value", "GripButton"),
                    Action("trigger", ActionType.Axis1D, "/input/trigger/value", "Trigger"),
                    Action("triggerPressed", ActionType.Binary, "/input/trigger/value", "TriggerButton"),
                    Action("thumbstick", ActionType.Axis2D, "/input/thumbstick", "Primary2DAxis"),
                    Action("thumbstickClicked", ActionType.Binary, "/input/thumbstick/click", "Primary2DAxisClick"),
                    Action("devicePose", ActionType.Pose, "/input/grip/pose", "Device"),
                    Action("pointer", ActionType.Pose, "/input/aim/pose", "Pointer"),
                    Action("haptic", ActionType.Vibrate, "/output/haptic", "Haptic")
                }
            });
        }

        private static DeviceConfig Device(string path, InputDeviceCharacteristics side) => new DeviceConfig
        {
            userPath = path,
            characteristics = InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.TrackedDevice |
                              InputDeviceCharacteristics.Controller | side
        };

        private ActionConfig Action(string name, ActionType type, string path, string usage) => new ActionConfig
        {
            name = name,
            localizedName = name,
            type = type,
            usages = new List<string> { usage },
            bindings = new List<ActionBinding>
            {
                new ActionBinding { interactionPath = path, interactionProfileName = ProfilePath }
            }
        };
    }
}
