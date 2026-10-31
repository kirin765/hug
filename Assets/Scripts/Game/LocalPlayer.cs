using UnityEngine;

namespace Hug
{
    /// <summary>Player-driven avatar: camera-relative movement, dash and hug pose.</summary>
    public class LocalPlayer : MonoBehaviour
    {
        public PlayerVisual Visual;
        public int Id;
        public string PlayerName;
        public int Score;

        public float Speed = Proto.Speed;
        public float Radius = Proto.ArenaRadius;

        Vector3 _velocity;
        float _yaw;
        float _hugUntil;
        float _hugCooldownUntil;

        float _dashUntil;
        float _dashReadyAt;
        Vector3 _dashDir;
        Vector3 _lastDashFrom;

        public float Yaw => _yaw;
        public bool IsHugging => Time.time < _hugUntil;
        public bool CanHug => Time.time >= _hugCooldownUntil;
        public bool IsDashing => Time.time < _dashUntil;
        public bool DashReady => Time.time >= _dashReadyAt;
        public float DashCharge => Mathf.Clamp01(1f - (_dashReadyAt - Time.time) / Proto.DashCooldown);
        public Vector3 LastDashFrom => _lastDashFrom;

        public void Init(int id, string playerName, Color color, Vector3 position)
        {
            Id = id;
            PlayerName = playerName;
            transform.position = position;
            gameObject.name = $"LocalPlayer_{playerName}";

            Visual = gameObject.AddComponent<PlayerVisual>();
            Visual.Build(color, playerName);
            Visual.SetName(playerName);
        }

        public void Tick(Vector2 move, Vector3 cameraForward, Vector3 cameraRight, float dt)
        {
            Vector3 wish = cameraForward * move.y + cameraRight * move.x;
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            if (IsDashing)
            {
                _velocity = _dashDir * Proto.DashSpeed;
            }
            else
            {
                Vector3 targetVelocity = wish * Speed;
                float accel = wish.sqrMagnitude > 0.01f ? 48f : 30f;
                _velocity = Vector3.MoveTowards(_velocity, targetVelocity, accel * dt);
            }

            Vector3 next = transform.position + _velocity * dt;

            // Stay on the island.
            float maxRadius = Radius - 0.7f;
            Vector2 flat = new Vector2(next.x, next.z);
            if (flat.magnitude > maxRadius)
            {
                flat = flat.normalized * maxRadius;
                next.x = flat.x;
                next.z = flat.y;
                _velocity *= 0.4f;
            }

            transform.position = next;

            if (IsDashing)
            {
                _yaw = Mathf.Atan2(_dashDir.x, _dashDir.z) * Mathf.Rad2Deg;
            }
            else if (wish.sqrMagnitude > 0.01f)
            {
                float targetYaw = Mathf.Atan2(wish.x, wish.z) * Mathf.Rad2Deg;
                _yaw = Mathf.MoveTowardsAngle(_yaw, targetYaw, 900f * dt);
            }

            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            Visual.SetWalk(IsDashing ? 1f : _velocity.magnitude / Speed);
            Visual.SetHugging(IsHugging);
        }

        /// <summary>Short burst of speed in the given direction. Returns false if cooling down.</summary>
        public bool TryDash(Vector3 direction)
        {
            if (!DashReady) return false;

            _dashDir = direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;
            _dashDir.y = 0f;
            _dashDir.Normalize();

            _lastDashFrom = transform.position;
            _dashUntil = Time.time + Proto.DashDuration;
            _dashReadyAt = Time.time + Proto.DashCooldown;
            return true;
        }

        public void FaceTowards(Vector3 point, float dt)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.001f) return;
            float targetYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            _yaw = Mathf.MoveTowardsAngle(_yaw, targetYaw, 720f * dt);
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        public void PlayHug(Vector3 towards)
        {
            _hugUntil = Time.time + 0.95f;
            _hugCooldownUntil = Time.time + 0.7f;
            FaceTowards(towards, 1f);
            Visual.SetHugging(true);
        }
    }
}
