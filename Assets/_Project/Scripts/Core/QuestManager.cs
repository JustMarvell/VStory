using System;
using UnityEngine;

namespace VRGame.Core
{
    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }
        public static event Action<string> OnQuestFlagSet;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public static void SetFlag(string flagId)
        {
            if (IsFlagSet(flagId)) return;
            SaveManager.Current.questFlags[flagId] = true;
            OnQuestFlagSet?.Invoke(flagId);
            Debug.Log("Flag Is Set : " + flagId);
            SaveManager.SaveToDisk();
        }

        public static bool IsFlagSet(string flagId) =>
            SaveManager.Current.questFlags.TryGetValue(flagId, out var set) && set;
    }
}