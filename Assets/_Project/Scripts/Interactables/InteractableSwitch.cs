using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using VRGame.Core;
using System.Collections;

namespace VRGame.Interactables
{
    public enum SwitchMode { Button, Toggle }

    [RequireComponent(typeof(XRSimpleInteractable))]
    public class InteractableSwitch : MonoBehaviour
    {
        [SerializeField] SwitchMode mode = SwitchMode.Button;
        [SerializeField] string requiredFlag;

        [SerializeField] UnityEvent onActivated;
        [SerializeField] UnityEvent onToggleOn;
        [SerializeField] UnityEvent onToggleOff;
        [SerializeField] UnityEvent onLockedAttempt;

        [Header("Visual Feedback")]
        [SerializeField] Transform visualModel;
        [SerializeField] Transform pressedPose;
        [SerializeField] Vector3 autoPressOffset = new Vector3(0f, -0.02f, 0f);
        [SerializeField] float moveSpeed = 10f;
        [SerializeField] float buttonReturnDelay = 0.15f;

        Vector3 initialLocalPos;
        Quaternion initialLocalRot;
        Vector3 targetLocalPos;
        Quaternion targetLocalRot;
        bool visualPressed;

        XRSimpleInteractable interactable;
        bool isOn;

        public bool IsOn => isOn;

        void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();

            if (visualModel != null)
            {
                initialLocalPos = visualModel.localPosition;
                initialLocalRot = visualModel.localRotation;

                if (pressedPose != null)
                {
                    targetLocalPos = pressedPose.localPosition;
                    targetLocalRot = pressedPose.localRotation;
                }
                else
                {
                    targetLocalPos = initialLocalPos + autoPressOffset;
                    targetLocalRot = initialLocalRot;
                }
            }
        }

        void Update()
        {
            if (visualModel == null) return;

            var pos = visualPressed ? targetLocalPos : initialLocalPos;
            var rot = visualPressed ? targetLocalRot : initialLocalRot;

            visualModel.localPosition = Vector3.Lerp(visualModel.localPosition, pos, Time.deltaTime * moveSpeed);
            visualModel.localRotation = Quaternion.Slerp(visualModel.localRotation, rot, Time.deltaTime * moveSpeed);
        }

        void OnEnable() => interactable.selectEntered.AddListener(OnSelectEntered);
        void OnDisable() => interactable.selectEntered.RemoveListener(OnSelectEntered);

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (!string.IsNullOrEmpty(requiredFlag) && !QuestManager.IsFlagSet(requiredFlag))
            {
                onLockedAttempt.Invoke();
                return;
            }

            onActivated.Invoke();

            if (mode == SwitchMode.Toggle)
            {
                isOn = !isOn;
                (isOn ? onToggleOn : onToggleOff).Invoke();
            }

            if (mode == SwitchMode.Toggle)
                visualPressed = isOn;
            else
                StartCoroutine(ButtonPulse());
        }

        IEnumerator ButtonPulse()
        {
            visualPressed = true;
            yield return new WaitForSeconds(buttonReturnDelay);
            visualPressed = false;
        }
    }
}