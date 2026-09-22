using UnityEngine;
using SteamRush.Core;
using SteamRush.Track;

namespace SteamRush.Features.Runner
{
    // Luu checkpoint an toan moi 200m (GDD v1.3 muc 7). Khi Runner chet (PlayerDeathEvent qua
    // EventBus - SteamRush.Core), dich Runner ve checkpoint gan nhat va yeu cau hoi tim, cung
    // qua EventBus. KHONG tham chieu truc tiep module HP/Heart (chua ton tai luc viet file nay -
    // se noi qua 1 adapter rieng sau khi RunnerHealthSystem cua Truong (S1-24) merge, khong dung
    // toi CheckpointManager).
    //
    // Doc quang duong qua WorldSpeedManager (SteamRush.Track) - class ha tang dung chung cho ca
    // 2 luong gameplay (ChatLaneRunnerController cua Tu cung doc/ghi toc do cuon qua day), khong
    // phai goi thang vao logic nghiep vu cua module khac nen khong vi pham nguyen tac decoupling.
    public class CheckpointManager : MonoBehaviour
    {
        [SerializeField] private Transform _runnerTransform;
        [SerializeField] private WorldSpeedManager _worldSpeedManager;
        [SerializeField] private float _checkpointIntervalMeters = 200f;
        [SerializeField] private int _heartCount = 3;

        private float _distanceSinceLastCheckpoint;
        private float _totalDistanceTraveled;
        private Vector3 _lastCheckpointPosition;
        private float _lastCheckpointDistance;
        private bool _hasCheckpoint;

        private void Awake()
        {
            if (_runnerTransform != null)
            {
                _lastCheckpointPosition = _runnerTransform.position;
                _hasCheckpoint = true;
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<PlayerDeathEvent>(OnPlayerDeath);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PlayerDeathEvent>(OnPlayerDeath);
        }

        private void Update()
        {
            if (_worldSpeedManager == null)
            {
                return;
            }

            float step = _worldSpeedManager.CurrentSpeed * Time.deltaTime;
            _distanceSinceLastCheckpoint += step;
            _totalDistanceTraveled += step;

            if (_distanceSinceLastCheckpoint >= _checkpointIntervalMeters)
            {
                _distanceSinceLastCheckpoint -= _checkpointIntervalMeters;
                SaveCheckpoint();
            }
        }

        private void SaveCheckpoint()
        {
            if (_runnerTransform == null)
            {
                return;
            }

            _lastCheckpointPosition = _runnerTransform.position;
            _lastCheckpointDistance = _totalDistanceTraveled;
            _hasCheckpoint = true;
        }

        // Doan 3: diem debug bat buoc theo task - bao Runner da hoi sinh tai checkpoint nao.
        private void OnPlayerDeath(PlayerDeathEvent deathEvent)
        {
            if (_runnerTransform != null && _hasCheckpoint)
            {
                _runnerTransform.position = _lastCheckpointPosition;
            }

            Debug.Log($"[CheckpointManager] Runner da hoi sinh tai checkpoint gan nhat (quang duong da luu={_lastCheckpointDistance:F0}m).");

            EventBus.Publish(new RestoreHeartsRequestEvent(_heartCount));
        }
    }
}
