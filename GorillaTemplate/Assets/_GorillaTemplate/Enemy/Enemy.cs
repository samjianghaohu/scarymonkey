using Normal.Realtime;
using ScaryMonkey.Utility;
using System.Collections;
using UnityEngine;
using Player = GorillaLocomotion.Player;

namespace ScaryMonkey.Enemy
{
    [RequireComponent(typeof(EnemyDataSync))]
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

        [SerializeField]
        private RealtimeTransform realtimeTransform;

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

        private SynchronizedStateMachine _stateMachine = null;
        private EnemyDataSync _dataSync = null;
        private RealtimeView _realtimeView = null;

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
            if (!TryGetComponent<EnemyDataSync>(out _dataSync))
            {
                Debug.LogError($"Enemy '{name}' is missing required EnemyDataSync component.");
                return;
            }

            if (_dataSync != null)
            {
                _realtimeView = _dataSync.RealtimeView;

                // Initialize State Machine. All client needs to do this.
                _stateMachine = new SynchronizedStateMachine(_dataSync);
                _stateMachine.AddState((ushort)State.Idle, OnEnterIdle, OnUpdateIdle, null);
                _stateMachine.AddState((ushort)State.FoundTarget, OnEnterFoundTarget, OnUpdateFoundTarget, null);
                _stateMachine.AddState((ushort)State.ChaseTarget, OnEnterChaseTarget, OnUpdateChaseTarget, OnExitChaseTarget);
                _stateMachine.AddState((ushort)State.LostTarget, OnEnterLostPlayer, OnUpdateLostPlayer, OnExitLostPlayer);

                // Listen to synced value changed events so that we can update remote enemies.
                _dataSync.OnRadarDisabledChangedAction += OnRadarDisabledChanged;
            }

            if (radar != null)
            {
                radar.OnLocalPlayerEnterRadar += OnLocalPlayerEnteredRadar;
            }
        }

        private void Start()
        {
            idleStartPositionWS = transform.position;
            _stateMachine?.InitializeWithState((ushort)State.Idle);
        }

        private void Update()
        {
            EnforceOwner();

            _stateMachine?.Update();
        }

        private void OnDestroy()
        {
            if (radar != null)
            {
                radar.OnLocalPlayerEnterRadar -= OnLocalPlayerEnteredRadar;
            }

            if (_dataSync != null)
            {
                _dataSync.OnRadarDisabledChangedAction -= OnRadarDisabledChanged;
            }

            _stateMachine?.Dispose();
        }

        private void EnforceOwner()
        {
            if (_realtimeView.realtime == null || !_realtimeView.realtime.connected)
            {
                return;
            }

            if (_realtimeView != null && _realtimeView.isUnownedSelf)
            {
                // Enemy all starts unowned. Try have this client claim ownership and become its initial owner.
                ClaimOwnership();
            }
        }

        private void ClaimOwnership()
        {
            if (_realtimeView == null || _realtimeView.isOwnedLocallySelf)
            {
                return;
            }

            _realtimeView.RequestOwnership();
            _realtimeView.preventOwnershipTakeover = true;

            if (realtimeTransform != null)
            {
                // Also request ownership of the transform so that movement is synced properly.
                realtimeTransform.RequestOwnership();
            }

            // Re-enable takeover after a delay to allow target players to claim ownership.
            StartCoroutine(AllowTakeoverAfterDelay(1f));
        }

        private IEnumerator AllowTakeoverAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (_realtimeView != null)
            {
                _realtimeView.preventOwnershipTakeover = false;
            }
        }

        #endregion

        #region State Callbacks

        #region Idle

        private void OnEnterIdle(ushort previousState, ushort newState)
        {
            transform.position = idleStartPositionWS;

            SetRadarDisabled(disabled: false);
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
            // Disable rader so the enemy can focus on the spotted target.
            // TODO: Consider keeping rader active and allow choosing the closest target.
            SetRadarDisabled(disabled: true);

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

            // Re-enable radar to allow spotting new targets.
            SetRadarDisabled(disabled: false);
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
                transform.position = targetLastSpotPosition;
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

        public void SetRadarDisabled(bool disabled)
        {
            if (radar != null)
            {
                radar.gameObject.SetActive(!disabled);
            }

            if (_dataSync != null)
            {
                _dataSync.AuthoritySetRadarDisabled(disabled);
            }
        }

        private void OnRadarDisabledChanged(bool disabled)
        {
            if (radar != null)
            {
                radar.gameObject.SetActive(!disabled);
            }
        }

        private void OnLocalPlayerEnteredRadar(Player player)
        {
            if (_currentTarget == player)
            {
                return;
            }

            _currentTarget = player;

            // Enemy started chasing this client. Claim ownership so we are the source of truth.
            ClaimOwnership();
        }

        #endregion
    }
}
