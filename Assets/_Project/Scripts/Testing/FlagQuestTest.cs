using UnityEngine;
using VRGame.Core;

namespace VRGame.Testing
{
    public class FlagQuestTest : MonoBehaviour
    {
        void Start()
        {
            QuestManager.SetFlag("test_flag");
            if (QuestManager.IsFlagSet("test_flag"))          
                Debug.Log("test_flag is set!");
        }
    }
}