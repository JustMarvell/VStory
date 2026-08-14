namespace VRGame.Core
{
    public static class StoryUnlockUtility
    {
        public static bool IsUnlocked(StoryDefinition story)
        {
            if (story.unlockRequirement == null) return true;
            return SaveManager.Current.storiesUnlocked.TryGetValue(story.unlockRequirement.storyId, out var done) && done;
        }
    }
}