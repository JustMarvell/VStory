using TMPro;
using UnityEngine;
using VRGame.Core;

namespace VRGame.Dialogue
{
    public class DialogueRunner : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI speakerText;
        [SerializeField] TextMeshProUGUI bodyText;
        [SerializeField] GameObject panelRoot;

        DialogueSequence current;
        int lineIndex;

        public void StartDialogue(DialogueSequence sequence)
        {
            current = sequence;
            lineIndex = 0;
            panelRoot.SetActive(true);
            ShowCurrentLine();
        }

        public void Next()
        {
            if (current == null) return;

            lineIndex++;
            if (lineIndex >= current.lines.Count)
            {
                EndDialogue();
                return;
            }
            ShowCurrentLine();
        }

        void ShowCurrentLine()
        {
            var line = current.lines[lineIndex];
            speakerText.text = line.speakerName;
            bodyText.text = line.text;
        }

        void EndDialogue()
        {
            panelRoot.SetActive(false);
            if (!string.IsNullOrEmpty(current.setFlagOnComplete))
                QuestManager.SetFlag(current.setFlagOnComplete);
            current = null;
        }
    }
}