using UnityEngine;
using UnityEngine.UI;
using SteamRush.Core;
using SteamRush.Features.Runner;
using SteamRush.Features.StreamIntegration;

namespace SteamRush.MinhHuy.ChatRunnerTest
{
    // Script test ca nhan cho ChatRunnerTest.unity - KHONG thuoc deliverable 4 subtask.
    // CHI mo phong Like/Chat/Follow/Death bang Context Menu + nut bam - KHONG con noi
    // Manager<->UI o day nua (phan do da chuyen sang ChatRunnerUIBinder.cs, mot script san
    // xuat that su, de scene tich hop cung phai them dung script do thi UI moi chay).
    public class ChatRunnerTestHarness : MonoBehaviour
    {
        [Header("Managers (keo tha tu ChatRunnerManagers)")]
        [SerializeField] private FactionTugOfWarManager _factionManager;
        [SerializeField] private ChatRunnerQueueManager _queueManager;

        [Header("Nut bam test (keo tha tu Canvas)")]
        [SerializeField] private Button _fanLikeButton;
        [SerializeField] private Button _antiLikeButton;

        [Header("Du lieu mo phong")]
        [SerializeField] private string _testUserId = "viewer_01";
        [SerializeField] private string _testChatMessage = "left, right, fast";

        private readonly ChatCommandSanitizer _sanitizer = new ChatCommandSanitizer();

        private void Awake()
        {
            if (_fanLikeButton != null)
            {
                _fanLikeButton.onClick.AddListener(SimulateFanLike);
            }

            if (_antiLikeButton != null)
            {
                _antiLikeButton.onClick.AddListener(SimulateAntiLike);
            }
        }

        [ContextMenu("1. Sanitize thu chat message")]
        private void SimulateSanitize()
        {
            _sanitizer.SanitizeAndParse(_testChatMessage);
        }

        [ContextMenu("2. Vao phe FAN + 1 Like")]
        private void SimulateFanLike()
        {
            _factionManager.OnChatCommand(_testUserId, "#fan");
            _factionManager.OnLikeReceived(_testUserId);
        }

        [ContextMenu("3. Vao phe ANTI + 1 Like")]
        private void SimulateAntiLike()
        {
            _factionManager.OnChatCommand(_testUserId, "#anti");
            _factionManager.OnLikeReceived(_testUserId);
        }

        [ContextMenu("4. Anti Like x500 (kich hoat nguong sinh xe)")]
        private void SimulateAntiLikeBurst()
        {
            _factionManager.OnChatCommand(_testUserId, "#anti");
            for (int i = 0; i < 500; i++)
            {
                _factionManager.OnLikeReceived(_testUserId);
            }
        }

        [ContextMenu("5. Follower moi vao hang doi")]
        private void SimulateFollowerJoin()
        {
            _queueManager.TryEnqueueFollower(_testUserId);
        }

        [ContextMenu("6. Runner chet (hoi sinh ve Checkpoint)")]
        private void SimulatePlayerDeath()
        {
            EventBus.Publish(new PlayerDeathEvent());
        }
    }
}
