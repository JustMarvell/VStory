using UnityEngine;
using UnityEngine.Events;

namespace VRGame.Core
{
    [RequireComponent(typeof(Collider))]
    public class AreaTrigger : MonoBehaviour
    {
        [SerializeField] string areaId;
        [SerializeField] UnityEvent onPlayerEnter;

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            onPlayerEnter?.Invoke();
        }
    }
}