using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SpaceApps
{
    /// <summary>Cámara orbital: clic derecho (o central) + arrastrar para girar, rueda para acercar.</summary>
    public class OrbitCamera : MonoBehaviour
    {
        public Vector3 Target = new Vector3(0, 1.2f, 0);
        public float Distance = 5.6f, Yaw = 12f, Pitch = 6f;
        public bool BlockInput;   // la UI lo activa cuando el mouse está sobre un panel

        void LateUpdate()
        {
            if (!BlockInput)
            {
                ReadInput(out bool drag, out Vector2 delta, out float wheel);
                if (drag) { Yaw += delta.x * 0.25f; Pitch = Mathf.Clamp(Pitch - delta.y * 0.25f, -80f, 80f); }
                if (Mathf.Abs(wheel) > 0.001f) Distance = Mathf.Clamp(Distance * (wheel > 0 ? 0.9f : 1.1f), 1.5f, 150f);
            }
            var rot = Quaternion.Euler(Pitch, Yaw, 0f);
            transform.position = Target + rot * new Vector3(0, 0, -Distance);
            transform.rotation = Quaternion.LookRotation(Target - transform.position, Vector3.up);
        }

        static void ReadInput(out bool drag, out Vector2 delta, out float wheel)
        {
            drag = false; delta = Vector2.zero; wheel = 0f;
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            if (m == null) return;
            drag = m.rightButton.isPressed || m.middleButton.isPressed;
            delta = m.delta.ReadValue() * 0.5f;
            wheel = m.scroll.ReadValue().y;
#elif ENABLE_LEGACY_INPUT_MANAGER
            drag = Input.GetMouseButton(1) || Input.GetMouseButton(2);
            delta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 10f;
            wheel = Input.mouseScrollDelta.y;
#endif
        }

        /// <summary>Clic izquierdo de este cuadro (posición en píxeles de pantalla).</summary>
        public static bool LeftClicked(out Vector2 pos)
        {
            pos = default;
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            if (m == null || !m.leftButton.wasPressedThisFrame) return false;
            pos = m.position.ReadValue();
            return true;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (!Input.GetMouseButtonDown(0)) return false;
            pos = Input.mousePosition;
            return true;
#else
            return false;
#endif
        }

        public static bool SpacePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Space);
#else
            return false;
#endif
        }
    }
}
