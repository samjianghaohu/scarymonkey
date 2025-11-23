using Normal.Realtime;
using ScaryMonkey.Gameplay;
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

        [Header("Components")]
        [SerializeField]
        private EnemyRadar radar;

        [SerializeField]
        private GameObject enemyLight;

        [SerializeField]
        private RespawnLocalPlayerVolume hurtBox;

        [SerializeField]
        private AudioSource sfxPlayer;

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

        [Header("Audiovisual Effects")]
        [SerializeField]
        private AudioClip localAlertSFX;

        [SerializeField]
        private AudioClip allChaseSFX;

        private SynchronizedStateMachine _stateMachine = null;
        private EnemyDataSync _dataSync = null;
        private RealtimeView _realtimeView = null;

        private Player _currentTarget = null;

        private Vector3 defaultPositionWS;
        private Quaternion defaultRotationWS;
        private float foundTargetStartTime = 0f;

        private Vector3 targetLastSpotPosition;
        private bool reachedTargetLastSpotPosition = false;
        private float reachedTargetLastSpotTime = 0f;

        #endregion

        #region Properties

        private Vector3 CurrentTargetPosition => _currentTarget != null ? _currentTarget.headCollider.transform.position : transform.position; // If no target, return own position so that it wouldn't chase anywhere.

        private bool CanAttackPlayer => _stateMachine != null && _stateMachine.CurrentState != (ushort)State.Idle && _stateMachine.CurrentState != (ushort)State.FoundTarget;

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
                _dataSync.OnLightEnabledChangedAction += OnLightEnabledChanged;
                _dataSync.OnChaseSFXPlayingChangedAction += OnChaseSFXPlayingChanged;
            }

            if (radar != null)
            {
                radar.OnLocalPlayerEnterRadar += OnLocalPlayerEnteredRadar;
            }

            if (enemyLight != null)
            {
                enemyLight.SetActive(false);
            }

            if (hurtBox != null)
            {
                hurtBox.ShouldLocalPlayerRespawnCondition = ShouldRespawnLocalPlayer;
                hurtBox.OnLocalPlayerRespawned += OnRespawnedLocalPlayer;
            }
        }

        private void Start()
        {
            defaultPositionWS = transform.position;
            defaultRotationWS = transform.rotation;

            _stateMachine?.InitializeWithState((ushort)State.Idle);
        }

        private void Update()
        {
            // Enforce ownership in case the owner left.
            // Local client will try to claim ownership when that happens.
            EnforceOwner();

            _stateMachine?.Update();
        }

        private void OnDestroy()
        {
            if (radar != null)
            {
                radar.OnLocalPlayerEnterRadar -= OnLocalPlayerEnteredRadar;
            }

            if (hurtBox != null)
            {
                hurtBox.ShouldLocalPlayerRespawnCondition = null;
                hurtBox.OnLocalPlayerRespawned -= OnRespawnedLocalPlayer;
            }

            if (_dataSync != null)
            {
                _dataSync.OnRadarDisabledChangedAction -= OnRadarDisabledChanged;
                _dataSync.OnLightEnabledChangedAction -= OnLightEnabledChanged;
                _dataSync.OnChaseSFXPlayingChangedAction -= OnChaseSFXPlayingChanged;
            }

            _stateMachine?.Dispose();
        }

        private void EnforceOwner()
        {
            if (_realtimeView == null)
            {
                return;
            }

            if (_realtimeView.realtime == null || !_realtimeView.realtime.connected)
            {
                return;
            }

            if (_realtimeView.isUnownedSelf)
            {
                // Enemy all starts unowned. Try have this client claim ownership and become its initial owner.
                ClaimOwnership();
            }else if (_realtimeView.isOwnedLocallySelf && realtimeTransform != null && !realtimeTransform.isOwnedLocallySelf)
            {
                ClaimOwnershipForTransform();
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

            // Also request ownership of the transform so that movement is synced properly.
            ClaimOwnershipForTransform();

            // Re-enable takeover after a delay to allow target players to claim ownership.
            StartCoroutine(AllowTakeoverAfterDelay(1f));
        }

        private void ClaimOwnershipForTransform()
        {
            if (realtimeTransform != null)
            {
                // Also request ownership of the transform so that movement is synced properly.
                realtimeTransform.RequestOwnership();
            }
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
            SetRadarDisabled(disabled: false);
            SetLightEnabled(enable: false);

            SetChasingSFXPlaying(false);

            transform.SetPositionAndRotation(defaultPositionWS, defaultRotationWS);

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
            var newPosition =  defaultPositionWS + Vector3.up * (Mathf.Sin(Time.time * idleHoverFrequency) * idleHoverAmplitude);
            transform.position = newPosition;
        }

        #endregion

        #region Found Target

        private void OnEnterFoundTarget(ushort previousState, ushort newState)
        {
            foundTargetStartTime = Time.time;

            // Disable rader so the enemy can focus on the spotted target.
            // TODO: Consider keeping rader active and allow choosing the closest target.
            SetRadarDisabled(disabled: true);

            // Turn on light to make target aware of its presence.
            SetLightEnabled(enable: true);

            PlaySFX(localAlertSFX, volume: 0.8f, randomPitch: true);
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

            transform.LookAt(CurrentTargetPosition, Vector3.up);
        }

        #endregion

        #region Chase Target

        private void OnEnterChaseTarget(ushort previousState, ushort newState)
        {
            targetLastSpotPosition = CurrentTargetPosition;

            SetChasingSFXPlaying(true);
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
                var directionToTarget = (targetLastSpotPosition - transform.position).normalized;
                transform.position += chaseSpeed * Time.deltaTime * directionToTarget;
                transform.LookAt(targetLastSpotPosition, Vector3.up);
            }
        }

        private bool CheckIfPlayerInLineOfSight(out Vector3 lastSeenPosition)
        {
            var enemyToTarget = CurrentTargetPosition - transform.position;
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
                    lastSeenPosition = CurrentTargetPosition;
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
                transform.LookAt(targetLastSpotPosition, Vector3.up);

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

        private void SetRadarDisabled(bool disabled)
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

        private void SetLightEnabled(bool enable)
        {
            if (enemyLight != null)
            {
                enemyLight.SetActive(enable);
            }

            if (_dataSync!= null)
            {
                _dataSync.AuthoritySetLightEnabled(enable);
            }
        }

        private void OnRadarDisabledChanged(bool disabled)
        {
            if (radar != null)
            {
                radar.gameObject.SetActive(!disabled);
            }
        }

        private void OnLightEnabledChanged(bool enabled)
        {
            if (enemyLight != null)
            {
                enemyLight.SetActive(enabled);
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

        private bool ShouldRespawnLocalPlayer(Player localPlayer)
        {
            // Enemy should not respawn local player if not the states where attack is allowed.
            if (!CanAttackPlayer)
            {
                return false;
            }

            // Enemy should only respawn the local player if it's currently targeting them.
            if (_currentTarget != localPlayer)
            {
                return false;
            }

            // Make sure the local player this enemy is targetting is also the owner!
            return _dataSync != null &&  _dataSync.realtimeView.isOwnedLocallySelf;
        }

        private void OnRespawnedLocalPlayer(Player localPlayer)
        {
            // Force enemy to lose target state after touching and respawning the local player (i.e. its target).
            _currentTarget = null;
            _stateMachine.EnterState((ushort)State.LostTarget);
        }

        #endregion

        #region SFX

        private void SetChasingSFXPlaying(bool playing)
        {
            LocalPlayOrStopChasingSFX(playing);

            if (_dataSync != null)
            {
                _dataSync.AuthoritySetChaseSFXPlaying(playing);
            }
        }

        private void OnChaseSFXPlayingChanged(bool playing)
        {
            if (_dataSync == null || _dataSync.realtimeView.isOwnedLocallySelf)
            {
                // Local/Authority player already played the chase sfx before setting the synchronized value.
                return;
            }

            // Play or stop the SFX on remote client.
            LocalPlayOrStopChasingSFX(playing);
        }

        private void LocalPlayOrStopChasingSFX(bool play)
        {
            if (play)
            {
                PlaySFX(allChaseSFX, volume: 0.8f, loop: true);
            }
            else
            {
                StopSFX();
            }
        }

        private void PlaySFX(AudioClip clip, float volume = 1f, bool loop = false, bool randomPitch = false)
        {
            if (sfxPlayer == null || clip == null)
            {
                return;
            }

            float pitch = 1f;
            if (randomPitch)
            {
                pitch += Random.Range(-0.1f, 0.1f);
            }

            StopSFX();

            sfxPlayer.clip = clip;
            sfxPlayer.volume = volume;
            sfxPlayer.loop = loop;
            sfxPlayer.pitch = pitch;
            sfxPlayer.Play();
        }

        private void StopSFX()
        {
            if (sfxPlayer != null)
            {
                sfxPlayer.Stop();
            }
        }

        #endregion
    }
}
