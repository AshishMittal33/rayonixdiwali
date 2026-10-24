using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System.Runtime.InteropServices;

#if IMAGINE_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace Imagine.WebAR
{
    [RequireComponent(typeof(Camera))]
    public class ARCamera : MonoBehaviour//
    {
        [DllImport("__Internal")] private static extern void WebGLStartCamera();
        [DllImport("__Internal")] private static extern bool WebGLIsCameraStarted();
        [DllImport("__Internal")] private static extern void WebGLUnpauseCamera();
        [DllImport("__Internal")] private static extern void WebGLPauseCamera();
        [DllImport("__Internal")] private static extern void WebGLGetCameraTexture(int textureId);
        [DllImport("__Internal")] private static extern string WebGLGetVideoDims();
        [DllImport("__Internal")] private static extern string WebGLSubscribeVideoTexturePtr(int textureId);
        [DllImport("__Internal")] private static extern string WebGLUseUnityVideoPlane(bool b);

        
         public enum VideoPlaneMode {
            NONE,
            TEXTURE_PTR,
        }

        [SerializeField] public VideoPlaneMode videoPlaneMode = VideoPlaneMode.TEXTURE_PTR;
        [SerializeField] private Material videoPlaneMat;
        [SerializeField] private float videoDistance = 100;
        
        private Camera cam;
        private GameObject videoBackground;
        private Texture2D videoTexture;
        private int videoTextureId;


        [Space][SerializeField] private bool unpausePauseOnEnableDisable = false;
        [SerializeField] private bool pauseOnDestroy = false;
        private bool paused = false;

        [SerializeField] private bool pauseOnApplicationLostFocus = false;
        

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        private IEnumerator Start()
        {
#if IMAGINE_URP
            //Debug.Log(GraphicsSettings.defaultRenderPipeline.GetType());
            if (GraphicsSettings.currentRenderPipeline != null &&
                 GraphicsSettings.defaultRenderPipeline.GetType().ToString().EndsWith("UniversalRenderPipelineAsset") &&
                 videoPlaneMode == VideoPlaneMode.NONE
                 )
            {
                Debug.Log("URP detected");
                cam.clearFlags = CameraClearFlags.Depth;
                cam.allowHDR = false;
                var camData = GetComponent<UniversalAdditionalCameraData>();
                camData.renderPostProcessing = false;

                Debug.Log(cam.clearFlags + " " + camData.renderPostProcessing);
            }
#endif
            StartCamera();
            yield break;
            
        }

        private void OnEnable(){
            if(unpausePauseOnEnableDisable)
                UnpauseCamera();
        }

        private void OnDisable(){
            if(unpausePauseOnEnableDisable)
                PauseCamera();
        }

        private void OnDestroy(){
            if(pauseOnDestroy)
                PauseCamera();
        }

        void StartCamera(){
#if UNITY_WEBGL && !UNITY_EDITOR
            if(WebGLIsCameraStarted()){
                Debug.Log("SetVideoDims");
                SetVideoDims();
            }
            else{
                Debug.Log("StartCamera");
                WebGLStartCamera();
            }
#endif  
        }

        void OnStartWebcamSuccess(){
            SetVideoDims();
        }

        void OnStartWebcamFail(){
            Debug.LogError("Webcam failed to start!");
        }

        void SetCameraFov(float fov)
        {
            cam.fieldOfView = fov;
            Debug.Log("SetCameraFov " + cam.fieldOfView);
        }

        public void PauseCamera()
        {
            if(paused)
                return;

            Debug.Log("Pausing Camera...");
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLPauseCamera();
#endif
            paused = true;
        }

        public void UnpauseCamera()
        {
            if(!paused)
                return;

            Debug.Log("Unpausing Camera...");
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLUnpauseCamera();
#endif
            paused = false;
        }



        void Resize(string dims)
        {

#if UNITY_WEBGL && !UNITY_EDITOR
            //this will tell arCamera in js if unity is also rendering a videoplane
            //and will avoid rendering duplicate planes, and save some fps
            WebGLUseUnityVideoPlane(videoPlaneMode != VideoPlaneMode.NONE);
#endif

            if(videoPlaneMode == VideoPlaneMode.NONE)
            {
                //we resize only when videoplane is active
                return;
            }

            var vals = dims.Split(new string[] { "," }, System.StringSplitOptions.RemoveEmptyEntries);
            var width = int.Parse(vals[0]);
            var height = int.Parse(vals[1]);
            Debug.Log("Got Video Texture Size - " + width + " x " + height);

            if(videoBackground != null){
                Destroy(videoBackground);
            }
            CreateVideoPlane(width, height);

            if(videoTexture != null)
                Destroy(videoTexture);
            
            videoTexture = new Texture2D(width, height);


            videoPlaneMat.mainTexture = videoTexture;
            videoTextureId = (int)videoTexture.GetNativeTexturePtr();

            Debug.Log("Unity WebGLSubscribeVideoTexturePtr -> " + videoTextureId);
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLSubscribeVideoTexturePtr(videoTextureId);
#endif
        }


        void CreateVideoPlane(int width, int height)
        {
            Debug.Log("Init video plane");

            videoBackground = GameObject.CreatePrimitive(PrimitiveType.Quad);
            videoBackground.name = "VideoBackground";
            videoBackground.transform.parent = transform;

            videoPlaneMat.mainTexture = null;
            videoBackground.GetComponent<Renderer>().material = videoPlaneMat;

            var ar = (float)Screen.width / (float)Screen.height;
            var v_ar = (float)width / (float)height;

            float heightScale = 1;

            if(v_ar > ar){
                Debug.Log("Bleed horizontally");
                heightScale = 2 * videoDistance * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad / 2);
            }
            else{
                Debug.Log("Bleed vertically");
                var heightRatio = ar / v_ar;
                heightScale = 2 * videoDistance * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad / 2) * heightRatio;
            }

            var widthScale = heightScale * v_ar;

            videoBackground.transform.localScale = new Vector3(widthScale, heightScale, 1);
            videoBackground.transform.localPosition = new Vector3(0, 0, videoDistance);
            videoBackground.transform.localEulerAngles = Vector3.zero;
        }

        void SetVideoDims(){
            Resize( WebGLGetVideoDims());
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if(pauseOnApplicationLostFocus){
#if UNITY_WEBGL && !UNITY_EDITOR
                if(WebGLIsCameraStarted()){
                    if(hasFocus)
                        UnpauseCamera();
                    else
                        PauseCamera();
                }
#endif
            }
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if(pauseOnApplicationLostFocus){
#if UNITY_WEBGL && !UNITY_EDITOR
                if(WebGLIsCameraStarted()){
                    if(!pauseStatus)
                        UnpauseCamera();
                    else
                        PauseCamera();
                }
#endif
            }
        }

    }
}

