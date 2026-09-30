using System;
using System.Collections.Generic;

namespace SteamRush.Features.StreamIntegration
{
    // Danh sach nguoi da bam Follow trong phien live hien tai. Luu trong RAM, tat game la mat
    // (dung yeu cau giai doan thu nghiem). Khoa dinh danh la userId (nickname/uniqueId chi de hien thi).
    // Class thuan, khong MonoBehaviour. Chi goi tu main thread (TikTokLiveClient dung OnUnityThread).
    public class TikTokFollowerRegistry : IFollowerStatusProvider
    {
        private readonly HashSet<string> _followerIds = new HashSet<string>();

        // Ban sau khi them 1 follower MOI (khong ban lai neu nguoi do da co).
        public event Action<string> FollowerAdded;

        public int Count => _followerIds.Count;

        public bool IsFollower(string userId)
        {
            return !string.IsNullOrEmpty(userId) && _followerIds.Contains(userId);
        }

        // Tra ve true neu la follower moi. Phai goi ham nay TRUOC khi cho nguoi do vao hang doi,
        // vi FollowerGate.CanJoinQueue se kiem tra lai chinh danh sach nay.
        public bool AddFollower(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return false;
            }

            bool added = _followerIds.Add(userId);
            if (added)
            {
                FollowerAdded?.Invoke(userId);
            }

            return added;
        }

        public void Clear()
        {
            _followerIds.Clear();
        }

        // Dang ky lam nguon follower dung chung cho moi FollowerGate. Goi khi bat dau ket noi.
        public void Register()
        {
            FollowerGate.SetSharedProvider(this);
        }

        // Go dang ky khi tat ket noi/OnDisable, de quay ve che do mock (cho qua) khi test offline.
        public void Unregister()
        {
            FollowerGate.ClearSharedProvider(this);
        }
    }
}