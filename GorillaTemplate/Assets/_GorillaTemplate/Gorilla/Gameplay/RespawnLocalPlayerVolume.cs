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

        private void Awake()
        {
            // This component might be used on prefabs that are instantiated at runtime,
            // in which case we need to find the respawn point reference at runtime as well.
            // NOTE: This is not the most efficient way. Consider having a respawn point manager singleton and retreive info from there.
            if (respawnPoint == null)
            {
                var objectFound = GameObject.FindGameObjectWithTag("Respawn");
                if (objectFound != null)
                {
                    respawnPoint = objectFound.transform;
                }
            }
        }

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
