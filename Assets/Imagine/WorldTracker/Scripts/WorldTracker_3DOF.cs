using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System.Runtime.InteropServices;
using System.Globalization;


namespace Imagine.WebAR
{
    [System.Serializable]
    public class Settings3DOF
    {
        public float armLength = 0.4f;
        public bool useExtraSmoothing = false;
        [Range(1,50)] public float smoothenFactor = 10;
    }

    public partial class WorldTracker
    {
        private Vector3 targetPos3DOF;
        private Quaternion targetRot3DOF;

        void Awake_3DOF()
        {
            
        }

        void Start_3DOF()
        {            
            
            Input.multiTouchEnabled = true;

            var json = "{";
            json += "\"MODE\":\"3DOF\",";
            json += "\"CAM_START_HEIGHT\":" + cameraStartHeight.ToStringInvariantCulture() + ",";
            json += "\"ARM_LENGTH\":" + s3dof.armLength.ToStringInvariantCulture() + ",";
            json += "\"USE_COMPASS\":" + (useCompass?"true":"false");

            json += "}";
#if !UNITY_EDITOR && UNITY_WEBGL
            SetWebGLwTrackerSettings(json);
#endif        
        }

        public void Update_3DOF()
        {
            if(s3dof.useExtraSmoothing){
                trackerCamera.transform.position = Vector3.Lerp(trackerCamera.transform.position, targetPos3DOF, Time.deltaTime * s3dof.smoothenFactor);
                trackerCamera.transform.rotation = Quaternion.Slerp(trackerCamera.transform.rotation, targetRot3DOF, Time.deltaTime * s3dof.smoothenFactor);
                //Debug.Log("Update Orbit Mode: " + targetPos3DOF + " " + targetRot3DOF);
            }   
        }


        void UpdateCameraTransform_3DOF(string data)
        {
            var vals = data.Split(new string[] { "," }, System.StringSplitOptions.RemoveEmptyEntries);

            var w = float.Parse(vals[0], CultureInfo.InvariantCulture);
            var i = float.Parse(vals[1], CultureInfo.InvariantCulture);
            var j = float.Parse(vals[2], CultureInfo.InvariantCulture);
            var k = float.Parse(vals[3], CultureInfo.InvariantCulture);
            var x = float.Parse(vals[4], CultureInfo.InvariantCulture);
            var y = float.Parse(vals[5], CultureInfo.InvariantCulture);
            var z = float.Parse(vals[6], CultureInfo.InvariantCulture);
            var s = float.Parse(vals[7], CultureInfo.InvariantCulture);
            var c = float.Parse(vals[8], CultureInfo.InvariantCulture);

            var pos = new Vector3(x, y, z);
            var rot = new Quaternion(i, j, k, w);

            if(!s3dof.useExtraSmoothing){
                trackerCamera.transform.position = pos;
                trackerCamera.transform.rotation = rot;
            }    
            else{
                targetPos3DOF = pos;
                targetRot3DOF = rot;
            }
            
        }

        void Place_3DOF(Vector3 pos){
            mainObject.transform.position = pos;
        }

        void Reset_3DOF()
        {
            
        }
    }
}
