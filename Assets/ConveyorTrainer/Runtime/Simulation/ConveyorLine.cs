using System;
using System.Collections.Generic;
using ConveyorTrainer.Domain;
using UnityEngine;

namespace ConveyorTrainer
{
    public sealed class ConveyorLine : MonoBehaviour
    {
        [SerializeField] private Washer washerPrefab;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private PusherActuator actuator;
        [SerializeField] private Renderer sensorLamp;
        [SerializeField, Min(0.1f)] private float beltSpeed = 0.65f;
        [SerializeField, Min(0.5f)] private float spawnInterval = 1.6f;
        [SerializeField, Min(0)] private float sensorDelay = 1.69f;
        [SerializeField] private Vector2 beltXLimits = new Vector2(-2.5f, 2.5f);
        [SerializeField, Min(0.1f)] private float beltHalfWidth = 0.42f;
        private readonly List<Washer> washers = new List<Washer>();
        private readonly List<ScheduledStroke> strokes = new List<ScheduledStroke>();
        private MaterialPropertyBlock lampProperties;
        private TrainingStation station;
        private MaintenanceModel model;
        private float nextSpawn;
        private float lampUntil;
        private bool nextMetal = true;
        private int lastRevision = -1;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private struct ScheduledStroke { public Washer washer; public float due; public int revision; }
        public int ActiveWasherCount => washers.Count;
        public int PendingStrokeCount => strokes.Count;
        public PusherActuator Actuator => actuator;
        public bool Initialized => model != null;
        public int DetectedMetalCount { get; private set; }
        public int CorrectMetalCount { get; private set; }
        public int CorrectPlasticCount { get; private set; }
        public int MisroutedCount { get; private set; }

        private void Awake() { lampProperties = new MaterialPropertyBlock(); }

        public void Configure(Washer prefab, Transform spawn, PusherActuator pusher, Renderer lamp)
        { washerPrefab = prefab; spawnPoint = spawn; actuator = pusher; sensorLamp = lamp; }
        public void Initialize(TrainingStation training)
        { station = training; BindModel(training.Model); }
        public void BindModel(MaintenanceModel equipment)
        {
            if (model != null) model.Changed -= OnModelChanged;
            model = equipment ?? throw new ArgumentNullException(nameof(equipment));
            lastRevision = -1;
            model.Changed += OnModelChanged;
            OnModelChanged();
        }

        private void Update()
        {
            if (model == null) return;
            if (model.Running && Time.time >= nextSpawn)
            {
                Spawn(nextMetal); nextMetal = !nextMetal; nextSpawn = Time.time + spawnInterval;
            }
            for (var i = strokes.Count - 1; i >= 0; i--)
            {
                var scheduled = strokes[i];
                if (Time.time < scheduled.due) continue;
                strokes.RemoveAt(i);
                if (scheduled.washer != null && scheduled.revision == model.Revision && model.PusherOperational) actuator.TryStroke();
            }
            if (sensorLamp != null)
            {
                lampProperties.SetColor(BaseColor, model.SensorOperational && Time.time < lampUntil ? Color.yellow : new Color(0.12f, 0.15f, 0.08f));
                sensorLamp.SetPropertyBlock(lampProperties);
            }
        }

        private void FixedUpdate()
        {
            if (model == null || !model.Running) return;
            for (var i = washers.Count - 1; i >= 0; i--)
            {
                var washer = washers[i];
                if (washer == null || washer.Consumed) continue;
                var p = washer.Body.position;
                if (p.x >= beltXLimits.x && p.x <= beltXLimits.y && Mathf.Abs(p.z) < beltHalfWidth && p.y > 0.65f && p.y < 1.05f)
                {
                    var v = washer.Body.velocity;
                    v.x = beltSpeed;
                    washer.Body.velocity = v;
                }
                if (p.y < -1f || Time.time >= washer.ExpiresAt) washer.Consume(false, false);
            }
        }

        public Washer Spawn(bool metallic)
        {
            var washer = Instantiate(washerPrefab, spawnPoint.position, spawnPoint.rotation, transform);
            washer.Initialize(this, metallic, model.Revision);
            washers.Add(washer);
            return washer;
        }

        public void Detect(Washer washer)
        {
            if (!model.SensorOperational || !washer.Metallic || washer.Detected || washer.Consumed) return;
            washer.Detected = true;
            DetectedMetalCount++;
            lampUntil = Time.time + 0.25f;
            strokes.Add(new ScheduledStroke { washer = washer, due = Time.time + sensorDelay, revision = model.Revision });
        }

        public void Collect(Washer washer, bool metalDestination, bool validDestination)
        {
            washers.Remove(washer);
            if (validDestination)
            {
                if (washer.Metallic != metalDestination) MisroutedCount++;
                else if (washer.Metallic) CorrectMetalCount++;
                else CorrectPlasticCount++;
            }
            if (validDestination && washer.BornRevision == model.Revision)
                station?.Procedure?.RegisterSorted(washer.Metallic, washer.Metallic == metalDestination);
        }

        private void OnModelChanged()
        {
            if (lastRevision == model.Revision) return;
            lastRevision = model.Revision;
            strokes.Clear();
            actuator?.ResetStroke();
            lampUntil = 0;
            if (model.Running) return;
            ResetSimulation();
        }

        public void ResetSimulation()
        {
            strokes.Clear();
            actuator?.ResetStroke();
            lampUntil = 0;
            for (var i = washers.Count - 1; i >= 0; i--) if (washers[i] != null) Destroy(washers[i].gameObject);
            washers.Clear();
            nextSpawn = Time.time + 0.25f;
            nextMetal = true;
            DetectedMetalCount = CorrectMetalCount = CorrectPlasticCount = MisroutedCount = 0;
        }

        private void OnDestroy() { if (model != null) model.Changed -= OnModelChanged; }
    }
}
