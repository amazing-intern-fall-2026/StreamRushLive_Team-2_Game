namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Định danh các loại chướng ngại vật (Obstacles) trong game.
    /// </summary>
    public enum ObstacleType
    {
        LowBarrier = 0,         // Rào thấp (buộc nhảy né)
        HighBarrier = 1,        // Xà cao (buộc cúi/trượt né)
        StopSign = 2,           // Bảng dừng (buộc dừng hoặc dùng khiên phá)
        [System.Obsolete("Đã loại bỏ obstacle đèn giao thông")]
        TrafficLight = 3,       // Đã loại bỏ
        FallingHazard = 4,      // Vật rơi từ trên trời xuống
        BouncingBoulder = 5     // Đá / Thùng lăn bập bênh
    }
}
