using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRGame.Core;
namespace VRGame.Puzzle
{
    public class WardSocket : MonoBehaviour
    {
        [SerializeField] string puzzleId;
        [SerializeField] string requiredDollId;
        [SerializeField] UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor socketInteractor;

        bool isFilled;

        void OnEnable() => socketInteractor.selectEntered.AddListener(OnSelectEntered);
        void OnDisable() => socketInteractor.selectEntered.RemoveListener(OnSelectEntered);

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (isFilled) return;
            if (!args.interactableObject.transform.TryGetComponent<WardDoll>(out var doll)) return;
            if (doll.dollId != requiredDollId) return;

            isFilled = true;
            PuzzleManager.Current.ReportSubStepComplete(puzzleId, requiredDollId);
        }

        public void RestoreIfSolved()
        {
            if (!QuestManager.IsFlagSet($"{puzzleId}_solved")) return;
            isFilled = true;
        }
    }
}