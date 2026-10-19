using System;
using System.Collections;
using System.Linq;
using ConveyorTrainer.Domain;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;

namespace ConveyorTrainer.Tests
{
    public sealed class TrainingIntegrationTests
    {
        private TrainingStation station;
        private ConveyorLine line;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("ConveyorTraining");
            yield return null;
            station = UnityEngine.Object.FindObjectOfType<TrainingStation>();
            line = UnityEngine.Object.FindObjectOfType<ConveyorLine>();
            Assert.That(station.Model, Is.Not.Null);
        }

        private void Do(Request request)
            => Assert.That(station.Execute(request, request.ToString()).Succeeded, Is.True, request.ToString());

        [UnityTest] public IEnumerator StoppingClearsWashersPendingCommandsAndStroke()
        {
            Do(new Request(Operation.Power));
            var washer = line.Spawn(true); line.Detect(washer);
            Assert.That(line.PendingStrokeCount, Is.EqualTo(1));
            line.Actuator.TryStroke();
            Do(new Request(Operation.Power));
            yield return null;
            Assert.That(line.ActiveWasherCount, Is.Zero);
            Assert.That(line.PendingStrokeCount, Is.Zero);
            Assert.That(line.Actuator.Moving, Is.False);
        }

        [UnityTest] public IEnumerator SensorIgnoresPlasticAndDeduplicatesMetalDetection()
        {
            Do(new Request(Operation.Power));
            var plastic = line.Spawn(false); line.Detect(plastic);
            Assert.That(line.PendingStrokeCount, Is.Zero);
            var metal = line.Spawn(true); line.Detect(metal); line.Detect(metal);
            Assert.That(line.PendingStrokeCount, Is.EqualTo(1));
            Assert.That(line.Actuator.Moving, Is.False, "Sensor command must be delayed.");
            yield return null;
        }

        [UnityTest] public IEnumerator IntroRequiresInspectionAndUnlocksNextButton()
        {
            Do(new Request(Operation.Power));
            Do(new Request(Operation.Inspect, index: 0));
            Do(new Request(Operation.Inspect, index: 1));
            Assert.That(station.Completed, Is.False);
            Do(new Request(Operation.Inspect, index: 2));
            yield return null;
            Assert.That(station.Completed, Is.True);
            Assert.That(UnityEngine.Object.FindObjectOfType<TrainingPanel>().NextButton.interactable, Is.True);
        }

        [UnityTest] public IEnumerator AllFourRepairScenariosCompleteThroughPhysicalSorting()
        {
            foreach (var index in new[] { 1, 2, 3, 4 })
            {
                station.BeginScenario(index);
                TestProcedure.Repair(Do, station.Scenario.Fault, reverse: index % 2 == 0);
                Assert.That(station.Completed, Is.False);
                var deadline = Time.realtimeSinceStartup + 22f;
                var trace = new System.Text.StringBuilder();
                Washer firstMetal = null;
                while (!station.Completed && Time.realtimeSinceStartup < deadline)
                {
                    if (firstMetal == null && trace.Length == 0) firstMetal = UnityEngine.Object.FindObjectsOfType<Washer>().FirstOrDefault(w => w.Metallic);
                    if (firstMetal != null && trace.Length < 2200) trace.Append(firstMetal.transform.position.ToString("F2")).Append('/').Append(firstMetal.Body.velocity.ToString("F2")).Append(';');
                    yield return new WaitForSeconds(0.25f);
                }
                var positions = string.Join("; ", UnityEngine.Object.FindObjectsOfType<Washer>().Select(w => $"{w.Metallic}:{w.transform.position}"));
                var missing = string.Join(", ", station.Scenario.Steps.Where(s => !station.Procedure.Satisfied(s.requirement)).Select(s => s.requirement));
                Assert.That(station.Completed, Is.True, $"{station.Scenario.Id}: detected={line.DetectedMetalCount}, metal={line.CorrectMetalCount}, plastic={line.CorrectPlasticCount}, wrong={line.MisroutedCount}; missing={missing}; {positions}; trace={trace}");
                Assert.That(station.Attempt.errorCount, Is.Zero);
            }
        }

        [UnityTest] public IEnumerator DeniedActionIsCountedOnceAndRestartClearsAttempt()
        {
            station.BeginScenario(4);
            var failed = station.Execute(new Request(Operation.Vent), "vent");
            Assert.That(failed.Succeeded, Is.False);
            yield return null; yield return null;
            Assert.That(station.Attempt.errorCount, Is.EqualTo(1));
            Assert.That(station.Message, Is.Not.Empty);
            var previous = station.Attempt.attemptId;
            line.Spawn(false);
            station.Restart();
            yield return null;
            Assert.That(station.Attempt.attemptId, Is.Not.EqualTo(previous));
            Assert.That(station.Attempt.errorCount, Is.Zero);
            Assert.That(station.Model.Pusher.Healthy, Is.False);
            Assert.That(line.ActiveWasherCount, Is.Zero, "A reset must clear old washers even when the new scenario starts powered on.");
        }

        [UnityTest] public IEnumerator XRGrabAndSocketReleaseUpdateDomainState()
        {
            var item = UnityEngine.Object.FindObjectsOfType<ServiceItem>().Single(i => i.ItemId == ItemId.Wrench);
            var ray = UnityEngine.Object.FindObjectsOfType<XRRayInteractor>().Single(r => r.transform.name == "Right Controller");
            var manager = UnityEngine.Object.FindObjectOfType<XRInteractionManager>();
            ray.StartManualInteraction((IXRSelectInteractable)item);
            yield return null;
            Assert.That(station.Model.Held(Hand.Right)?.Id, Is.EqualTo(ItemId.Wrench));
            item.transform.position = item.Bench.Anchor.position;
            item.GetComponent<Rigidbody>().position = item.Bench.Anchor.position;
            ray.EndManualInteraction();
            yield return null;
            Assert.That(station.Model.Held(Hand.Right), Is.Null);
            Assert.That(station.Model.Item(ItemId.Wrench).Location, Is.EqualTo(Location.Bench));
        }

        [UnityTest] public IEnumerator TeleportRejectsOccupiedFloorAndAcceptsOpenFloor()
        {
            var floor = UnityEngine.Object.FindObjectOfType<SafeTeleportationArea>();
            Physics.SyncTransforms();
            Assert.That(floor.HasClearance(new Vector3(0, 0, 0)), Is.False);
            Assert.That(floor.HasClearance(new Vector3(0, 0, -2.5f)), Is.True);
            yield return null;
        }

        [UnityTest] public IEnumerator KeyboardAndMouseDriveSimulatedControllerAction()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>("TrainerTestKeyboard");
            var mouse = InputSystem.AddDevice<Mouse>("TrainerTestMouse");
            var background = InputSystem.settings.backgroundBehavior;
            var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var inputDriver = new GameObject("Keyboard Mouse Driver").AddComponent<KeyboardMouseDriver>();
            inputDriver.Keyboard = keyboard; inputDriver.Mouse = mouse;
            var ray = UnityEngine.Object.FindObjectsOfType<XRRayInteractor>().Single(r => r.name == "Work Ray" && r.transform.parent.name == "Right Controller");
            var button = UnityEngine.Object.FindObjectsOfType<MaintenanceTarget>().Single(t => t.TargetId == "power");
            var aim = new GameObject("Test Aim");
            aim.transform.position = button.transform.position + Vector3.back;
            aim.transform.rotation = Quaternion.LookRotation(button.transform.position - aim.transform.position);
            var originalOrigin = ray.rayOriginTransform;
            var grabRay = UnityEngine.Object.FindObjectsOfType<XRRayInteractor>().Single(r => r.transform.name == "Right Controller");
            var originalGrabOrigin = grabRay.rayOriginTransform;
            ray.rayOriginTransform = aim.transform;
            try
            {
                inputDriver.SelectRight = true;
                yield return null; yield return null;
                inputDriver.SelectRight = false;
                yield return null; yield return null;
                inputDriver.Trigger = true;
                yield return null; yield return null; yield return null;
                var controller = ray.GetComponent<ActionBasedController>();
                var simulator = UnityEngine.Object.FindObjectOfType<UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRDeviceSimulator>();
                Assert.That(station.Model.Running, Is.True, $"Keyboard/mouse → XR: applied={inputDriver.AppliedMousePress}; simAction={simulator.triggerAction.action.name}; paths={string.Join(",", simulator.triggerAction.action.controls.Select(c => c.path))}; simulatorEnabled={simulator.triggerAction.action.enabled}; controls={controller.selectAction.action.controls.Count}; hover={ray.hasHover}; message={station.Message}");
                inputDriver.Trigger = false;
                yield return null; yield return null;
                var sensor = UnityEngine.Object.FindObjectsOfType<ServiceItem>().Single(i => i.ItemId == ItemId.SensorOriginal);
                aim.transform.position = sensor.transform.position + Vector3.back;
                aim.transform.rotation = Quaternion.LookRotation(sensor.transform.position - aim.transform.position);
                grabRay.rayOriginTransform = aim.transform;
                inputDriver.Grip = true;
                for (var frame = 0; frame < 6; frame++) yield return null;
                Assert.That(station.Attempt.errorCount, Is.EqualTo(1), "Holding a rejected grip must not count as multiple attempts.");
                inputDriver.Grip = false;
                yield return null; yield return null;
                Do(new Request(Operation.Inspect, index: 0));
                Do(new Request(Operation.Inspect, index: 1));
                Do(new Request(Operation.Inspect, index: 2));
                Assert.That(station.Completed, Is.True);
                var nextButton = UnityEngine.Object.FindObjectOfType<TrainingPanel>().NextButton;
                var rect = nextButton.GetComponent<RectTransform>();
                var buttonCenter = rect.TransformPoint(rect.rect.center);
                aim.transform.position = buttonCenter - nextButton.GetComponentInParent<Canvas>().transform.forward;
                aim.transform.rotation = Quaternion.LookRotation(buttonCenter - aim.transform.position);
                yield return null; yield return null;
                inputDriver.Trigger = true;
                yield return null; yield return null;
                inputDriver.Trigger = false;
                yield return null; yield return null; yield return null;
                ray.TryGetCurrentUIRaycastResult(out var uiHit);
                Assert.That(station.ScenarioIndex, Is.EqualTo(1), $"XR world-space UI did not advance the scenario; hit={uiHit.gameObject?.name}");
            }
            finally
            {
                inputDriver.enabled = false;
                UnityEngine.Object.Destroy(inputDriver.gameObject);
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(mouse);
                ray.rayOriginTransform = originalOrigin;
                grabRay.rayOriginTransform = originalGrabOrigin;
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
                UnityEngine.Object.Destroy(aim);
            }
        }
    }
}
