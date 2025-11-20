using Normal.Realtime;
using System;
using Unity.VisualScripting;

namespace ScaryMonkey.Utility
{
    /// <summary>
    /// A state machine that is synchronized across the network.
    /// Only runs on the authoritative client.
    /// </summary>
    public class SynchronizedStateMachine : SimpleStateMachine
    {
        private readonly ISynchronizedStateMachineDataSync _dataSync;

        public SynchronizedStateMachine(ISynchronizedStateMachineDataSync dataSync) : base()
        {
            _dataSync = dataSync;
            if (!_dataSync.IsUnityNull())
            {
                _dataSync.OnStateChangedOnNonAuthority += OnStateChangedOnNonAuthority;
            }
        }

        private bool RunStateLogic => !_dataSync.IsUnityNull() && _dataSync.RealtimeView.isOwnedLocallySelf;

        public override void InitializeWithState(ushort initialState)
        {
            if (!RunStateLogic)
            {
                return;
            }

            _dataSync.AuthoritySetState(initialState);
            base.InitializeWithState(initialState);
        }

        public override void EnterState(ushort newState)
        {
            if (!RunStateLogic)
            {
                return;
            }

            _dataSync.AuthoritySetState(newState);
            base.EnterState(newState);
        }

        public override void Update()
        {
            if (!RunStateLogic) return;
            base.Update();
        }

        public override void Dispose()
        {
            base.Dispose();

            if (!_dataSync.IsUnityNull())
            {
                _dataSync.OnStateChangedOnNonAuthority -= OnStateChangedOnNonAuthority;
            }
        }

        private void OnStateChangedOnNonAuthority(ushort newState)
        {
            if (RunStateLogic)
            {
                return;
            }

            // On non-authority clients, we just update state value.
            _currentState = newState;
        }
    }

    /// <summary>
    /// The realtime component that actually syncs the state machine data,
    /// since the state machine itself doesn't do that.
    /// </summary>
    public interface ISynchronizedStateMachineDataSync
    {
        Action<ushort> OnStateChangedOnNonAuthority { get; set; }

        RealtimeView RealtimeView { get; }

        void AuthoritySetState(ushort newState);
    }
}
