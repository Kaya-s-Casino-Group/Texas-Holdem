using UnityEngine;

namespace BattlePresentation.Unity
{
    /// <summary>Authored world-space camera pose and lens. This is an anchor, not a rendering camera.</summary>
    [DisallowMultipleComponent]
    public sealed class CameraShot : MonoBehaviour
    {
        [SerializeField, Range(1, 179)] private float fieldOfView = 40;
        [SerializeField, Min(.001f)] private float orthographicSize = 5;
        public float FieldOfView => fieldOfView;
        public float OrthographicSize => orthographicSize;
        public void Capture(Camera source)
        {
            transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            fieldOfView = source.fieldOfView;
            orthographicSize = source.orthographicSize;
        }
    }
}
