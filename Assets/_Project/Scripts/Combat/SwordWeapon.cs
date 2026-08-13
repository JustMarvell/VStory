using UnityEngine;

namespace VRGame.Combat
{
    public class SwordWeapon : MonoBehaviour
    {
        [SerializeField] Transform bladeTip;
        [SerializeField] LayerMask enemyLayerMask;
        [SerializeField] float minHitVelocity = 1.5f;
        [SerializeField] float baseDamage = 10f;

        Vector3 lastTipPos;
        Collider lastHitCollider;

        void Start() => lastTipPos = bladeTip.position;

        void FixedUpdate()
        {
            var currentTipPos = bladeTip.position;
            var tipVelocity = (currentTipPos - lastTipPos) / Time.fixedDeltaTime;

            if (tipVelocity.magnitude > minHitVelocity &&
                Physics.Linecast(lastTipPos, currentTipPos, out var hit, enemyLayerMask))
            {
                if (hit.collider != lastHitCollider)
                {
                    RegisterHit(hit, tipVelocity);
                    lastHitCollider = hit.collider;
                }
            }
            else if (tipVelocity.magnitude <= minHitVelocity)
            {
                lastHitCollider = null;
            }

            lastTipPos = currentTipPos;
        }

        void RegisterHit(RaycastHit hit, Vector3 velocity)
        {
            if (!hit.collider.TryGetComponent<IDamageable>(out var damageable)) return;

            damageable.ApplyDamage(new DamageInfo
            {
                amount = baseDamage,
                hitPoint = hit.point,
                hitDirection = velocity.normalized,
                impactVelocity = velocity.magnitude,
                sourceType = DamageSourceType.Melee,
                hitZone = HitZone.Torso,
                instigator = gameObject
            });
        }
    }
}