using Normal.Realtime;
using ScaryMonkey.Utility;
using System;

namespace ScaryMonkey.Enemy
{
    [RealtimeModel]
    public partial class EnemyDataSyncModel
    {
        [RealtimeProperty(1, true, true)]
        private ushort _currentState;

        [RealtimeProperty(2, true, true)]
        private bool _radarDisabled;

        [RealtimeProperty(3, true, true)]
        private bool _lightEnabled;

        [RealtimeProperty(4, true, true)]
        private bool _chaseSFXPlaying;

        [RealtimeProperty(5, true, true)]
        private ushort _currentLightMode;
    }

    public partial class EnemyDataSync : RealtimeComponent<EnemyDataSyncModel>, ISynchronizedStateMachineDataSync
    {
        public Action<bool> OnRadarDisabledChangedAction;
        public Action<bool> OnLightEnabledChangedAction;
        public Action<bool> OnChaseSFXPlayingChangedAction;
        public Action<ushort> OnCurrentLightModeChangedAction;

        protected override void OnRealtimeModelReplaced(EnemyDataSyncModel previousModel, EnemyDataSyncModel currentModel)
        {
            if (previousModel != null)
            {
                previousModel.currentStateDidChange  -= OnCurrentStateChanged;
                previousModel.radarDisabledDidChange -= OnRadarDisabledChanged;
                previousModel.lightEnabledDidChange  -= OnLightEnabledChanged;
                previousModel.chaseSFXPlayingDidChange -= OnChaseSFXPlayingChanged;
                previousModel.currentLightModeDidChange -= OnCurrentLightModeChanged;
            }

            if (currentModel != null)
            {
                currentModel.currentStateDidChange += OnCurrentStateChanged;
                currentModel.radarDisabledDidChange += OnRadarDisabledChanged;
                currentModel.lightEnabledDidChange += OnLightEnabledChanged;
                currentModel.chaseSFXPlayingDidChange += OnChaseSFXPlayingChanged;
                currentModel.currentLightModeDidChange += OnCurrentLightModeChanged;

                if (model.isFreshModel)
                {
                    // Locally created this model, give default values
                    currentModel.currentState = 0;
                    currentModel.radarDisabled = false;
                    currentModel.lightEnabled = false;
                    currentModel.chaseSFXPlaying = false;
                    currentModel.currentLightMode = 0;
                }
            }
        }

        public void AuthoritySetRadarDisabled(bool disabled)
        {
            if (!realtimeView.isOwnedLocallySelf)
            {
                return;
            }

            if (model.radarDisabled == disabled)
            {
                return;
            }

            model.radarDisabled = disabled;
        }

        public void AuthoritySetLightEnabled(bool enabled)
        {
            if (!realtimeView.isOwnedLocallySelf)
            {
                return;
            }

            if (model.lightEnabled == enabled)
            {
                return;
            }

            model.lightEnabled = enabled;
        }

        public void AuthoritySetChaseSFXPlaying(bool playing)
        {
            if (!realtimeView.isOwnedLocallySelf)
            {
                return;
            }

            if (model.chaseSFXPlaying == playing)
            {
                return;
            }

            model.chaseSFXPlaying = playing;
        }

        public void AuthoritySetCurrentLightMode(ushort mode)
        {
            if (!realtimeView.isOwnedLocallySelf)
            {
                return;
            }

            if (model.currentLightMode == mode)
            {
                return;
            }

            model.currentLightMode = mode;
        }

        #region ISynchronizedStateMachineDataSync Implementation

        public Action<ushort> OnCurrentStateChangedAction { get; set; }

        public RealtimeView RealtimeView => realtimeView;

        public void AuthoritySetState(ushort newState)
        {
            if (!realtimeView.isOwnedLocallySelf)
            {
                return;
            }

            if (model.currentState == newState)
            {
                return;
            }

            model.currentState = newState;
        }

        #endregion

        #region Value Change Callbacks

        private void OnCurrentStateChanged(EnemyDataSyncModel model, ushort value)
        {
            OnCurrentStateChangedAction?.Invoke(value);
        }

        private void OnRadarDisabledChanged(EnemyDataSyncModel model, bool value)
        {
            OnRadarDisabledChangedAction?.Invoke(value);
        }

        private void OnLightEnabledChanged(EnemyDataSyncModel model, bool value)
        {
            OnLightEnabledChangedAction?.Invoke(value);
        }

        private void OnChaseSFXPlayingChanged(EnemyDataSyncModel model, bool value)
        {
            OnChaseSFXPlayingChangedAction?.Invoke(value);
        }

        private void OnCurrentLightModeChanged(EnemyDataSyncModel model, ushort value)
        {
            OnCurrentLightModeChangedAction?.Invoke(value);
        }

        #endregion
    }
}
