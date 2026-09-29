#if UNITY_EDITOR
using UnityEngine;

namespace SteamRush.Features.StreamIntegration
{
    // [DEBUG - TAM THOI] Ban dang bi chan Follow that (xem ghi chu trong chat), nen script nay
    // cho phep tu them 1 userId vao TikTokFollowerRegistry bang tay, de test rieng phan Chat
    // (red/blue doi phe qua FollowerGate) ma khong can cho TikTok gui Follow event.
    //
    // Cach dung:
    //   1. Gan script nay vao CUNG GameObject voi TikTokLiveClient (hoac bat ky GameObject nao,
    //      no se tu FindFirstObjectByType neu de trong o Inspector).
    //   2. Vao Play Mode, doi log "[TikTokLiveClient] JOIN"... khong co, JOIN khong log userId rieng
    //      qua TikTokLiveClient (chi qua Console cua backend/Unity neu ban tu them). Cach de nhat:
    //      lay userId tu dong "[TikTokLiveClient] FOLLOW: ..." neu co, hoac tu chinh CHAT log
    //      (displayName + userId) khi nguoi do gui chat "red"/"blue" lan dau - dong CHAT van in ra
    //      userId that ngay ca khi bi FollowerGate chan.
    //   3. Go userId vao o nhap trong khung GUI (goc tren-trai man hinh Game view luc Play),
    //      bam nut "Add Follower (DEBUG)".
    //   4. Nguoi do gio da nam trong _followerIds -> gui lai "red"/"blue" se duoc FollowerGate
    //      cho qua binh thuong.
    //
    // KHONG commit script nay vao nhanh feature that neu khong can nua - day la cong cu tam thoi
    // de go bug rieng, khong thuoc pham vi task "Gift dance" hay "TikTok Live Integration".
    // Duoc bao boc #if UNITY_EDITOR nen du lo commit nham cung KHONG bi bien dich vao build that.
    public class TikTokDebugFollowInjector : MonoBehaviour
    {
        [Tooltip("De trong se tu tim TikTokLiveClient dau tien trong scene.")]
        [SerializeField] private TikTokLiveClient _client;

        private string _userIdInput = "";
        private string _lastStatus = "";

        private void Awake()
        {
            if (_client == null)
            {
                _client = FindFirstObjectByType<TikTokLiveClient>();
            }
        }

        private void OnGUI()
        {
            const float w = 340f;
            GUILayout.BeginArea(new Rect(10, 10, w, 110), GUI.skin.box);
            GUILayout.Label("[DEBUG] Tự thêm Follower (không chờ TikTok)");

            if (_client == null)
            {
                GUILayout.Label("Không tìm thấy TikTokLiveClient trong scene.");
                GUILayout.EndArea();
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("userId:", GUILayout.Width(50));
            _userIdInput = GUILayout.TextField(_userIdInput);
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Add Follower (DEBUG)"))
            {
                string userId = _userIdInput.Trim();
                if (string.IsNullOrEmpty(userId))
                {
                    _lastStatus = "userId trống, không làm gì.";
                }
                else
                {
                    bool isNew = _client.Followers.AddFollower(userId);
                    _lastStatus = isNew
                        ? $"Đã thêm '{userId}' làm follower (mock)."
                        : $"'{userId}' đã có trong danh sách rồi.";
                    Debug.Log($"[TikTokDebugFollowInjector] {_lastStatus}");
                }
            }

            GUILayout.Label($"Follower hiện có: {_client.Followers.Count}");
            if (!string.IsNullOrEmpty(_lastStatus))
            {
                GUILayout.Label(_lastStatus);
            }

            GUILayout.EndArea();
        }
    }
}
#endif