using UnityEngine;
using SteamRush.Core;

namespace SteamRush.Features.StreamIntegration
{
    // StreamIntegration se cung cap trang thai Follow that qua interface nay (module chua ton
    // tai - chua co IStreamAdapter/TikTokLiveAdapter/MockStreamSimulator nao trong code).
    public interface IFollowerStatusProvider
    {
        bool IsFollower(string userId);
    }

    // Chan quyen vao hang doi / gui lenh chat - chi Follower moi duoc phep (GDD v1.3 muc 3).
    // Neu chua gan IFollowerStatusProvider that, mac dinh cho qua (mock) kem Debug.Log canh bao,
    // de test duoc trong scene ca nhan ma khong can cho module StreamIntegration.
    public class FollowerGate
    {
        private readonly IFollowerStatusProvider _followerStatusProvider;

        public FollowerGate(IFollowerStatusProvider followerStatusProvider = null)
        {
            _followerStatusProvider = followerStatusProvider;
        }

        public bool CanJoinQueue(string userId)
        {
            return IsFollower(userId);
        }

        // GDD v1.3 muc 3.2: nguoi chua Follow co gui lenh (left/right/fast/slow) -> tu dong bo qua,
        // kem "thong bao bot nhac nho" - publish qua EventBus de module chat that (chua ton tai)
        // tra loi comment nhac Follow.
        public bool CanSendCommand(string userId)
        {
            bool isFollower = IsFollower(userId);

            if (!isFollower)
            {
                Debug.Log($"[FollowerGate] {userId} chưa Follow - bỏ qua lệnh (bot nhắc nhở Follow để điều khiển).");
                EventBus.Publish(new NonFollowerCommandRejectedEvent(userId));
            }

            return isFollower;
        }

        private bool IsFollower(string userId)
        {
            if (_followerStatusProvider == null)
            {
                Debug.Log("[FollowerGate] chờ module StreamIntegration - mock cho qua Follower Gate.");
                return true;
            }

            return _followerStatusProvider.IsFollower(userId);
        }
    }
}
