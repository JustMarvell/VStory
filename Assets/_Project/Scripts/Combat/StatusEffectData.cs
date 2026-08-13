using UnityEngine;

namespace VRGame.Combat
{
    [CreateAssetMenu(menuName = "Combat/StatusEffectData")]
    public class StatusEffectData : ScriptableObject
    {
        public string effectId;
        public float duration;
    }
}