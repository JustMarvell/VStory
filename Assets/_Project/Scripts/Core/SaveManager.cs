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
            // JSON persistence comes later (Dictionary needs Newtonsoft or a wrapper - see docs/02)
        }

        public static void LoadFromDisk() { }
    }
}