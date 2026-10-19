using System;
using System.IO;
using System.Linq;
using ConveyorTrainer.Domain;
using ConveyorTrainer.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConveyorTrainer.Tests
{
    public sealed class ProjectContractTests
    {
        [Test] public void SavedSceneHasAllRequiredReferencesAndPrefabInstances()
        {
            var scene = EditorSceneManager.OpenScene(MvpBuilder.ScenePath);
            var components = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            Assert.That(components.All(c => c != null), Is.True, "Missing script in saved scene.");
            Assert.That(components.OfType<TrainingStation>().Count(), Is.EqualTo(1));
            Assert.That(components.OfType<ServiceItem>().Count(), Is.EqualTo(6));
            Assert.That(components.OfType<MaintenanceTarget>().Count(), Is.EqualTo(13));
            foreach (var component in components)
            {
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.NextVisible(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference)
                        Assert.That(property.objectReferenceValue != null || property.objectReferenceInstanceIDValue == 0, Is.True, component.name + ": " + property.propertyPath);
            }
            foreach (var item in components.OfType<ServiceItem>())
            {
                Assert.That(item.Bench, Is.Not.Null);
                Assert.That(PrefabUtility.IsPartOfPrefabInstance(item), Is.True);
            }
            Assert.That(PrefabUtility.IsPartOfPrefabInstance(components.OfType<TrainingPanel>().Single()), Is.True);
        }
        [Test] public void FiveScenarioAssetsHaveUniqueIdsAndMeaningfulRequirements()
        {
            var definitions = AssetDatabase.FindAssets("t:ScenarioDefinition", new[] { "Assets/ConveyorTrainer/Generated/Scenarios" })
                .Select(g => AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            Assert.That(definitions.Length, Is.EqualTo(5));
            Assert.That(definitions.Select(d => d.Id).Distinct().Count(), Is.EqualTo(5));
            foreach (var definition in definitions)
            {
                Assert.That(definition.Steps.All(s => !string.IsNullOrWhiteSpace(s.instruction)), Is.True);
                Assert.That(definition.Requirements.Distinct().Count(), Is.EqualTo(definition.Steps.Length));
                if (definition.Fault != Fault.None) Assert.That(definition.Requirements, Does.Contain(Requirement.SortingVerified));
            }
            Assert.That(definitions.Count(d => !d.Guided), Is.EqualTo(2));
        }
        [Test] public void JsonRoundTripPreservesErrorsAndUpdatesSameAttempt()
        {
            var directory = Path.Combine(Path.GetTempPath(), "ConveyorTrainerTests", Guid.NewGuid().ToString("N"));
            var store = new JsonAttemptStore(directory);
            var attempt = new AttemptRecord { attemptId = Guid.NewGuid().ToString("N"), scenarioId = "02-sensor-guided", startedUtc = DateTime.UtcNow.ToString("O") };
            try
            {
                store.Save(attempt);
                attempt.errors.Add(new ErrorRecord { seconds = 1.2, action = "Vent", target = "vent", reason = "AirSupplyOpen" });
                attempt.errorCount = 1; attempt.status = "completed"; store.Save(attempt);
                Assert.That(Directory.GetFiles(directory, "*.json").Length, Is.EqualTo(1));
                var restored = JsonUtility.FromJson<AttemptRecord>(File.ReadAllText(Path.Combine(directory, attempt.attemptId + ".json")));
                Assert.That(restored.errors.Single().reason, Is.EqualTo("AirSupplyOpen"));
                Assert.That(restored.errorCount, Is.EqualTo(1));
                Assert.That(restored.status, Is.EqualTo("completed"));
            }
            finally { if (Directory.Exists(directory)) { foreach (var file in Directory.GetFiles(directory)) File.Delete(file); Directory.Delete(directory); } }
        }
        [Test] public void StoreRejectsInvalidAttemptPath()
        {
            var store = new JsonAttemptStore(Path.GetTempPath());
            Assert.Throws<ArgumentException>(() => store.Save(new AttemptRecord { attemptId = "../outside" }));
        }
    }
}
