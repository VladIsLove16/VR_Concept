using System;

namespace ConveyorTrainer.Domain
{
    // This model owns all equipment rules. It has no scene, input, or persistence dependencies.
    public sealed class MaintenanceModel
    {
        private readonly ItemState[] items = new ItemState[6];
        private readonly bool[] sensorNuts = new bool[2];
        private readonly bool[] tubes = new bool[2];
        public event Action<Request, ActionResult> Attempted;
        public event Action Changed;
        public bool Running { get; private set; }
        public bool AirOpen { get; private set; }
        public bool Pressure { get; private set; }
        public bool Cable { get; private set; }
        public int Revision { get; private set; }
        public ItemState Sensor => Mounted(Location.SensorMount);
        public ItemState Pusher => Mounted(Location.PusherMount);
        public bool SensorSecured => sensorNuts[0] && sensorNuts[1];
        public bool PusherSecured { get; private set; }
        public bool SensorOperational => Running && Sensor != null && Sensor.Healthy && SensorSecured && Cable;
        public bool PusherOperational => Running && Pusher != null && Pusher.Healthy && PusherSecured && tubes[0] && tubes[1] && AirOpen && Pressure;
        public bool AssemblyReady => Sensor != null && Pusher != null && SensorSecured && PusherSecured && Cable && tubes[0] && tubes[1];
        public bool HealthyAssembly => AssemblyReady && Sensor.Healthy && Pusher.Healthy && AirOpen && Pressure;
        public ItemState Item(ItemId id) => items[(int)id];
        public bool NutTight(int index) => sensorNuts[index];
        public bool TubeConnected(int index) => tubes[index];
        public ItemState Held(Hand hand) => Mounted(hand == Hand.Left ? Location.LeftHand : Location.RightHand);

        public MaintenanceModel() { for (var i = 0; i < items.Length; i++) items[i] = new ItemState((ItemId)i); Reset(Fault.None, false); }

        public void Reset(Fault fault, bool running)
        {
            foreach (var item in items) { item.Healthy = true; item.Location = Location.Bench; item.PreviousLocation = Location.Bench; }
            Item(ItemId.SensorOriginal).Location = Location.SensorMount;
            Item(ItemId.PusherOriginal).Location = Location.PusherMount;
            Item(ItemId.SensorOriginal).Healthy = fault != Fault.Sensor;
            Item(ItemId.PusherOriginal).Healthy = fault != Fault.Pusher;
            for (var i = 0; i < 2; i++) { sensorNuts[i] = true; tubes[i] = true; }
            PusherSecured = true;
            Cable = AirOpen = Pressure = true;
            Running = running;
            Revision++;
            Changed?.Invoke();
        }

        public ActionResult Execute(Request request)
        {
            if (((request.Operation == Operation.SensorNut || request.Operation == Operation.PusherTube) && (uint)request.Index > 1)
                || (request.Operation == Operation.PusherBolt && request.Index != 0))
                throw new ArgumentOutOfRangeException(nameof(request));
            var wasRunning = Running;
            var result = new ActionResult(Apply(request));
            var repeatedPowerCommand = (request.Operation == Operation.Start || request.Operation == Operation.Stop) && wasRunning == Running;
            if (result.Succeeded && request.Operation != Operation.Inspect && !repeatedPowerCommand)
            {
                // Moving a tool does not change the equipment or invalidate a washer already in flight.
                if ((request.Operation != Operation.Grab && request.Operation != Operation.Place) || !Item(request.Item).IsTool) Revision++;
                Changed?.Invoke();
            }
            Attempted?.Invoke(request, result);
            return result;
        }

        // Dropping outside a socket restores the previous legal location without advancing a scenario.
        public void CancelGrab(ItemId id)
        {
            var item = Item(id);
            if (item.Location != Location.LeftHand && item.Location != Location.RightHand) return;
            var destination = item.PreviousLocation;
            item.Location = Mounted(destination) == null ? destination : Location.Bench;
            if (!item.IsTool) Revision++;
            Changed?.Invoke();
        }

        private Failure Apply(Request r)
        {
            var held = Held(r.Hand);
            if (held != null && held.IsTool && r.Operation != Operation.SensorNut && r.Operation != Operation.PusherBolt
                && r.Operation != Operation.Place && r.Operation != Operation.Grab && r.Operation != Operation.Inspect && r.Operation != Operation.InvalidDrop)
                return Failure.WrongTool;
            switch (r.Operation)
            {
                case Operation.Power:
                case Operation.Start:
                case Operation.Stop:
                    var start = r.Operation == Operation.Start || r.Operation == Operation.Power && !Running;
                    if (start && !Running && !AssemblyReady) return Failure.EquipmentLoose;
                    Running = start; return Failure.None;
                case Operation.Air:
                    if (Running) return Failure.LineRunning;
                    if (!AirOpen && (Pusher == null || !PusherSecured || !tubes[0] || !tubes[1])) return Failure.EquipmentLoose;
                    AirOpen = !AirOpen;
                    if (AirOpen) Pressure = true;
                    return Failure.None;
                case Operation.Vent:
                    if (Running) return Failure.LineRunning;
                    if (AirOpen) return Failure.AirSupplyOpen;
                    Pressure = false; return Failure.None;
                case Operation.SensorCable:
                    if (Running) return Failure.LineRunning;
                    if (Sensor == null) return Failure.EquipmentMissing;
                    if (!SensorSecured) return Failure.EquipmentLoose;
                    Cable = !Cable; return Failure.None;
                case Operation.SensorNut:
                    if (Running) return Failure.LineRunning;
                    if (Held(r.Hand)?.Id != ItemId.Wrench) return Failure.WrongTool;
                    if (Sensor == null) return Failure.EquipmentMissing;
                    if (sensorNuts[r.Index] && Cable) return Failure.CableConnected;
                    sensorNuts[r.Index] = !sensorNuts[r.Index]; return Failure.None;
                case Operation.PusherBolt:
                    if (Running) return Failure.LineRunning;
                    if (Held(r.Hand)?.Id != ItemId.Driver) return Failure.WrongTool;
                    if (Pusher == null) return Failure.EquipmentMissing;
                    if (PusherSecured)
                    {
                        if (AirOpen) return Failure.AirSupplyOpen;
                        if (Pressure) return Failure.ResidualPressure;
                        if (tubes[0] || tubes[1]) return Failure.TubesConnected;
                    }
                    PusherSecured = !PusherSecured; return Failure.None;
                case Operation.PusherTube:
                    if (Running) return Failure.LineRunning;
                    if (Pusher == null) return Failure.EquipmentMissing;
                    if (!PusherSecured) return Failure.EquipmentLoose;
                    if (AirOpen) return Failure.AirSupplyOpen;
                    if (Pressure) return Failure.ResidualPressure;
                    tubes[r.Index] = !tubes[r.Index]; return Failure.None;
                case Operation.Grab: return Grab(r);
                case Operation.Place: return Place(r);
                case Operation.Inspect: return Failure.None;
                case Operation.InvalidDrop: return Failure.InvalidPlacement;
                default: throw new ArgumentOutOfRangeException(nameof(r));
            }
        }

        private Failure Grab(Request r)
        {
            if (Held(r.Hand) != null) return Failure.HandOccupied;
            var item = Item(r.Item);
            if (item.Location == Location.LeftHand || item.Location == Location.RightHand) return Failure.HandOccupied;
            if (!item.IsTool && Running) return Failure.LineRunning;
            if (item.Location == Location.SensorMount)
            {
                if (Cable) return Failure.CableConnected;
                if (sensorNuts[0] || sensorNuts[1]) return Failure.FastenersTight;
            }
            if (item.Location == Location.PusherMount)
            {
                if (AirOpen) return Failure.AirSupplyOpen;
                if (Pressure) return Failure.ResidualPressure;
                if (tubes[0] || tubes[1]) return Failure.TubesConnected;
                if (PusherSecured) return Failure.FastenersTight;
            }
            item.PreviousLocation = item.Location;
            item.Location = r.Hand == Hand.Left ? Location.LeftHand : Location.RightHand;
            return Failure.None;
        }

        private Failure Place(Request r)
        {
            var item = Item(r.Item);
            if (Held(r.Hand) != item) return Failure.NotHeld;
            if (r.Destination != Location.Bench)
            {
                if (Running) return Failure.LineRunning;
                if (r.Destination != Location.SensorMount && r.Destination != Location.PusherMount) return Failure.WrongSocket;
                if (r.Destination == Location.SensorMount && !item.IsSensor || r.Destination == Location.PusherMount && !item.IsPusher) return Failure.WrongSocket;
                if (Mounted(r.Destination) != null) return Failure.SocketOccupied;
            }
            item.Location = r.Destination;
            return Failure.None;
        }

        private ItemState Mounted(Location location)
        {
            foreach (var item in items) if (item.Location == location) return item;
            return null;
        }
    }
}
