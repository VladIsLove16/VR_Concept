using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ConveyorTrainer.Editor
{
    internal sealed class PrefabFactory
    {
        public const string Root = "Assets/ConveyorTrainer/Generated";
        public const string Prefabs = Root + "/Prefabs";
        public readonly Material Steel;
        public readonly Material Dark;
        public readonly Material Teal;
        public readonly Material Orange;
        public readonly Material Floor;
        public readonly Material LineMaterial;
        public GameObject Block { get; }
        public GameObject Label { get; }
        public GameObject UiText { get; }
        public GameObject UiButton { get; }
        public GameObject Panel { get; }
        public GameObject ActionTarget { get; }
        public GameObject Sensor { get; }
        public GameObject Pusher { get; }
        public GameObject Wrench { get; }
        public GameObject Driver { get; }
        public GameObject Socket { get; }
        public GameObject Washer { get; }
        public GameObject Actuator { get; }
        public GameObject Collector { get; }
        public GameObject Detection { get; }
        public GameObject Reticle { get; }
        private Font font;

        public PrefabFactory()
        {
            Directory.CreateDirectory(Prefabs);
            Directory.CreateDirectory(Root + "/Materials");
            AssetDatabase.Refresh();
            Steel = Material("Steel", new Color(0.55f, 0.63f, 0.68f), 0.7f);
            Dark = Material("Graphite", new Color(0.08f, 0.12f, 0.16f));
            Teal = Material("Teal", new Color(0.1f, 0.65f, 0.55f));
            Orange = Material("Polymer", new Color(0.95f, 0.5f, 0.12f));
            Floor = Material("Floor", new Color(0.18f, 0.23f, 0.28f));
            LineMaterial = Material("Ray", new Color(0.2f, 0.9f, 0.75f));
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Block = Save("GeometryBlock", GameObject.CreatePrimitive(PrimitiveType.Cube));
            UiText = CreateTextPrefab();
            UiButton = CreateButtonPrefab();
            Label = CreateLabelPrefab();
            Panel = CreatePanelPrefab();
            ActionTarget = CreateTargetPrefab();
            Sensor = CreateItemPrefab("Sensor", new Vector3(0.18f, 0.15f, 0.18f));
            Pusher = CreateItemPrefab("Pusher", new Vector3(0.32f, 0.2f, 0.25f));
            Wrench = CreateToolPrefab("Wrench", false);
            Driver = CreateToolPrefab("MountingTool", true);
            Socket = CreateSocketPrefab();
            Washer = CreateWasherPrefab();
            Actuator = CreateActuatorPrefab();
            Collector = CreateZonePrefab("Collector", false);
            Detection = CreateZonePrefab("SensorZone", true);
            Reticle = CreateReticlePrefab();
        }

        private Material Material(string name, Color color, float metallic = 0)
        {
            var path = Root + "/Materials/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", 0.35f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        public static GameObject Save(string name, GameObject source)
        {
            var path = Prefabs + "/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing == null) { source.name = name; existing = PrefabUtility.SaveAsPrefabAsset(source, path); }
            Object.DestroyImmediate(source);
            return existing;
        }

        public static GameObject Spawn(GameObject prefab, string name, Transform parent = null)
        {
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            obj.name = name;
            if (parent != null) obj.transform.SetParent(parent, false);
            return obj;
        }

        public GameObject Box(string name, Vector3 position, Vector3 size, Material material, Transform parent = null, bool solid = true)
        {
            var obj = Spawn(Block, name, parent);
            obj.layer = 8;
            obj.transform.position = position;
            obj.transform.localScale = size;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            obj.GetComponent<Collider>().enabled = solid;
            return obj;
        }

        public void WorldLabel(string text, Vector3 position, float width = 0.7f, Transform parent = null)
        {
            var obj = Spawn(Label, text, parent);
            obj.transform.position = position;
            var canvas = obj.GetComponent<RectTransform>();
            canvas.sizeDelta = new Vector2(width * 1000, 120);
            obj.GetComponentInChildren<Text>().text = text;
        }

        public Text Text(Transform parent, string name, string content, Vector2 position, Vector2 size, int fontSize = 26)
        {
            var obj = Spawn(UiText, name, parent);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = obj.GetComponent<Text>();
            text.text = content; text.fontSize = fontSize;
            return text;
        }

        private GameObject CreateTextPrefab()
        {
            var obj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var text = obj.GetComponent<Text>();
            text.font = font; text.fontSize = 26; text.color = new Color(0.88f, 0.95f, 0.98f);
            text.alignment = TextAnchor.UpperLeft; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return Save("Text", obj);
        }

        private GameObject CreateButtonPrefab()
        {
            var obj = new GameObject("Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            obj.GetComponent<RectTransform>().sizeDelta = new Vector2(430, 72);
            obj.GetComponent<Image>().color = new Color(0.08f, 0.4f, 0.38f);
            var label = Text(obj.transform, "Label", "Кнопка", Vector2.zero, new Vector2(430, 72), 28);
            label.alignment = TextAnchor.MiddleCenter;
            return Save("Button", obj);
        }

        private GameObject CreateLabelPrefab()
        {
            var obj = new GameObject("WorldLabel", typeof(RectTransform), typeof(Canvas));
            obj.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            obj.GetComponent<RectTransform>().sizeDelta = new Vector2(700, 120);
            obj.transform.localScale = Vector3.one * 0.001f;
            var label = Text(obj.transform, "Label", "Подпись", Vector2.zero, new Vector2(700, 120), 36);
            var rect = label.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            label.alignment = TextAnchor.MiddleCenter;
            return Save("WorldLabel", obj);
        }

        private GameObject CreatePanelPrefab()
        {
            var obj = new GameObject("TrainingPanel", typeof(RectTransform), typeof(Canvas), typeof(TrackedDeviceGraphicRaycaster), typeof(CanvasRenderer), typeof(Image), typeof(TrainingPanel));
            obj.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            obj.GetComponent<RectTransform>().sizeDelta = new Vector2(1000, 1080);
            obj.GetComponent<Image>().color = new Color(0.025f, 0.055f, 0.085f, 0.98f);
            obj.transform.localScale = Vector3.one * 0.0017f;
            var title = Text(obj.transform, "Title", "Обслуживание линии", new Vector2(40, -35), new Vector2(920, 85), 38);
            var task = Text(obj.transform, "Procedure", "Задание", new Vector2(40, -130), new Vector2(920, 455), 27);
            var feedback = Text(obj.transform, "Feedback", "Обратная связь", new Vector2(40, -600), new Vector2(920, 190), 26);
            feedback.color = new Color(1f, 0.8f, 0.35f);
            var equipment = Text(obj.transform, "Equipment", "Состояние оборудования", new Vector2(40, -800), new Vector2(920, 85), 25);
            var hover = Text(obj.transform, "Hover", "", new Vector2(40, -895), new Vector2(920, 55), 27);
            var next = Spawn(UiButton, "NextScenario", obj.transform);
            next.GetComponent<RectTransform>().anchorMin = next.GetComponent<RectTransform>().anchorMax = new Vector2(0, 1);
            next.GetComponent<RectTransform>().pivot = new Vector2(0, 1);
            next.GetComponent<RectTransform>().anchoredPosition = new Vector2(520, -970);
            next.GetComponentInChildren<Text>().text = "Следующий сценарий";
            var restart = Spawn(UiButton, "Restart", obj.transform);
            restart.GetComponent<RectTransform>().anchorMin = restart.GetComponent<RectTransform>().anchorMax = new Vector2(0, 1);
            restart.GetComponent<RectTransform>().pivot = new Vector2(0, 1);
            restart.GetComponent<RectTransform>().anchoredPosition = new Vector2(40, -970);
            restart.GetComponentInChildren<Text>().text = "Повторить";
            obj.GetComponent<TrainingPanel>().Configure(title, task, feedback, equipment, hover, next.GetComponent<Button>(), restart.GetComponent<Button>());
            return Save("TrainingPanel", obj);
        }

        private GameObject CreateTargetPrefab()
        {
            var obj = new GameObject("ActionTarget");
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual"; visual.transform.SetParent(obj.transform, false); visual.transform.localScale = new Vector3(0.1f, 0.08f, 0.08f);
            visual.GetComponent<Renderer>().sharedMaterial = Teal;
            obj.AddComponent<MaintenanceTarget>().interactionLayers = 1;
            return Save("ActionTarget", obj);
        }

        private GameObject CreateItemPrefab(string name, Vector3 size)
        {
            var obj = new GameObject(name);
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Body"; visual.transform.SetParent(obj.transform, false); visual.transform.localScale = size;
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.GetComponent<Renderer>().sharedMaterial = Teal;
            obj.AddComponent<BoxCollider>().size = size;
            var body = obj.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            var grab = obj.AddComponent<ServiceItem>();
            grab.interactionLayers = 2; grab.throwOnDetach = false;
            grab.movementType = XRBaseInteractable.MovementType.Kinematic;
            grab.useDynamicAttach = false;
            return Save(name, obj);
        }

        private GameObject CreateToolPrefab(string name, bool driver)
        {
            var baseItem = CreateItemPrefab(name, new Vector3(0.045f, 0.04f, 0.28f));
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/" + name + "Complete.prefab") != null)
                return AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/" + name + "Complete.prefab");
            var obj = Spawn(baseItem, name);
            var head = GameObject.CreatePrimitive(driver ? PrimitiveType.Cylinder : PrimitiveType.Cube);
            head.transform.SetParent(obj.transform, false); head.name = "Head";
            head.transform.localPosition = new Vector3(0, 0, 0.13f);
            head.transform.localScale = driver ? new Vector3(0.025f, 0.12f, 0.025f) : new Vector3(0.12f, 0.045f, 0.08f);
            if (driver) head.transform.localRotation = Quaternion.Euler(90, 0, 0);
            Object.DestroyImmediate(head.GetComponent<Collider>());
            head.GetComponent<Renderer>().sharedMaterial = Steel;
            return Save(name + "Complete", obj);
        }

        private GameObject CreateSocketPrefab()
        {
            var obj = new GameObject("ItemSocket");
            var anchor = new GameObject("Anchor").transform; anchor.SetParent(obj.transform, false);
            obj.AddComponent<ItemSocket>().Configure(Domain.Location.Bench, anchor);
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "PlacementMarker"; marker.transform.SetParent(obj.transform, false);
            marker.transform.localPosition = new Vector3(0, -0.12f, 0);
            marker.transform.localScale = new Vector3(0.4f, 0.012f, 0.4f);
            marker.GetComponent<Renderer>().sharedMaterial = Teal;
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            return Save("ItemSocket", obj);
        }

        private GameObject CreateWasherPrefab()
        {
            var obj = new GameObject("Washer"); obj.layer = 9;
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.transform.SetParent(obj.transform, false); visual.name = "Disk";
            visual.transform.localScale = new Vector3(0.14f, 0.015f, 0.14f);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            obj.AddComponent<BoxCollider>().size = new Vector3(0.14f, 0.03f, 0.14f);
            obj.GetComponent<Collider>().sharedMaterial = WasherSurface();
            var body = obj.AddComponent<Rigidbody>(); body.mass = 0.08f;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate; body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            obj.AddComponent<Washer>().Configure(visual.GetComponent<Renderer>(), Steel, Orange);
            return Save("Washer", obj);
        }

        internal static PhysicMaterial WasherSurface()
        {
            var path = Root + "/Materials/WasherSurface.physicMaterial";
            var material = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(path);
            if (material != null) return material;
            material = new PhysicMaterial("WasherSurface") { dynamicFriction = 0, staticFriction = 0, bounciness = 0,
                frictionCombine = PhysicMaterialCombine.Minimum, bounceCombine = PhysicMaterialCombine.Minimum };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private GameObject CreateActuatorPrefab()
        {
            var obj = new GameObject("PusherStriker"); obj.layer = 8;
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube); visual.name = "Striker";
            visual.transform.SetParent(obj.transform, false); visual.transform.localScale = new Vector3(0.22f, 0.13f, 0.18f);
            visual.GetComponent<Renderer>().sharedMaterial = Steel;
            var body = obj.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            obj.AddComponent<PusherActuator>();
            return Save("PusherStriker", obj);
        }

        private GameObject CreateZonePrefab(string name, bool sensor)
        {
            var obj = new GameObject(name);
            obj.AddComponent<BoxCollider>().isTrigger = true;
            if (sensor) obj.AddComponent<SensorZone>(); else obj.AddComponent<CollectorZone>();
            return Save(name, obj);
        }

        private GameObject CreateReticlePrefab()
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            obj.transform.localScale = new Vector3(0.35f, 0.005f, 0.35f);
            obj.GetComponent<Renderer>().sharedMaterial = Teal;
            Object.DestroyImmediate(obj.GetComponent<Collider>());
            return Save("TeleportReticle", obj);
        }
    }
}
