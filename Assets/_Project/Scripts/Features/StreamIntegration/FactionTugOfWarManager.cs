using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using SteamRush.Core;

namespace SteamRush.Features.StreamIntegration
{
    public enum FactionType
    {
        Fan,
        Anti
    }

    // Quan ly "keo co" 2 phe Fan/Anti (GDD v1.3 muc 4 - Hai phe). Tra cuu phe theo userId O(1)
    // qua Dictionary. KHONG goi thang SingleObstacleSpawner.cs (DangHuy, S1-23) - chi Publish
    // RequestCarSpawnEvent qua EventBus khi du dieu kien; 1 adapter rieng se noi 2 ben sau khi
    // S1-23 co san (xem ghi chu trong EventBus.cs).
    // Bao du lieu ra ngoai (UI) qua UnityEvent - FactionTugOfWarUI la View thuan, khong tu doc
    // Dictionary cua class nay.
    public class FactionTugOfWarManager : MonoBehaviour
    {
        [SerializeField] private int _antiCarThreshold = 500;
        [SerializeField] private int _antiCarCost = 300;
        [SerializeField] private FactionType _defaultFaction = FactionType.Fan;

        [Serializable] public class FactionValuesChangedEvent : UnityEvent<int, int> { }
        [SerializeField] private FactionValuesChangedEvent _factionValuesChanged = new FactionValuesChangedEvent();
        public FactionValuesChangedEvent FactionValuesChanged => _factionValuesChanged;

        // GDD v1.3 muc 2: "Khan gia DA FOLLOW moi duoc tu do chon gia nhap 1 trong 2 phe" - Like/
        // doi phe/donate deu phai qua Gate nay truoc, giong het cach ChatRunnerQueueManager dang
        // dung cho lenh dieu khien. Chua gan IFollowerStatusProvider that -> mac dinh cho qua (mock).
        private readonly FollowerGate _followerGate = new FollowerGate();

        private readonly Dictionary<string, FactionType> _userFactions = new Dictionary<string, FactionType>();

        private int _fanLikes;
        private int _antiLikes;

        public int FanLikes => _fanLikes;
        public int AntiLikes => _antiLikes;

        // Doc phe hien tai cua 1 nguoi dung - chua tung xuat hien thi mac dinh _defaultFaction.
        public FactionType GetFaction(string userId)
        {
            return _userFactions.TryGetValue(userId, out FactionType faction) ? faction : _defaultFaction;
        }

        public void OnLikeReceived(string userId)
        {
            if (!_followerGate.CanSendCommand(userId))
            {
                return;
            }

            FactionType faction = GetFaction(userId);

            if (faction == FactionType.Fan)
            {
                _fanLikes++;
            }
            else
            {
                _antiLikes++;
                CheckAntiCarThreshold();
            }

            _factionValuesChanged.Invoke(_fanLikes, _antiLikes);
        }

        // Doan 2: diem debug bat buoc theo task - bao du tim Anti TRUOC khi ban event yeu cau sinh xe.
        private void CheckAntiCarThreshold()
        {
            if (_antiLikes < _antiCarThreshold)
            {
                return;
            }

            Debug.Log($"[FactionTugOfWarManager] Phe Anti du {_antiCarThreshold} tim - yeu cau sinh xe can duong.");

            EventBus.Publish(new RequestCarSpawnEvent());

            _antiLikes -= _antiCarCost;
        }

        // GDD v1.3 muc 4: Fan Like sac "Nang Luong" dung cho lenh fast. Ben thuc thi lenh
        // (Tu, S1-22, ChatLaneRunnerController - chua ton tai) se goi ham nay truoc/trong luc
        // fast dang chay de tru dan nang luong; false = khong du nang luong, ben goi tu quyet
        // dinh tu choi hoac ket thuc fast som (xem GDD: "can canh se ve lai toc do thuong khi
        // can nang luong"). Khong dung EventBus vi day la hoi-dap 2 chieu (can gia tri tra ve
        // ngay), khac voi cac tin hieu 1 chieu (RequestCarSpawnEvent, PlayerDeathEvent).
        public bool TryConsumeFanEnergy(int amount)
        {
            if (_fanLikes < amount)
            {
                return false;
            }

            _fanLikes -= amount;
            _factionValuesChanged.Invoke(_fanLikes, _antiLikes);
            return true;
        }

        // Doi phe qua chat: #FAN/#Blue -> Fan, #ANTI/#Red -> Anti (khong phan biet hoa thuong).
        public void OnChatCommand(string userId, string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            if (!_followerGate.CanSendCommand(userId))
            {
                return;
            }

            string normalized = message.Trim().ToLowerInvariant();

            if (normalized == "#fan" || normalized == "#blue")
            {
                SetFaction(userId, FactionType.Fan);
            }
            else if (normalized == "#anti" || normalized == "#red")
            {
                SetFaction(userId, FactionType.Anti);
            }
        }

        // Doi phe theo hanh vi donate (GDD v1.3 muc 4): tang qua bay -> Anti, tang Khien/Mau -> Fan.
        public void OnDonateReceived(string userId, bool isTrapGift)
        {
            if (!_followerGate.CanSendCommand(userId))
            {
                return;
            }

            SetFaction(userId, isTrapGift ? FactionType.Anti : FactionType.Fan);
        }

        private void SetFaction(string userId, FactionType faction)
        {
            _userFactions[userId] = faction;
        }
    }
}
