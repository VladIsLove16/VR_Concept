using System;
using System.IO;
using ConveyorTrainer.Domain;
using UnityEngine;

namespace ConveyorTrainer
{
    public sealed class TrainingStation : MonoBehaviour
    {
        [SerializeField] private ScenarioDefinition[] scenarios;
        [SerializeField] private ConveyorLine line;
        [SerializeField] private TrainingPanel panel;
        [SerializeField] private int initialScenario;
        private IAttemptStore store;
        private double started;
        private bool saveFailed;
        public MaintenanceModel Model { get; private set; }
        public ProcedureTracker Procedure { get; private set; }
        public AttemptRecord Attempt { get; private set; }
        public ScenarioDefinition Scenario => scenarios[ScenarioIndex];
        public int ScenarioIndex { get; private set; }
        public int ScenarioCount => scenarios.Length;
        public string Message { get; private set; } = "";
        public string HoverText { get; private set; } = "";
        public event Action Refreshed;
        public bool Completed => Attempt != null && Attempt.status == "completed";

        public void Configure(ScenarioDefinition[] definitions, ConveyorLine conveyor, TrainingPanel interfacePanel)
        { scenarios = definitions; line = conveyor; panel = interfacePanel; }

        private void Start()
        {
            store = new JsonAttemptStore(Path.Combine(Application.persistentDataPath, "Attempts"));
            Model = new MaintenanceModel();
            line.Initialize(this);
            panel.Initialize(this);
            BeginScenario(initialScenario);
        }

        public void BeginScenario(int index)
        {
            if (index < 0 || index >= scenarios.Length) throw new ArgumentOutOfRangeException(nameof(index));
            EndAttempt();
            if (Procedure != null) { Procedure.Updated -= Refresh; Procedure.Dispose(); }
            Model.Attempted -= OnAttempt;
            ScenarioIndex = index;
            Model.Reset(Scenario.Fault, Scenario.Fault != Fault.None);
            line.ResetSimulation();
            Procedure = new ProcedureTracker(Model, Scenario.Requirements, Scenario.Fault == Fault.None);
            Procedure.Updated += Refresh;
            started = Time.realtimeSinceStartupAsDouble;
            Attempt = new AttemptRecord { attemptId = Guid.NewGuid().ToString("N"), scenarioId = Scenario.Id,
                startedUtc = DateTime.UtcNow.ToString("O") };
            Model.Attempted += OnAttempt;
            Message = Scenario.Briefing;
            Refresh();
            Save();
        }

        public ActionResult Execute(Request request, string targetId)
        {
            if (Completed && request.Operation != Operation.Place && request.Operation != Operation.InvalidDrop && request.Operation != Operation.Inspect)
                return ActionResult.Success;
            lastTarget = targetId;
            return Model.Execute(request);
        }
        private string lastTarget;

        public void ShowHover(string text) { HoverText = text; Refreshed?.Invoke(); }
        public void ShowMessage(string text) { Message = text; Refreshed?.Invoke(); }
        public void Restart() => BeginScenario(ScenarioIndex);
        public void Next()
        {
            if (!Completed) { ShowMessage("Сначала выполните обязательные условия сценария."); return; }
            if (ScenarioIndex + 1 < scenarios.Length) BeginScenario(ScenarioIndex + 1);
            else ShowMessage("Все пять сценариев завершены. Можно повторить текущий сценарий.");
        }

        private void OnAttempt(Request request, ActionResult result)
        {
            if (Completed)
            {
                if (request.Operation == Operation.Inspect) { Message = TrainerText.Information((InfoTarget)request.Index); Refreshed?.Invoke(); }
                return;
            }
            if (!result.Succeeded)
            {
                Attempt.errors.Add(new ErrorRecord { seconds = Time.realtimeSinceStartupAsDouble - started,
                    action = request.ToString(), target = lastTarget ?? request.ToString(), reason = result.Failure.ToString() });
                Attempt.errorCount = Attempt.errors.Count;
                Message = TrainerText.Failure(result.Failure, request.Operation);
            }
            else if (request.Operation == Operation.Inspect) Message = TrainerText.Information((InfoTarget)request.Index);
            else Message = "Действие выполнено.";
            Refresh();
            Save();
        }

        private void Refresh()
        {
            if (Attempt == null || Procedure == null) return;
            if (!Completed && Procedure.Complete)
            {
                Attempt.durationSeconds = Time.realtimeSinceStartupAsDouble - started;
                Attempt.status = "completed";
                Message = $"Сценарий завершён. Ошибок: {Attempt.errorCount}.";
                Save();
            }
            Refreshed?.Invoke();
        }

        private void Save()
        {
            if (Attempt == null) return;
            if (Attempt.status != "completed" || Attempt.durationSeconds == 0)
                Attempt.durationSeconds = Time.realtimeSinceStartupAsDouble - started;
            Attempt.completedRequirements.Clear();
            foreach (var step in Scenario.Steps) if (Procedure.Satisfied(step.requirement)) Attempt.completedRequirements.Add(step.requirement.ToString());
            try { store.Save(Attempt); saveFailed = false; }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Message = "Не удалось сохранить результат. Проверьте доступ к папке результатов.";
                if (!saveFailed) Debug.LogError($"Attempt save failed: {e.Message}", this);
                saveFailed = true;
                Refreshed?.Invoke();
            }
        }

        private void EndAttempt()
        {
            if (Attempt == null) return;
            if (!Completed) Attempt.status = "interrupted";
            Save();
        }
        private void OnApplicationPause(bool paused) { if (paused) Save(); }
        private void OnDestroy()
        {
            EndAttempt();
            if (Model != null) Model.Attempted -= OnAttempt;
            if (Procedure != null) { Procedure.Updated -= Refresh; Procedure.Dispose(); }
        }
    }
}
