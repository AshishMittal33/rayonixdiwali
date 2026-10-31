using UnityEngine;

namespace RayonixAR.Room
{
    /// <summary>
    /// Keeps a bottom-anchored panel at most <see cref="maxWidth"/> wide and inside the screen with a
    /// margin, so the same layout works on portrait phones and landscape tablets.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class UiMaxWidth : MonoBehaviour
    {
        [SerializeField] private float maxWidth = 760f;
        [SerializeField] private float margin = 16f;

        private RectTransform rect;
        private RectTransform parentRect;
        private float lastParentWidth = -1f;

        private void OnEnable()
        {
            rect = (RectTransform)transform;
            parentRect = transform.parent as RectTransform;
            Apply();
        }

        private void Update()
        {
            if (parentRect != null && !Mathf.Approximately(parentRect.rect.width, lastParentWidth))
                Apply();
        }

        private void Apply()
        {
            if (rect == null || parentRect == null) return;
            lastParentWidth = parentRect.rect.width;
            float width = Mathf.Min(maxWidth, lastParentWidth - margin * 2f);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(0f, width));
        }
    }
}
