using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SteamRush.Features.StreamIntegration;

namespace SteamRush.Features.Runner
{
    /// <summary>
    /// Simulates a TikTok Live session with real-time audience interaction, likes, faction tug-of-war, and queue handovers.
    /// </summary>
    public class LiveSessionDemoRunner : MonoBehaviour
    {
        public static LiveSessionDemoRunner Instance { get; private set; }

        [Header("Operation State")]
        [Tooltip("Automatically start live simulation upon entering Play Mode")]
        [SerializeField] private bool autoStart = true;
        [SerializeField] private bool isRunning = false;

        [Header("Follower & Handover Simulation")]
        [Tooltip("Enable/disable mock follower baton handover during Live Demo")]
        [SerializeField] private bool enableMockFollowers = false;
        [Tooltip("Sync initial mock follower settings with ChatRunnerQueueManager")]
        [SerializeField] private bool syncQueueManagerMock = true;

        [Header("Interaction Pace (Seconds)")]
        [SerializeField] private float chatIntervalMin = 0.8f;
        [SerializeField] private float chatIntervalMax = 2.0f;
        [SerializeField] private float giftIntervalMin = 4.0f;
        [SerializeField] private float giftIntervalMax = 8.5f;
        [SerializeField] private float queueIntervalMin = 12.0f;
        [SerializeField] private float queueIntervalMax = 22.0f;

        [Header("Audience & Faction Simulation")]
        [SerializeField] private int initialFanCount = 6;
        [SerializeField] private int initialAntiCount = 8;
        [SerializeField] private float joinIntervalMin = 2.0f;
        [SerializeField] private float joinIntervalMax = 4.5f;
        [SerializeField] private float switchIntervalMin = 4.0f;
        [SerializeField] private float switchIntervalMax = 7.5f;
        [SerializeField] private int totalRoomViewers = 1240;

        [Header("Live Likes Simulation")]
        [Tooltip("Enable/disable continuous audience like tapping simulation")]
        [SerializeField] private bool enableLikeSimulation = true;
        [Tooltip("Minimum interval between like taps in seconds")]
        [SerializeField] private float likeIntervalMin = 0.25f;
        [Tooltip("Maximum interval between like taps in seconds")]
        [SerializeField] private float likeIntervalMax = 0.60f;
        [Tooltip("Minimum likes per tap batch")]
        [SerializeField] private int minLikesPerBatch = 2;
        [Tooltip("Maximum likes per tap batch")]
        [SerializeField] private int maxLikesPerBatch = 8;
        [Tooltip("Ratio of likes attributed to Fan faction (0.52 = 52% Fan vs 48% Anti)")]
        [Range(0f, 1f)] [SerializeField] private float fanLikeRatio = 0.52f;
        [Tooltip("Total accumulated likes in the live room")]
        [SerializeField] private int totalRoomLikes = 15400;

        [Header("Sub-Feature Toggles")]
        [Tooltip("Enable/disable automated chat comments loop")]
        [SerializeField] private bool enableChatSimulation = true;
        [Tooltip("Enable/disable automated gifts donation loop")]
        [SerializeField] private bool enableGiftSimulation = true;
        [Tooltip("Enable/disable automated audience join & faction switch loop")]
        [SerializeField] private bool enableAudienceSimulation = true;

        public bool EnableChatSimulation
        {
            get => enableChatSimulation;
            set => enableChatSimulation = value;
        }

        public bool EnableGiftSimulation
        {
            get => enableGiftSimulation;
            set => enableGiftSimulation = value;
        }

        public bool EnableLikeSimulation
        {
            get => enableLikeSimulation;
            set => enableLikeSimulation = value;
        }

        public bool EnableAudienceSimulation
        {
            get => enableAudienceSimulation;
            set => enableAudienceSimulation = value;
        }

        public bool IsRunning => isRunning;
        public int TotalRoomViewers => totalRoomViewers;
        public int TotalRoomLikes => totalRoomLikes;

        [Header("Livestream Delay Simulation")]
        [Tooltip("Enable/disable livestream broadcast & network latency simulation")]
        [SerializeField] private bool enableStreamDelay = true;
        [Tooltip("Minimum live broadcast latency in seconds (e.g. 1.5s)")]
        [SerializeField] private float streamDelayMin = 1.5f;
        [Tooltip("Maximum live broadcast latency in seconds (e.g. 3.0s)")]
        [SerializeField] private float streamDelayMax = 3.0f;

        public bool EnableStreamDelay
        {
            get => enableStreamDelay;
            set => enableStreamDelay = value;
        }

        public float StreamDelayMin
        {
            get => streamDelayMin;
            set => streamDelayMin = Mathf.Max(0f, value);
        }

        public float StreamDelayMax
        {
            get => streamDelayMax;
            set => streamDelayMax = Mathf.Max(streamDelayMin, value);
        }

        public float CurrentAverageDelay => enableStreamDelay ? (streamDelayMin + streamDelayMax) * 0.5f : 0f;

        public bool EnableMockFollowers
        {
            get => enableMockFollowers;
            set
            {
                if (enableMockFollowers == value) return;
                enableMockFollowers = value;
                ApplyMockFollowerState();
            }
        }

        public bool SyncQueueManagerMock
        {
            get => syncQueueManagerMock;
            set => syncQueueManagerMock = value;
        }

        private static readonly string[] BaseFanNames = new string[]
        {
            "LinhDan_99", "MinhVu_Pro", "HoangLong_Gamer", "ThuTrang_Cute", "BaoNam_Fan",
            "GamerHuy_Live", "ThanhHa_98", "QuangHuy_Speed", "KhanhVy_Official", "TuanKiet_Racer",
            "CoBeMuaDong", "ThanhNienNghiemTuc", "Gamer_Chi", "Phuong_Thao", "Duc_Anh",
            "Hai_Dang", "Ngoc_Mai", "Hung_Master", "Top1_Sever", "BeHeo_Unnie", "AnhBaCongNghe",
            "MaiAnh_Cute", "TrangBong_Fan", "MinhTri_Pro", "GiaBao_Gamer", "ThienAn_99"
        };

        private static readonly string[] BaseAntiNames = new string[]
        {
            "AntiFan_ChinhHieu", "TrumPhaGame", "XeDien_Pro", "ChuyenGiaGank", "QuayXe_99",
            "BaoXe_Anti", "ThanhNi_Anti", "KingOfTroll", "ChongFan_Cung", "XeBanTai_01",
            "TruckMaster", "GhostRider", "Anti_Alex", "TrollXe_Vip", "PhaDam_Sever", "LopTruongAnti",
            "HackerXe_Vip", "SieuPha_Team", "TrumGank_No1", "BaoCat_Anti", "DenDo_Team"
        };

        private Coroutine _chatRoutine;
        private Coroutine _giftRoutine;
        private Coroutine _queueRoutine;
        private Coroutine _joinRoutine;
        private Coroutine _switchRoutine;
        private Coroutine _likeRoutine;

        private int _viewerSerial = 100;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            if (syncQueueManagerMock && !enableMockFollowers)
            {
                var queueMgr = ChatRunnerQueueManager.Instance ?? FindFirstObjectByType<ChatRunnerQueueManager>();
                if (queueMgr != null)
                {
                    queueMgr.SetMockFollowersEnabled(false, true);
                }
            }

            if (autoStart)
            {
                var configMgr = FindFirstObjectByType<SteamRush.Features.UI.PreGameConfig.PreGameConfigManager>();
                if (configMgr == null)
                {
                    StartLiveDemo();
                }
            }
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            // When user is typing inside any InputField, ignore debug hotkeys
            if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null)
            {
                var selected = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
                if (selected.GetComponent<TMPro.TMP_InputField>() != null || selected.GetComponent<UnityEngine.UI.InputField>() != null)
                {
                    return;
                }
            }

            // Also ignore all debug hotkeys when PreGameConfig setup modal is open
            if (SteamRush.Features.UI.PreGameConfig.PreGameConfigManager.Instance != null && SteamRush.Features.UI.PreGameConfig.PreGameConfigManager.Instance.IsOpen)
            {
                return;
            }

            if (Keyboard.current.lKey.wasPressedThisFrame || Keyboard.current.pKey.wasPressedThisFrame)
            {
                ToggleLiveDemo();
            }

            if (Keyboard.current.kKey.wasPressedThisFrame)
            {
                enableStreamDelay = !enableStreamDelay;
                Debug.Log($"[LiveSessionDemoRunner] Livestream Broadcast Delay: {(enableStreamDelay ? $"ON ({streamDelayMin:F1}s - {streamDelayMax:F1}s)" : "OFF (0s)")}");
            }

            if (Keyboard.current.oKey.wasPressedThisFrame)
            {
                ToggleMockFollowers();
            }
        }

        public void ToggleLiveDemo()
        {
            if (isRunning)
            {
                StopLiveDemo();
            }
            else
            {
                StartLiveDemo();
            }
        }

        public void ToggleMockFollowers()
        {
            EnableMockFollowers = !enableMockFollowers;
            Debug.Log($"[LiveSessionDemoRunner] Mock Follower Baton Relay: {(enableMockFollowers ? "<color=#00FF88>ON</color>" : "<color=#FF4444>OFF</color>")}");
        }

        private void ApplyMockFollowerState()
        {
            if (syncQueueManagerMock)
            {
                var queueMgr = ChatRunnerQueueManager.Instance ?? FindFirstObjectByType<ChatRunnerQueueManager>();
                if (queueMgr != null)
                {
                    queueMgr.SetMockFollowersEnabled(enableMockFollowers, !enableMockFollowers);
                }
            }

            if (isRunning)
            {
                if (enableMockFollowers && _queueRoutine == null)
                {
                    _queueRoutine = StartCoroutine(SimulateQueueLoop());
                }
                else if (!enableMockFollowers && _queueRoutine != null)
                {
                    StopCoroutine(_queueRoutine);
                    _queueRoutine = null;
                }
            }
        }

        public void StartLiveDemo()
        {
            if (isRunning) return;
            isRunning = true;

            Debug.Log("<color=#00FF88><b>[LiveSessionDemoRunner] >>> LIVE DEMO SESSION STARTED! (Press L or P to Pause) <<<</b></color>");

            InitInitialFactionMembers();

            if (enableChatSimulation) _chatRoutine = StartCoroutine(SimulateChatLoop());
            if (enableGiftSimulation) _giftRoutine = StartCoroutine(SimulateGiftLoop());
            if (enableMockFollowers) _queueRoutine = StartCoroutine(SimulateQueueLoop());
            if (enableAudienceSimulation)
            {
                _joinRoutine = StartCoroutine(SimulateAudienceJoinLoop());
                _switchRoutine = StartCoroutine(SimulateFactionSwitchLoop());
            }
            if (enableLikeSimulation) _likeRoutine = StartCoroutine(SimulateLikeLoop());
        }

        /// <summary>
        /// Configures and applies all live session simulation features from Pre-Game Config.
        /// </summary>
        public void ConfigureDemoSimulation(bool masterEnabled, bool chatEnabled = true, bool giftEnabled = true, bool likeEnabled = true, bool followersEnabled = false, bool delayEnabled = false)
        {
            enableChatSimulation = chatEnabled;
            enableGiftSimulation = giftEnabled;
            enableLikeSimulation = likeEnabled;
            enableAudienceSimulation = chatEnabled || giftEnabled;
            EnableMockFollowers = followersEnabled;
            EnableStreamDelay = delayEnabled;

            if (masterEnabled)
            {
                if (isRunning)
                {
                    StopLiveDemo();
                }
                StartLiveDemo();
            }
            else
            {
                StopLiveDemo();
            }
        }

        public void StopLiveDemo()
        {
            if (!isRunning) return;
            isRunning = false;

            StopAllCoroutines();

            _chatRoutine = null;
            _giftRoutine = null;
            _queueRoutine = null;
            _joinRoutine = null;
            _switchRoutine = null;
            _likeRoutine = null;

            Debug.Log("<color=#FF8800><b>[LiveSessionDemoRunner] --- LIVE DEMO SESSION PAUSED (Press L or P to Resume) ---</b></color>");
        }

        private void OnDestroy()
        {
            StopLiveDemo();
        }

        /// <summary>
        /// Coordinates viewer action execution with simulated broadcast latency.
        /// If enableStreamDelay is active, actions are dispatched after a randomized delay.
        /// </summary>
        public void DispatchViewerAction(System.Action action)
        {
            if (action == null || !isRunning) return;

            if (!enableStreamDelay || streamDelayMax <= 0.01f)
            {
                action.Invoke();
                return;
            }

            float delay = Random.Range(streamDelayMin, streamDelayMax);
            StartCoroutine(ExecuteDelayedAction(action, delay));
        }

        private IEnumerator ExecuteDelayedAction(System.Action action, float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            if (isRunning)
            {
                action?.Invoke();
            }
        }

        private void InitInitialFactionMembers()
        {
            var factionMgr = FindFirstObjectByType<FactionTugOfWarManager>();
            if (factionMgr == null) return;

            for (int i = 0; i < initialFanCount; i++)
            {
                string name = i < BaseFanNames.Length ? BaseFanNames[i] : $"Fan_{i + 1}";
                factionMgr.SetFaction(name, FactionType.Fan);
            }

            for (int i = 0; i < initialAntiCount; i++)
            {
                string name = i < BaseAntiNames.Length ? BaseAntiNames[i] : $"Anti_{i + 1}";
                factionMgr.SetFaction(name, FactionType.Anti);
            }
        }

        private IEnumerator SimulateLikeLoop()
        {
            yield return new WaitForSeconds(0.8f);

            var factionMgr = FindFirstObjectByType<FactionTugOfWarManager>();

            while (isRunning)
            {
                if (!enableLikeSimulation)
                {
                    yield return new WaitForSeconds(0.5f);
                    continue;
                }

                yield return new WaitForSeconds(Random.Range(likeIntervalMin, likeIntervalMax));

                if (factionMgr == null)
                {
                    factionMgr = FindFirstObjectByType<FactionTugOfWarManager>();
                    if (factionMgr == null) continue;
                }

                int likes = Random.Range(minLikesPerBatch, maxLikesPerBatch + 1);
                totalRoomLikes += likes;

                bool isFan = Random.value < fanLikeRatio;
                factionMgr.AddLikes(isFan ? FactionType.Fan : FactionType.Anti, likes);
            }
        }

        private IEnumerator SimulateAudienceJoinLoop()
        {
            yield return new WaitForSeconds(1.0f);

            var console = MockChatConsole.Instance ?? FindFirstObjectByType<MockChatConsole>();

            while (isRunning)
            {
                yield return new WaitForSeconds(Random.Range(joinIntervalMin, joinIntervalMax));

                if (console == null)
                {
                    console = MockChatConsole.Instance ?? FindFirstObjectByType<MockChatConsole>();
                    if (console == null) continue;
                }

                totalRoomViewers += Random.Range(3, 14);

                _viewerSerial++;
                string newViewer;
                bool joinFan = Random.value < 0.55f;

                if (joinFan)
                {
                    newViewer = Random.value < 0.6f && _viewerSerial < 150
                        ? BaseFanNames[Random.Range(0, BaseFanNames.Length)]
                        : $"Fan_{_viewerSerial}";

                    DispatchViewerAction(() => console.SimulateViewerChat(newViewer, "blue"));
                }
                else
                {
                    newViewer = Random.value < 0.6f && _viewerSerial < 150
                        ? BaseAntiNames[Random.Range(0, BaseAntiNames.Length)]
                        : $"Anti_{_viewerSerial}";

                    DispatchViewerAction(() => console.SimulateViewerChat(newViewer, "red"));
                }
            }
        }

        private IEnumerator SimulateFactionSwitchLoop()
        {
            yield return new WaitForSeconds(3.5f);

            var console = MockChatConsole.Instance ?? FindFirstObjectByType<MockChatConsole>();
            var factionMgr = FindFirstObjectByType<FactionTugOfWarManager>();

            while (isRunning)
            {
                yield return new WaitForSeconds(Random.Range(switchIntervalMin, switchIntervalMax));

                if (console == null) console = MockChatConsole.Instance ?? FindFirstObjectByType<MockChatConsole>();
                if (factionMgr == null) factionMgr = FindFirstObjectByType<FactionTugOfWarManager>();
                if (console == null || factionMgr == null) continue;

                bool fanToAnti = Random.value < 0.5f;

                if (fanToAnti)
                {
                    var fanMembers = factionMgr.GetMembersOfFaction(FactionType.Fan);
                    if (fanMembers.Count > 2)
                    {
                        string memberToSwitch = fanMembers[Random.Range(0, fanMembers.Count)];
                        DispatchViewerAction(() => console.SimulateViewerChat(memberToSwitch, "red"));
                    }
                }
                else
                {
                    var antiMembers = factionMgr.GetMembersOfFaction(FactionType.Anti);
                    if (antiMembers.Count > 2)
                    {
                        string memberToSwitch = antiMembers[Random.Range(0, antiMembers.Count)];
                        DispatchViewerAction(() => console.SimulateViewerChat(memberToSwitch, "blue"));
                    }
                }
            }
        }

        /// <summary>
        /// Simulates continuous viewer chat commands and vehicle obstacle spawns (0.8s - 2.0s interval).
        /// </summary>
        private IEnumerator SimulateChatLoop()
        {
            yield return new WaitForSeconds(1.5f);

            var console = MockChatConsole.Instance ?? FindFirstObjectByType<MockChatConsole>();

            while (isRunning)
            {
                yield return new WaitForSeconds(Random.Range(chatIntervalMin, chatIntervalMax));

                if (console == null)
                {
                    console = MockChatConsole.Instance ?? FindFirstObjectByType<MockChatConsole>();
                    if (console == null) continue;
                }

                bool isFan = Random.value < 0.60f;

                if (isFan)
                {
                    string fanUser = BaseFanNames[Random.Range(0, BaseFanNames.Length)];
                    float roll = Random.value;

                    if (roll < 0.70f)
                    {
                        int lane = Random.Range(1, 4);
                        DispatchViewerAction(() => console.SimulateViewerChat(fanUser, lane.ToString()));
                    }
                    else if (roll < 0.90f)
                    {
                        string jumpCmd = Random.value < 0.5f ? "jump" : "j";
                        DispatchViewerAction(() => console.SimulateViewerChat(fanUser, jumpCmd));
                    }
                    else
                    {
                        DispatchViewerAction(() => console.SimulateViewerChat(fanUser, "blue"));
                    }
                }
                else
                {
                    string antiUser = BaseAntiNames[Random.Range(0, BaseAntiNames.Length)];
                    float roll = Random.value;

                    if (roll < 0.85f)
                    {
                        int lane = Random.Range(1, 4);
                        DispatchViewerAction(() => console.SimulateViewerChat(antiUser, lane.ToString()));
                    }
                    else
                    {
                        DispatchViewerAction(() => console.SimulateViewerChat(antiUser, "red"));
                    }
                }
            }
        }

        private IEnumerator SimulateGiftLoop()
        {
            yield return new WaitForSeconds(3.0f);

            var console = MockChatConsole.Instance ?? FindFirstObjectByType<MockChatConsole>();

            while (isRunning)
            {
                yield return new WaitForSeconds(Random.Range(giftIntervalMin, giftIntervalMax));

                if (console == null)
                {
                    console = MockChatConsole.Instance ?? FindFirstObjectByType<MockChatConsole>();
                    if (console == null) continue;
                }

                bool isFanGift = Random.value < 0.50f;

                if (isFanGift)
                {
                    string sender = BaseFanNames[Random.Range(0, BaseFanNames.Length)];
                    float roll = Random.value;

                    if (roll < 0.35f)
                    {
                        DispatchViewerAction(() => console.MockDonateShield(sender));
                    }
                    else if (roll < 0.60f)
                    {
                        DispatchViewerAction(() => console.MockFanEnergyBottle(sender));
                    }
                    else if (roll < 0.80f)
                    {
                        DispatchViewerAction(() => console.MockActivateFanSprintBuff(sender));
                    }
                    else if (roll < 0.90f)
                    {
                        DispatchViewerAction(() => console.MockBuyVipTicket(sender));
                    }
                    else
                    {
                        DispatchViewerAction(() => console.MockGiftDance(sender));
                    }
                }
                else
                {
                    string sender = BaseAntiNames[Random.Range(0, BaseAntiNames.Length)];
                    float roll = Random.value;

                    if (roll < 0.35f)
                    {
                        DispatchViewerAction(() => console.MockSpawnPickupTruck(sender));
                    }
                    else if (roll < 0.60f)
                    {
                        DispatchViewerAction(() => console.MockAntiEnergyBottle(sender));
                    }
                    else if (roll < 0.80f)
                    {
                        DispatchViewerAction(() => console.MockSpawnHeavyTruck(sender));
                    }
                    else if (roll < 0.90f)
                    {
                        DispatchViewerAction(() => console.MockWeatherHazard(sender));
                    }
                    else
                    {
                        DispatchViewerAction(() => console.MockActivateAntiCarUnlimited(sender));
                    }
                }
            }
        }

        private IEnumerator SimulateQueueLoop()
        {
            yield return new WaitForSeconds(5.0f);

            var console = MockChatConsole.Instance ?? FindFirstObjectByType<MockChatConsole>();

            while (isRunning)
            {
                yield return new WaitForSeconds(Random.Range(queueIntervalMin, queueIntervalMax));

                if (console == null)
                {
                    console = MockChatConsole.Instance ?? FindFirstObjectByType<MockChatConsole>();
                    if (console == null) continue;
                }

                string newFollower = BaseFanNames[Random.Range(0, BaseFanNames.Length)];
                if (Random.value < 0.85f)
                {
                    DispatchViewerAction(() => console.MockNewFollower(newFollower));
                }
                else
                {
                    DispatchViewerAction(() => console.MockBuyVipTicket(newFollower));
                }
            }
        }

        private void OnGUI()
        {
            if (!isRunning) return;

            var factionMgr = FindFirstObjectByType<FactionTugOfWarManager>();
            int fanCount = factionMgr != null ? factionMgr.FanMemberCount : initialFanCount;
            int antiCount = factionMgr != null ? factionMgr.AntiMemberCount : initialAntiCount;

            GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            boxStyle.normal.textColor = Color.white;

            int boxWidth = 590;
            GUI.color = new Color(0.1f, 0.1f, 0.15f, 0.88f);
            GUI.Box(new Rect(Screen.width - boxWidth - 10, 10, boxWidth, 28), "", boxStyle);
            GUI.color = Color.white;

            GUIStyle redDotStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            redDotStyle.normal.textColor = new Color(1f, 0.25f, 0.25f);
            GUI.Label(new Rect(Screen.width - boxWidth, 12, 55, 24), "[LIVE]", redDotStyle);

            GUIStyle textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            textStyle.normal.textColor = Color.white;

            string likesText = totalRoomLikes >= 1000 ? $"{(totalRoomLikes / 1000f):F1}k" : totalRoomLikes.ToString();
            string viewersText = SteamRush.Features.UI.FactionTugOfWarUI.FormatNumberShorthand(totalRoomViewers);
            string followerInfo = enableMockFollowers ? " | <color=#00FF88>Follower: ON (O)</color>" : " | <color=#888888>Follower: OFF (O)</color>";
            string delayInfo = enableStreamDelay ? $" | <color=#FFD700>Delay: {((streamDelayMin + streamDelayMax) * 0.5f):F1}s (K)</color>" : " | <color=#888888>Delay: 0s (K)</color>";
            string info = $"{viewersText} Viewers | <color=#FF4D88>Likes: {likesText}</color> | <color=#38B6FF>Blue: {SteamRush.Features.UI.FactionTugOfWarUI.FormatNumberShorthand(fanCount)}</color> vs <color=#FF4D4D>Red: {SteamRush.Features.UI.FactionTugOfWarUI.FormatNumberShorthand(antiCount)}</color>{followerInfo}{delayInfo}";
            GUI.Label(new Rect(Screen.width - boxWidth + 55, 12, boxWidth - 60, 24), info, textStyle);
        }
    }
}
