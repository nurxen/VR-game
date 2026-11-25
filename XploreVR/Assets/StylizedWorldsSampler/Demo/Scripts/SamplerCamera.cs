using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StylizedWorldsSampler.Demo
{
    /// <summary>
    /// Demo camera for the free sampler. It glides slowly round the village by itself; move the mouse with the right
    /// button held to look, WASD to fly (Shift faster, Q/E down and up). After 20 seconds without input it goes back to
    /// gliding. The doors open as the camera comes near (StructureDoor uses the main camera when there is no Player).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class SamplerCamera : MonoBehaviour
    {
        [Tooltip("The point the camera glides round.")] public Vector3 centre = new Vector3(0f, 2f, 0f);
        public float glideRadius = 26f, glideHeight = 7f;
        [Tooltip("Degrees per second round the centre.")] public float glideSpeed = 4f;
        public float flySpeed = 6f, lookSpeed = 0.15f;
        [Tooltip("Seconds without input before the camera glides again.")] public float idleToGlide = 20f;
        [Tooltip("Keep the camera this far above the ground (raycast down).")] public float minHeight = 1.6f;

        float angle, yaw, pitch, idle = 999f;

        void Start()
        {
            var e = transform.eulerAngles;
            yaw = e.y;
            pitch = e.x > 180f ? e.x - 360f : e.x;
            angle = Mathf.Atan2(transform.position.x - centre.x, transform.position.z - centre.z) * Mathf.Rad2Deg;
        }

        void Update()
        {
            Vector2 look = Vector2.zero;
            Vector3 move = Vector3.zero;
            bool fast = false;
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (mouse != null && mouse.rightButton.isPressed) look = mouse.delta.ReadValue();
            if (kb != null)
            {
                move = new Vector3((kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0),
                                   (kb.eKey.isPressed ? 1 : 0) - (kb.qKey.isPressed ? 1 : 0),
                                   (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0));
                fast = kb.leftShiftKey.isPressed;
            }
#else
            if (Input.GetMouseButton(1)) look = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 10f;
            move = new Vector3(Input.GetAxisRaw("Horizontal"),
                               (Input.GetKey(KeyCode.E) ? 1 : 0) - (Input.GetKey(KeyCode.Q) ? 1 : 0),
                               Input.GetAxisRaw("Vertical"));
            fast = Input.GetKey(KeyCode.LeftShift);
#endif
            if (look.sqrMagnitude > 0f || move.sqrMagnitude > 0f) idle = 0f;
            else idle += Time.deltaTime;

            if (idle >= idleToGlide) Glide();
            else
            {
                yaw += look.x * lookSpeed;
                pitch = Mathf.Clamp(pitch - look.y * lookSpeed, -80f, 80f);
                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
                transform.position += transform.rotation * move * flySpeed * (fast ? 3f : 1f) * Time.deltaTime;
            }
            KeepAboveGround();
        }

        void Glide()
        {
            angle += glideSpeed * Time.deltaTime;
            var a = angle * Mathf.Deg2Rad;
            var target = centre + new Vector3(Mathf.Sin(a) * glideRadius, glideHeight, Mathf.Cos(a) * glideRadius);
            transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-Time.deltaTime * 1.5f));
            var rot = Quaternion.LookRotation(centre - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, 1f - Mathf.Exp(-Time.deltaTime * 2f));
            var e = transform.eulerAngles;
            yaw = e.y;
            pitch = e.x > 180f ? e.x - 360f : e.x;
        }

        void KeepAboveGround()
        {
            var p = transform.position;
            if (Physics.Raycast(p + Vector3.up * 200f, Vector3.down, out var hit, 400f, ~0, QueryTriggerInteraction.Ignore)
                && p.y < hit.point.y + minHeight)
                transform.position = new Vector3(p.x, hit.point.y + minHeight, p.z);
        }
    }
}
