using UnityEngine;

namespace VRGame.Combat
{
    public enum EnemyState { Dormant, Idle, Approach, Telegraph, AttackActive, Recover, HitReact, Dead }
    public enum HitTier { Light, Heavy, Parried, Killing }

    public class EnemyController : MonoBehaviour, IDamageable
    {
        [Header("References")]
        [SerializeField] Transform target;
        [SerializeField] Renderer bodyRenderer;

        [Header("Movement")]
        [SerializeField] float approachSpeed = 1.5f;
        [SerializeField] float attackRange = 1.5f;

        [Header("Attack Timing")]
        [SerializeField] float windupDuration = 0.5f;
        [SerializeField] float attackActiveDuration = 0.3f;
        [SerializeField] float recoverDuration = 0.8f;

        [Header("Health")]
        [SerializeField] float maxHP = 30f;
        [SerializeField] float heavyHitVelocityThreshold = 4f;

        public event System.Action<EnemyController> OnDeath;

        EnemyState currentState = EnemyState.Dormant;
        float stateTimer;
        float currentHP;

        static readonly Color[] StateColors =
        {
            Color.gray, Color.white, Color.cyan, Color.yellow, Color.red, Color.blue, Color.magenta, Color.black
        };

        void Awake() => currentHP = maxHP;

        public void Activate()
        {
            if (currentState == EnemyState.Dormant)
                SetState(EnemyState.Idle);
        }

        void Update()
        {
            if (currentState is EnemyState.Dormant or EnemyState.Dead) return;

            stateTimer -= Time.deltaTime;

            switch (currentState)
            {
                case EnemyState.Idle:
                case EnemyState.Approach:
                    HandleApproach();
                    break;
                case EnemyState.Telegraph:
                    if (stateTimer <= 0f) SetState(EnemyState.AttackActive);
                    break;
                case EnemyState.AttackActive:
                    if (stateTimer <= 0f) SetState(EnemyState.Recover);
                    break;
                case EnemyState.Recover:
                    if (stateTimer <= 0f) SetState(EnemyState.Approach);
                    break;
                case EnemyState.HitReact:
                    if (stateTimer <= 0f) SetState(EnemyState.Approach);
                    break;
            }
        }

        void HandleApproach()
        {
            if (target == null) return;
            var distance = Vector3.Distance(transform.position, target.position);

            if (distance <= attackRange)
            {
                SetState(EnemyState.Telegraph);
                return;
            }

            if (currentState != EnemyState.Approach) SetState(EnemyState.Approach);
            transform.position = Vector3.MoveTowards(transform.position, target.position, approachSpeed * Time.deltaTime);
        }

        public void ApplyDamage(DamageInfo info)
        {
            if (currentState == EnemyState.Dead) return;

            currentHP -= info.amount;
            var tier = info.impactVelocity >= heavyHitVelocityThreshold ? HitTier.Heavy : HitTier.Light;

            Debug.Log($"[EnemyController] Took {info.amount} dmg ({tier}), HP: {currentHP}/{maxHP}", this);

            if (currentHP <= 0f)
            {
                SetState(EnemyState.Dead);
                OnDeath?.Invoke(this);
                return;
            }

            if (tier == HitTier.Heavy)
                SetState(EnemyState.HitReact);
        }

        void SetState(EnemyState newState)
        {
            currentState = newState;
            stateTimer = newState switch
            {
                EnemyState.Telegraph => windupDuration,
                EnemyState.AttackActive => attackActiveDuration,
                EnemyState.Recover => recoverDuration,
                EnemyState.HitReact => 0.3f,
                _ => 0f
            };

            if (bodyRenderer != null)
                bodyRenderer.material.color = StateColors[(int)newState];
        }
    }
}