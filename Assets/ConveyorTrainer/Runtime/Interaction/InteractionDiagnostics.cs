using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ConveyorTrainer.Domain;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.XR.Interaction.Toolkit;

namespace ConveyorTrainer
{
    // Opt-in investigation of a reported device issue; this is not performance telemetry.
    public sealed class InteractionDiagnostics : MonoBehaviour
    {
        private static InteractionDiagnostics instance;
        private readonly List<HandProbe> probes = new List<HandProbe>();
        private StreamWriter writer;
        private TrainingStation station;
        private ServiceItem sensor;
        private float nextSnapshot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (!Debug.isDebugBuild || !Environment.GetCommandLineArgs().Contains("-interaction-diagnostics")) return;
            if (instance != null) return;
            new GameObject("Interaction Diagnostics").AddComponent<InteractionDiagnostics>();
        }

        private void Awake()
        {
            instance = this;
            var directory = Path.Combine(Application.persistentDataPath, "Diagnostics");
            try
            {
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, "interaction-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + ".log");
                writer = new StreamWriter(path, false, new System.Text.UTF8Encoding(true)) { AutoFlush = true };
                Debug.Log("[XR-DIAG] Log file: " + path);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Debug.LogWarning("[XR-DIAG] Cannot create diagnostic file: " + error.Message);
            }
            Application.logMessageReceived += OnLogMessage;
            InputSystem.onDeviceChange += OnDeviceChange;
            Write($"SESSION unity={Application.unityVersion} build={Application.buildGUID} cpu={SystemInfo.processorType} gpu={SystemInfo.graphicsDeviceName} ramMB={SystemInfo.systemMemorySize}");
            station = FindObjectOfType<TrainingStation>();
            sensor = FindObjectsOfType<ServiceItem>().FirstOrDefault(item => item.ItemId == ItemId.SensorOriginal);
            if (sensor != null)
                foreach (var collider in sensor.colliders)
                    Write($"SENSOR_COLLIDER name={collider.name} enabled={collider.enabled} layer={collider.gameObject.layer} trigger={collider.isTrigger} bounds={collider.bounds}");
            foreach (var hand in FindObjectsOfType<InteractorHand>())
            {
                var near = hand.GetComponentInChildren<XRDirectInteractor>(true);
                if (near != null) probes.Add(new HandProbe(this, hand, near));
            }
            Write("PROBES count=" + probes.Count);
            foreach (var device in InputSystem.devices) LogDevice(device, "present");
        }

        private void LateUpdate()
        {
            var snapshot = Time.unscaledTime >= nextSnapshot;
            if (snapshot) nextSnapshot = Time.unscaledTime + 2f;
            foreach (var probe in probes) probe.Poll(snapshot);
        }

        public static void RecordAction(TrainingStation owner, Request request, string target, ActionResult result, bool ignored = false)
        {
            if (instance == null) return;
            instance.Write($"ACTION scenario={owner.Scenario.Id} hand={request.Hand} request={request} target={target} success={result.Succeeded} reason={result.Failure} ignoredCompleted={ignored} {instance.EquipmentState()}");
        }

        private string EquipmentState()
        {
            var model = station != null ? station.Model : null;
            if (model == null) return "model=not-ready";
            return $"scenario={station.Scenario.Id} completed={station.Completed} running={model.Running} cable={model.Cable} nut0={model.NutTight(0)} nut1={model.NutTight(1)} sensorLocation={model.Item(ItemId.SensorOriginal).Location} leftHeld={model.Held(Hand.Left)?.Id.ToString() ?? "none"} rightHeld={model.Held(Hand.Right)?.Id.ToString() ?? "none"}";
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change) => LogDevice(device, change.ToString());

        private void LogDevice(InputDevice device, string change)
        {
            var controls = device.allControls.Where(control => IsGripControl(control.name)).Select(control => control.path);
            Write($"DEVICE change={change} id={device.deviceId} layout={device.layout} product={device.description.product} interface={device.description.interfaceName} usages={string.Join(",", device.usages)} gripControls={string.Join(",", controls)}");
        }

        private static bool IsGripControl(string name)
            => name.IndexOf("grip", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("squeeze", StringComparison.OrdinalIgnoreCase) >= 0;

        private static string RawGrip(Hand hand)
        {
            var usage = hand == Hand.Left ? CommonUsages.LeftHand : CommonUsages.RightHand;
            var values = new List<string>();
            foreach (var device in InputSystem.devices)
            {
                if (!device.usages.Contains(usage)) continue;
                foreach (var control in device.allControls)
                    if (IsGripControl(control.name) && control is AxisControl axis)
                        values.Add(control.path + "=" + axis.ReadValue().ToString("F3", CultureInfo.InvariantCulture));
            }
            return string.Join(",", values);
        }

        private static bool RawGripPressed(Hand hand)
        {
            var usage = hand == Hand.Left ? CommonUsages.LeftHand : CommonUsages.RightHand;
            foreach (var device in InputSystem.devices)
            {
                if (!device.usages.Contains(usage)) continue;
                foreach (var control in device.allControls)
                    if (IsGripControl(control.name) && control is AxisControl axis && axis.ReadValue() >= InputSystem.settings.defaultButtonPressPoint)
                        return true;
            }
            return false;
        }

        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
                Write("UNITY_ERROR " + condition + " " + stackTrace);
        }

        private void Write(string message)
        {
            if (writer == null) return;
            try
            {
                writer.WriteLine(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + " frame=" + Time.frameCount + " " + message);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                try { writer.Dispose(); } catch (IOException) { }
                writer = null;
                Debug.LogWarning("[XR-DIAG] Cannot write diagnostic file: " + error.Message);
            }
        }

        private void OnDestroy()
        {
            foreach (var probe in probes) probe.Dispose();
            InputSystem.onDeviceChange -= OnDeviceChange;
            Application.logMessageReceived -= OnLogMessage;
            Write("SESSION_END");
            try { writer?.Dispose(); } catch (IOException) { }
            writer = null;
            if (instance == this) instance = null;
        }

        private sealed class HandProbe : IDisposable
        {
            private readonly InteractionDiagnostics owner;
            private readonly InteractorHand hand;
            private readonly XRDirectInteractor near;
            private bool pressed;
            private bool rawPressed;

            public HandProbe(InteractionDiagnostics owner, InteractorHand hand, XRDirectInteractor near)
            {
                this.owner = owner;
                this.hand = hand;
                this.near = near;
                owner.Write($"HAND hand={hand.Hand} controller={near.xrController?.name ?? "none"} physicsMask={near.physicsLayerMask.value} interactionMask={near.interactionLayers.value}");
                near.hoverEntered.AddListener(OnHoverEntered);
                near.hoverExited.AddListener(OnHoverExited);
                near.selectEntered.AddListener(OnSelectEntered);
                near.selectExited.AddListener(OnSelectExited);
            }

            public void Poll(bool snapshot)
            {
                var controller = near.xrController;
                var current = controller != null && controller.selectInteractionState.active;
                var raw = RawGripPressed(hand.Hand);
                if (current != pressed) Snapshot(current ? "GRIP_DOWN" : "GRIP_UP");
                else if (raw != rawPressed) Snapshot(raw ? "RAW_GRIP_DOWN" : "RAW_GRIP_UP");
                else if (snapshot) Snapshot("STATE");
                pressed = current;
                rawPressed = raw;
            }

            private void Snapshot(string kind)
            {
                var controller = near.xrController as ActionBasedController;
                var action = controller != null ? controller.selectAction.action : null;
                var valueAction = controller != null ? controller.selectActionValue.action : null;
                var sensor = owner.sensor;
                var distance = sensor != null ? Vector3.Distance(near.transform.position, sensor.transform.position) : -1f;
                var hover = string.Join(",", near.interactablesHovered.Select(item => item.transform.name));
                var selected = string.Join(",", near.interactablesSelected.Select(item => item.transform.name));
                owner.Write($"{kind} hand={hand.Hand} nearEnabled={near.isActiveAndEnabled} " +
                    $"controllerEnabled={controller != null && controller.isActiveAndEnabled} tracked={controller != null && controller.currentControllerState.isTracked} " +
                    $"selectActive={near.xrController != null && near.xrController.selectInteractionState.active} triggerMode={near.selectActionTrigger} " +
                    $"action={action?.name ?? "none"} actionEnabled={action != null && action.enabled} actionPressed={action != null && action.IsPressed()} " +
                    $"gripValue={(valueAction != null ? valueAction.ReadValue<float>() : -1f).ToString("F3", CultureInfo.InvariantCulture)} " +
                    $"boundControls={(action != null ? string.Join(",", action.controls.Select(control => control.path)) : "none")} " +
                    $"rawGrip={RawGrip(hand.Hand)} hover=[{hover}] selected=[{selected}] handPosition={near.transform.position.ToString("F3")} " +
                    $"sensorDistance={distance.ToString("F3", CultureInfo.InvariantCulture)} sensorSelectable={sensor != null && sensor.IsSelectableBy((IXRSelectInteractor)near)} {owner.EquipmentState()}");
            }

            private void OnHoverEntered(HoverEnterEventArgs args) => owner.Write($"HOVER_ENTER hand={hand.Hand} target={args.interactableObject.transform.name}");
            private void OnHoverExited(HoverExitEventArgs args) => owner.Write($"HOVER_EXIT hand={hand.Hand} target={args.interactableObject.transform.name}");
            private void OnSelectEntered(SelectEnterEventArgs args) => owner.Write($"SELECT_ENTER hand={hand.Hand} target={args.interactableObject.transform.name}");
            private void OnSelectExited(SelectExitEventArgs args) => owner.Write($"SELECT_EXIT hand={hand.Hand} target={args.interactableObject.transform.name} canceled={args.isCanceled}");

            public void Dispose()
            {
                if (near == null) return;
                near.hoverEntered.RemoveListener(OnHoverEntered);
                near.hoverExited.RemoveListener(OnHoverExited);
                near.selectEntered.RemoveListener(OnSelectEntered);
                near.selectExited.RemoveListener(OnSelectExited);
            }
        }
    }
}
