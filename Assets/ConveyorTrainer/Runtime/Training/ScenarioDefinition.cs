using System;
using ConveyorTrainer.Domain;
using UnityEngine;

namespace ConveyorTrainer
{
    [Serializable]
    public sealed class ScenarioStep
    {
        public Requirement requirement;
        [TextArea] public string instruction;
        public string targetId;
    }

    [CreateAssetMenu(menuName = "Conveyor Trainer/Scenario")]
    public sealed class ScenarioDefinition : ScriptableObject
    {
        [SerializeField] private string scenarioId;
        [SerializeField] private string title;
        [SerializeField, TextArea] private string briefing;
        [SerializeField] private Fault fault;
        [SerializeField] private bool guided;
        [SerializeField] private ScenarioStep[] steps;
        public string Id => scenarioId;
        public string Title => title;
        public string Briefing => briefing;
        public Fault Fault => fault;
        public bool Guided => guided;
        public ScenarioStep[] Steps => steps;
        public Requirement[] Requirements
        {
            get { var result = new Requirement[steps.Length]; for (var i = 0; i < result.Length; i++) result[i] = steps[i].requirement; return result; }
        }
        public void Configure(string id, string displayTitle, string description, Fault initialFault, bool showHints, ScenarioStep[] procedure)
        { scenarioId = id; title = displayTitle; briefing = description; fault = initialFault; guided = showHints; steps = procedure; }
    }
}
