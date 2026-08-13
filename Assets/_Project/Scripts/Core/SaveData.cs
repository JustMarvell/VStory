using System;
using System.Collections.Generic;

namespace VRGame.Core
{
    [Serializable]
    public class SaveData
    {
        public Dictionary<string, bool> questFlags = new();
        public Dictionary<string, bool> storiesUnlocked = new();
        public Dictionary<string, string> lastCheckpointPerStory = new();
        public Dictionary<string, int> inventoryCounts = new();
    }
}