using ScaryMonkey.Utility;
using UnityEngine;
using Player = GorillaLocomotion.Player;

namespace ScaryMonkey.Enemy
{
    public class Enemy : MonoBehaviour
    {
        #region Enums
        private enum State : ushort
        {
            Idle,
            FoundTarget,
            ChaseTarget,
            LostTarget
        }
        #endregion

        #region Constants
        private const float DISTANCE_EPSILON = 0.1f;
        #endregion

        #region Fields

        [SerializeField]
        private EnemyRadar radar;

        [Header("State Tunings")]
        [SerializeField]
        private float idleHoverAmplitude = 0.5f;

        [SerializeField]
        private float idleHoverFrequency = 1f;

        [SerializeField]
        private float foundPlayerWaitDuration = 5f;

        [SerializeField]
        private float chaseSpeed = 3f;

        [SerializeField]
        private float lostTargetWaitDuration = 5f;

        private SimpleStateMachine _stateMachine = null;

        private Player _currentTarget = null;

        private Vector3 idleStartPositionWS;
        private float foundTargetStartTime = 0f;

        private Vector3 targetLastSpotPosition;
        private bool reachedTargetLastSpotPosition = false;
        private float reachedTargetLastSpotTime = 0f;

        #endregion

        #region Monobehavior

        private void Awake()
        {
            // Initialize State Machine
            _stateMachine = new SimpleStateMachine();
            _stateMachine.AddState((ushort)State.Idle, OnEnterIdle, OnUpdateIdle, null);
            _stateMachine.AddState((ushort)State.FoundTarget, OnEnterFoundTarget, OnUpdateFoundTarget, null);
            _stateMachine.AddState((ushort)State.ChaseTarget, OnEnterChaseTarget, OnUpdateChaseTarget, OnExitChaseTarget);
            _stateMachine.AddState((ushort)State.LostTarget, OnEnterLostPlayer, OnUpdateLostPlayer, OnExitLostPlayer);
           
            if (radar != null)
            {
                radar.OnPlayerEnterRadar += OnPlayerEnteredRadar;
            }
        }

        private void Start()
        {
            idleStartPositionWS = transform.position;
            _stateMachine.InitializeWithState((ushort)State.Idle);
        }

        private void Update()
        {
            _stateMachine?.Update();
        }

        private void OnDestroy()
        {
            if (radar != null)
            {
                radar.OnPlayerEnterRadar -= OnPlayerEnteredRadar;
            }
        }

        #endregion

        #region State Callbacks

        #region Idle

        private void OnEnterIdle(ushort previousState, ushort newState)
        {
            transform.position = idleStartPositionWS;
            // TODO: Add some one-time audiovisual effect for teleporting.
        }

        private void OnUpdateIdle()
        {
            // Check for target
            if (_currentTarget != null)
            {
                _stateMachine.EnterState((ushort)State.FoundTarget);
                return;
            }

            // Idle behavior: teleport back to starting position and then do sine wave hover
            // TODO: this could be expanded to some more interesting behaviors.
            var newPosition =  idleStartPositionWS + Vector3.up * (Mathf.Sin(Time.time * idleHoverFrequency) * idleHoverAmplitude);
            transform.position = newPosition;
        }

        #endregion

        #region Found Target

        private void OnEnterFoundTarget(ushort previousState, ushort newState)
        {
            foundTargetStartTime = Time.time;

            // TODO: Add some one-time audiovisual effect for spotting the player.
        }

        private void OnUpdateFoundTarget()
        {
            if (_currentTarget == null)
            {
                // Target player is destroyed before we could chase them.
                _stateMachine.EnterState((ushort)State.Idle);
                return;
            }

            if (Time.time - foundTargetStartTime > foundPlayerWaitDuration)
            {
                _stateMachine.EnterState((ushort)State.ChaseTarget);
                return;
            }
        }

        #endregion

        #region Chase Target

        private void OnEnterChaseTarget(ushort previousState, ushort newState)
        {
            targetLastSpotPosition = _currentTarget.transform.position;
        }

        private void OnUpdateChaseTarget()
        {
            if (_currentTarget == null)
            {
                // Target player is destroyed while we were chasing them.
                _stateMachine.EnterState((ushort)State.LostTarget);
                return;
            }

            // Do a raycast to see if enemy has line of sight to the player
            bool canSeeTarget = CheckIfPlayerInLineOfSight(out targetLastSpotPosition);
            if (!canSeeTarget)
            {
                // Lost sight of target.
                _stateMachine.EnterState((ushort)State.LostTarget);
                return;
            }
            else
            {
                // Move towards target
                var directionToTarget = (_currentTarget.transform.position - transform.position).normalized;
                transform.position += chaseSpeed * Time.deltaTime * directionToTarget;
            }

            // TODO: Add logic for catching the player.
        }

        private bool CheckIfPlayerInLineOfSight(out Vector3 lastSeenPosition)
        {
            var enemyToTarget = _currentTarget.transform.position - transform.position;
            var rayDirection = enemyToTarget.normalized;
            var rayMaxDistance = enemyToTarget.magnitude;

            // TODO: Incorporate a distance threshold. Enemy shouldn't infinite eye sight. 
            if (Physics.Raycast(
                origin: transform.position,
                rayDirection,
                out RaycastHit hitInfo,
                rayMaxDistance,
                layerMask: LayerMask.GetMask("Default", "Gorilla Collider"),
                queryTriggerInteraction: QueryTriggerInteraction.Ignore
                ))
            {
                // Hit a player
                var hitPlayer = hitInfo.collider.GetComponentInParent<Player>();
                if (hitPlayer != null && hitPlayer == _currentTarget)
                {
                    // Target is in line of sight. Update last seen position with target's current position.
                    lastSeenPosition = _currentTarget.transform.position;
                    return true;
                }
            }

            // In all other case, target is not in line of sight.
            // Keep last seen position as it was.
            lastSeenPosition = targetLastSpotPosition;
            return false;
        }

        private void OnExitChaseTarget(ushort previousState, ushort newState)
        {
            if (newState == (ushort)State.LostTarget)
            {
                _currentTarget = null;
            }
        }

        #endregion

        #region Lost Target

        private void OnEnterLostPlayer(ushort previousState, ushort newState)
        {
            // Ensure clean state when entering LostTarget state.
            reachedTargetLastSpotPosition = false;
        }

        private void OnUpdateLostPlayer()
        {
            if (_currentTarget != null)
            {
                // New target spotted.
                _stateMachine.EnterState((ushort)State.FoundTarget);
                return;
            }   

            if (reachedTargetLastSpotPosition)
            {
                if (Time.time - reachedTargetLastSpotTime > lostTargetWaitDuration)
                {
                    // Waited long enough at last seen position without spotting target again.
                    _stateMachine.EnterState((ushort)State.Idle);
                    return;
                }
            }
            else
            {
                // Move towards target's last seen position.
                var directionToLastSpot = (targetLastSpotPosition - transform.position).normalized;
                transform.position += chaseSpeed * Time.deltaTime * directionToLastSpot;

                if ((transform.position - targetLastSpotPosition).sqrMagnitude <= (DISTANCE_EPSILON * DISTANCE_EPSILON))
                {
                    reachedTargetLastSpotPosition = true;
                    reachedTargetLastSpotTime = Time.time;
                }
            }
        }

        private void OnExitLostPlayer(ushort previousState, ushort newState)
        {
            reachedTargetLastSpotPosition = false;
        }

        #endregion

        #endregion

        #region Player Detection

        private void OnPlayerEnteredRadar(Player player)
        {
            if (_stateMachine.CurrentState != (ushort)State.Idle && _stateMachine.CurrentState != (ushort)State.LostTarget)
            {
                return;
            }

            if (_currentTarget == player)
            {
                return;
            }

            _currentTarget = player;
        }

        #endregion
    }
}
