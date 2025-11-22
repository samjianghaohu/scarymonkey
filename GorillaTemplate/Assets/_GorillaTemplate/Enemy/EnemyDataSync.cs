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
    }

    public partial class EnemyDataSync : RealtimeComponent<EnemyDataSyncModel>, ISynchronizedStateMachineDataSync
    {
        public Action<bool> OnRadarDisabledChangedAction;
        public Action<bool> OnLightEnabledChangedAction;

        protected override void OnRealtimeModelReplaced(EnemyDataSyncModel previousModel, EnemyDataSyncModel currentModel)
        {
            if (previousModel != null)
            {
                previousModel.currentStateDidChange  -= OnCurrentStateChanged;
                previousModel.radarDisabledDidChange -= OnRadarDisabledChanged;
                previousModel.lightEnabledDidChange  -= OnLightEnabledChanged;
            }

            if (currentModel != null)
            {
                currentModel.currentStateDidChange += OnCurrentStateChanged;
                currentModel.radarDisabledDidChange += OnRadarDisabledChanged;
                currentModel.lightEnabledDidChange += OnLightEnabledChanged;

                if (model.isFreshModel)
                {
                    // Locally created this model, give default values
                    currentModel.currentState = 0;
                    currentModel.radarDisabled = false;
                    currentModel.lightEnabled = false;
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

        #endregion
    }
}
