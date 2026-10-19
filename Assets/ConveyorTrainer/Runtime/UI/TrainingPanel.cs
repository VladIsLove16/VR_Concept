using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace ConveyorTrainer
{
    public sealed class TrainingPanel : MonoBehaviour
    {
        [SerializeField] private Text title;
        [SerializeField] private Text task;
        [SerializeField] private Text feedback;
        [SerializeField] private Text equipment;
        [SerializeField] private Text hover;
        [SerializeField] private Button next;
        [SerializeField] private Button restart;
        [SerializeField] private MaintenanceTarget[] targets;
        private TrainingStation station;
        public Button NextButton => next;
        public void Configure(Text titleText, Text taskText, Text feedbackText, Text equipmentText, Text hoverText, Button nextButton, Button restartButton)
        { title = titleText; task = taskText; feedback = feedbackText; equipment = equipmentText; hover = hoverText; next = nextButton; restart = restartButton; }
        public void SetTargets(MaintenanceTarget[] sceneTargets) { targets = sceneTargets; }
        public void Initialize(TrainingStation owner)
        {
            station = owner;
            station.Refreshed += Refresh;
            next.onClick.AddListener(station.Next);
            restart.onClick.AddListener(station.Restart);
        }
        private void Refresh()
        {
            if (station.Attempt == null) return;
            title.text = $"{station.ScenarioIndex + 1}/5  {station.Scenario.Title}";
            var text = new StringBuilder();
            string suggestedId = null;
            if (station.Completed) text.Append($"СЦЕНАРИЙ ЗАВЕРШЁН\n\nОшибок: {station.Attempt.errorCount}\n\n")
                .Append(station.ScenarioIndex + 1 == station.ScenarioCount ? "Все пять сценариев завершены." : "Переходите к следующему сценарию.");
            else if (!station.Scenario.Guided) text.Append(station.Scenario.Briefing).Append("\n\nВыполните обслуживание самостоятельно.\nПошаговые подсказки отключены.");
            else
            {
                foreach (var step in station.Scenario.Steps)
                {
                    var done = station.Procedure.Satisfied(step.requirement);
                    if (!done && suggestedId == null) suggestedId = step.targetId;
                    text.Append(done ? "✓  " : "•  ").Append(step.instruction).Append('\n');
                }
            }
            task.text = text.ToString();
            feedback.text = station.Message;
            hover.text = station.HoverText;
            var model = station.Model;
            equipment.text = $"Линия: {(model.Running ? "работает" : "остановлена")}    Воздух: {(model.AirOpen ? "открыт" : "закрыт")}\nДавление: {(model.Pressure ? "есть" : "нет")}";
            next.interactable = station.Completed && station.ScenarioIndex + 1 < station.ScenarioCount;
            if (targets != null) foreach (var target in targets) if (target != null) target.Suggested = !station.Completed && station.Scenario.Guided && target.TargetId == suggestedId;
        }
        private void OnDestroy()
        {
            if (station == null) return;
            station.Refreshed -= Refresh;
            next.onClick.RemoveListener(station.Next);
            restart.onClick.RemoveListener(station.Restart);
        }
    }
}
