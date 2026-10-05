namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Obstacle type identifiers.
    /// </summary>
    public enum ObstacleType
    {
        LowBarrier,         // Low barrier (jump to avoid)
        HighBarrier,        // High barrier (duck/slide to avoid)
        StopSign,           // Stop sign hazard
        TrafficLight,       // Traffic light crossing hazard
        FallingHazard,      // Overhead falling hazard
        BouncingBoulder     // Rolling/bouncing obstacle
    }
}
