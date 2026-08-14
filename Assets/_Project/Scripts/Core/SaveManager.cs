using UnityEngine;

namespace VRGame.Core
{
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }
        public static SaveData Current { get; private set; } = new SaveData();

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

        public static void SaveToDisk()
        {
            var wrapper = new SaveDataWrapper();
            foreach (var kv in Current.questFlags) { wrapper.flagKeys.Add(kv.Key); wrapper.flagValues.Add(kv.Value); }
            foreach (var kv in Current.storiesUnlocked) { wrapper.storyKeys.Add(kv.Key); wrapper.storyValues.Add(kv.Value); }
            foreach (var kv in Current.lastCheckpointPerStory) { wrapper.checkpointStoryKeys.Add(kv.Key); wrapper.checkpointChapterValues.Add(kv.Value); }

            var json = JsonUtility.ToJson(wrapper);
            System.IO.File.WriteAllText(Application.persistentDataPath + "/save.json", json);
        }

        public static void LoadFromDisk()
        {
            var path = Application.persistentDataPath + "/save.json";
            if (!System.IO.File.Exists(path)) return;

            var wrapper = JsonUtility.FromJson<SaveDataWrapper>(System.IO.File.ReadAllText(path));
            Current = new SaveData();
            for (int i = 0; i < wrapper.flagKeys.Count; i++) Current.questFlags[wrapper.flagKeys[i]] = wrapper.flagValues[i];
            for (int i = 0; i < wrapper.storyKeys.Count; i++) Current.storiesUnlocked[wrapper.storyKeys[i]] = wrapper.storyValues[i];
            for (int i = 0; i < wrapper.checkpointStoryKeys.Count; i++) Current.lastCheckpointPerStory[wrapper.checkpointStoryKeys[i]] = wrapper.checkpointChapterValues[i];
        }
    }
}