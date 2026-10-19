using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ConveyorTrainer
{
    [Serializable]
    public sealed class ErrorRecord
    {
        public double seconds;
        public string action;
        public string target;
        public string reason;
    }

    [Serializable]
    public sealed class AttemptRecord
    {
        public int schemaVersion = 1;
        public string attemptId;
        public string scenarioId;
        public string startedUtc;
        public double durationSeconds;
        public string status = "in_progress";
        public int errorCount;
        public List<string> completedRequirements = new List<string>();
        public List<ErrorRecord> errors = new List<ErrorRecord>();
    }

    public interface IAttemptStore { void Save(AttemptRecord attempt); }

    public sealed class JsonAttemptStore : IAttemptStore
    {
        public string DirectoryPath { get; }
        public JsonAttemptStore(string directoryPath) { DirectoryPath = directoryPath ?? throw new ArgumentNullException(nameof(directoryPath)); }
        public void Save(AttemptRecord attempt)
        {
            if (!Guid.TryParseExact(attempt.attemptId, "N", out _)) throw new ArgumentException("Invalid attempt identifier.", nameof(attempt));
            Directory.CreateDirectory(DirectoryPath);
            var path = Path.Combine(DirectoryPath, attempt.attemptId + ".json");
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(attempt, true), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
    }
}
