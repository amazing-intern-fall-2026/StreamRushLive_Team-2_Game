using UnityEngine;
using SteamRush.Core;

namespace SteamRush.Features.StreamIntegration
{
    // StreamIntegration se cung cap trang thai Follow that qua interface nay.
    public interface IFollowerStatusProvider
    {
        bool IsFollower(string userId);
    }

    // Chan quyen vao hang doi / gui lenh chat - chi Follower moi duoc phep (GDD v1.3 muc 3).
    // Thu tu chon nguon follower: provider truyen qua constructor -> provider dung chung
    // (SetSharedProvider) -> neu ca hai deu chua co thi cho qua (mock) de test scene ca nhan.
    public class FollowerGate
    {
        // [TikTok] Provider dung chung: TikTokFollowerRegistry tu dang ky khi ket noi, nen cac
        // class dang "new FollowerGate()" (FactionTugOfWarManager, ChatRunnerQueueManager) tu dung
        // duoc follower that ma khong phai sua tung noi.
        private static IFollowerStatusProvider _sharedProvider;

        /// <summary>
        /// When false (default): Anyone can play without follow restriction.
        /// When true: Only viewers who follow during the live stream can control or join the queue.
        /// </summary>
        public static bool StrictFollowerOnly = false;

        private readonly IFollowerStatusProvider _followerStatusProvider;

        public FollowerGate(IFollowerStatusProvider followerStatusProvider = null)
        {
            _followerStatusProvider = followerStatusProvider;
        }

        public static void SetSharedProvider(IFollowerStatusProvider provider)
        {
            _sharedProvider = provider;
        }

        // Chi go neu provider dang gan chinh la provider nay (tranh go nham cua nguoi khac).
        public static void ClearSharedProvider(IFollowerStatusProvider provider)
        {
            if (_sharedProvider == provider)
            {
                _sharedProvider = null;
            }
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
            if (!StrictFollowerOnly)
            {
                return true;
            }

            IFollowerStatusProvider provider = _followerStatusProvider ?? _sharedProvider;

            if (provider == null)
            {
                return true;
            }

            return provider.IsFollower(userId);
        }
    }
}