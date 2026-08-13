using UnityEngine;

namespace VRGame.Combat
{
    public class TestDummy : MonoBehaviour, IDamageable
    {
        public void ApplyDamage(DamageInfo info)
        {
            Debug.Log($"[TestDummy] Hit for {info.amount} dmg | vel: {info.impactVelocity:F2} | source: {info.sourceType} | zone: {info.hitZone}", this);
        }
    }
}