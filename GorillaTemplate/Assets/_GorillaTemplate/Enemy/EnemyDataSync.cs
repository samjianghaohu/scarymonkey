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
    }

    public partial class EnemyDataSync : RealtimeComponent<EnemyDataSyncModel>, ISynchronizedStateMachineDataSync
    {
        protected override void OnRealtimeModelReplaced(EnemyDataSyncModel previousModel, EnemyDataSyncModel currentModel)
        {
            if (previousModel != null)
            {
                previousModel.currentStateDidChange  -= OnCurrentStateChanged;
            }

            if (currentModel != null)
            {
                currentModel.currentStateDidChange += OnCurrentStateChanged;

                if (model.isFreshModel)
                {
                    // Locally created this model, give default values
                    currentModel.currentState = 0;
                }
            }
        }

        #region ISynchronizedStateMachineDataSync Implementation

        public Action<ushort> OnStateChangedOnNonAuthority { get; set; }

        public RealtimeView RealtimeView => realtimeView;

        public void AuthoritySetState(ushort newState)
        {
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
            if (realtimeView.isOwnedLocallySelf) return;

            OnStateChangedOnNonAuthority?.Invoke(value);
        }

        #endregion
    }
}
