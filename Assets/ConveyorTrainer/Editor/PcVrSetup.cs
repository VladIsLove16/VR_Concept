using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace ConveyorTrainer.Editor
{
    public static class PcVrSetup
    {
        private const string InputPath = PrefabFactory.Root + "/Input/TrainerXR.inputactions";
        private const string RigPath = PrefabFactory.Prefabs + "/XRPlayer.prefab";

        [MenuItem("Conveyor Trainer/Apply PC VR controls to existing project")]
        public static void Apply()
        {
            if (!SaveOpenScene()) return;
            UpdateInputActions();
            UpdateRigPrefab();
            UpdateToolPrefab("Wrench");
            UpdateToolPrefab("MountingTool");
            UsePcVr();
            Debug.Log("PC VR controls applied. No equipment or scenario assets were regenerated.");
        }

        public static void ApplyAndBuild()
        {
            Apply();
            MvpBuilder.BuildWindowsPlayer();
        }

        [MenuItem("Conveyor Trainer/Input mode/PC VR")]
        public static void UsePcVr() => SetMode(true);

        [MenuItem("Conveyor Trainer/Input mode/Keyboard simulator")]
        public static void UseSimulator() => SetMode(false);

        private static bool SaveOpenScene()
            => Application.isBatchMode || EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

        private static void UpdateInputActions()
        {
            var actions = InputActionAsset.FromJson(File.ReadAllText(InputPath));
            try
            {
                foreach (var side in new[] { "Left", "Right" })
                {
                    var map = actions.FindActionMap(side, true);
                    var device = "<XRController>{" + side + "Hand}";
                    ConfigureTracking(map.FindAction("Position", true), side, "devicePosition", "devicePose/position");
                    ConfigureTracking(map.FindAction("Rotation", true), side, "deviceRotation", "devicePose/rotation");
                    ConfigureTracking(map.FindAction("IsTracked", true), side, "isTracked", "devicePose/isTracked");
                    ConfigureTracking(map.FindAction("TrackingState", true), side, "trackingState", "devicePose/trackingState");
                    AddBinding(map.FindAction("Grip", true), device + "/grip");
                    AddBinding(map.FindAction("Trigger", true), device + "/trigger");
                    var turn = map.FindAction("Turn") ?? map.AddAction("Turn", InputActionType.Value, expectedControlLayout: "Vector2");
                    AddBinding(turn, device + "/{Primary2DAxis}", "stickDeadzone(min=0.75,max=1)");
                }
                File.WriteAllText(InputPath, actions.ToJson());
            }
            finally { UnityEngine.Object.DestroyImmediate(actions); }
            AssetDatabase.ImportAsset(InputPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void AddBinding(InputAction action, string path, string processors = null)
        {
            if (action.bindings.Any(binding => binding.path == path)) return;
            action.AddBinding(path, processors: processors);
        }

        private static void ConfigureTracking(InputAction action, string side, string legacyControl, string poseControl)
        {
            var genericDevice = "<XRController>{" + side + "Hand}";
            var oldPath = genericDevice + "/" + legacyControl;
            for (var i = action.bindings.Count - 1; i >= 0; i--)
                if (action.bindings[i].path == oldPath) action.ChangeBinding(i).Erase();
            // The XR descriptor supplies pose offsets. Avoid assuming another controller's legacy offsets.
            AddBinding(action, genericDevice + "/" + poseControl);
            AddBinding(action, "<XRSimulatedController>{" + side + "Hand}/" + legacyControl);
        }

        private static InputActionProperty Action(InputActionAsset actions, string name)
        {
            var action = actions.FindAction(name, true);
            var path = PrefabFactory.Root + "/Input/" + name.Replace("/", "") + ".asset";
            var reference = AssetDatabase.LoadAssetAtPath<InputActionReference>(path);
            if (reference == null)
            {
                reference = InputActionReference.Create(action);
                AssetDatabase.CreateAsset(reference, path);
            }
            return new InputActionProperty(reference);
        }

        private static void UpdateRigPrefab()
        {
            var rig = PrefabUtility.LoadPrefabContents(RigPath);
            try
            {
                var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
                foreach (var hand in rig.GetComponentsInChildren<InteractorHand>(true))
                {
                    var side = hand.Hand == Domain.Hand.Left ? "Left" : "Right";
                    var controller = hand.GetComponent<ActionBasedController>();
                    controller.activateAction = Action(actions, side + "/Trigger");
                    controller.activateActionValue = Action(actions, side + "/TriggerValue");
                    controller.hapticDeviceAction = Action(actions, side + "/GripValue");
                    var distanceGrab = hand.GetComponent<XRRayInteractor>();
                    var workRay = hand.transform.Find("Work Ray").GetComponent<XRRayInteractor>();
                    var nearObject = hand.transform.Find("Near Grab");
                    if (nearObject == null)
                    {
                        nearObject = new GameObject("Near Grab").transform;
                        nearObject.SetParent(hand.transform, false);
                    }
                    var collider = GetOrAdd<SphereCollider>(nearObject.gameObject);
                    collider.isTrigger = true;
                    collider.radius = 0.09f;
                    var body = GetOrAdd<Rigidbody>(nearObject.gameObject);
                    body.isKinematic = true;
                    body.useGravity = false;
                    var direct = GetOrAdd<XRDirectInteractor>(nearObject.gameObject);
                    direct.interactionLayers = 2;
                    direct.physicsLayerMask = 1 << 10;
                    direct.selectActionTrigger = XRBaseControllerInteractor.InputTriggerType.State;
                    direct.allowActivate = true;
                    distanceGrab.selectActionTrigger = XRBaseControllerInteractor.InputTriggerType.State;
                    distanceGrab.hitClosestOnly = true;
                    distanceGrab.raycastMask = 1 << 10;
                    var group = GetOrAdd<XRInteractionGroup>(hand.gameObject);
                    group.startingGroupMembers = new List<UnityEngine.Object> { direct, distanceGrab };
                    GetOrAdd<HandInteractionRouter>(hand.gameObject).Configure(direct, distanceGrab, workRay);
                }
                var snapTurn = GetOrAdd<ActionBasedSnapTurnProvider>(rig);
                snapTurn.system = rig.GetComponent<LocomotionSystem>();
                snapTurn.rightHandSnapTurnAction = Action(actions, "Right/Turn");
                snapTurn.turnAmount = 30;
                snapTurn.debounceTime = 0.5f;
                snapTurn.enableTurnAround = false;
                PrefabUtility.SaveAsPrefabAsset(rig, RigPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(rig); }
        }

        private static void UpdateToolPrefab(string name)
        {
            var path = PrefabFactory.Prefabs + "/" + name + ".prefab";
            var tool = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var tip = tool.transform.Find("Work Tip");
                if (tip == null)
                {
                    tip = new GameObject("Work Tip").transform;
                    tip.SetParent(tool.transform, false);
                    tip.localPosition = new Vector3(0, 0, 0.13f);
                }
                var grip = tool.transform.Find("Grip Anchor");
                if (grip == null)
                {
                    grip = new GameObject("Grip Anchor").transform;
                    grip.SetParent(tool.transform, false);
                    grip.localPosition = new Vector3(0, 0, -0.06f);
                }
                var item = tool.GetComponent<ServiceItem>();
                item.ConfigureToolTip(tip);
                item.attachTransform = grip;
                PrefabUtility.SaveAsPrefabAsset(tool, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(tool); }
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
            => target.TryGetComponent<T>(out var component) ? component : target.AddComponent<T>();

        private static void SetMode(bool pcVr)
        {
            if (!SaveOpenScene()) return;
            ConfigureOpenXr(pcVr);
            var scene = SceneManager.GetActiveScene().path == MvpBuilder.ScenePath
                ? SceneManager.GetActiveScene() : EditorSceneManager.OpenScene(MvpBuilder.ScenePath);
            var roots = scene.GetRootGameObjects();
            var manager = roots.SelectMany(root => root.GetComponentsInChildren<XRInteractionManager>(true)).Single();
            var origin = roots.SelectMany(root => root.GetComponentsInChildren<XROrigin>(true)).Single();
            origin.RequestedTrackingOriginMode = pcVr ? XROrigin.TrackingOriginMode.Floor : XROrigin.TrackingOriginMode.Device;
            origin.CameraFloorOffsetObject.transform.localPosition = pcVr ? Vector3.zero : Vector3.up * origin.CameraYOffset;
            PrefabUtility.RecordPrefabInstancePropertyModifications(origin);
            PrefabUtility.RecordPrefabInstancePropertyModifications(origin.CameraFloorOffsetObject.transform);
            foreach (var hand in origin.GetComponentsInChildren<InteractorHand>(true))
            {
                // Both paths use Grip; the interaction group gives nearby pickup priority.
                var distanceGrab = hand.GetComponent<XRRayInteractor>();
                distanceGrab.enabled = true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(distanceGrab);
                foreach (var interactor in hand.GetComponentsInChildren<XRBaseInteractor>(true))
                {
                    interactor.interactionManager = manager;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(interactor);
                }
                foreach (var group in hand.GetComponentsInChildren<XRInteractionGroup>(true))
                {
                    group.interactionManager = manager;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(group);
                }
            }
            foreach (var simulator in roots.SelectMany(root => root.GetComponentsInChildren<XRDeviceSimulator>(true)))
            {
                simulator.gameObject.SetActive(!pcVr);
                PrefabUtility.RecordPrefabInstancePropertyModifications(simulator.gameObject);
            }
            var board = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(target => target.name == "Simulator Controls");
            var controls = board != null ? board.GetComponentInChildren<Text>(true) : null;
            if (controls != null)
            {
                controls.text = pcVr ? PcVrControls : SimulatorControls;
                PrefabUtility.RecordPrefabInstancePropertyModifications(controls);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log(pcVr ? "PC VR input selected; start the headset's OpenXR runtime before Play." : "Keyboard simulator selected; OpenXR automatic initialization disabled.");
        }

        private static void ConfigureOpenXr(bool initializeOnStart)
        {
            if (!EditorBuildSettings.TryGetConfigObject<XRGeneralSettingsPerBuildTarget>(XRGeneralSettings.k_SettingsKey, out var settings))
                throw new InvalidOperationException("XR Plug-in Management settings are missing.");
            if (!settings.HasManagerSettingsForBuildTarget(BuildTargetGroup.Standalone))
                settings.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Standalone);
            var general = settings.SettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (!XRPackageMetadataStore.AssignLoader(general.Manager, typeof(OpenXRLoader).FullName, BuildTargetGroup.Standalone))
                throw new InvalidOperationException("Could not assign the OpenXR loader.");
            general.InitManagerOnStart = initializeOnStart;
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Standalone);
            var openXr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
            Enable<OculusTouchControllerProfile>(openXr);
            Enable<HTCViveControllerProfile>(openXr);
            Enable<ValveIndexControllerProfile>(openXr);
            Enable<ViveFocus3ControllerProfile>(openXr);
            Enable<Pico4ControllerProfile>(openXr);
            openXr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            EditorUtility.SetDirty(openXr);
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(settings);
        }

        private static void Enable<T>(OpenXRSettings settings) where T : UnityEngine.XR.OpenXR.Features.OpenXRFeature
        {
            var feature = settings.GetFeature<T>();
            if (feature == null) throw new InvalidOperationException("OpenXR feature missing: " + typeof(T).Name);
            feature.enabled = true;
            EditorUtility.SetDirty(feature);
        }

        private const string PcVrControls = "УПРАВЛЕНИЕ PC VR\nGrip — взять предмет рядом или по лучу\nGrip можно удерживать до касания предмета\nОтпустить Grip — поставить в выделенное место\nИнструмент к крепежу + Trigger — действие\nСвободная рука: луч + Trigger — кнопки и UI\nЛуч на пол: Trigger и отпустить — телепорт\nПравый стик / тачпад вбок — поворот на 30°\nОдна рука удерживает один предмет";
        private const string SimulatorControls = "УПРАВЛЕНИЕ XR-СИМУЛЯТОРОМ\nT / Y — левая / правая рука\nПКМ — управление головой\nWASD / Q E — перемещение устройства\nR — поворот / перемещение мышью\nG — удерживать предмет\nИнструмент к крепежу + ЛКМ — действие\nСвободная рука: ЛКМ — кнопки / UI / телепорт";
    }
}
