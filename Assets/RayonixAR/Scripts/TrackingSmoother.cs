using UnityEngine;
using Imagine.WebAR;

namespace RayonixAR
{
    /// <summary>
    /// Smooths the raw 6DOF camera pose and the depth scale the Imagine tracker writes every frame,
    /// so the placed object stops jittering. Runs only after the object has been placed; the
    /// placement phase already has the plugin's own smoothing.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class TrackingSmoother : MonoBehaviour
    {
        [SerializeField] private WorldTracker tracker;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Transform scaledObject;

        [Tooltip("Higher = snappier, lower = smoother. Roughly the number of corrections per second.")]
        [SerializeField, Range(1f, 60f)] private float positionSmoothing = 14f;
        [SerializeField, Range(1f, 60f)] private float rotationSmoothing = 18f;
        [SerializeField, Range(1f, 60f)] private float scaleSmoothing = 10f;

        private Vector3 pos;
        private Quaternion rot;
        private Vector3 scale;
        private bool primed;

        private void LateUpdate()
        {
            if (tracker == null || cameraTransform == null || !tracker.IsPlaced)
            {
                primed = false;
                return;
            }

            // Whatever is on the transforms now is the latest raw value from the tracker
            // (or our own smoothed value, if no update arrived this frame).
            var rawPos = cameraTransform.position;
            var rawRot = cameraTransform.rotation;
            var rawScale = scaledObject != null ? scaledObject.localScale : Vector3.one;

            if (!primed)
            {
                pos = rawPos;
                rot = rawRot;
                scale = rawScale;
                primed = true;
                return;
            }

            float dt = Time.deltaTime;
            pos = Vector3.Lerp(pos, rawPos, 1f - Mathf.Exp(-positionSmoothing * dt));
            rot = Quaternion.Slerp(rot, rawRot, 1f - Mathf.Exp(-rotationSmoothing * dt));
            scale = Vector3.Lerp(scale, rawScale, 1f - Mathf.Exp(-scaleSmoothing * dt));

            cameraTransform.SetPositionAndRotation(pos, rot);
            if (scaledObject != null)
                scaledObject.localScale = scale;
        }
    }
}
