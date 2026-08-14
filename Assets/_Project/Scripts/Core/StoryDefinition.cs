using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Core
{
    [CreateAssetMenu(menuName = "Story/StoryDefinition")]
    public class StoryDefinition : ScriptableObject
    {
        public string storyId;
        public string displayName;
        public List<ChapterDefinition> chapters;
        public StoryDefinition unlockRequirement;
    }
}