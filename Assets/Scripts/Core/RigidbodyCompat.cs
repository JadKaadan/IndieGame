using UnityEngine;

namespace IndieGame.Core
{
    /// <summary>
    /// Unity 6 renamed <c>Rigidbody.velocity</c> to <c>Rigidbody.linearVelocity</c>
    /// and <c>drag</c>/<c>angularDrag</c> to <c>linearDamping</c>/<c>angularDamping</c>.
    /// All simulation code goes through these helpers so the project compiles on
    /// Unity 2022 LTS and Unity 6 without edits.
    /// </summary>
    public static class RigidbodyCompat
    {
        public static Vector3 GetLinearVelocity(this Rigidbody body)
        {
#if UNITY_6000_0_OR_NEWER
            return body.linearVelocity;
#else
            return body.velocity;
#endif
        }

        public static void SetLinearVelocity(this Rigidbody body, Vector3 value)
        {
#if UNITY_6000_0_OR_NEWER
            body.linearVelocity = value;
#else
            body.velocity = value;
#endif
        }

        public static void SetLinearDamping(this Rigidbody body, float value)
        {
#if UNITY_6000_0_OR_NEWER
            body.linearDamping = value;
#else
            body.drag = value;
#endif
        }

        public static void SetAngularDamping(this Rigidbody body, float value)
        {
#if UNITY_6000_0_OR_NEWER
            body.angularDamping = value;
#else
            body.angularDrag = value;
#endif
        }
    }
}
