using UnityEngine;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Barrier obstacle (LowBarrier or HighBarrier):
    /// - Inherits from ObstacleBase.
    /// - Allows configuring specific collision parameters on each prefab in the Inspector.
    /// </summary>
    public class BarrierObstacle : ObstacleBase
    {
        [Header("Barrier Settings")]
        [Tooltip("Apply physics impulse to barrier upon player hit.")]
        [SerializeField] private bool applyImpactPhysics = false;

        public override void OnHitPlayer(GameObject player)
        {
            if (applyImpactPhysics && TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = false;
                rb.AddForce((Vector3.back + Vector3.up) * 5f, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * 10f, ForceMode.Impulse);
            }
        }
    }
}
