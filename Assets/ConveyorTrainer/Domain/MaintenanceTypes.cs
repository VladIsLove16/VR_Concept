using System;

namespace ConveyorTrainer.Domain
{
    public enum Hand { Left, Right }
    public enum ItemId { Wrench, Driver, SensorOriginal, SensorSpare, PusherOriginal, PusherSpare }
    public enum Location { Bench, SensorMount, PusherMount, LeftHand, RightHand }
    public enum Fault { None, Sensor, Pusher }
    public enum Operation { Power, Air, Vent, SensorCable, SensorNut, PusherTube, PusherBolt, Grab, Place, Inspect, InvalidDrop }
    public enum Failure { None, LineRunning, WrongTool, EquipmentMissing, EquipmentLoose, CableConnected, TubesConnected,
        FastenersTight, AirSupplyOpen, ResidualPressure, HandOccupied, NotHeld, WrongSocket, SocketOccupied, InvalidPlacement }
    public enum InfoTarget { Conveyor, Sensor, Pusher }

    public readonly struct Request
    {
        public Operation Operation { get; }
        public Hand Hand { get; }
        public ItemId Item { get; }
        public int Index { get; }
        public Location Destination { get; }
        public Request(Operation operation, Hand hand = Hand.Right, ItemId item = ItemId.Wrench,
            int index = 0, Location destination = Location.Bench)
        { Operation = operation; Hand = hand; Item = item; Index = index; Destination = destination; }
        public override string ToString() => $"{Operation}:{(Operation == Operation.Grab || Operation == Operation.Place ? Item.ToString() : Index.ToString())}";
    }

    public readonly struct ActionResult
    {
        public Failure Failure { get; }
        public bool Succeeded => Failure == Failure.None;
        public ActionResult(Failure failure) { Failure = failure; }
        public static ActionResult Success => new ActionResult(Failure.None);
    }

    public sealed class ItemState
    {
        public ItemId Id { get; }
        public bool IsSensor => Id == ItemId.SensorOriginal || Id == ItemId.SensorSpare;
        public bool IsPusher => Id == ItemId.PusherOriginal || Id == ItemId.PusherSpare;
        public bool IsTool => !IsSensor && !IsPusher;
        public bool Healthy { get; internal set; }
        public Location Location { get; internal set; }
        internal Location PreviousLocation { get; set; }
        internal ItemState(ItemId id) { Id = id; }
    }
}
