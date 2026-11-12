using System.IO;
using ConveyorTrainer.Domain;
using UnityEditor;
using UnityEngine;

namespace ConveyorTrainer.Editor
{
    internal static class ScenarioFactory
    {
        private static ScenarioStep Step(Requirement requirement, string instruction, string target)
            => new ScenarioStep { requirement = requirement, instruction = instruction, targetId = target };
        public static ScenarioDefinition[] Create()
        {
            Directory.CreateDirectory(PrefabFactory.Root + "/Scenarios");
            var intro = new[] {
                Step(Requirement.Started, "Включите линию.", "power.start"),
                Step(Requirement.InfoConveyor, "Изучите конвейер: кнопка i.", "info.conveyor"),
                Step(Requirement.InfoSensor, "Изучите датчик: кнопка i.", "info.sensor"),
                Step(Requirement.InfoPusher, "Изучите толкатель: кнопка i.", "info.pusher") };
            var sensor = new[] {
                Step(Requirement.Stopped, "Остановите линию.", "power.stop"),
                Step(Requirement.CableDisconnected, "Отсоедините кабель датчика.", "sensor.cable"),
                Step(Requirement.SensorNutsReleased, "Ключом освободите обе гайки.", "sensor.nut.0"),
                Step(Requirement.SensorOldOnBench, "Снимите старый датчик и положите на стол.", ""),
                Step(Requirement.SensorSpareMounted, "Установите запасной датчик в кронштейн.", ""),
                Step(Requirement.SensorNutsSecured, "Ключом затяните обе гайки.", "sensor.nut.0"),
                Step(Requirement.CableRestored, "Подключите кабель датчика.", "sensor.cable"),
                Step(Requirement.Started, "Запустите линию.", "power.start"),
                Step(Requirement.SortingVerified, "Дождитесь правильной сортировки двух типов шайб.", "") };
            var pusher = new[] {
                Step(Requirement.Stopped, "Остановите линию.", "power.stop"),
                Step(Requirement.AirClosed, "Перекройте подачу воздуха.", "air"),
                Step(Requirement.PressureVented, "Стравите остаточное давление.", "vent"),
                Step(Requirement.TubesDisconnected, "Отсоедините обе трубки.", "pusher.tube.0"),
                Step(Requirement.PusherBoltsReleased, "Монтажным инструментом освободите крепёж толкателя.", "pusher.bolt.0"),
                Step(Requirement.PusherOldOnBench, "Снимите старый толкатель и положите на стол.", ""),
                Step(Requirement.PusherSpareMounted, "Установите запасной толкатель.", ""),
                Step(Requirement.PusherBoltsSecured, "Затяните крепёж толкателя.", "pusher.bolt.0"),
                Step(Requirement.TubesRestored, "Подключите обе трубки.", "pusher.tube.0"),
                Step(Requirement.AirRestored, "Откройте подачу воздуха.", "air"),
                Step(Requirement.Started, "Запустите линию.", "power.start"),
                Step(Requirement.SortingVerified, "Дождитесь правильной сортировки двух типов шайб.", "") };
            return new[] {
                Definition("01-introduction", "Знакомство", "Запустите линию и изучите три информационные кнопки.", Fault.None, true, intro),
                Definition("02-sensor-guided", "Замена датчика", "Замените неисправный датчик. Независимые действия можно переставлять.", Fault.Sensor, true, sensor),
                Definition("03-pusher-guided", "Замена толкателя", "Замените неисправный толкатель с соблюдением условий обслуживания.", Fault.Pusher, true, pusher),
                Definition("04-sensor-exam", "Датчик — самостоятельно", "Замените неисправный датчик и проверьте сортировку.", Fault.Sensor, false, sensor),
                Definition("05-pusher-exam", "Толкатель — самостоятельно", "Замените неисправный толкатель и проверьте сортировку.", Fault.Pusher, false, pusher) };
        }
        private static ScenarioDefinition Definition(string id, string title, string briefing, Fault fault, bool guided, ScenarioStep[] steps)
        {
            var path = PrefabFactory.Root + "/Scenarios/" + id + ".asset";
            var definition = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(path);
            if (definition != null)
            {
                foreach (var step in definition.Steps)
                {
                    var replacement = System.Array.Find(steps, candidate => candidate.requirement == step.requirement);
                    if (replacement == null) continue;
                    switch (step.requirement)
                    {
                        case Requirement.Started:
                        case Requirement.Stopped:
                            if (step.targetId != "power") break;
                            step.targetId = replacement.targetId;
                            EditorUtility.SetDirty(definition);
                            break;
                        case Requirement.PusherBoltsReleased:
                        case Requirement.PusherBoltsSecured:
                            if (!step.instruction.Contains("оба болта")) break;
                            step.instruction = replacement.instruction;
                            EditorUtility.SetDirty(definition);
                            break;
                    }
                }
                return definition;
            }
            definition = ScriptableObject.CreateInstance<ScenarioDefinition>();
            definition.Configure(id, title, briefing, fault, guided, steps);
            AssetDatabase.CreateAsset(definition, path);
            return definition;
        }
    }
}
