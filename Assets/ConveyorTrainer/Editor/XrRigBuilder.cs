using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ConveyorTrainer.Domain;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ConveyorTrainer.Editor
{
    internal static class XrRigBuilder
    {
        public static XRInteractionManager Build(PrefabFactory factory, out TeleportationProvider teleportation)
        {
            var actions = CreateActions();
            var manager = new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            var root = new GameObject("XR Player");
            var origin = root.AddComponent<XROrigin>();
            origin.Origin = root;
            root.transform.position = new Vector3(0, 0, -2.8f);
            var offset = new GameObject("Camera Offset"); offset.transform.SetParent(root.transform, false);
            origin.CameraFloorOffsetObject = offset;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
            origin.CameraYOffset = 1.65f;
            offset.transform.localPosition = new Vector3(0, 1.65f, 0);
            var cameraObject = new GameObject("XR Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(offset.transform, false);
            var camera = cameraObject.GetComponent<Camera>(); camera.nearClipPlane = 0.05f; camera.farClipPlane = 50; camera.fieldOfView = 75;
            camera.backgroundColor = new Color(0.08f, 0.13f, 0.19f);
            origin.Camera = camera;
            var tracking = cameraObject.AddComponent<TrackedPoseDriver>();
            tracking.positionInput = Property(actions.FindAction("Head/Position", true));
            tracking.rotationInput = Property(actions.FindAction("Head/Rotation", true));
            tracking.trackingStateInput = Property(actions.FindAction("Head/TrackingState", true));
            var inputManager = root.AddComponent<InputActionManager>();
            inputManager.actionAssets = new List<InputActionAsset> { actions };
            var locomotion = root.AddComponent<LocomotionSystem>(); locomotion.xrOrigin = origin;
            teleportation = root.AddComponent<TeleportationProvider>(); teleportation.system = locomotion;
            BuildHand(factory, offset.transform, manager, actions, Hand.Left);
            BuildHand(factory, offset.transform, manager, actions, Hand.Right);
            var system = new GameObject("XR Event System", typeof(EventSystem), typeof(XRUIInputModule));
            BuildSimulator(camera.transform);
            var prefab = PrefabFactory.Save("XRPlayer", UnityEngine.Object.Instantiate(root));
            UnityEngine.Object.DestroyImmediate(root);
            // Persist the rig as an editable prefab. Its external manager reference is wired on the scene instance.
            var instance = PrefabFactory.Spawn(prefab, "XR Player");
            instance.transform.position = new Vector3(0, 0, -2.8f);
            foreach (var interactor in instance.GetComponentsInChildren<XRBaseInteractor>()) interactor.interactionManager = manager;
            teleportation = instance.GetComponent<TeleportationProvider>();
            var simulator = UnityEngine.Object.FindObjectOfType<XRDeviceSimulator>();
            simulator.cameraTransform = instance.GetComponentInChildren<Camera>().transform;
            PrefabFactory.Save("XREventSystem", UnityEngine.Object.Instantiate(system));
            UnityEngine.Object.DestroyImmediate(system);
            PrefabFactory.Spawn(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFactory.Prefabs + "/XREventSystem.prefab"), "XR Event System");
            return manager;
        }

        private static void BuildHand(PrefabFactory factory, Transform parent, XRInteractionManager manager, InputActionAsset actions, Hand hand)
        {
            var map = hand == Hand.Left ? "Left" : "Right";
            var obj = new GameObject(map + " Controller"); obj.transform.SetParent(parent, false);
            obj.AddComponent<InteractorHand>().Configure(hand);
            var controller = obj.AddComponent<ActionBasedController>();
            controller.positionAction = Property(actions.FindAction(map + "/Position", true));
            controller.rotationAction = Property(actions.FindAction(map + "/Rotation", true));
            controller.isTrackedAction = Property(actions.FindAction(map + "/IsTracked", true));
            controller.trackingStateAction = Property(actions.FindAction(map + "/TrackingState", true));
            controller.selectAction = Property(actions.FindAction(map + "/Grip", true));
            controller.selectActionValue = Property(actions.FindAction(map + "/GripValue", true));
            var grabRay = obj.AddComponent<XRRayInteractor>();
            grabRay.interactionManager = manager; grabRay.interactionLayers = 2;
            grabRay.enableUIInteraction = false; grabRay.useForceGrab = true; grabRay.maxRaycastDistance = 6;
            grabRay.selectActionTrigger = XRBaseControllerInteractor.InputTriggerType.StateChange;
            var work = new GameObject("Work Ray"); work.transform.SetParent(obj.transform, false);
            var workController = work.AddComponent<ActionBasedController>();
            workController.enableInputTracking = false;
            workController.selectAction = Property(actions.FindAction(map + "/Trigger", true));
            workController.selectActionValue = Property(actions.FindAction(map + "/TriggerValue", true));
            workController.uiPressAction = Property(actions.FindAction(map + "/Trigger", true));
            workController.uiPressActionValue = Property(actions.FindAction(map + "/TriggerValue", true));
            var workRay = work.AddComponent<XRRayInteractor>();
            workRay.interactionManager = manager; workRay.interactionLayers = 1;
            workRay.enableUIInteraction = true; workRay.maxRaycastDistance = 8;
            workRay.selectActionTrigger = XRBaseControllerInteractor.InputTriggerType.StateChange;
            var line = work.AddComponent<LineRenderer>(); line.sharedMaterial = factory.LineMaterial;
            var lineVisual = work.AddComponent<XRInteractorLineVisual>(); lineVisual.lineWidth = 0.008f;
            lineVisual.reticle = factory.Reticle;
            var handVisual = factory.Box(map + " Hand", Vector3.zero, new Vector3(0.06f, 0.06f, 0.12f), factory.Teal, obj.transform, false);
            handVisual.transform.localPosition = Vector3.zero;
        }

        private static InputActionAsset CreateActions()
        {
            var folder = PrefabFactory.Root + "/Input";
            Directory.CreateDirectory(folder);
            var path = folder + "/TrainerXR.inputactions";
            var existing = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var head = asset.AddActionMap("Head");
            head.AddAction("Position", InputActionType.Value, "<XRHMD>/centerEyePosition", expectedControlLayout: "Vector3");
            head.AddAction("Rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation", expectedControlLayout: "Quaternion");
            head.AddAction("TrackingState", InputActionType.Value, "<XRHMD>/trackingState", expectedControlLayout: "Integer");
            foreach (var side in new[] { "Left", "Right" })
            {
                var map = asset.AddActionMap(side);
                var device = "<XRController>{" + side + "Hand}";
                map.AddAction("Position", InputActionType.Value, device + "/devicePosition", expectedControlLayout: "Vector3");
                map.AddAction("Rotation", InputActionType.Value, device + "/deviceRotation", expectedControlLayout: "Quaternion");
                map.AddAction("IsTracked", InputActionType.Button, device + "/isTracked");
                map.AddAction("TrackingState", InputActionType.Value, device + "/trackingState", expectedControlLayout: "Integer");
                map.AddAction("Grip", InputActionType.Button, device + "/gripPressed");
                map.AddAction("GripValue", InputActionType.Value, device + "/grip", expectedControlLayout: "Axis");
                map.AddAction("Trigger", InputActionType.Button, device + "/triggerPressed");
                map.AddAction("TriggerValue", InputActionType.Value, device + "/trigger", expectedControlLayout: "Axis");
            }
            File.WriteAllText(path, asset.ToJson());
            UnityEngine.Object.DestroyImmediate(asset);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        }

        private static InputActionProperty Property(InputAction action)
        {
            var path = PrefabFactory.Root + "/Input/" + action.actionMap.name + action.name + ".asset";
            var reference = AssetDatabase.LoadAssetAtPath<InputActionReference>(path);
            if (reference == null) { reference = InputActionReference.Create(action); AssetDatabase.CreateAsset(reference, path); }
            return new InputActionProperty(reference);
        }

        private static void BuildSimulator(Transform camera)
        {
            var sample = Sample.FindByPackage("com.unity.xr.interaction.toolkit", "2.5.4")
                .First(s => s.displayName == "XR Device Simulator");
            if (!sample.isImported && !sample.Import(Sample.ImportOptions.None)) throw new InvalidOperationException("Could not import XR Device Simulator sample.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var samplePath = sample.importPath.Replace('\\', '/');
            if (Path.IsPathRooted(samplePath)) samplePath = "Assets" + samplePath.Substring(Application.dataPath.Length);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(samplePath + "/XR Device Simulator.prefab");
            var obj = PrefabFactory.Spawn(source, "XR Device Simulator");
            var simulator = obj.GetComponent<XRDeviceSimulator>(); simulator.cameraTransform = camera;
            // Controls are presented on an authored world-space panel, so the sample's overlay is unnecessary.
            simulator.deviceSimulatorUI = null;
        }
    }
}
