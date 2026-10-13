namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Core obstacle classification types (Car, Animals, Trains).
    /// </summary>
    public enum ObstacleType
    {
        Car = 0,
        Animals = 1,
        Trains = 2,

        // --- Legacy v0.2 Compatibility ---
        [System.Obsolete("Legacy obstacle type. Use Car, Animals, or Trains.")]
        LowBarrier = 10,
        [System.Obsolete("Legacy obstacle type. Use Car, Animals, or Trains.")]
        HighBarrier = 11,
        [System.Obsolete("Legacy obstacle type.")]
        StopSign = 12,
        [System.Obsolete("Legacy obstacle type.")]
        TrafficLight = 13,
        [System.Obsolete("Legacy obstacle type.")]
        FallingHazard = 14,
        [System.Obsolete("Legacy obstacle type.")]
        BouncingBoulder = 15
    }

    /// <summary>
    /// Backward-compatibility alias enum mapping VehicleTier directly to ObstacleType.
    /// </summary>
    [System.Obsolete("VehicleTier is deprecated. Please use ObstacleType instead.")]
    public enum VehicleTier
    {
        SedanCar = ObstacleType.Car,
        PickupTruck = ObstacleType.Animals,
        HeavyTruck = ObstacleType.Trains
    }
}
