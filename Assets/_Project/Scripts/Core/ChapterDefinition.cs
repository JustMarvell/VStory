using UnityEngine;

namespace VRGame.Core
{
    [CreateAssetMenu(menuName = "Story/ChapterDefinition")]
    public class ChapterDefinition : ScriptableObject
    {
        public string chapterId;
        public string storyId;
        public Vector3 checkpointSpawnPos;
        public Quaternion checkpointSpawnRot;
    }
}