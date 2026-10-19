using System;
using ConveyorTrainer.Domain;

namespace ConveyorTrainer.Tests
{
    public static class TestProcedure
    {
        public static void Repair(Action<Request> execute, Fault fault, bool reverse = false)
        {
            var first = reverse ? 1 : 0;
            var second = 1 - first;
            execute(new Request(Operation.Power));
            if (fault == Fault.Sensor)
            {
                execute(new Request(Operation.SensorCable));
                execute(new Request(Operation.Grab, item: ItemId.Wrench));
                execute(new Request(Operation.SensorNut, index: first));
                execute(new Request(Operation.SensorNut, index: second));
                execute(new Request(Operation.Grab, Hand.Left, ItemId.SensorOriginal));
                execute(new Request(Operation.Place, Hand.Left, ItemId.SensorOriginal));
                execute(new Request(Operation.Grab, Hand.Left, ItemId.SensorSpare));
                execute(new Request(Operation.Place, Hand.Left, ItemId.SensorSpare, destination: Location.SensorMount));
                execute(new Request(Operation.SensorNut, index: second));
                execute(new Request(Operation.SensorNut, index: first));
                execute(new Request(Operation.Place, item: ItemId.Wrench));
                execute(new Request(Operation.SensorCable));
            }
            else
            {
                execute(new Request(Operation.Air));
                execute(new Request(Operation.Vent));
                execute(new Request(Operation.PusherTube, index: first));
                execute(new Request(Operation.PusherTube, index: second));
                execute(new Request(Operation.Grab, item: ItemId.Driver));
                execute(new Request(Operation.PusherBolt, index: second));
                execute(new Request(Operation.PusherBolt, index: first));
                execute(new Request(Operation.Grab, Hand.Left, ItemId.PusherOriginal));
                execute(new Request(Operation.Place, Hand.Left, ItemId.PusherOriginal));
                execute(new Request(Operation.Grab, Hand.Left, ItemId.PusherSpare));
                execute(new Request(Operation.Place, Hand.Left, ItemId.PusherSpare, destination: Location.PusherMount));
                execute(new Request(Operation.PusherBolt, index: first));
                execute(new Request(Operation.PusherBolt, index: second));
                execute(new Request(Operation.Place, item: ItemId.Driver));
                execute(new Request(Operation.PusherTube, index: second));
                execute(new Request(Operation.PusherTube, index: first));
                execute(new Request(Operation.Air));
            }
            execute(new Request(Operation.Power));
        }
    }
}
