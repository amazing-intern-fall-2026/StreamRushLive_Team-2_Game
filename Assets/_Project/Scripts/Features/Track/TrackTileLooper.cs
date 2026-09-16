using System;
using UnityEngine;
using SteamRush.Track;

namespace SteamRush.Track
{
    public class TrackTileLooper : MonoBehaviour
    {
        [SerializeField] private Transform[] _tiles;

        [SerializeField, Tooltip("Tile mesh length in world units.")]
        private float _tileLengthInWorldUnits = 10f;

        [SerializeField] private WorldSpeedManager _speedManager;

        public float WorldSpeed
        {
            get => _speedManager != null ? _speedManager.CurrentSpeed : 0f;
            set { if (_speedManager != null) _speedManager.CurrentSpeed = value; }
        }

        [SerializeField, Tooltip("X position threshold to recycle tile forward.")]
        private float _recycleXThreshold = -15f;

        [SerializeField, Tooltip("Optional tracker to record traveled world distance.")]
        private TrackProgressTracker _progressTracker;


        private void Awake()
        {
            if (_tiles == null || _tiles.Length == 0)
            {
                Debug.LogError("TrackTileLooper: no tiles assigned.", this);
                return;
            }

            for (int i = 0; i < _tiles.Length; i++)
            {
                if (_tiles[i] == null)
                {
                    Debug.LogError($"TrackTileLooper: tile at index {i} is not assigned.", this);
                }
            }

            Array.Sort(_tiles, (a, b) => a.position.x.CompareTo(b.position.x));
        }

        private void Update()
        {
            if (_tiles == null || _tiles.Length == 0)
            {
                return;
            }

           float step = WorldSpeed * Time.deltaTime;
            Vector3 delta = Vector3.left * step;

            for (int i = 0; i < _tiles.Length; i++)
            {
                _tiles[i].position += delta;
            }

            while (_tiles[0].position.x <= _recycleXThreshold)
            {
                RecycleFirstTile();
            }

            _progressTracker?.AddDistance(step);
        }

        private void RecycleFirstTile()
        {
            Transform firstTile = _tiles[0];
            float furthestX = firstTile.position.x;

            for (int i = 1; i < _tiles.Length; i++)
            {
                if (_tiles[i].position.x > furthestX)
                {
                    furthestX = _tiles[i].position.x;
                }
            }

            firstTile.position = new Vector3(furthestX + _tileLengthInWorldUnits, firstTile.position.y, firstTile.position.z);

            for (int i = 1; i < _tiles.Length; i++)
            {
                _tiles[i - 1] = _tiles[i];
            }

            _tiles[_tiles.Length - 1] = firstTile;
        }
    }
}