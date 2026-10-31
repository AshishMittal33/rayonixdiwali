using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Imagine.WebAR;

namespace RayonixAR.Room
{
    /// <summary>
    /// Room configurator on top of the Imagine WebAR World Tracker (6DOF).
    ///
    /// Imagine tracks the camera around ONE placed origin on the floor and expresses depth by scaling
    /// the tracker's MainObject. Every item is therefore a child of <see cref="itemRoot"/> (under
    /// MainObject) and all positions are in that local space, so items follow the tracked pose and
    /// depth scale together. Items far from the origin are tracked less steadily; the UI says so.
    ///
    /// Interaction is buttons only. A short single-finger tap selects an item; touches near the left
    /// and right screen edges (thumbs holding a tablet), multi-finger touches and touches on UI are
    /// ignored, so holding and moving the device never moves an item.
    /// </summary>
    public class RoomConfigurator : MonoBehaviour
    {
        private const float TvHeight = 1.2f;        // TV mounting height above the floor
        private const float PlacementMinZ = 1.0f;   // placement arrow at least this far ahead (plugin default 0.5)
        private const float PlacementMaxZ = 3.5f;
        /// <summary>Imagine turns MainObject to face the camera 0.25 s after placing; spawn after that.</summary>
        private const float AfterPlaceDelay = 0.35f;
        private const float SlotClearance = 0.45f;  // a spawn slot is taken if an item is closer than this

        /// <summary>
        /// Where new items go, in metres relative to the placed origin as the user sees it:
        /// x = to the right, y = further away. The first free slot is used.
        /// </summary>
        private static readonly Vector2[] SpawnSlots =
        {
            new Vector2(0f, 0f), new Vector2(0.9f, 0f), new Vector2(-0.9f, 0f),
            new Vector2(0f, 0.9f), new Vector2(0.9f, 0.9f), new Vector2(-0.9f, 0.9f),
            new Vector2(0f, -0.7f), new Vector2(0.9f, -0.7f), new Vector2(-0.9f, -0.7f),
        };
        private const float StepMetres = 0.05f;
        private const float StepDegrees = 10f;
        private const float StepScale = 1.1f;
        private const float MinScale = 0.3f;
        private const float MaxScale = 3f;
        private const float StableRadius = 1.5f;    // beyond this, Imagine 6DOF tracking gets noticeably weaker
        private const float TapMaxTime = 0.4f;
        private const float TapMaxMoveFraction = 0.03f; // of the screen's shorter side
        private const float GripEdgeFraction = 0.1f;    // left/right screen edges where thumbs rest
        private const int UndoLimit = 30;

        [Header("Tracking")]
        [SerializeField] private WorldTracker tracker;
        [SerializeField] private Camera arCamera;
        [Tooltip("Parent for placed items. Must sit under the tracker's MainObject.")]
        [SerializeField] private Transform itemRoot;

        [Header("Catalogue")]
        [SerializeField] private RoomItem tvPrefab;
        [SerializeField] private RoomItem chairPrefab;
        [SerializeField] private RoomItem tablePrefab;
        [SerializeField] private Material selectionMaterial;

        [Header("UI")]
        [SerializeField] private GameObject cataloguePanel;
        [SerializeField] private GameObject itemPanel;
        [SerializeField] private Text statusText;
        [SerializeField] private Text guideText;
        [SerializeField] private Text itemTitle;
        [SerializeField] private Button[] undoButtons;
        [SerializeField] private Button clearButton;

        private readonly List<RoomItem> items = new List<RoomItem>();
        private RoomItem selected;
        private int nextNumber = 1;

        private struct Snapshot
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
        }

        private enum UndoType { Transform, Add, Delete }

        private struct UndoEntry
        {
            public UndoType type;
            public RoomItem item;
            public Snapshot before;
        }

        private readonly List<UndoEntry> undo = new List<UndoEntry>();

        private bool tapActive;
        private int tapFinger = -1;
        private Vector2 tapStart;
        private float tapStartTime;
        private bool tapIgnored;
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        private string shownStatus;
        private string shownGuide;
        private bool placingForSpawn;

        // ------------------------------------------------------------------ lifecycle

        private void Start()
        {
            if (tracker != null) tracker.SetPlacementRange(PlacementMinZ, PlacementMaxZ);
        }

        private void OnEnable()
        {
            if (tracker == null || tracker.eventSettings == null) return;
            tracker.eventSettings.OnPlacedOrigin?.AddListener(OnPlaced);
            tracker.eventSettings.OnResetOrigin?.AddListener(OnReset);
        }

        private void OnDisable()
        {
            if (tracker == null || tracker.eventSettings == null) return;
            tracker.eventSettings.OnPlacedOrigin?.RemoveListener(OnPlaced);
            tracker.eventSettings.OnResetOrigin?.RemoveListener(OnReset);
        }

        private void Update()
        {
            bool placed = tracker != null && tracker.IsPlaced;
            if (placed) HandleTap();
            else tapActive = false;
            UpdateUi(placed);
        }

        private void OnPlaced()
        {
            Debug.Log("[RoomAR] Origin placed, " + items.Count + " items");
        }

        private void OnReset()
        {
            Select(null);
        }

        // ------------------------------------------------------------------ catalogue (button targets)

        public void AddTV() { Spawn(tvPrefab); }
        public void AddChair() { Spawn(chairPrefab); }
        public void AddTable() { Spawn(tablePrefab); }

        public void ClearAll()
        {
            Select(null);
            foreach (var item in items)
                if (item != null) Destroy(item.gameObject);
            foreach (var entry in undo)
                if (entry.type == UndoType.Delete && entry.item != null) Destroy(entry.item.gameObject);
            items.Clear();
            undo.Clear();
        }

        // ------------------------------------------------------------------ selected item (button targets)

        public void MoveLeft() { Nudge(-StepMetres, 0f, 0f); }
        public void MoveRight() { Nudge(StepMetres, 0f, 0f); }
        public void MoveUp() { Nudge(0f, StepMetres, 0f); }
        public void MoveDown() { Nudge(0f, -StepMetres, 0f); }
        public void MoveCloser() { Nudge(0f, 0f, -StepMetres); }
        public void MoveFarther() { Nudge(0f, 0f, StepMetres); }

        public void TurnLeft() { Turn(-StepDegrees); }
        public void TurnRight() { Turn(StepDegrees); }

        public void FaceMe()
        {
            if (selected == null) return;
            PushUndo(UndoType.Transform, selected);
            var toCamera = Flat(CameraLocalPosition() - selected.transform.localPosition);
            if (toCamera.sqrMagnitude > 1e-6f)
                selected.transform.localRotation = Quaternion.LookRotation(toCamera, Vector3.up);
        }

        public void Smaller() { ScaleBy(1f / StepScale); }
        public void Bigger() { ScaleBy(StepScale); }

        public void Duplicate()
        {
            if (selected == null) return;
            var prefab = PrefabFor(selected.kind);
            if (prefab == null) return;
            var copy = Instantiate(prefab, itemRoot);
            copy.name = prefab.label + " " + nextNumber++;
            var right = Vector3.Cross(Vector3.up, CameraLocalForward());
            float width = selected.boundsSize.x * selected.transform.localScale.x;
            copy.transform.localPosition = selected.transform.localPosition + right * (width + 0.1f);
            copy.transform.localRotation = selected.transform.localRotation;
            copy.transform.localScale = selected.transform.localScale;
            items.Add(copy);
            PushUndo(UndoType.Add, copy);
            Select(copy);
        }

        public void Delete()
        {
            if (selected == null) return;
            var item = selected;
            PushUndo(UndoType.Delete, item);
            Select(null);
            items.Remove(item);
            item.gameObject.SetActive(false);
        }

        public void Done()
        {
            Select(null);
        }

        public void Undo()
        {
            if (undo.Count == 0) return;
            var entry = undo[undo.Count - 1];
            undo.RemoveAt(undo.Count - 1);
            if (entry.item == null) return;

            switch (entry.type)
            {
                case UndoType.Transform:
                    Apply(entry.item, entry.before);
                    break;
                case UndoType.Add:
                    if (selected == entry.item) Select(null);
                    items.Remove(entry.item);
                    Destroy(entry.item.gameObject);
                    break;
                case UndoType.Delete:
                    entry.item.gameObject.SetActive(true);
                    items.Add(entry.item);
                    Select(entry.item);
                    break;
            }
        }

        // ------------------------------------------------------------------ placement maths (itemRoot local space)

        /// <summary>
        /// Adds an item. Before the origin is placed, this first places it where the arrow points,
        /// so one tap on TV, Chair or Table does both.
        /// </summary>
        private void Spawn(RoomItem prefab)
        {
            if (prefab == null || itemRoot == null || arCamera == null || tracker == null || placingForSpawn) return;
            if (!tracker.IsPlaced)
            {
                tracker.PlaceOriginAtIndicator();
                StartCoroutine(SpawnAfterPlacement(prefab));
                return;
            }
            SpawnNow(prefab);
        }

        private IEnumerator SpawnAfterPlacement(RoomItem prefab)
        {
            placingForSpawn = true;
            yield return new WaitForSeconds(AfterPlaceDelay);
            placingForSpawn = false;
            if (tracker.IsPlaced) SpawnNow(prefab);
        }

        private void SpawnNow(RoomItem prefab)
        {
            // Imagine's 6DOF frame has the placed origin at (0,0,0) on the floor, but the camera is
            // NOT above the floor (Place_6DOF puts it at y = 0, and depth is expressed by scaling
            // MainObject). So items go at and around the origin, never relative to camera height.
            var camPos = CameraLocalPosition();
            var forward = CameraLocalForward();
            var right = Vector3.Cross(Vector3.up, forward);

            var position = Vector3.zero;
            foreach (var slot in SpawnSlots)
            {
                var candidate = right * slot.x + forward * slot.y;
                if (!IsSlotTaken(candidate, prefab.wallItem))
                {
                    position = candidate;
                    break;
                }
            }
            if (prefab.wallItem) position.y = TvHeight;

            var item = Instantiate(prefab, itemRoot);
            item.name = prefab.label + " " + nextNumber++;
            item.transform.localPosition = position;
            var toCamera = Flat(camPos - position);
            item.transform.localRotation = toCamera.sqrMagnitude > 1e-6f
                ? Quaternion.LookRotation(toCamera, Vector3.up)
                : Quaternion.identity;
            item.transform.localScale = Vector3.one;

            items.Add(item);
            PushUndo(UndoType.Add, item);
            Select(item);
        }

        /// <summary>True if another item of the same kind of placement (wall or floor) stands near this spot.</summary>
        private bool IsSlotTaken(Vector3 spot, bool wall)
        {
            foreach (var item in items)
            {
                if (item == null || item.wallItem != wall) continue;
                var p = item.transform.localPosition;
                if (new Vector2(p.x - spot.x, p.z - spot.z).magnitude < SlotClearance) return true;
            }
            return false;
        }

        /// <summary>Moves the selected item along the user's right (x), up (y) and forward (z).</summary>
        private void Nudge(float dx, float dy, float dz)
        {
            if (selected == null) return;
            PushUndo(UndoType.Transform, selected);
            var forward = CameraLocalForward();
            var right = Vector3.Cross(Vector3.up, forward);
            var p = selected.transform.localPosition + right * dx + Vector3.up * dy + forward * dz;
            if (!selected.wallItem) p.y = Mathf.Max(0f, p.y);
            selected.transform.localPosition = p;
        }

        private void Turn(float degrees)
        {
            if (selected == null) return;
            PushUndo(UndoType.Transform, selected);
            selected.transform.localRotation = Quaternion.AngleAxis(degrees, Vector3.up) * selected.transform.localRotation;
        }

        private void ScaleBy(float factor)
        {
            if (selected == null) return;
            PushUndo(UndoType.Transform, selected);
            float s = Mathf.Clamp(selected.transform.localScale.x * factor, MinScale, MaxScale);
            selected.transform.localScale = Vector3.one * s;
        }

        private Vector3 CameraLocalPosition()
        {
            return itemRoot.InverseTransformPoint(arCamera.transform.position);
        }

        private Vector3 CameraLocalForward()
        {
            var f = Flat(itemRoot.InverseTransformDirection(arCamera.transform.forward));
            return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        private RoomItem PrefabFor(RoomItemKind kind)
        {
            switch (kind)
            {
                case RoomItemKind.TV: return tvPrefab;
                case RoomItemKind.Chair: return chairPrefab;
                default: return tablePrefab;
            }
        }

        // ------------------------------------------------------------------ undo

        private void PushUndo(UndoType type, RoomItem item)
        {
            undo.Add(new UndoEntry { type = type, item = item, before = Capture(item) });
            if (undo.Count > UndoLimit)
            {
                var dropped = undo[0];
                undo.RemoveAt(0);
                // A deleted item that can no longer be restored is gone for good.
                if (dropped.type == UndoType.Delete && dropped.item != null) Destroy(dropped.item.gameObject);
            }
        }

        private static Snapshot Capture(RoomItem item)
        {
            return new Snapshot
            {
                position = item.transform.localPosition,
                rotation = item.transform.localRotation,
                scale = item.transform.localScale,
            };
        }

        private static void Apply(RoomItem item, Snapshot s)
        {
            item.transform.localPosition = s.position;
            item.transform.localRotation = s.rotation;
            item.transform.localScale = s.scale;
        }

        // ------------------------------------------------------------------ selection

        private void HandleTap()
        {
            if (Input.touchCount > 0)
            {
                // Only single-finger taps count; extra fingers are hands holding the device.
                if (Input.touchCount > 1)
                {
                    tapActive = false;
                    return;
                }
                var t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began) BeginTap(t.position, t.fingerId);
                else if (t.phase == TouchPhase.Ended && tapActive && t.fingerId == tapFinger) EndTap(t.position);
                else if (t.phase == TouchPhase.Canceled) tapActive = false;
                return;
            }

            if (Input.GetMouseButtonDown(0)) BeginTap(Input.mousePosition, -1);
            else if (Input.GetMouseButtonUp(0) && tapActive) EndTap(Input.mousePosition);
        }

        private void BeginTap(Vector2 position, int finger)
        {
            tapActive = true;
            tapFinger = finger;
            tapStart = position;
            tapStartTime = Time.unscaledTime;
            bool inGripZone = position.x < Screen.width * GripEdgeFraction
                              || position.x > Screen.width * (1f - GripEdgeFraction);
            tapIgnored = inGripZone || IsOverUi(position);
        }

        private void EndTap(Vector2 position)
        {
            tapActive = false;
            if (tapIgnored) return;
            float maxMove = Mathf.Min(Screen.width, Screen.height) * TapMaxMoveFraction;
            if ((position - tapStart).magnitude > maxMove) return;
            if (Time.unscaledTime - tapStartTime > TapMaxTime) return;
            SelectAt(position);
        }

        private bool IsOverUi(Vector2 position)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var data = new PointerEventData(es) { position = position };
            uiHits.Clear();
            es.RaycastAll(data, uiHits);
            return uiHits.Count > 0;
        }

        private void SelectAt(Vector2 screenPosition)
        {
            var ray = arCamera.ScreenPointToRay(screenPosition);
            RoomItem hitItem = null;
            if (Physics.Raycast(ray, out RaycastHit hit, 50f))
            {
                var candidate = hit.collider.GetComponentInParent<RoomItem>();
                if (candidate != null && items.Contains(candidate)) hitItem = candidate;
            }
            Select(hitItem);
        }

        private void Select(RoomItem item)
        {
            if (selected == item) return;
            if (selected != null) SetOutline(selected, false);
            selected = item;
            if (selected != null) SetOutline(selected, true);
        }

        private void SetOutline(RoomItem item, bool visible)
        {
            var outline = item.transform.Find("Selection");
            if (outline == null)
            {
                if (!visible) return;
                outline = BuildOutline(item);
            }
            outline.gameObject.SetActive(visible);
        }

        /// <summary>Twelve thin bars along the edges of the item's bounds.</summary>
        private Transform BuildOutline(RoomItem item)
        {
            var root = new GameObject("Selection").transform;
            root.SetParent(item.transform, false);
            var size = item.boundsSize + Vector3.one * 0.03f;
            var half = size * 0.5f;
            const float thickness = 0.012f;

            for (int axis = 0; axis < 3; axis++)
            {
                int b = (axis + 1) % 3;
                int c = (axis + 2) % 3;
                for (int i = 0; i < 4; i++)
                {
                    var scale = Vector3.one * thickness;
                    scale[axis] = size[axis];
                    var pos = item.boundsCenter;
                    pos[b] += (i & 1) == 0 ? -half[b] : half[b];
                    pos[c] += (i & 2) == 0 ? -half[c] : half[c];

                    var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    bar.name = "Edge";
                    Destroy(bar.GetComponent<Collider>());
                    bar.transform.SetParent(root, false);
                    bar.transform.localPosition = pos;
                    bar.transform.localScale = scale;
                    var r = bar.GetComponent<MeshRenderer>();
                    r.sharedMaterial = selectionMaterial;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
            }
            return root;
        }

        // ------------------------------------------------------------------ UI

        private void UpdateUi(bool placed)
        {
            // The catalogue shows before placing too: tapping an item places the origin and adds it.
            if (cataloguePanel != null) cataloguePanel.SetActive(selected == null && !placingForSpawn);
            if (itemPanel != null) itemPanel.SetActive(placed && selected != null);

            string status = placed ? "Tracking" : "Find the floor";
            string guide;
            if (!placed)
                guide = "Point the arrow at the floor spot for your furniture (for a TV: the bottom of the wall), then tap TV, Chair or Table.";
            else if (selected == null)
                guide = items.Count == 0 ? "Add a TV, chair or table. It appears on the spot you placed." : "Tap an item to adjust it.";
            else if (new Vector2(selected.transform.localPosition.x, selected.transform.localPosition.z).magnitude > StableRadius)
                guide = "Far from the placement point: tracking is less steady here. Keep items within about 1.5 m.";
            else if (selected.wallItem)
                guide = "Use Farther to push the TV back to the wall, and Up or Down for its height.";
            else
                guide = "Use the buttons to move, turn and size it. Tap Done when it is right.";

            if (statusText != null && status != shownStatus) { statusText.text = status; shownStatus = status; }
            if (guideText != null && guide != shownGuide) { guideText.text = guide; shownGuide = guide; }
            if (itemTitle != null && selected != null && itemTitle.text != selected.name) itemTitle.text = selected.name;

            bool canUndo = undo.Count > 0;
            if (undoButtons != null)
                foreach (var b in undoButtons)
                    if (b != null && b.interactable != canUndo) b.interactable = canUndo;
            if (clearButton != null)
            {
                bool any = items.Count > 0;
                if (clearButton.interactable != any) clearButton.interactable = any;
            }
        }
    }
}
