using System;
using UnityEngine;
using Player = GorillaLocomotion.Player;

namespace ScaryMonkey.Enemy
{
    public class EnemyRadar : MonoBehaviour
    {
        public Action<Player> OnPlayerEnterRadar;

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.layer == LayerMask.NameToLayer("Gorilla Collider"))
            {
                Player enteredPlayer = other.gameObject.GetComponentInParent<Player>();
                if (enteredPlayer != null)
                {
                    OnPlayerEnterRadar?.Invoke(enteredPlayer);
                }
            }
        }
    }
}
