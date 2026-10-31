using UnityEngine;

namespace Hug
{
    /// <summary>Another player's avatar, smoothly interpolated between snapshots.</summary>
    public class RemotePlayer : MonoBehaviour
    {
        public int Id;
        public string PlayerName;
        public int Score;
        public PlayerVisual Visual;

        Vector3 _targetPosition;
        float _targetYaw;
        bool _hasTarget;
        bool _hugging;
        float _hugAnimUntil;
        float _lastSeen;

        public float LastSeen => _lastSeen;
        public bool HasTarget => _hasTarget;
        public Vector3 TargetPosition => _targetPosition;

        public void Init(int id, string playerName, Color color, Vector3 position)
        {
            Id = id;
            PlayerName = playerName;
            transform.position = position;
            _targetPosition = position;
            _hasTarget = true;
            _lastSeen = Time.time;
            gameObject.name = $"Player_{playerName}";

            Visual = gameObject.AddComponent<PlayerVisual>();
            Visual.Build(color, playerName);
        }

        public void Apply(PlayerState state)
        {
            Score = state.s;
            _lastSeen = Time.time;
            _targetPosition = new Vector3(state.x, 0f, state.z);
            _targetYaw = state.r;
            _hasTarget = true;

            if (!string.Equals(PlayerName, state.n))
            {
                PlayerName = state.n;
                Visual.SetName(state.n);
            }

            _hugging = state.h == 1;
            if (_hugging) _hugAnimUntil = Time.time + 0.2f;
        }

        public void PlayHug()
        {
            _hugging = true;
            _hugAnimUntil = Time.time + 0.95f;
        }

        public void LookAt(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 0f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            bool hugging = _hugging || Time.time < _hugAnimUntil;

            if (_hasTarget)
            {
                // Snappy but smooth: exponential smoothing is frame-rate independent.
                float k = 1f - Mathf.Exp(-14f * dt);
                Vector3 before = transform.position;
                transform.position = Vector3.Lerp(before, _targetPosition, k);
                if ((_targetPosition - before).sqrMagnitude > 64f)
                {
                    transform.position = _targetPosition; // teleport / respawn
                }

                float yaw = Mathf.LerpAngle(transform.rotation.eulerAngles.y, _targetYaw, k);
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);

                float speed = (transform.position - before).magnitude / Mathf.Max(dt, 0.0001f);
                Visual.SetWalk(Mathf.Clamp01(speed / Proto.Speed));
            }

            Visual.SetHugging(hugging);
        }
    }
}
