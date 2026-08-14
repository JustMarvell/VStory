using UnityEngine;

namespace VRGame.Core
{
    public class CheckpointManager : MonoBehaviour
    {
        [SerializeField] Transform xrOriginRoot;

        public void SetCheckpoint(ChapterDefinition chapter)
        {
            SaveManager.Current.lastCheckpointPerStory[chapter.storyId] = chapter.chapterId;
            SaveManager.SaveToDisk();
        }

        public void RespawnAtCheckpoint(ChapterDefinition chapter)
        {
            xrOriginRoot.SetPositionAndRotation(chapter.checkpointSpawnPos, chapter.checkpointSpawnRot);
        }
    }
}