using System;
using UnityEngine;
using Player = GorillaLocomotion.Player;

namespace ScaryMonkey.Gameplay
{
    public class RespawnLocalPlayerVolume : MonoBehaviour
    {
        [SerializeField]
        private Transform respawnPoint;

        public delegate bool ShouldLocalPlayerRespawn(Player localPlayer);
        public ShouldLocalPlayerRespawn ShouldLocalPlayerRespawnCondition = null;

        public Action<Player> OnLocalPlayerRespawned;

        private void OnTriggerEnter(Collider other)
        {
            if (respawnPoint == null)
            {
                Debug.LogWarning($"Respawn Point is not set on RespawnLocalPlayerVolume: {gameObject.name}.");
                return;
            }

            if (other.gameObject.layer == LayerMask.NameToLayer("Gorilla Collider"))
            {
                Player enteredPlayer = other.gameObject.GetComponentInParent<Player>();
                if (enteredPlayer == null)
                {
                    return;
                }

                if (ShouldLocalPlayerRespawnCondition != null && !ShouldLocalPlayerRespawnCondition(enteredPlayer))
                {
                    return;
                }

                enteredPlayer.TeleportHead(respawnPoint.position, respawnPoint.rotation);

                OnLocalPlayerRespawned?.Invoke(enteredPlayer);
            }
        }
    }
}
