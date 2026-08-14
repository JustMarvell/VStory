using System.Collections.Generic;

namespace VRGame.Core
{
    [System.Serializable]
    public class SaveDataWrapper
    {
        public List<string> flagKeys = new();
        public List<bool> flagValues = new();
        public List<string> storyKeys = new();
        public List<bool> storyValues = new();
        public List<string> checkpointStoryKeys = new();
        public List<string> checkpointChapterValues = new();
    }
}