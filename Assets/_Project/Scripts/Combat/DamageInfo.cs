using UnityEngine;

namespace VRGame.Combat
{
    public enum DamageSourceType { Melee, Thrown, Dart }
    public enum HitZone { Head, Torso, Limb }

    public struct DamageInfo
    {
        public float amount;
        public Vector3 hitPoint;
        public Vector3 hitDirection;
        public float impactVelocity;
        public DamageSourceType sourceType;
        public HitZone hitZone;
        public GameObject instigator;
        public StatusEffectData appliedEffect;
    }

    public interface IDamageable
    {
        void ApplyDamage(DamageInfo info);
    }
}