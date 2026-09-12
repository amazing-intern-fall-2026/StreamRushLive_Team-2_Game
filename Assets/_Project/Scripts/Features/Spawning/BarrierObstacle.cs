using UnityEngine;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Chướng ngại vật dạng rào chắn (Rào thấp LowBarrier hoặc Xà cao HighBarrier):
    /// - Kế thừa ObstacleBase.
    /// - Cho phép tinh chỉnh thông số va chạm riêng trực tiếp trên Inspector của từng Prefab.
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
