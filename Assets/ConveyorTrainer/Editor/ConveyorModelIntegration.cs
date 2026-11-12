using System;
using System.Collections.Generic;
using System.Linq;
using ConveyorTrainer.Domain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using Object = UnityEngine.Object;

namespace ConveyorTrainer.Editor
{
    public static class ConveyorModelIntegration
    {
        private const string ConveyorPath = PrefabFactory.Prefabs + "/Conveyor.prefab";
        private const string PhysicsRoot = "Conveyor Physics";
        private const int EquipmentLayer = 8;
        private const int WasherLayer = 9;
        private const int PickupLayer = 10;

        [MenuItem("Conveyor Trainer/Integrate Conveyor Model (once)")]
        public static void Apply()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != MvpBuilder.ScenePath) scene = EditorSceneManager.OpenScene(MvpBuilder.ScenePath);
            if (!ApplyTo(scene)) return;
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
        }

        internal static bool ApplyTo(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            var conveyor = roots.SingleOrDefault(root => root.name == "Conveyor");
            if (conveyor == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ConveyorPath);
                if (prefab == null) return false;
                conveyor = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            }
            if (conveyor.transform.Find(PhysicsRoot) != null) return false;

            var transforms = conveyor.GetComponentsInChildren<Transform>(true);
            Transform Part(string name) => transforms.Single(t => t.name == name);
            var floor = RequireRoot(roots, "Teleport Floor").GetComponent<Collider>();
            var foundation = VisibleBounds(Part("Куб.027"));
            conveyor.transform.position += Vector3.up * (floor.bounds.max.y + 0.002f - foundation.min.y);
            Record(conveyor.transform);
            var station = Find<TrainingStation>(roots).Single();
            var line = Find<ConveyorLine>(roots).Single();
            var panel = Find<TrainingPanel>(roots).Single();
            var manager = Find<XRInteractionManager>(roots).Single();
            var items = Find<ServiceItem>(roots).ToDictionary(item => item.ItemId);
            var targets = Find<MaintenanceTarget>(roots).ToDictionary(target => target.TargetId);
            var labels = roots.Where(root => root.GetComponent<Canvas>() != null && root.GetComponentInChildren<Text>() != null)
                .ToDictionary(root => root.name);
            var geometry = ReadGeometry(Part);
            var physics = new GameObject(PhysicsRoot).transform;
            physics.SetParent(conveyor.transform, true);
            BuildPhysicalLine(geometry, physics, line, roots, Part);

            var sensor = InstallModules("ConveyorSensor", Part("SensorBody"), Part("SensorBody"), station, manager,
                items[ItemId.SensorOriginal], items[ItemId.SensorSpare]);
            var pusher = InstallModules("ConveyorPusher", Part("PusherAssembly"), Part("PusherBody"), station, manager,
                items[ItemId.PusherOriginal], items[ItemId.PusherSpare]);
            sensor.original.Mount.transform.SetParent(physics, true);
            pusher.original.Mount.transform.SetParent(physics, true);
            pusher.original.Mount.name = "PusherMount";
            Record(sensor.original.Mount.transform, pusher.original.Mount.transform, pusher.original.Mount.gameObject);
            ArrangeBench(roots, items[ItemId.Wrench], items[ItemId.Driver], sensor, pusher, geometry, labels);
            var sensorIndicator = Part("SensorIndicator").GetComponent<Renderer>();
            line.ConfigureModelGeometry(geometry.BeltFrame, geometry.BeltHalfExtents, geometry.SensorDelay, sensorIndicator,
                PusherMotion(pusher.original), PusherMotion(pusher.spare));

            BindTargets(Part, geometry, station, manager, targets, labels, physics);
            panel.SetTargets(targets.Values.ToArray());
            Record(panel);
            ArrangeWorkshop(roots, geometry);
            ScenarioFactory.Create();
            EditorSceneManager.MarkSceneDirty(scene);
            return true;
        }

        private static PusherActuator.MotionVisual PusherMotion(ServiceItem item)
        {
            var group = item.GetComponentsInChildren<Transform>().Single(t => t.name == "PusherMovingPart");
            return new PusherActuator.MotionVisual { group = group, rod = group.Find("PusherRod").GetComponent<MeshRenderer>() };
        }

        private static T[] Find<T>(GameObject[] roots) where T : Component
            => roots.SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static GameObject RequireRoot(GameObject[] roots, string name)
            => roots.Single(root => root != null && root.name == name);

        private static void Record(params Object[] objects)
        {
            foreach (var item in objects)
                if (PrefabUtility.IsPartOfPrefabInstance(item)) PrefabUtility.RecordPrefabInstancePropertyModifications(item);
        }

        private static Bounds VisibleBounds(Transform part)
        {
            var renderers = part.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException($"Missing mesh on {part.name}.");
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static (ServiceItem original, ServiceItem spare) InstallModules(string prefabName, Transform source, Transform grabPart,
            TrainingStation station, XRInteractionManager manager, ServiceItem oldOriginal, ServiceItem oldSpare)
        {
            var center = VisibleBounds(grabPart).center;
            var size = VisibleBounds(grabPart).size;
            var mount = oldOriginal.Mount;
            mount.transform.SetPositionAndRotation(center, Quaternion.identity);
            mount.Configure(mount.Location, mount.Anchor, oldOriginal.ItemId == ItemId.SensorOriginal ? 0.22f : 0.35f);
            var mountMarker = mount.transform.Find("PlacementMarker").gameObject;
            mountMarker.SetActive(false);
            Record(mount, mount.transform, mountMarker);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFactory.Prefabs + "/" + prefabName + ".prefab");
            if (prefab == null)
            {
                var holder = new GameObject(prefabName);
                holder.layer = PickupLayer;
                holder.transform.position = center;
                var model = Object.Instantiate(source.gameObject, source.parent, false);
                model.name = source.name;
                model.transform.SetParent(holder.transform, true);
                model.SetActive(true);
                var collider = holder.AddComponent<BoxCollider>();
                collider.size = Vector3.Max(size + Vector3.one * 0.025f, Vector3.one * 0.14f);
                var body = holder.AddComponent<Rigidbody>();
                body.useGravity = false;
                body.isKinematic = true;
                var interactable = holder.AddComponent<ServiceItem>();
                interactable.interactionLayers = 2;
                interactable.throwOnDetach = false;
                interactable.movementType = XRBaseInteractable.MovementType.Kinematic;
                interactable.useDynamicAttach = false;
                var visual = model.GetComponentsInChildren<Transform>().Single(t => t.name == grabPart.name);
                interactable.UseModelVisual(visual.GetComponentInChildren<MeshRenderer>());
                holder.transform.position = Vector3.zero;
                prefab = PrefabFactory.Save(prefabName, holder);
            }

            ServiceItem Replace(ServiceItem old, bool installed)
            {
                var item = PrefabFactory.Spawn(prefab, old.name).GetComponent<ServiceItem>();
                var visual = item.GetComponentsInChildren<Transform>().Single(t => t.name == grabPart.name);
                item.Configure(station, old.ItemId, old.name, old.Bench, mount, visual.GetComponentInChildren<MeshRenderer>());
                item.interactionManager = manager;
                var pose = installed ? mount.Anchor : old.Bench.Anchor;
                item.transform.SetPositionAndRotation(pose.position, pose.rotation);
                Record(item, item.transform, item.gameObject);
                Object.DestroyImmediate(old.gameObject);
                return item;
            }

            source.gameObject.SetActive(false);
            Record(source.gameObject);
            return (Replace(oldOriginal, true), Replace(oldSpare, false));
        }

        private static void BindTargets(Func<string, Transform> part, LineGeometry geometry, TrainingStation station, XRInteractionManager manager,
            Dictionary<string, MaintenanceTarget> targets, Dictionary<string, GameObject> labels, Transform parent)
        {
            if (targets.TryGetValue("power", out var power))
            {
                targets.Remove("power");
                targets.Add("power.start", power);
                targets.Add("power.stop", PrefabFactory.Spawn(
                    AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFactory.Prefabs + "/ActionTarget.prefab"), "power.stop").GetComponent<MaintenanceTarget>());
            }
            if (targets.TryGetValue("pusher.bolt.1", out var secondBolt))
            {
                targets.Remove("pusher.bolt.1");
                Object.DestroyImmediate(secondBolt.gameObject);
            }

            void Bind(string id, string label, Operation operation, Transform mount, int index = 0, Renderer wire = null)
            {
                var target = targets[id];
                target.name = id;
                target.interactionManager = manager;
                target.gameObject.layer = EquipmentLayer;
                var oldVisual = target.transform.Find("Visual");
                oldVisual.gameObject.layer = EquipmentLayer;
                var placeholder = oldVisual.GetComponent<Renderer>();
                placeholder.enabled = false;
                var bounds = VisibleBounds(mount);
                var center = bounds.center;
                var size = bounds.size + Vector3.one * 0.018f;
                if (operation == Operation.SensorNut) size = bounds.size + Vector3.one * 0.008f;
                if (operation == Operation.PusherBolt)
                {
                    // The four mounting tabs share one mesh and one service operation. Use the exposed front tab.
                    center = new Vector3(bounds.max.x - 0.035f, center.y, bounds.max.z - 0.035f);
                    size = new Vector3(0.09f, 0.07f, 0.10f);
                }
                oldVisual.localScale = Vector3.Min(size, Vector3.one * 0.22f);
                target.transform.SetPositionAndRotation(center, Quaternion.identity);
                target.transform.SetParent(parent, true);
                var renderer = mount.GetComponentInChildren<MeshRenderer>();
                target.Configure(station, id, label, operation, index, renderer);
                target.UseModelVisual(renderer, wire);
                Record(target, target.gameObject, target.transform, oldVisual, oldVisual.gameObject, placeholder);
            }

            Bind("power.start", "Пуск", Operation.Start, part("StartButton"));
            Bind("power.stop", "Стоп", Operation.Stop, part("StopButton"));
            Bind("air", "Подача воздуха", Operation.Air, part("AirShutoffValve"));
            Bind("vent", "Стравливание", Operation.Vent, part("VentValve"));
            Bind("sensor.cable", "Кабель датчика", Operation.SensorCable, part("ДАТЧИК.007"), wire: part("ТраекторияNurbs.001").GetComponent<MeshRenderer>());
            Bind("sensor.nut.0", "Гайка 1", Operation.SensorNut, part("SensorNut_01"));
            Bind("sensor.nut.1", "Гайка 2", Operation.SensorNut, part("SensorNut_02"), 1);
            Bind("pusher.bolt.0", "Крепёж толкателя", Operation.PusherBolt, part("PusherBolt_01"));
            Bind("pusher.tube.0", "Шланг 1", Operation.PusherTube, part("Куб.032"), wire: part("ТраекторияNurbs.004").GetComponent<MeshRenderer>());
            Bind("pusher.tube.1", "Шланг 2", Operation.PusherTube, part("Куб.033"), 1, part("ТраекторияNurbs.005").GetComponent<MeshRenderer>());

            foreach (var label in new[] { "Пуск / Стоп", "Пуск", "Стоп", "Подача воздуха", "Стравливание", "Кабель датчика", "Гайка 1",
                "Гайка 2", "Болт 1", "Болт 2", "Крепёж толкателя", "Трубка 1", "Трубка 2" })
                if (labels.TryGetValue(label, out var oldLabel)) Object.DestroyImmediate(oldLabel);

            void MoveInfo(string id, string label, Vector3 position)
            {
                targets[id].transform.position = position;
                targets[id].transform.SetParent(parent, true);
                Record(targets[id].transform);
                if (!labels.TryGetValue(label, out var text)) return;
                text.transform.position = position + Vector3.up * 0.10f;
                text.transform.SetParent(parent, true);
                Record(text.transform);
            }

            MoveInfo("info.conveyor", "i Конвейер", geometry.Spawn + geometry.ServiceSide * 0.45f + Vector3.up * 0.32f);
            MoveInfo("info.sensor", "i Датчик", VisibleBounds(part("SensorMount")).center + geometry.ServiceSide * 0.18f + Vector3.up * 0.24f);
            MoveInfo("info.pusher", "i Толкатель", geometry.PusherHome + geometry.ServiceSide * 0.72f + Vector3.up * 0.24f);
        }

        private sealed class LineGeometry
        {
            public Quaternion Rotation;
            public Vector3 ServiceSide;
            public Bounds Feed;
            public Bounds MainExit;
            public Bounds MetalExit;
            public Vector3 Spawn;
            public Vector3 PusherHome;
            public float PusherTravel;
            public float SensorDelay;
            public Transform BeltFrame;
            public Vector3 BeltHalfExtents;
        }

        private static LineGeometry ReadGeometry(Func<string, Transform> part)
        {
            var belt = part("Belt");
            var forward = Vector3.ProjectOnPlane(-belt.right, Vector3.up).normalized;
            var bin = VisibleBounds(part("MetalBin"));
            if (Vector3.Dot(bin.center - VisibleBounds(belt).center, forward) < 0) forward = -forward;
            var across = Vector3.Cross(forward, Vector3.up);
            var rotation = Quaternion.LookRotation(across, Vector3.up);
            var runs = belt.Cast<Transform>().Select(t => BoundsInFrame(t, rotation)).OrderBy(b => b.min.x).ToArray();
            if (runs.Length != 3) throw new InvalidOperationException("Expected a feed surface and two exit surfaces under Belt.");
            var main = runs.Skip(1).OrderByDescending(b => b.max.x).First();
            var metal = runs.Skip(1).OrderBy(b => b.max.x).First();
            var feed = runs[0];
            var head = BoundsInFrame(part("PusherHead"), rotation);
            var sensor = BoundsInFrame(part("SensorBody"), rotation);
            var side = head.center.z > feed.center.z ? 1 : -1;
            const float strokeSpeed = 3.2f;
            const float washerRadius = 0.07f;
            const float detectorThickness = 0.12f;
            var lateralDistance = Mathf.Max(0, (head.center.z - main.center.z) * side - head.extents.z - washerRadius);
            var delay = (head.center.x - sensor.center.x + washerRadius + detectorThickness * 0.5f) / 0.65f - lateralDistance / strokeSpeed;
            return new LineGeometry
            {
                Rotation = rotation,
                ServiceSide = across * side,
                Feed = feed,
                MainExit = main,
                MetalExit = metal,
                Spawn = rotation * new Vector3(feed.min.x + 0.23f, feed.max.y + 0.07f, main.center.z),
                PusherHome = rotation * new Vector3(head.center.x, feed.max.y + 0.075f, head.center.z),
                PusherTravel = Mathf.Max(0.2f, Mathf.Abs(head.center.z - main.center.z) + 0.13f),
                SensorDelay = Mathf.Max(0, delay)
            };
        }

        private static Bounds BoundsInFrame(Transform part, Quaternion rotation)
        {
            var inverse = Quaternion.Inverse(rotation);
            var corners = part.GetComponentsInChildren<MeshRenderer>(true).SelectMany(renderer =>
            {
                var bounds = renderer.localBounds;
                var points = new Vector3[8];
                for (var i = 0; i < points.Length; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (i & 2) == 0 ? bounds.min.y : bounds.max.y, (i & 4) == 0 ? bounds.min.z : bounds.max.z);
                    points[i] = inverse * renderer.transform.TransformPoint(corner);
                }
                return points;
            }).ToArray();
            var result = new Bounds(corners[0], Vector3.zero);
            foreach (var point in corners) result.Encapsulate(point);
            return result;
        }

        private static void BuildPhysicalLine(LineGeometry geometry, Transform parent, ConveyorLine line,
            GameObject[] roots, Func<string, Transform> part)
        {
            var rotation = geometry.Rotation;
            var feed = geometry.Feed;
            var main = geometry.MainExit;
            var metal = geometry.MetalExit;

            BoxCollider Box(string name, Vector3 center, Vector3 size, Quaternion orientation)
            {
                var block = new GameObject(name) { layer = EquipmentLayer };
                block.transform.SetParent(parent, false);
                block.transform.SetPositionAndRotation(center, orientation);
                var collider = block.AddComponent<BoxCollider>();
                collider.size = size;
                return collider;
            }
            BoxCollider ModelBox(string name, string partName)
            {
                var bounds = BoundsInFrame(part(partName), rotation);
                return Box(name, rotation * bounds.center, bounds.size, rotation);
            }
            void Deck(string name, Bounds surface)
                => Box(name, rotation * new Vector3(surface.center.x, surface.max.y - 0.025f, surface.center.z),
                    new Vector3(surface.size.x + 0.02f, 0.05f, surface.size.z), rotation);
            void Rail(string name, string partName)
            {
                var bounds = BoundsInFrame(part(partName), rotation);
                Box(name, rotation * new Vector3(bounds.center.x, feed.max.y + 0.035f, bounds.center.z),
                    new Vector3(bounds.size.x, 0.14f, Mathf.Max(0.035f, bounds.size.z * 0.65f)), rotation);
            }

            Deck("Feed belt", feed);
            Deck("Main exit belt", main);
            Deck("Metal exit belt", metal);
            Rail("Service side rail", "конвеер.001");
            Rail("Metal side rail", "конвеер.002");
            Rail("Lane divider", "конвеер.005");
            // The frame mesh includes the empty space under the line. Its box is for player clearance.
            ModelBox("Conveyor frame clearance", "Куб.027").excludeLayers = 1 << WasherLayer;
            ModelBox("Pneumatic cabinet", "Куб.016");
            ModelBox("Control cabinet", "ьеьеь.003");

            var chute = BoundsInFrame(part("Куб"), rotation);
            var lip = rotation * new Vector3(metal.max.x - 0.025f, metal.max.y - 0.035f, chute.center.z);
            var outlet = rotation * new Vector3(chute.max.x, chute.min.y + 0.018f, chute.center.z);
            var slope = (outlet - lip).normalized;
            var rampAcross = rotation * Vector3.forward;
            var rampUp = Vector3.Cross(rampAcross, slope);
            var rampRotation = Quaternion.LookRotation(rampAcross, rampUp);
            var rampMiddle = (lip + outlet) * 0.5f;
            var rampLength = Vector3.Distance(lip, outlet);
            Box("Chute floor", rampMiddle - rampUp * 0.022f, new Vector3(rampLength, 0.044f, chute.size.z), rampRotation);
            foreach (var side in new[] { -1, 1 })
                Box("Chute side", rampMiddle + rampAcross * ((chute.extents.z + 0.03f) * side) + rampUp * 0.075f,
                    new Vector3(rampLength, 0.15f, 0.04f), rampRotation);

            var bin = BoundsInFrame(part("MetalBin"), rotation);
            Box("Metal bin base", rotation * new Vector3(bin.center.x, bin.min.y + 0.03f, bin.center.z),
                new Vector3(bin.size.x, 0.06f, bin.size.z), rotation);
            var bounds = feed;
            bounds.Encapsulate(main);
            bounds.Encapsulate(metal);
            var transport = new GameObject("Belt transport frame").transform;
            transport.SetParent(parent, false);
            transport.SetPositionAndRotation(rotation * new Vector3(bounds.center.x, feed.max.y + 0.07f, bounds.center.z), rotation);
            geometry.BeltFrame = transport;
            geometry.BeltHalfExtents = new Vector3(bounds.extents.x, 0.13f, bounds.extents.z);

            var spawn = line.transform.Find("Washer Spawn");
            spawn.SetParent(transport, true);
            spawn.SetPositionAndRotation(geometry.Spawn, rotation);
            var actuator = RequireRoot(roots, "Pusher Striker").GetComponent<PusherActuator>();
            actuator.transform.SetParent(parent, true);
            actuator.transform.SetPositionAndRotation(geometry.PusherHome, Quaternion.LookRotation(-geometry.ServiceSide));
            var proxy = actuator.GetComponentInChildren<BoxCollider>();
            proxy.GetComponent<Renderer>().enabled = false;
            proxy.transform.localScale = new Vector3(0.22f, 0.16f, 0.12f);
            proxy.gameObject.layer = EquipmentLayer;
            actuator.ConfigureStroke(geometry.PusherTravel, 3.2f);
            Record(actuator, actuator.transform, proxy.transform, proxy.gameObject, proxy.GetComponent<Renderer>());

            void PlaceTrigger(string name, Vector3 center, Vector3 size)
            {
                var trigger = RequireRoot(roots, name).GetComponent<BoxCollider>();
                trigger.transform.SetParent(parent, true);
                trigger.transform.SetPositionAndRotation(rotation * center, rotation);
                trigger.size = size;
                Record(trigger, trigger.transform);
            }
            var sensor = BoundsInFrame(part("SensorBody"), rotation);
            PlaceTrigger("Sensor Detection", new Vector3(sensor.center.x, feed.max.y + 0.06f, main.center.z), new Vector3(0.12f, 0.22f, 0.40f));
            PlaceTrigger("End Collector", new Vector3(main.max.x + 0.08f, main.max.y + 0.015f, main.center.z),
                new Vector3(0.20f, 0.35f, main.size.z));
            PlaceTrigger("Metal Collector", new Vector3(bin.center.x, bin.center.y + 0.06f, bin.center.z),
                new Vector3(bin.size.x - 0.10f, bin.size.y * 0.70f, bin.size.z - 0.10f));
            Object.DestroyImmediate(RequireRoot(roots, "Sensor Lamp"));
        }

        private static void ArrangeBench(GameObject[] roots, ServiceItem wrench, ServiceItem driver,
            (ServiceItem original, ServiceItem spare) sensor, (ServiceItem original, ServiceItem spare) pusher,
            LineGeometry geometry, Dictionary<string, GameObject> labels)
        {
            var workshop = RequireRoot(roots, "Workshop").transform;
            var bench = roots.SingleOrDefault(root => root != null && root.name == "Bench")?.transform ?? workshop.Find("Bench");
            var legs = bench.Cast<Transform>().Concat(workshop.Cast<Transform>()).Where(t => t.name == "Bench Leg").ToArray();
            foreach (var leg in legs) if (leg.parent != bench) leg.SetParent(bench, true);
            bench.SetParent(null, true);
            var forward = geometry.Rotation * Vector3.right;
            var position = geometry.Spawn - forward * 1.2f + geometry.ServiceSide * 1.45f;
            position.y = bench.position.y;
            bench.position = position;
            Record(bench);
            var top = bench.GetComponent<Collider>().bounds.max.y;
            var columns = new[] { -0.53f, 0.53f };
            var rows = new[] { -0.60f, -0.10f, 0.49f };
            var all = new[] { wrench, driver, sensor.original, sensor.spare, pusher.original, pusher.spare };
            for (var i = 0; i < all.Length; i++)
            {
                var item = all[i];
                var model = VisibleBounds(item.transform);
                var height = item.transform.position.y - model.min.y;
                var socket = item.Bench;
                socket.transform.position = new Vector3(position.x + columns[i % 2], top + height + 0.01f, position.z + rows[i / 2]);
                socket.Configure(Location.Bench, socket.Anchor, 0.30f);
                var marker = socket.transform.Find("PlacementMarker");
                marker.position = new Vector3(socket.transform.position.x, top + 0.006f, socket.transform.position.z);
                marker.localScale = new Vector3(Mathf.Max(0.26f, model.size.x + 0.08f), 0.012f, Mathf.Max(0.26f, model.size.z + 0.06f));
                if (labels.TryGetValue(item.name, out var label))
                {
                    label.transform.position = marker.position + Vector3.back * (marker.localScale.z * 0.5f) + Vector3.up * 0.04f;
                    Record(label.transform);
                }
                if (item != sensor.original && item != pusher.original)
                    item.transform.SetPositionAndRotation(socket.Anchor.position, socket.Anchor.rotation);
                Record(item.transform, socket, socket.transform, marker);
            }
        }

        private static void ArrangeWorkshop(GameObject[] roots, LineGeometry geometry)
        {
            var workshop = RequireRoot(roots, "Workshop");
            var controls = workshop.transform.Find("Simulator Controls");
            if (controls != null)
            {
                controls.SetParent(null, true);
                var position = geometry.Spawn - geometry.Rotation * Vector3.right * 1.2f + geometry.ServiceSide * 2.25f;
                position.y = 1.8f;
                controls.position = position;
                Record(controls);
            }
            var legacyGeometry = new HashSet<string> { "Belt", "Conveyor Leg", "Sensor Bracket", "Pusher Stand", "Divider Left", "Divider Right",
                "Metal Chute", "Chute Guide Left", "Chute Guide Right", "Bin Base", "Bin Left", "Bin Right", "Bin Back", "МЕТАЛЛ" };
            foreach (var obsolete in workshop.transform.Cast<Transform>().Where(t => legacyGeometry.Contains(t.name)).ToArray())
                Object.DestroyImmediate(obsolete.gameObject);
            if (workshop.transform.childCount == 0) Object.DestroyImmediate(workshop);
        }
    }
}
