using System.Collections.Generic;
using UnityEngine;
using VRGame.Core;

namespace VRGame.Puzzle
{
    public class PuzzleManager : MonoBehaviour
    {
        public static PuzzleManager Current { get; private set; }

        [SerializeField] List<PuzzleDefinitionEntry> puzzleDefinitions;

        readonly Dictionary<string, HashSet<string>> requirements = new();
        readonly Dictionary<string, HashSet<string>> progress = new();

        [System.Serializable]
        public class PuzzleDefinitionEntry
        {
            public string puzzleId;
            public List<string> requiredStepIds;
        }

        void Awake()
        {
            Current = this;
            foreach (var def in puzzleDefinitions)
            {
                requirements[def.puzzleId] = new HashSet<string>(def.requiredStepIds);
                progress[def.puzzleId] = new HashSet<string>();
            }
        }

        public void ReportSubStepComplete(string puzzleId, string stepId)
        {
            if (!progress.ContainsKey(puzzleId)) return;

            progress[puzzleId].Add(stepId);
            if (progress[puzzleId].SetEquals(requirements[puzzleId]))
                QuestManager.SetFlag($"{puzzleId}_solved");
        }
    }
}