using System.Collections.Generic;
using UnityEngine;

namespace ScaryMonkey.Utility
{
    public class SimpleStateMachine
    {
        #region Structs
        private struct StateActions
        {
            public EnterExitAction OnEnter;
            public UpdateAction OnUpdate;
            public EnterExitAction OnExit;
        }
        #endregion

        #region Fields

        public delegate void EnterExitAction(ushort previousState, ushort newState);
        public delegate void UpdateAction();

        protected ushort _currentState = 0;

        private readonly Dictionary<ushort, StateActions> _statesToActions = new Dictionary<ushort, StateActions>();

        #endregion

        #region Properties

        public ushort CurrentState => _currentState;

        #endregion

        public SimpleStateMachine()
        {
            _statesToActions.Clear();
        }

        #region Interfaces

        public void AddState(ushort state, EnterExitAction onEnter, UpdateAction onUpdate, EnterExitAction onExit)
        {
            StateActions actions = new StateActions
            {
                OnEnter = onEnter,
                OnUpdate = onUpdate,
                OnExit = onExit
            };
            _statesToActions[state] = actions;
        }

        public virtual void InitializeWithState(ushort initialState)
        {
            _currentState = initialState;
            if (_statesToActions.ContainsKey(_currentState))
            {
                _statesToActions[_currentState].OnEnter?.Invoke(0, _currentState);
            }
            else
            {
                Debug.LogWarning($"StateMachine: Initialized with state {_currentState} which was not found.");
            }
        }

        public virtual void EnterState(ushort newState)
        {
            if (_statesToActions.ContainsKey(_currentState))
            {
                _statesToActions[_currentState].OnExit?.Invoke(_currentState, newState);
            }

            ushort previousState = _currentState;
            _currentState = newState;

            if (_statesToActions.ContainsKey(_currentState))
            {
                _statesToActions[_currentState].OnEnter?.Invoke(previousState, _currentState);
            }
            else
            {
                Debug.LogWarning($"StateMachine: Entered state {_currentState} which was not found.");
            }
        }

        public virtual void Update()
        {
            if (_statesToActions.ContainsKey(_currentState))
            {
                _statesToActions[_currentState].OnUpdate?.Invoke();
            }
        }

        public virtual void Dispose()
        {
            _statesToActions.Clear();
        }

        #endregion
    }
}
