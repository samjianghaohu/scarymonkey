using UnityEngine;
using UnityEngine.InputSystem;

namespace Normal.XR {
    /// <summary>
    /// Provides basic character movement using the keyboard and mouse.
    /// For debugging/testing purposes only.
    /// </summary>
    public class XRKeyboardAndMouseMovement : MonoBehaviour {

        private const float MAX_HEAD_PITCH_ANGLE = 50f;

        [SerializeField]
        private Rigidbody _target;

        [SerializeField]
        private float _movementSpeed = 3f;

        [SerializeField]
        private float _jumpVelocity = 4f;

        [SerializeField]
        private float _turnSpeed = 35f;

        [SerializeField]
        private bool _holdLeftMouseToTurn = true;

        [SerializeField]
        private bool _holdLeftMouseToLookUpDown = true;

        private void Update() {
            var targetTransform = _target.transform;

            PollMouse(out var eulerDelta);

            if (eulerDelta != Vector3.zero) {
                // Calculate and apply pitch and yaw changes this frame.
                eulerDelta *= _turnSpeed * Time.deltaTime;
                targetTransform.rotation *= Quaternion.Euler(eulerDelta.x, eulerDelta.y, 0f);

                // Clamp pitch to avoid flipping over
                float currentPitch = targetTransform.eulerAngles.x;
                if (currentPitch > 180f) {
                    currentPitch -= 360f;
                }

                float clampedPitch = Mathf.Clamp(currentPitch, -MAX_HEAD_PITCH_ANGLE, MAX_HEAD_PITCH_ANGLE);

                // Reapply clamped pitch together with current yaw
                targetTransform.rotation = Quaternion.Euler(clampedPitch, targetTransform.eulerAngles.y, 0f /*No roll*/); 
            }

            PollKeyboard(out var localMovementDirection, out var jump);

            if (localMovementDirection != Vector3.zero) {
                // Adjust for current facing direction
                var globalMovementDirection = targetTransform.TransformDirection(localMovementDirection);

                // Move
                targetTransform.position += globalMovementDirection * (_movementSpeed * Time.deltaTime);
            }

            if (jump) {
                // Jump by setting the Rigidbody's velocity
                // Gravity is applied automatically by the Rigidbody
                _target.linearVelocity += Vector3.up * _jumpVelocity;
            }
        }

        private void PollKeyboard(out Vector3 movementDirection, out bool jump) {
            movementDirection = Vector3.zero;
            jump = false;

            var keyboard = Keyboard.current;
            if (keyboard == null) {
                return;
            }

            // Check WASD keys
            if (keyboard.wKey.isPressed) {
                movementDirection.z += 1f;
            }
            if (keyboard.sKey.isPressed) {
                movementDirection.z -= 1f;
            }
            if (keyboard.dKey.isPressed) {
                movementDirection.x += 1f;
            }
            if (keyboard.aKey.isPressed) {
                movementDirection.x -= 1f;
            }

            if (movementDirection != Vector3.zero) {
                movementDirection.Normalize();
            }

            // Check space bar
            jump = keyboard.spaceKey.wasPressedThisFrame;
        }

        private void PollMouse(out Vector3 eulerDelta) {
            eulerDelta = Vector3.zero;

            var mouse = Mouse.current;
            if (mouse == null) {
                return;
            }

            if (mouse.leftButton.isPressed || !_holdLeftMouseToTurn)
            {
                eulerDelta.y = mouse.delta.x.value;
            }

            if (mouse.leftButton.isPressed || !_holdLeftMouseToLookUpDown)
            {
                eulerDelta.x = -mouse.delta.y.value;
            }
        }
    }
}
