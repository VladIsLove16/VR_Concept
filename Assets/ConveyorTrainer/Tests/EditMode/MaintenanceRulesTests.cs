using System;
using System.Linq;
using ConveyorTrainer.Domain;
using NUnit.Framework;

namespace ConveyorTrainer.Tests
{
    public sealed class MaintenanceRulesTests
    {
        private MaintenanceModel model;
        [SetUp] public void SetUp() { model = new MaintenanceModel(); }
        private void Do(Request request) { Assert.That(model.Execute(request).Succeeded, Is.True, request.ToString()); }

        [Test] public void ClosingSupplyPreservesPressureUntilVented()
        {
            Do(new Request(Operation.Air));
            Assert.That(model.Pressure, Is.True);
            Do(new Request(Operation.Vent));
            Assert.That(model.Pressure, Is.False);
        }
        [Test] public void VentWithOpenSupplyDoesNotChangePressure()
        {
            Assert.That(model.Execute(new Request(Operation.Vent)).Failure, Is.EqualTo(Failure.AirSupplyOpen));
            Assert.That(model.Pressure, Is.True);
        }
        [TestCase(false, Failure.AirSupplyOpen)]
        [TestCase(true, Failure.ResidualPressure)]
        public void TubeDisconnectRequiresSupplyClosedAndPressureReleased(bool closeAir, Failure expected)
        {
            if (closeAir) Do(new Request(Operation.Air));
            Assert.That(model.Execute(new Request(Operation.PusherTube)).Failure, Is.EqualTo(expected));
            Assert.That(model.TubeConnected(0), Is.True);
        }
        [Test] public void TubeCanDisconnectAfterVentButAirCannotReopen()
        {
            Do(new Request(Operation.Air)); Do(new Request(Operation.Vent)); Do(new Request(Operation.PusherTube));
            Assert.That(model.Execute(new Request(Operation.Air)).Failure, Is.EqualTo(Failure.EquipmentLoose));
        }
        [TestCase(Operation.SensorCable)]
        [TestCase(Operation.Air)]
        [TestCase(Operation.SensorNut)]
        [TestCase(Operation.PusherBolt)]
        [TestCase(Operation.PusherTube)]
        public void RunningLineBlocksMaintenance(Operation operation)
        {
            model.Reset(Fault.None, true);
            Assert.That(model.Execute(new Request(operation)).Failure, Is.EqualTo(Failure.LineRunning));
        }
        [TestCase(Operation.SensorNut, ItemId.Driver)]
        [TestCase(Operation.PusherBolt, ItemId.Wrench)]
        public void FastenerRequiresMatchingToolInActingHand(Operation operation, ItemId tool)
        {
            Do(new Request(Operation.Grab, item: tool));
            Assert.That(model.Execute(new Request(operation)).Failure, Is.EqualTo(Failure.WrongTool));
        }
        [Test] public void ToolRestrictsOnlyHoldingHand()
        {
            Do(new Request(Operation.Grab, item: ItemId.Wrench));
            Assert.That(model.Execute(new Request(Operation.SensorCable)).Failure, Is.EqualTo(Failure.WrongTool));
            Do(new Request(Operation.SensorCable, Hand.Left));
        }
        [Test] public void SensorFastenersCannotBeReleasedBeforeCableDisconnect()
        {
            Do(new Request(Operation.Grab, item: ItemId.Wrench));
            Assert.That(model.Execute(new Request(Operation.SensorNut)).Failure, Is.EqualTo(Failure.CableConnected));
            Assert.That(model.NutTight(0), Is.True);
            Do(new Request(Operation.SensorCable, Hand.Left));
            Do(new Request(Operation.SensorNut));
        }
        [Test] public void PusherFastenersRequirePneumaticIsolation()
        {
            Do(new Request(Operation.Grab, item: ItemId.Driver));
            Assert.That(model.Execute(new Request(Operation.PusherBolt)).Failure, Is.EqualTo(Failure.AirSupplyOpen));
            Do(new Request(Operation.Air, Hand.Left));
            Assert.That(model.Execute(new Request(Operation.PusherBolt)).Failure, Is.EqualTo(Failure.ResidualPressure));
            Do(new Request(Operation.Vent, Hand.Left));
            Assert.That(model.Execute(new Request(Operation.PusherBolt)).Failure, Is.EqualTo(Failure.TubesConnected));
            Do(new Request(Operation.PusherTube, Hand.Left)); Do(new Request(Operation.PusherTube, Hand.Left, index: 1));
            Do(new Request(Operation.PusherBolt));
        }
        [Test] public void HandCannotHoldTwoItemsOrStealFromOtherHand()
        {
            Do(new Request(Operation.Grab, item: ItemId.Wrench));
            Assert.That(model.Execute(new Request(Operation.Grab, item: ItemId.Driver)).Failure, Is.EqualTo(Failure.HandOccupied));
            Assert.That(model.Execute(new Request(Operation.Grab, Hand.Left, ItemId.Wrench)).Failure, Is.EqualTo(Failure.HandOccupied));
        }
        [Test] public void SensorCannotBeRemovedWithCableOrOneRemainingNut()
        {
            Assert.That(model.Execute(new Request(Operation.Grab, item: ItemId.SensorOriginal)).Failure, Is.EqualTo(Failure.CableConnected));
            Do(new Request(Operation.SensorCable)); Do(new Request(Operation.Grab, item: ItemId.Wrench));
            Do(new Request(Operation.SensorNut));
            Assert.That(model.Execute(new Request(Operation.Grab, Hand.Left, ItemId.SensorOriginal)).Failure, Is.EqualTo(Failure.FastenersTight));
        }
        [Test] public void CableCannotBeConnectedToLooseSensor()
        {
            Do(new Request(Operation.SensorCable)); Do(new Request(Operation.Grab, item: ItemId.Wrench)); Do(new Request(Operation.SensorNut));
            Assert.That(model.Execute(new Request(Operation.SensorCable, Hand.Left)).Failure, Is.EqualTo(Failure.EquipmentLoose));
        }
        [Test] public void WrongMountDoesNotReleaseItem()
        {
            Do(new Request(Operation.Grab, item: ItemId.SensorSpare));
            Assert.That(model.Execute(new Request(Operation.Place, item: ItemId.SensorSpare, destination: Location.PusherMount)).Failure, Is.EqualTo(Failure.WrongSocket));
            Assert.That(model.Held(Hand.Right).Id, Is.EqualTo(ItemId.SensorSpare));
        }
        [Test] public void OccupiedMountDoesNotOverwriteEquipment()
        {
            Do(new Request(Operation.Grab, item: ItemId.SensorSpare));
            Assert.That(model.Execute(new Request(Operation.Place, item: ItemId.SensorSpare, destination: Location.SensorMount)).Failure, Is.EqualTo(Failure.SocketOccupied));
            Assert.That(model.Sensor.Id, Is.EqualTo(ItemId.SensorOriginal));
        }
        [Test] public void ResetClearsHeldObjectsAndRestoresScenarioFault()
        {
            Do(new Request(Operation.Grab, item: ItemId.Wrench)); Do(new Request(Operation.Air, Hand.Left));
            model.Reset(Fault.Pusher, true);
            Assert.That(model.Held(Hand.Right), Is.Null);
            Assert.That(model.Pusher.Healthy, Is.False);
            Assert.That(model.Sensor.Healthy && model.AirOpen && model.Pressure && model.Running, Is.True);
        }
        [TestCase(Fault.Sensor, false)]
        [TestCase(Fault.Sensor, true)]
        [TestCase(Fault.Pusher, false)]
        [TestCase(Fault.Pusher, true)]
        public void ReplacementSupportsBothOrdersAndNeedsBothCorrectSortingResults(Fault fault, bool reverse)
        {
            model.Reset(fault, true);
            using (var tracker = new ProcedureTracker(model, new[] { fault == Fault.Sensor ? Requirement.SensorSpareMounted : Requirement.PusherSpareMounted, Requirement.SortingVerified }, false))
            {
                TestProcedure.Repair(Do, fault, reverse);
                Assert.That(tracker.Complete, Is.False);
                tracker.RegisterSorted(true, true); Assert.That(tracker.Complete, Is.False);
                tracker.RegisterSorted(false, false); Assert.That(tracker.Complete, Is.False);
                tracker.RegisterSorted(false, true); Assert.That(tracker.Complete, Is.True);
            }
        }
        [Test] public void StoppingAfterVerificationInvalidatesCompletion()
        {
            model.Reset(Fault.Sensor, true);
            using (var tracker = new ProcedureTracker(model, new[] { Requirement.SortingVerified }, false))
            {
                TestProcedure.Repair(Do, Fault.Sensor);
                tracker.RegisterSorted(true, true); tracker.RegisterSorted(false, true);
                Assert.That(tracker.Complete, Is.True);
                Do(new Request(Operation.Power));
                Assert.That(tracker.Complete, Is.False);
            }
        }
        [Test] public void InitialAssemblyDoesNotPretendReplacementWasPerformed()
        {
            using (var tracker = new ProcedureTracker(model, new[] { Requirement.SensorNutsSecured, Requirement.CableRestored }, false))
            {
                Assert.That(tracker.Satisfied(Requirement.SensorNutsSecured), Is.False);
                Assert.That(tracker.Satisfied(Requirement.CableRestored), Is.False);
            }
        }
        [Test] public void MovingToolDoesNotInvalidateSortingVerification()
        {
            model.Reset(Fault.Sensor, true);
            using (var tracker = new ProcedureTracker(model, new[] { Requirement.SortingVerified }, false))
            {
                TestProcedure.Repair(Do, Fault.Sensor);
                tracker.RegisterSorted(true, true);
                Do(new Request(Operation.Grab, item: ItemId.Wrench));
                tracker.RegisterSorted(false, true);
                Assert.That(tracker.Complete, Is.True);
            }
        }
        [Test] public void CancelledGrabRestoresOriginalLegalLocation()
        {
            Do(new Request(Operation.Grab, item: ItemId.SensorSpare));
            model.CancelGrab(ItemId.SensorSpare);
            Assert.That(model.Item(ItemId.SensorSpare).Location, Is.EqualTo(Location.Bench));
            Assert.That(model.Held(Hand.Right), Is.Null);
        }
    }
}
