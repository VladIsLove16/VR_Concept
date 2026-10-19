using System;
using System.Collections.Generic;
using System.IO;
using ConveyorTrainer.Domain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

namespace ConveyorTrainer.Editor
{
    public static class MvpBuilder
    {
        public const string ScenePath = "Assets/ConveyorTrainer/Generated/Scenes/ConveyorTraining.unity";
        [MenuItem("Conveyor Trainer/Create MVP Scene (once)")]
        public static void Build()
        {
            if (File.Exists(ScenePath))
            {
                Debug.Log("MVP scene already exists. Open and edit the saved scene/prefabs; generation does not overwrite manual changes.");
                if (SceneManager.GetActiveScene().path != ScenePath)
                {
                    if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                    EditorSceneManager.OpenScene(ScenePath);
                }
                return;
            }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(PrefabFactory.Root + "/Scenes");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var f = new PrefabFactory();
            var definitions = ScenarioFactory.Create();
            var interactionManager = XrRigBuilder.Build(f, out var teleportation);
            var station = new GameObject("Training Station").AddComponent<TrainingStation>();
            var line = new GameObject("Conveyor Simulation").AddComponent<ConveyorLine>();
            var geometry = new GameObject("Workshop").transform;
            var lighting = new GameObject("Workshop Light").AddComponent<Light>();
            lighting.type = LightType.Directional; lighting.intensity = 1.4f;
            lighting.transform.rotation = Quaternion.Euler(45, -30, 0);
            RenderSettings.ambientLight = new Color(0.55f, 0.63f, 0.7f);
            var floor = f.Box("Teleport Floor", new Vector3(0, -0.06f, 0), new Vector3(12, 0.1f, 10), f.Floor, geometry);
            floor.layer = 0;
            var area = floor.AddComponent<SafeTeleportationArea>(); area.interactionManager = interactionManager;
            area.interactionLayers = 1; area.teleportationProvider = teleportation;
            area.teleportTrigger = BaseTeleportationInteractable.TeleportTrigger.OnSelectExited;
            f.Box("Belt", new Vector3(0, 0.7f, 0), new Vector3(5, 0.12f, 0.85f), f.Dark, geometry);
            foreach (var x in new[] { -2.1f, 2.1f }) foreach (var z in new[] { -0.3f, 0.3f })
                f.Box("Conveyor Leg", new Vector3(x, 0.32f, z), new Vector3(0.1f, 0.64f, 0.1f), f.Steel, geometry);
            f.Box("Bench", new Vector3(-3.7f, 0.72f, -1.6f), new Vector3(2.2f, 0.1f, 1.8f), f.Dark, geometry);
            foreach (var x in new[] { -4.6f, -2.8f }) foreach (var z in new[] { -2.3f, -0.9f })
                f.Box("Bench Leg", new Vector3(x, 0.34f, z), new Vector3(0.1f, 0.68f, 0.1f), f.Steel, geometry);
            f.Box("Sensor Bracket", new Vector3(-0.9f, 0.88f, -0.64f), new Vector3(0.38f, 0.06f, 0.3f), f.Steel, geometry);
            f.Box("Pusher Stand", new Vector3(0.2f, 0.74f, -0.65f), new Vector3(0.5f, 0.06f, 0.4f), f.Steel, geometry);
            f.Box("Divider Left", new Vector3(-1.3f, 0.84f, 0.49f), new Vector3(2.4f, 0.17f, 0.06f), f.Steel, geometry);
            f.Box("Divider Right", new Vector3(1.6f, 0.84f, 0.49f), new Vector3(1.8f, 0.17f, 0.06f), f.Steel, geometry);
            var ramp = f.Box("Metal Chute", new Vector3(0.2f, 0.4f, 1.15f), new Vector3(0.6f, 0.06f, 1.4f), f.Steel, geometry);
            ramp.transform.rotation = Quaternion.Euler(25, 0, 0);
            AddChuteGuides(f, geometry);
            f.Box("Bin Base", new Vector3(0.2f, 0.08f, 1.9f), new Vector3(0.85f, 0.08f, 0.85f), f.Dark, geometry);
            f.Box("Bin Left", new Vector3(-0.25f, 0.23f, 1.9f), new Vector3(0.06f, 0.35f, 0.9f), f.Teal, geometry);
            f.Box("Bin Right", new Vector3(0.65f, 0.23f, 1.9f), new Vector3(0.06f, 0.35f, 0.9f), f.Teal, geometry);
            f.Box("Bin Back", new Vector3(0.2f, 0.23f, 2.3f), new Vector3(0.9f, 0.35f, 0.06f), f.Teal, geometry);
            f.WorldLabel("МЕТАЛЛ", new Vector3(0.2f, 0.4f, 2.28f), 0.7f, geometry);

            var sensorMount = Socket(f, "Sensor Mount", new Vector3(-0.9f, 1.0f, -0.64f), Location.SensorMount);
            var pusherMount = Socket(f, "Pusher Mount", new Vector3(0.2f, 0.89f, -0.65f), Location.PusherMount);
            var names = new[] { "Гаечный ключ", "Монтажный инструмент", "Старый датчик", "Запасной датчик", "Старый толкатель", "Запасной толкатель" };
            var prefabs = new[] { f.Wrench, f.Driver, f.Sensor, f.Sensor, f.Pusher, f.Pusher };
            for (var i = 0; i < 6; i++)
            {
                var bench = Socket(f, "Bench Slot " + (ItemId)i, new Vector3(i % 2 == 0 ? -4.15f : -3.25f, 0.92f, -2.15f + (i / 2) * 0.55f), Location.Bench);
                f.WorldLabel(names[i], bench.transform.position + new Vector3(0, -0.14f, -0.2f), 0.8f);
                var obj = PrefabFactory.Spawn(prefabs[i], names[i]);
                obj.layer = 10;
                var mount = i == 2 || i == 3 ? sensorMount : i >= 4 ? pusherMount : null;
                obj.GetComponent<ServiceItem>().Configure(station, (ItemId)i, names[i], bench, mount, obj.GetComponentInChildren<Renderer>());
                obj.GetComponent<ServiceItem>().interactionManager = interactionManager;
                obj.transform.position = i == 2 || i == 4 ? mount.Anchor.position : bench.Anchor.position;
            }

            var targets = new List<MaintenanceTarget>();
            AddTarget(f, station, interactionManager, targets, "power", "Пуск / Стоп", Operation.Power, 0, new Vector3(1.55f, 0.98f, -0.7f));
            AddTarget(f, station, interactionManager, targets, "air", "Подача воздуха", Operation.Air, 0, new Vector3(1.05f, 1.18f, -0.7f));
            AddTarget(f, station, interactionManager, targets, "vent", "Стравливание", Operation.Vent, 0, new Vector3(1.7f, 1.18f, -0.7f));
            AddTarget(f, station, interactionManager, targets, "sensor.cable", "Кабель датчика", Operation.SensorCable, 0, new Vector3(-0.9f, 1.16f, -0.85f));
            for (var i = 0; i < 2; i++)
            {
                AddTarget(f, station, interactionManager, targets, "sensor.nut." + i, "Гайка " + (i + 1), Operation.SensorNut, i, new Vector3(-1.06f + i * 0.32f, 0.87f, -0.87f));
                AddTarget(f, station, interactionManager, targets, "pusher.bolt." + i, "Болт " + (i + 1), Operation.PusherBolt, i, new Vector3(0.02f + i * 0.36f, 0.76f, -0.94f));
                AddTarget(f, station, interactionManager, targets, "pusher.tube." + i, "Трубка " + (i + 1), Operation.PusherTube, i, new Vector3(0.02f + i * 0.36f, 1.1f, -0.94f));
            }
            AddTarget(f, station, interactionManager, targets, "info.conveyor", "i Конвейер", Operation.Inspect, 0, new Vector3(-1.9f, 1.0f, -0.7f));
            AddTarget(f, station, interactionManager, targets, "info.sensor", "i Датчик", Operation.Inspect, 1, new Vector3(-1.4f, 1.16f, -0.7f));
            AddTarget(f, station, interactionManager, targets, "info.pusher", "i Толкатель", Operation.Inspect, 2, new Vector3(0.65f, 1.16f, -0.7f));

            var spawn = new GameObject("Washer Spawn").transform; spawn.SetParent(line.transform); spawn.position = new Vector3(-2.3f, 0.82f, 0);
            var actuator = PrefabFactory.Spawn(f.Actuator, "Pusher Striker").GetComponent<PusherActuator>(); actuator.transform.position = new Vector3(0.2f, 0.81f, -0.5f);
            var lamp = f.Box("Sensor Lamp", new Vector3(-0.9f, 1.13f, -0.64f), Vector3.one * 0.035f, f.Orange).GetComponent<Renderer>();
            line.Configure(f.Washer.GetComponent<Washer>(), spawn, actuator, lamp);
            var detection = PrefabFactory.Spawn(f.Detection, "Sensor Detection");
            detection.transform.position = new Vector3(-0.9f, 0.86f, 0);
            detection.GetComponent<BoxCollider>().size = new Vector3(0.12f, 0.25f, 0.8f);
            detection.GetComponent<SensorZone>().Configure(line);
            var metalCollector = PrefabFactory.Spawn(f.Collector, "Metal Collector");
            metalCollector.transform.position = new Vector3(0.2f, 0.4f, 1.9f);
            metalCollector.GetComponent<BoxCollider>().size = new Vector3(0.85f, 0.55f, 0.7f);
            metalCollector.GetComponent<CollectorZone>().Configure(true);
            var end = PrefabFactory.Spawn(f.Collector, "End Collector");
            end.transform.position = new Vector3(2.5f, 0.85f, 0);
            end.GetComponent<BoxCollider>().size = new Vector3(0.2f, 0.4f, 0.85f);
            end.GetComponent<CollectorZone>().Configure(false);

            var panel = PrefabFactory.Spawn(f.Panel, "Training Panel").GetComponent<TrainingPanel>();
            panel.transform.position = new Vector3(2.6f, 1.65f, -0.25f);
            panel.transform.rotation = Quaternion.Euler(0, 25, 0);
            panel.SetTargets(targets.ToArray());
            panel.GetComponent<Canvas>().worldCamera = teleportation.GetComponentInChildren<Camera>();
            station.Configure(definitions, line, panel);
            CreateControlsBoard(f, geometry);
            Physics.gravity = new Vector3(0, -9.81f, 0);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("MVP scene and editable prefabs created at " + ScenePath);
        }

        private static ItemSocket Socket(PrefabFactory factory, string name, Vector3 position, Location location)
        {
            var obj = PrefabFactory.Spawn(factory.Socket, name); obj.transform.position = position;
            var socket = obj.GetComponent<ItemSocket>(); socket.Configure(location, obj.transform.Find("Anchor"));
            return socket;
        }

        private static void AddTarget(PrefabFactory f, TrainingStation station, XRInteractionManager manager, List<MaintenanceTarget> targets,
            string id, string label, Operation operation, int index, Vector3 position)
        {
            var obj = PrefabFactory.Spawn(f.ActionTarget, id); obj.transform.position = position;
            foreach (var t in obj.GetComponentsInChildren<Transform>()) t.gameObject.layer = 8;
            var target = obj.GetComponent<MaintenanceTarget>(); target.interactionManager = manager;
            target.Configure(station, id, label, operation, index, obj.GetComponentInChildren<Renderer>());
            targets.Add(target);
            f.WorldLabel(label, position + new Vector3(0, 0.085f, -0.02f), 0.55f);
        }

        private static void CreateControlsBoard(PrefabFactory f, Transform parent)
        {
            var obj = PrefabFactory.Spawn(f.Label, "Simulator Controls", parent);
            obj.transform.position = new Vector3(-2.8f, 2.15f, -0.25f);
            obj.GetComponent<RectTransform>().sizeDelta = new Vector2(1700, 700);
            var text = obj.GetComponentInChildren<UnityEngine.UI.Text>(); text.fontSize = 38;
            text.alignment = TextAnchor.UpperLeft;
            text.text = "УПРАВЛЕНИЕ XR-СИМУЛЯТОРОМ\nT / Y — левая / правая рука\nПКМ — управление головой\nWASD / Q E — перемещение устройства\nR — поворот / перемещение мышью\nG — удерживать предмет\nЛКМ — действие / интерфейс\nЛКМ по полу и отпустить — телепорт\nОтпускайте предметы в выделенных местах";
        }

        public static void BuildWindowsPlayer()
        {
            Directory.CreateDirectory("Builds/ConveyorTrainer");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath },
                locationPathName = "Builds/ConveyorTrainer/ConveyorTrainer.exe", target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development });
            if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new InvalidOperationException("Windows player build failed.");
        }

        private static void AddChuteGuides(PrefabFactory factory, Transform geometry)
        {
            foreach (var x in new[] { -0.15f, 0.55f })
            {
                var guide = factory.Box(x < 0 ? "Chute Guide Left" : "Chute Guide Right", new Vector3(x, 0.56f, 1.15f), new Vector3(0.05f, 0.25f, 1.4f), factory.Steel, geometry);
                guide.transform.rotation = Quaternion.Euler(25, 0, 0);
            }
        }

    }
}
