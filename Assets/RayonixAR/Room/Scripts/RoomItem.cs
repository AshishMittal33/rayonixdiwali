using UnityEngine;

namespace RayonixAR.Room
{
    public enum RoomItemKind
    {
        TV,
        Chair,
        Table,
    }

    /// <summary>
    /// Marks a placeable catalogue item. The prefab's front faces +Z and its pivot is the mounting
    /// point: the centre of the back for wall items, the centre of the footprint for floor items.
    /// </summary>
    public class RoomItem : MonoBehaviour
    {
        public RoomItemKind kind;
        public string label = "Item";
        [Tooltip("Shown on the catalogue button under the label.")]
        public string detail = "";
        [Tooltip("Wall items (TV) appear at eye height; floor items stand on the floor.")]
        public bool wallItem;
        [Tooltip("Local-space bounds of the model, used for the selection outline.")]
        public Vector3 boundsCenter;
        public Vector3 boundsSize = Vector3.one;
    }
}
