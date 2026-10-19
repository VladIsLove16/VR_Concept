using System;
using System.Collections.Generic;

namespace ConveyorTrainer.Domain
{
    public enum Requirement { Started, InfoConveyor, InfoSensor, InfoPusher, Stopped, CableDisconnected,
        SensorNutsReleased, SensorOldOnBench, SensorSpareMounted, SensorNutsSecured, CableRestored,
        AirClosed, PressureVented, TubesDisconnected, PusherBoltsReleased, PusherOldOnBench,
        PusherSpareMounted, PusherBoltsSecured, TubesRestored, AirRestored, SortingVerified }

    public sealed class ProcedureTracker : IDisposable
    {
        private readonly MaintenanceModel model;
        private readonly HashSet<Requirement> observed = new HashSet<Requirement>();
        private readonly Requirement[] required;
        private readonly bool introduction;
        private int verifiedRevision = -1;
        private bool metalVerified;
        private bool nonMetalVerified;
        private int lastRevision;
        public event Action Updated;
        public bool Complete
        {
            get
            {
                foreach (var condition in required) if (!Satisfied(condition)) return false;
                return model.Running && (introduction || model.HealthyAssembly);
            }
        }

        public ProcedureTracker(MaintenanceModel model, Requirement[] required, bool introduction)
        {
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            this.required = (Requirement[])(required ?? throw new ArgumentNullException(nameof(required))).Clone();
            this.introduction = introduction;
            lastRevision = model.Revision;
            model.Attempted += Observe;
            model.Changed += InvalidateVerification;
        }

        public bool Satisfied(Requirement requirement)
        {
            if (requirement == Requirement.SortingVerified)
                return verifiedRevision == model.Revision && metalVerified && nonMetalVerified && model.SensorOperational && model.PusherOperational;
            if (!observed.Contains(requirement)) return false;
            switch (requirement)
            {
                case Requirement.SensorOldOnBench: return model.Item(ItemId.SensorOriginal).Location == Location.Bench;
                case Requirement.SensorSpareMounted: return model.Sensor?.Id == ItemId.SensorSpare;
                case Requirement.SensorNutsSecured: return model.Sensor?.Id == ItemId.SensorSpare && model.SensorSecured;
                case Requirement.CableRestored: return model.Sensor?.Id == ItemId.SensorSpare && model.Cable;
                case Requirement.PusherOldOnBench: return model.Item(ItemId.PusherOriginal).Location == Location.Bench;
                case Requirement.PusherSpareMounted: return model.Pusher?.Id == ItemId.PusherSpare;
                case Requirement.PusherBoltsSecured: return model.Pusher?.Id == ItemId.PusherSpare && model.PusherSecured;
                case Requirement.TubesRestored: return model.Pusher?.Id == ItemId.PusherSpare && model.TubeConnected(0) && model.TubeConnected(1);
                case Requirement.AirRestored: return model.AirOpen && model.Pressure;
                default: return true;
            }
        }

        public void RegisterSorted(bool metallic, bool correctDestination)
        {
            if (!correctDestination || !model.SensorOperational || !model.PusherOperational) return;
            verifiedRevision = model.Revision;
            if (metallic) metalVerified = true; else nonMetalVerified = true;
            Updated?.Invoke();
        }

        public void Dispose() { model.Attempted -= Observe; model.Changed -= InvalidateVerification; }

        private void InvalidateVerification()
        {
            if (lastRevision == model.Revision) return;
            lastRevision = model.Revision;
            metalVerified = nonMetalVerified = false;
            verifiedRevision = -1;
            Updated?.Invoke();
        }

        private void Observe(Request r, ActionResult result)
        {
            if (!result.Succeeded) return;
            switch (r.Operation)
            {
                case Operation.Power: observed.Add(model.Running ? Requirement.Started : Requirement.Stopped); break;
                case Operation.Inspect:
                    observed.Add((Requirement)((int)Requirement.InfoConveyor + r.Index)); break;
                case Operation.SensorCable:
                    if (!model.Cable) observed.Add(Requirement.CableDisconnected);
                    else if (model.Sensor?.Id == ItemId.SensorSpare) observed.Add(Requirement.CableRestored);
                    break;
                case Operation.SensorNut:
                    if (!model.NutTight(0) && !model.NutTight(1)) observed.Add(Requirement.SensorNutsReleased);
                    if (model.Sensor?.Id == ItemId.SensorSpare && model.SensorSecured) observed.Add(Requirement.SensorNutsSecured);
                    break;
                case Operation.Air:
                    if (!model.AirOpen) observed.Add(Requirement.AirClosed);
                    else if (model.Pusher?.Id == ItemId.PusherSpare) observed.Add(Requirement.AirRestored);
                    break;
                case Operation.Vent: if (!model.Pressure) observed.Add(Requirement.PressureVented); break;
                case Operation.PusherTube:
                    if (!model.TubeConnected(0) && !model.TubeConnected(1)) observed.Add(Requirement.TubesDisconnected);
                    if (model.Pusher?.Id == ItemId.PusherSpare && model.TubeConnected(0) && model.TubeConnected(1)) observed.Add(Requirement.TubesRestored);
                    break;
                case Operation.PusherBolt:
                    if (!model.BoltTight(0) && !model.BoltTight(1)) observed.Add(Requirement.PusherBoltsReleased);
                    if (model.Pusher?.Id == ItemId.PusherSpare && model.PusherSecured) observed.Add(Requirement.PusherBoltsSecured);
                    break;
                case Operation.Place:
                    if (r.Item == ItemId.SensorOriginal && r.Destination == Location.Bench) observed.Add(Requirement.SensorOldOnBench);
                    if (r.Item == ItemId.SensorSpare && r.Destination == Location.SensorMount) observed.Add(Requirement.SensorSpareMounted);
                    if (r.Item == ItemId.PusherOriginal && r.Destination == Location.Bench) observed.Add(Requirement.PusherOldOnBench);
                    if (r.Item == ItemId.PusherSpare && r.Destination == Location.PusherMount) observed.Add(Requirement.PusherSpareMounted);
                    break;
            }
            Updated?.Invoke();
        }
    }
}
