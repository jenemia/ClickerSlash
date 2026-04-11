using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 레인 이동 상태를 RobotKyle 애니메이터 파라미터로 변환합니다.
    /// </summary>
    public sealed class BattleRobotKyleAnimatorDriver : MonoBehaviour
    {
        private const float IdleSpeed = 0f;
        private const float MoveSpeed = 6f;
        private const float IdleYaw = 0f;
        private const float LeftYaw = -90f;
        private const float RightYaw = 90f;

        [SerializeField] [Min(0.01f)] private float parameterLerpSpeed = 12f;
        [SerializeField] [Min(0.01f)] private float rotationLerpSpeed = 14f;

        private Animator _animator;
        private int _speedHash;
        private int _motionSpeedHash;
        private int _groundedHash;
        private int _jumpHash;
        private int _freeFallHash;
        private float _currentSpeed;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            _speedHash = Animator.StringToHash("Speed");
            _motionSpeedHash = Animator.StringToHash("MotionSpeed");
            _groundedHash = Animator.StringToHash("Grounded");
            _jumpHash = Animator.StringToHash("Jump");
            _freeFallHash = Animator.StringToHash("FreeFall");
            ApplyPresentationState(false, 0f);
        }

        /// <summary>
        /// 현재 레인 이동 상태를 idle/walk-run 블렌드와 방향 회전으로 반영합니다.
        /// </summary>
        public void ApplyPresentationState(bool isMoving, float directionSign)
        {
            if (_animator == null)
            {
                return;
            }

            _animator.SetBool(_groundedHash, true);
            _animator.SetBool(_jumpHash, false);
            _animator.SetBool(_freeFallHash, false);

            var targetSpeed = isMoving ? MoveSpeed : IdleSpeed;
            _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed, Time.deltaTime * parameterLerpSpeed);
            if (_currentSpeed < 0.01f)
            {
                _currentSpeed = 0f;
            }

            _animator.SetFloat(_speedHash, _currentSpeed);
            _animator.SetFloat(_motionSpeedHash, isMoving ? 1f : 0f);

            var targetYaw = IdleYaw;
            if (isMoving)
            {
                targetYaw = directionSign < 0f ? LeftYaw : RightYaw;
            }

            var targetRotation = Quaternion.Euler(0f, targetYaw, 0f);
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation,
                targetRotation,
                Time.deltaTime * rotationLerpSpeed);
        }

        // RobotKyle animation clips still emit Starter Assets footstep/landing events.
        // In the battle scene we do not use those sounds, but we keep empty receivers
        // so the events do not throw warnings or reach removed controller logic.
        private void OnFootstep(AnimationEvent animationEvent)
        {
        }

        private void OnLand(AnimationEvent animationEvent)
        {
        }
    }
}
