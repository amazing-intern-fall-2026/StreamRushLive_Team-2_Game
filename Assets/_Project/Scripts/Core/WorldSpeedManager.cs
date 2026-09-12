using UnityEngine;

namespace SteamRush.Track
{
    public class WorldSpeedManager : MonoBehaviour
    {
        [SerializeField] private float _worldSpeed = 5f;

        public float CurrentSpeed
        {
            get => _worldSpeed;
            set => _worldSpeed = Mathf.Max(0f, value);
        }
    }
}