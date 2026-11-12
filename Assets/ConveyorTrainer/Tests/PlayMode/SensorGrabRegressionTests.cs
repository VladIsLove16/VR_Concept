using System.Collections;
using System.Linq;
using ConveyorTrainer.Domain;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace ConveyorTrainer.Tests
{
    public sealed class SensorGrabRegressionTests
    {
        private TrainingStation station;
        private ServiceItem sensor;
        private GripTestController input;
        private readonly WaitForSeconds settle = new WaitForSeconds(0.15f);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("ConveyorTraining");
            yield return null;
            foreach (var manager in Object.FindObjectsOfType<InputActionManager>())
                foreach (var asset in manager.actionAssets) asset.Disable();
            foreach (var simulator in Object.FindObjectsOfType<XRDeviceSimulator>()) simulator.gameObject.SetActive(false);
            foreach (var controller in Object.FindObjectsOfType<ActionBasedController>()) controller.enabled = false;
            foreach (var ray in Object.FindObjectsOfType<XRRayInteractor>()) ray.enabled = false;
            station = Object.FindObjectOfType<TrainingStation>();
            sensor = Object.FindObjectsOfType<ServiceItem>().Single(item => item.ItemId == ItemId.SensorOriginal);
            input = new GameObject("Grip regression input").AddComponent<GripTestController>();
            input.enableInputTracking = false;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(input.gameObject);
            foreach (var manager in Object.FindObjectsOfType<InputActionManager>())
                foreach (var asset in manager.actionAssets) asset.Disable();
            yield return null;
        }

        [UnityTest]
        public IEnumerator PreparedSensorFollowsEitherHandAndCanBePlacedOnBench()
        {
            foreach (var hand in new[] { Hand.Left, Hand.Right }) yield return GrabAndPlace(hand, false);
        }

        [UnityTest]
        public IEnumerator PreparedSensorCanBeTakenWhenGripWasPressedBeforeContact()
        {
            foreach (var hand in new[] { Hand.Left, Hand.Right }) yield return GrabAndPlace(hand, true);
        }

        [UnityTest]
        public IEnumerator RejectedHeldGripAddsOneErrorAndAllowsAnotherAttemptAfterRelease()
        {
            station.BeginScenario(1);
            var near = HandInteractor(Hand.Left);
            near.xrController = input;
            near.transform.parent.position = sensor.Mount.Anchor.position;
            Physics.SyncTransforms();
            yield return settle;
            input.Grip = true;
            yield return settle;
            yield return settle;
            Assert.That(station.Attempt.errorCount, Is.EqualTo(1));
            Assert.That(station.Attempt.errors[0].reason, Is.EqualTo(Failure.LineRunning.ToString()));
            Assert.That(station.Model.Sensor, Is.Not.Null);
            input.Grip = false;
            yield return settle;
            input.Grip = true;
            yield return settle;
            Assert.That(station.Attempt.errorCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator RayPickupPreservesSafetyAndAllowsSensorRepairToFinish()
        {
            station.BeginScenario(1);
            Do(new Request(Operation.Power));
            var hand = Object.FindObjectsOfType<InteractorHand>().Single(item => item.Hand == Hand.Left);
            var near = hand.GetComponentInChildren<XRDirectInteractor>();
            var ray = hand.GetComponent<XRRayInteractor>();
            Assert.That(ray.hitClosestOnly, Is.True, "Pickup must target only the nearest visible object.");
            Assert.That(ray.raycastMask.value, Is.EqualTo(1 << 10), "Equipment buttons must not occlude item pickup.");
            near.xrController = input;
            ray.xrController = input;
            ray.enabled = true;
            hand.transform.position = sensor.Mount.Anchor.position + Vector3.back;
            hand.transform.rotation = Quaternion.identity;
            Physics.SyncTransforms();
            yield return settle;
            Assert.That(ray.interactablesHovered.Contains(sensor), Is.True, "The sensor must be reachable by Grip ray in the saved scene.");
            input.Grip = true;
            yield return settle;
            yield return settle;
            Assert.That(station.Attempt.errorCount, Is.EqualTo(1));
            Assert.That(station.Attempt.errors[0].reason, Is.EqualTo(Failure.CableConnected.ToString()));
            Assert.That(sensor.isSelected, Is.False);
            input.Grip = false;
            yield return settle;
            PrepareSensor();
            yield return RayTakeAndPlace(ray, sensor, sensor.Bench.Anchor.position);
            var spare = Object.FindObjectsOfType<ServiceItem>().Single(item => item.ItemId == ItemId.SensorSpare);
            yield return RayTakeAndPlace(ray, spare, sensor.Mount.Anchor.position);
            Do(new Request(Operation.Grab, item: ItemId.Wrench));
            Do(new Request(Operation.SensorNut, index: 0));
            Do(new Request(Operation.SensorNut, index: 1));
            Do(new Request(Operation.Place, item: ItemId.Wrench));
            Do(new Request(Operation.SensorCable));
            Do(new Request(Operation.Power));
            var deadline = Time.realtimeSinceStartup + 22f;
            while (!station.Completed && Time.realtimeSinceStartup < deadline) yield return settle;
            Assert.That(station.Completed, Is.True, "Repair must finish after actual metal and plastic sorting.");
            Assert.That(station.Attempt.errorCount, Is.Zero);
        }

        private IEnumerator RayTakeAndPlace(XRRayInteractor ray, ServiceItem item, Vector3 destination)
        {
            // Approach the bench from above so another tool does not physically occlude the spare.
            ray.transform.position = item.transform.position + Vector3.up * 0.7f + Vector3.back * 0.7f;
            ray.transform.rotation = Quaternion.LookRotation(item.transform.position - ray.transform.position);
            Physics.SyncTransforms();
            yield return settle;
            Assert.That(ray.interactablesHovered.Contains(item), Is.True, item.ItemId.ToString());
            input.Grip = true;
            yield return settle;
            Assert.That(station.Model.Held(Hand.Left)?.Id, Is.EqualTo(item.ItemId));
            Assert.That(item.isSelected, Is.True);
            Assert.That(ray.GetComponentInChildren<XRDirectInteractor>().hasSelection, Is.False, "One hand must not select through both interactors.");
            ray.transform.position = destination;
            Physics.SyncTransforms();
            yield return settle;
            Assert.That(Vector3.Distance(item.transform.position, destination), Is.LessThan(0.1f));
            input.Grip = false;
            yield return settle;
            Assert.That(station.Model.Held(Hand.Left), Is.Null);
        }

        private IEnumerator GrabAndPlace(Hand hand, bool pressBeforeContact)
        {
            input.Grip = false;
            PrepareSensor();
            sensor.SynchronizePose();
            var near = HandInteractor(hand);
            near.xrController = input;
            var ray = near.transform.parent.GetComponent<XRRayInteractor>();
            ray.xrController = input;
            ray.enabled = !pressBeforeContact;
            near.transform.parent.position = sensor.Mount.Anchor.position + Vector3.back * 0.7f;
            Physics.SyncTransforms();
            yield return settle;
            if (pressBeforeContact)
            {
                input.Grip = true;
                yield return settle;
            }
            near.transform.parent.position = sensor.Mount.Anchor.position + Vector3.back * 0.12f;
            Physics.SyncTransforms();
            yield return settle;
            Assert.That(near.interactablesHovered.Contains(sensor), Is.True, "Sensor must be detected by the actual direct interactor.");
            if (!pressBeforeContact) input.Grip = true;
            yield return settle;
            Assert.That(station.Model.Held(hand)?.Id, Is.EqualTo(ItemId.SensorOriginal), $"hand={hand}, earlyGrip={pressBeforeContact}");
            Assert.That(sensor.isSelected, Is.True);
            Assert.That(near.hasSelection, Is.True, "Nearby pickup must take priority over the ray.");
            Assert.That(ray.hasSelection, Is.False);
            var destination = sensor.Bench.Anchor.position;
            near.transform.parent.position = destination;
            Physics.SyncTransforms();
            yield return settle;
            Assert.That(Vector3.Distance(sensor.transform.position, destination), Is.LessThan(0.1f), "The selected sensor must follow the hand.");
            input.Grip = false;
            yield return settle;
            Assert.That(station.Model.Item(ItemId.SensorOriginal).Location, Is.EqualTo(Location.Bench));
            Assert.That(station.Procedure.Satisfied(Requirement.SensorOldOnBench), Is.True);
            Assert.That(station.Attempt.errorCount, Is.Zero);
            near.transform.parent.position = destination + Vector3.back;
            Physics.SyncTransforms();
            yield return settle;
            ray.enabled = false;
            var originalController = near.transform.parent.GetComponent<ActionBasedController>();
            near.xrController = originalController;
            ray.xrController = originalController;
        }

        private static XRDirectInteractor HandInteractor(Hand hand)
            => Object.FindObjectsOfType<InteractorHand>().Single(item => item.Hand == hand).GetComponentInChildren<XRDirectInteractor>();

        private void Do(Request request)
            => Assert.That(station.Execute(request, request.ToString()).Succeeded, Is.True, request.ToString());

        private void PrepareSensor()
        {
            station.BeginScenario(1);
            Do(new Request(Operation.Power));
            Do(new Request(Operation.SensorCable));
            Do(new Request(Operation.Grab, item: ItemId.Wrench));
            Do(new Request(Operation.SensorNut, index: 0));
            Do(new Request(Operation.SensorNut, index: 1));
            Do(new Request(Operation.Place, item: ItemId.Wrench));
        }
    }
}
