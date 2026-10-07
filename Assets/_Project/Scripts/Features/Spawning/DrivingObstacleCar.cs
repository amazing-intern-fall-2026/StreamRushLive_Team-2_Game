using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SteamRush.Track;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Vehicle tiers according to design specs (Sedan, Pickup, Heavy Truck).
    /// </summary>
    public enum VehicleTier
    {
        SedanCar,
        PickupTruck,
        HeavyTruck
    }

    /// <summary>
    /// Controls dynamic obstacle vehicles with independent forward driving speed.
    /// </summary>
    public class DrivingObstacleCar : ObstacleBase
    {
        [Header("Vehicle Tier & Penalties (GDD v1.4)")]
        [Tooltip("Obstacle vehicle tier.")]
        [SerializeField] private VehicleTier _vehicleTier = VehicleTier.SedanCar;

        [Tooltip("Knockback distance pushed backward on collision (meters).")]
        [SerializeField] private float _knockbackDistance = 2.0f;

        [Tooltip("Recovery duration after knockback impulse (seconds).")]
        [SerializeField] private float _knockbackDuration = 0.45f;

        [Header("Speed Settings")]
        [Tooltip("Autonomous vehicle drive speed added to world scroll speed (m/s).")]
        [SerializeField] private float _drivingSpeed = 7.0f;

        [Tooltip("X coordinate threshold behind player where car auto-despawns.")]
        [SerializeField] private float _despawnXThreshold = -15f;

        [Header("Visual Effects")]
        [Tooltip("Automatically finds and rotates wheel transforms.")]
        [SerializeField] private bool _enableWheelSpin = true;

        [Tooltip("Wheel radius for angular velocity calculation (meters).")]
        [SerializeField] private float _wheelRadius = 0.36f;

        [Tooltip("Applies subtle engine vibration suspension wobble.")]
        [SerializeField] private bool _enableEngineRumble = true;
        [SerializeField] private float _rumbleFrequency = 30f;
        [SerializeField] private float _rumbleAmplitude = 0.008f;

        [Header("World Reverse Knockback (GDD v1.2/v1.4)")]
        [Tooltip("Peak reverse world scroll speed (m/s, negative value).")]
        [SerializeField] private float _reverseWorldPeakSpeed = -15f;
        [Tooltip("Duration of reverse world scroll impulse (seconds).")]
        [SerializeField] private float _reverseWorldDuration = 0.65f;

        [Header("Audio & Sound Effects")]
        [Tooltip("Optional custom SFX played on spawn/alert (overrides standard vehicle horn).")]
        [SerializeField] private AudioClip _customSpawnSFX;

        public AudioClip CustomSpawnSFX
        {
            get => _customSpawnSFX;
            set => _customSpawnSFX = value;
        }

        private WorldSpeedManager _speedManager;
        private readonly List<Transform> _wheelTransforms = new List<Transform>();
        private float _baseY;
        private float _rumbleSeed;
        private bool _isPinnedToPlayer;
        private Transform _pinnedPlayerTransform;
        private float _pinnedOffsetX;
        private bool _isDeflectedByShield = false;

        public bool IsDeflectedByShield => _isDeflectedByShield;
        public VehicleTier Tier => _vehicleTier;
        public float KnockbackDistance => _knockbackDistance;
        public float KnockbackDuration => _knockbackDuration;
        public float ReverseWorldPeakSpeed => _reverseWorldPeakSpeed;
        public float ReverseWorldDuration => _reverseWorldDuration;

        public float DrivingSpeed
        {
            get => _drivingSpeed;
            set => _drivingSpeed = Mathf.Max(0f, value);
        }

        public void Initialize(WorldSpeedManager speedManager, float drivingSpeed = 7.0f, float despawnXThreshold = -15f)
        {
            _speedManager = speedManager;
            _drivingSpeed = drivingSpeed;
            _despawnXThreshold = despawnXThreshold;
        }

        /// <summary>
        /// Configures collision penalty values per vehicle tier (GDD v1.4).
        /// </summary>
        public void ConfigureTier(VehicleTier tier)
        {
            _vehicleTier = tier;

            // Retrieve configuration directly from GiftManager if available
            if (StreamRushLive.Features.Gifts.GiftManager.Instance != null)
            {
                var cfg = StreamRushLive.Features.Gifts.GiftManager.Instance.GetCarConfig(tier);
                if (cfg != null)
                {
                    obstacleName = cfg.vehicleName;
                    obstacleType = tier == VehicleTier.HeavyTruck ? ObstacleType.HighBarrier : ObstacleType.LowBarrier;
                    energyPenaltyPercent = cfg.energyPenaltyPercent;
                    distancePenaltyMeters = cfg.distancePenaltyMeters;
                    hitStopDuration = cfg.hitStopDuration;
                    _knockbackDistance = cfg.knockbackDistance;
                    _knockbackDuration = cfg.knockbackDuration;
                    _drivingSpeed = cfg.drivingSpeed;
                    _reverseWorldPeakSpeed = cfg.reverseWorldPeakSpeed;
                    _reverseWorldDuration = cfg.reverseWorldDuration;
                    return;
                }
            }

            switch (tier)
            {
                case VehicleTier.SedanCar:
                    obstacleName = "Sedan Car";
                    obstacleType = ObstacleType.LowBarrier;
                    energyPenaltyPercent = 20f;       // -20% energy penalty
                    distancePenaltyMeters = 100f;     // -100m distance penalty
                    hitStopDuration = 0.10f;          // 0.10s hit-stop freeze
                    _knockbackDistance = 2.0f;        // 2.0m knockback
                    _knockbackDuration = 0.5f;
                    _drivingSpeed = 7.0f;
                    _reverseWorldPeakSpeed = -87.0f;  // reverse scroll ~100m
                    _reverseWorldDuration = 1.8f;
                    break;

                case VehicleTier.PickupTruck:
                    obstacleName = "Pickup Truck";
                    obstacleType = ObstacleType.LowBarrier;
                    energyPenaltyPercent = 40f;       // -40% energy penalty
                    distancePenaltyMeters = 200f;     // -200m distance penalty
                    hitStopDuration = 0.18f;          // 0.18s hit-stop freeze
                    _knockbackDistance = 3.0f;        // 3.0m knockback
                    _knockbackDuration = 0.6f;
                    _drivingSpeed = 6.2f;
                    _reverseWorldPeakSpeed = -130.0f; // reverse scroll ~200m
                    _reverseWorldDuration = 2.4f;
                    break;

                case VehicleTier.HeavyTruck:
                    obstacleName = "Heavy Truck";
                    obstacleType = ObstacleType.HighBarrier;
                    energyPenaltyPercent = 60f;       // -60% energy penalty
                    distancePenaltyMeters = 400f;     // -400m distance penalty
                    hitStopDuration = 0.25f;          // 0.25s heavy impact hit-stop
                    _knockbackDistance = 4.2f;        // 4.2m knockback
                    _knockbackDuration = 0.7f;
                    _drivingSpeed = 5.2f;
                    _reverseWorldPeakSpeed = -196.0f; // reverse scroll ~400m
                    _reverseWorldDuration = 3.2f;
                    break;
            }
        }

        private void Awake()
        {
            ConfigureTier(_vehicleTier);

            _baseY = transform.position.y;
            _rumbleSeed = Random.Range(0f, 100f);

            // Find all wheel transform hierarchies (supporting nested structures like trains)
            Transform[] allChildren = GetComponentsInChildren<Transform>(true);
            foreach (Transform child in allChildren)
            {
                if (child == transform) continue;
                string lowerName = child.name.ToLowerInvariant();
                bool isWheel = (lowerName.Contains("wheel") || 
                                lowerName.Contains("_fl") || lowerName.Contains("_fr") || 
                                lowerName.Contains("_rl") || lowerName.Contains("_rr")) && 
                               !lowerName.Contains("steering");
                if (isWheel)
                {
                    _wheelTransforms.Add(child);
                }
            }
        }

        private Rigidbody _rb;

        private void Start()
        {
            _rb = GetComponent<Rigidbody>();
            if (_speedManager == null)
            {
                _speedManager = WorldSpeedManager.Instance ?? FindFirstObjectByType<WorldSpeedManager>();
            }

            // Play custom spawn SFX or default vehicle horn
            if (_customSpawnSFX != null)
            {
                AudioManager.Instance?.PlaySFX(_customSpawnSFX, 1.0f);
            }
            else
            {
                switch (_vehicleTier)
                {
                    case VehicleTier.HeavyTruck:
                        AudioManager.Instance?.PlaySFX(SFXType.HeavyTruckHorn, 1.0f);
                        break;
                    case VehicleTier.PickupTruck:
                        AudioManager.Instance?.PlaySFX(SFXType.PickupHorn, 0.85f);
                        break;
                    case VehicleTier.SedanCar:
                    default:
                        AudioManager.Instance?.PlaySFX(SFXType.CarHorn, 0.8f);
                        break;
                }
            }
        }

        private void Update()
        {
            if (_isDeflectedByShield) return;

            if (_speedManager == null)
            {
                _speedManager = WorldSpeedManager.Instance ?? FindFirstObjectByType<WorldSpeedManager>();
            }

            // On collision: vehicle pins in place in front of runner during hit-stop
            if (_isPinnedToPlayer)
            {
                if (_pinnedPlayerTransform != null)
                {
                    Vector3 pinnedPos = transform.position;
                    pinnedPos.x = _pinnedPlayerTransform.position.x + _pinnedOffsetX;
                    if (_enableEngineRumble)
                    {
                        float rumbleOffset = Mathf.Sin((Time.time * _rumbleFrequency) + _rumbleSeed) * _rumbleAmplitude;
                        pinnedPos.y = _baseY + rumbleOffset;
                    }
                    transform.position = pinnedPos;
                    if (_rb != null) _rb.position = pinnedPos;
                }
                return;
            }

            // Maintain normal vehicle drive speed during reverse world scroll
            float effectiveWorldSpeed = 10f;
            if (_speedManager != null)
            {
                effectiveWorldSpeed = _speedManager.CurrentSpeed < 0f ? _speedManager.BaseSpeed : _speedManager.CurrentSpeed;
            }

            float totalSpeed = effectiveWorldSpeed + _drivingSpeed;
            float dt = Time.deltaTime;

            if (_rb != null && _rb.isKinematic)
            {
                _rb.position += Vector3.left * (totalSpeed * dt);
                transform.position = _rb.position;
            }
            else
            {
                transform.position += Vector3.left * (totalSpeed * dt);
            }

            if (_enableWheelSpin && _wheelTransforms.Count > 0 && _drivingSpeed > 0.01f)
            {
                float angularSpeedDeg = (_drivingSpeed / Mathf.Max(0.1f, _wheelRadius)) * Mathf.Rad2Deg;
                float angleStep = angularSpeedDeg * dt;

                for (int i = 0; i < _wheelTransforms.Count; i++)
                {
                    if (_wheelTransforms[i] != null)
                    {
                        _wheelTransforms[i].Rotate(Vector3.right, angleStep, Space.Self);
                    }
                }
            }

            if (_enableEngineRumble)
            {
                float rumbleOffset = Mathf.Sin((Time.time * _rumbleFrequency) + _rumbleSeed) * _rumbleAmplitude;
                Vector3 currentPos = transform.position;
                currentPos.y = _baseY + rumbleOffset;
                transform.position = currentPos;
            }

            if (transform.position.x <= _despawnXThreshold)
            {
                Destroy(gameObject);
            }
        }

        public override void OnHitPlayer(GameObject player)
        {
            if (_isDeflectedByShield || IsShieldDeflected) return;

            AudioManager.Instance?.PlaySFX(SFXType.HitCarCrash);

            Collider[] colliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null) colliders[i].enabled = false;
            }

            _drivingSpeed = 0f;
            _isPinnedToPlayer = true;
            _pinnedPlayerTransform = player != null ? player.transform : null;
            if (_pinnedPlayerTransform != null)
            {
                _pinnedOffsetX = Mathf.Clamp(transform.position.x - _pinnedPlayerTransform.position.x, 1.6f, 2.8f);
            }

            Destroy(gameObject, _reverseWorldDuration);
        }

        /// <summary>
        /// Called when the vehicle collides with runner's active shield.
        /// Vehicle is deflected away from the road without damaging the runner.
        /// </summary>
        public void DeflectByShield(Vector3 runnerPosition)
        {
            if (_isDeflectedByShield) return;
            _isDeflectedByShield = true;
            IsShieldDeflected = true;
            _isPinnedToPlayer = false;
            _drivingSpeed = 0f;

            // Disable world movement script to let physics trajectory take over
            var mover = GetComponent<MovingWorldObject>() ?? GetComponentInParent<MovingWorldObject>();
            if (mover != null) mover.enabled = false;

            // Disable all colliders to prevent interfering with runner
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null) colliders[i].enabled = false;
            }

            if (transform.root != null && transform.root != transform)
            {
                Collider[] rootColliders = transform.root.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < rootColliders.Length; i++)
                {
                    if (rootColliders[i] != null) rootColliders[i].enabled = false;
                }
            }

            if (_rb != null)
            {
                _rb.isKinematic = true;
                _rb.detectCollisions = false;
            }

            StartCoroutine(DeflectFlyRoutine(runnerPosition));
        }

        private IEnumerator DeflectFlyRoutine(Vector3 runnerPosition)
        {
            // Determine deflection side: left (+Z) or right (-Z)
            float sideZ = (transform.position.z >= runnerPosition.z) ? 1f : -1f;
            if (Mathf.Abs(transform.position.z - runnerPosition.z) < 0.2f)
            {
                sideZ = Random.value > 0.5f ? 1f : -1f;
            }

            float vx = 8f;            // Forward impulse velocity
            float vy = 15f;           // Upward launch velocity
            float vz = sideZ * 18f;   // Lateral deflection velocity
            float gravity = -28f;
            Vector3 rotAxis = new Vector3(Random.Range(240f, 420f), Random.Range(100f, 250f), sideZ * Random.Range(300f, 520f));

            Debug.Log($"[DrivingObstacleCar] Vehicle {gameObject.name} deflected {(sideZ > 0 ? "LEFT" : "RIGHT")} by shield!");

            float elapsed = 0f;
            float duration = 1.8f;

            while (elapsed < duration)
            {
                float dt = Time.deltaTime;
                elapsed += dt;

                vy += gravity * dt;
                Vector3 moveDelta = new Vector3(vx, vy, vz) * dt;
                transform.position += moveDelta;
                if (_rb != null) _rb.position = transform.position;

                transform.Rotate(rotAxis * dt, Space.World);

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
