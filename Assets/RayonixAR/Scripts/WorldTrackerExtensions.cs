using System.Globalization;
using UnityEngine;

namespace Imagine.WebAR
{
    /// <summary>
    /// Rayonix additions to the Imagine WorldTracker. This is a partial-class extension so the plugin's
    /// own files stay untouched and can be updated safely.
    /// </summary>
    public partial class WorldTracker
    {
        [Header("Rayonix")]
        [Tooltip("In 6DOF mode, place the object at the placement arrow's real distance instead of the fixed start distance.")]
        [SerializeField] public bool placeAtIndicatorDistance = true;

        /// <summary>True once the user has placed the object (6DOF tracking phase).</summary>
        public bool IsPlaced
        {
            get { return !usePlacementIndicator || placementIndicatorSettings.placed; }
        }

        public Camera TrackerCamera
        {
            get { return trackerCamera; }
        }

        /// <summary>
        /// Limits how near and far the placement arrow can be, in metres along the floor. A larger
        /// minimum stops the arrow from sitting under the user's feet when the device points down.
        /// </summary>
        public void SetPlacementRange(float minZ, float maxZ)
        {
            placementIndicatorSettings.minZ = minZ;
            placementIndicatorSettings.maxZ = Mathf.Max(minZ, maxZ);
        }

        /// <summary>
        /// Same as PlaceOrigin, but first tells the tracker how far away the placement arrow is, so the
        /// object appears on the floor where the user pointed rather than a fixed distance ahead.
        /// </summary>
        public void PlaceOriginAtIndicator()
        {
            var ps = placementIndicatorSettings;
            bool canUseIndicator = mode == TrackingMode.MODE_6DOF
                                   && placeAtIndicatorDistance
                                   && usePlacementIndicator
                                   && !ps.placed
                                   && ps.placementIndicator != null
                                   && ps.placementIndicator.activeSelf;

            if (canUseIndicator)
            {
                var cam = trackerCamera.transform.position;
                var ind = ps.placementIndicator.transform.position;
                float d = Vector2.Distance(new Vector2(cam.x, cam.z), new Vector2(ind.x, ind.z));
                d = Mathf.Clamp(d, ps.minZ, ps.maxZ);

                // Place_6DOF derives the start camera position from origPos, and the JS side uses START_Z
                // as its depth baseline, so both must describe the same distance.
                origPos = new Vector3(0f, cameraStartHeight, -d);
#if UNITY_WEBGL && !UNITY_EDITOR
                SetWebGLwTrackerSettings("{\"START_Z\":" + d.ToString(CultureInfo.InvariantCulture) + "}");
#endif
                Debug.Log("[RayonixAR] Placing object " + d.ToString("F2", CultureInfo.InvariantCulture) + " m from the camera");
            }

            PlaceOrigin();
        }
    }
}
