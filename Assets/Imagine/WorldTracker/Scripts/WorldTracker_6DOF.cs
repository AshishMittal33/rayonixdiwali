using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using UnityEngine;
using System.Runtime.InteropServices;

namespace Imagine.WebAR
{
    [System.Serializable]
    public class Settings6DOF
    {
        //public bool repositionOnTouch = false;

        public enum DepthMode {SCALE_AS_DEPTH, Z_AS_DEPTH_EXPERIMENTAL};
        public DepthMode depthMode = DepthMode.SCALE_AS_DEPTH;

       //public Vector3 camOffset;

       [Range(300, 600)] public int maxPixels = 450;
       [Range(50, 200)] public int maxPoints = 120;

    }

    public partial class WorldTracker
    {
        [DllImport("__Internal")] private static extern void WebGLPlaceOrigin(string camPosStr);
        [DllImport("__Internal")] private static extern void WebGLSyncSSPos(string vStr);



        void Awake_6DOF()
        {

        }

        void Start_6DOF()
        {
            var camPos = new Vector3(origPos.x, 0, origPos.z);
            var startZ = (mainObject.transform.position - camPos).magnitude;

            var dmode = "";
            if(s6dof.depthMode == Settings6DOF.DepthMode.SCALE_AS_DEPTH)
                dmode = "SCALE";
            else if(s6dof.depthMode == Settings6DOF.DepthMode.Z_AS_DEPTH_EXPERIMENTAL)
                dmode = "Z";

            var json = "{";
            json += "\"MODE\":\"3DOF\","; //<--3DOF for placement mode only
            json += "\"DEPTHMODE\":\"" + dmode + "\",";
            json += "\"START_Z\":" + startZ.ToStringInvariantCulture() + ",";
            json += "\"CAM_START_HEIGHT\":" + cameraStartHeight.ToStringInvariantCulture() + ",";
            json += "\"ARM_LENGTH\":" + s3dof.armLength.ToStringInvariantCulture() + ",";
            json += "\"MAX_PIXELS\":" + s6dof.maxPixels + ",";
            json += "\"MAX_POINTS\":" + s6dof.maxPoints + ",";

            json += "\"USE_COMPASS\":" + (useCompass?"true":"false");

            json += "}";
#if !UNITY_EDITOR && UNITY_WEBGL
            SetWebGLwTrackerSettings(json);
#endif

        }

        public void Update_6DOF()
        {
           if (usePlacementIndicator && !placementIndicatorSettings.placed)
            {
                Update_3DOF();
            }
        }

        void UpdateCameraTransform_6DOF(string data)
        {
            //Debug.Log(data);

            if (usePlacementIndicator && !placementIndicatorSettings.placed)
            {
                UpdateCameraTransform_3DOF(data);
            }
            else
            {
                var vals = data.Split(new string[]{","}, System.StringSplitOptions.RemoveEmptyEntries);

                var w = float.Parse(vals[0], CultureInfo.InvariantCulture);
                var i = float.Parse(vals[1], CultureInfo.InvariantCulture);
                var j = float.Parse(vals[2], CultureInfo.InvariantCulture);
                var k = float.Parse(vals[3], CultureInfo.InvariantCulture);
                var x = float.Parse(vals[4], CultureInfo.InvariantCulture);
                var y = float.Parse(vals[5], CultureInfo.InvariantCulture);
                var z = float.Parse(vals[6], CultureInfo.InvariantCulture);
                var s = float.Parse(vals[7], CultureInfo.InvariantCulture);
                var c = float.Parse(vals[8], CultureInfo.InvariantCulture); //TODO: use confidence to swap between 3DOF and 6DOF tracking
                
                trackerCamera.transform.position = new Vector3(x, y, z);
                trackerCamera.transform.rotation = new Quaternion(i, j, k, w);
                mainObject.transform.localScale = s * Vector3.one; //TODO: origScale
            }

        }

        void Place_6DOF(){

            StartCoroutine("SyncScreenSpaceRoutine");

            mainObject.transform.position = Vector3.zero;
            var startCamPos = new Vector3(origPos.x, 0, origPos.z);
            trackerCamera.transform.position = startCamPos;

#if UNITY_WEBGL && !UNITY_EDITOR
            SetWebGLwTrackerSettings("{\"MODE\":\"6DOF\"}");
            var camPosStr = startCamPos.x + "," + startCamPos.y + "," + startCamPos.z;
            WebGLPlaceOrigin(camPosStr);     
#endif
        }

        void Reset_6DOF()
        {
            StopCoroutine("SyncScreenSpaceRoutine");
#if UNITY_WEBGL && !UNITY_EDITOR
            SetWebGLwTrackerSettings("{\"MODE\":\"3DOF\"}");
#endif
        }

        IEnumerator SyncScreenSpaceRoutine()
        {
            while (true)
            {
                SyncScreenSpacePosition();
                yield return new WaitForEndOfFrame();
                //yield return new WaitForSeconds(0.2f);
            }
        }

        private void SyncScreenSpacePosition()
        {
            var ssPos = trackerCamera.WorldToScreenPoint(mainObject.transform.position);
            ssPos.x /= Screen.width;
            ssPos.y /= Screen.height;
            //Debug.Log("ssPos = " + ssPos);
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLSyncSSPos(ssPos.x.ToStringInvariantCulture() + "," + ssPos.y.ToStringInvariantCulture());
#endif

        }
    }

    // public static class FloatExtensions
    // {
    //     //this is needed to properly convert floating point strings for some languages to JSON
    //     public static string ToStringInvariantCulture(this float f)
    //     {
    //         return f.ToString(System.Globalization.CultureInfo.InvariantCulture);
    //     }
    // }
}
