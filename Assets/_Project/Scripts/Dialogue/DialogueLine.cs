using System;
using UnityEngine;

namespace VRGame.Dialogue
{
    [Serializable]
    public struct DialogueLine
    {
        public string speakerName;
        [TextArea] public string text;
    }
}