namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Định danh các loại vật phẩm (Items) có thể nhặt được trong game.
    /// </summary>
    public enum ItemType
    {
        EnergyBuff,     // Vật phẩm hồi năng lượng
        Shield,         // Khiên chắn - chặn 1 lần va chạm trong tối đa 20 giây
        HighJump,       // Giày bật cao - tăng 40% lực nhảy trong 10 giây
        HyperDash       // Tên lửa vô địch - tốc độ 18 m/s và bất tử trong 5 giây
    }
}

