using System.Collections.Generic;
using UnityEngine;

namespace Hug
{
    /// <summary>Third-person follow camera with drag-to-orbit and scroll zoom.</summary>
    public class CameraRig : MonoBehaviour
    {
        public Transform Target;
        public float Distance = 9.5f;
        public float Pitch = 50f;
        public float Yaw = 0f;
        public float MinDistance = 7f;
        public float MaxDistance = 17f;
        public float FocusHeight = 1.1f;

        Vector3 _smoothed;
        bool _snapped;
        Camera _camera;
        float _baseFov = 50f;
        float _punch;

        void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_camera != null) _baseFov = _camera.fieldOfView;
        }

        /// <summary>Quick FOV kick, used by the dash.</summary>
        public void Punch(float degrees)
        {
            _punch = Mathf.Max(_punch, degrees);
        }

        public Vector3 Forward
        {
            get
            {
                Vector3 f = Quaternion.Euler(0f, Yaw, 0f) * Vector3.forward;
                f.y = 0f;
                return f.normalized;
            }
        }

        public Vector3 Right
        {
            get
            {
                Vector3 r = Quaternion.Euler(0f, Yaw, 0f) * Vector3.right;
                r.y = 0f;
                return r.normalized;
            }
        }

        public void Orbit(float deltaX)
        {
            Yaw += deltaX * 0.28f;
        }

        public void Zoom(float amount)
        {
            Distance = Mathf.Clamp(Distance - amount * 0.02f, MinDistance, MaxDistance);
        }

        public void Snap()
        {
            _snapped = false;
        }

        void LateUpdate()
        {
            if (Target == null)
            {
                // Menu shot: slowly orbit the island while waiting for a player.
                Yaw += 3.5f * Time.deltaTime;
                Vector3 menuFocus = new Vector3(0f, 2f, 0f);
                Quaternion menuRot = Quaternion.Euler(34f, Yaw, 0f);
                Vector3 menuPos = menuFocus + menuRot * new Vector3(0f, 0f, -62f);
                transform.position = menuPos;
                transform.rotation = Quaternion.LookRotation(menuFocus - menuPos, Vector3.up);
                _snapped = false;
                return;
            }

            Quaternion rotation = Quaternion.Euler(Pitch, Yaw, 0f);
            Vector3 focus = Target.position + Vector3.up * FocusHeight;
            Vector3 desired = focus + rotation * new Vector3(0f, 0f, -Distance);

            if (!_snapped)
            {
                _smoothed = desired;
                _snapped = true;
            }

            _smoothed = Vector3.Lerp(_smoothed, desired, 1f - Mathf.Exp(-9f * Time.deltaTime));
            transform.position = _smoothed;
            transform.rotation = Quaternion.LookRotation(focus - _smoothed, Vector3.up);

            if (_camera != null)
            {
                _punch = Mathf.Lerp(_punch, 0f, 1f - Mathf.Exp(-7f * Time.deltaTime));
                _camera.fieldOfView = _baseFov + _punch;
            }
        }
    }
}
