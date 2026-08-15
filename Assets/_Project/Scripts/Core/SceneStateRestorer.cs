using UnityEngine;
using VRGame.Combat;
using VRGame.Puzzle;

namespace VRGame.Core
{
    public static class SceneStateRestorer
    {
        public static void ApplyQuestFlagsToScene()
        {
            foreach (var encounter in Object.FindObjectsByType<EncounterManager>(FindObjectsSortMode.None))
                encounter.RestoreIfCleared();

            foreach (var socket in Object.FindObjectsByType<WardSocket>(FindObjectsSortMode.None))
                socket.RestoreIfSolved();
        }
    }
}