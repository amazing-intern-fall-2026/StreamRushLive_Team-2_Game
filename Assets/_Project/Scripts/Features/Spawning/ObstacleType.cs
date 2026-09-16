namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Định danh các loại chướng ngại vật (Obstacles) trong game.
    /// </summary>
    public enum ObstacleType
    {
        LowBarrier,         // Rào thấp (buộc nhảy né)
        HighBarrier,        // Xà cao (buộc cúi/trượt né)
        StopSign,           // Bảng dừng (buộc dừng hoặc dùng khiên phá)
        TrafficLight,       // Đèn đỏ kích hoạt xe cắt ngang
        FallingHazard,      // Vật rơi từ trên trời xuống
        BouncingBoulder     // Đá / Thùng lăn bập bênh
    }
}
