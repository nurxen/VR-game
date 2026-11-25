using UnityEngine;

namespace StylizedWorldsSampler
{
    /// <summary>
    /// A door that swings open on its hinge (the object's origin). It opens by itself when the player (or the main
    /// camera, when no target is set) comes near, or from your own code with Open(), Close() and Toggle().
    /// </summary>
    [DisallowMultipleComponent]
    public class StructureDoor : MonoBehaviour
    {
        [Tooltip("How far the door swings, in degrees. Negative swings it the other way.")]
        public float openAngle = 100f;
        [Tooltip("Opening speed: 1 = one second from shut to open.")]
        [Min(0.1f)] public float speed = 1.6f;
        [Tooltip("Open by itself when the target comes within Near Distance.")]
        public bool openWhenNear = true;
        [Min(0.1f)] public float nearDistance = 2.4f;
        [Tooltip("Who opens the door when Open When Near is on. Empty: the object tagged Player, else the main camera.")]
        public Transform target;
        [Tooltip("Open now (used when Open When Near is off).")]
        public bool isOpen;

        Quaternion shut;
        float amount;

        void Awake()
        {
            shut = transform.localRotation;
            if (target == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null) target = player.transform;
            }
        }

        void Update()
        {
            if (openWhenNear)
            {
                var t = target != null ? target : Camera.main != null ? Camera.main.transform : null;
                if (t != null) isOpen = (t.position - transform.position).sqrMagnitude < nearDistance * nearDistance;
            }
            amount = Mathf.MoveTowards(amount, isOpen ? 1f : 0f, Time.deltaTime * speed);
            float eased = amount * amount * (3f - 2f * amount);
            transform.localRotation = shut * Quaternion.AngleAxis(openAngle * eased, Vector3.up);
        }

        public void Open() => isOpen = true;
        public void Close() => isOpen = false;
        public void Toggle() => isOpen = !isOpen;
    }
}
