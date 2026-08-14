using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Dialogue
{
    [CreateAssetMenu(menuName = "Dialogue/DialogueSequence")]
    public class DialogueSequence : ScriptableObject
    {
        public string sequenceId;
        public List<DialogueLine> lines;
        public string setFlagOnComplete;
    }
}