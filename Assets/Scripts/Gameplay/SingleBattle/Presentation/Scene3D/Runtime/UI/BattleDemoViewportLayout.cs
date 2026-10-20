using CardPresentation;
using UnityEngine;
using UIFoundation;

namespace Poker.SingleBattle
{
    /// <summary>Projects the world hand into the safe bottom strip without owning card or combat state.</summary>
    [ExecuteAlways, DefaultExecutionOrder(-100)]
    public sealed class BattleDemoViewportLayout : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private HandZoneView hand;
        [SerializeField] private float distance = 2;
        [SerializeField] private Rect handArea = new Rect(.35f, .025f, .48f, .17f);
        [SerializeField] private Transform backdrop;
        [SerializeField] private Rect backdropArea = new Rect(.284f, .006f, .71f, .258f);
        private Vector2 lastSize;
        public void Configure(Camera camera, HandZoneView view) { viewCamera=camera; hand=view; Refresh(); }
        public void ConfigureBackdrop(Transform value) { backdrop = value; Refresh(); }
        private void LateUpdate() => Refresh();
        public void Refresh()
        {
            if(viewCamera==null||hand==null)return;
            var viewport=viewCamera.pixelRect;
            var safe=Application.isPlaying?ScreenSafeArea.CameraRect(viewCamera):viewport;
            float w=Mathf.Max(1,viewCamera.pixelWidth),h=Mathf.Max(1,viewCamera.pixelHeight);
            var origin=safe.position-viewport.position;
            var center=new Vector3((origin.x+safe.width*handArea.center.x)/w,(origin.y+safe.height*handArea.center.y)/h,distance);
            hand.transform.position=viewCamera.ViewportToWorldPoint(center);
            hand.SetCameraFacing(CardZoneFacingMode.ParallelCameraPlane,viewCamera);
            if(backdrop!=null)
            {
                float depth=distance+.15f;
                backdrop.position=viewCamera.ViewportToWorldPoint(new Vector3(
                    (origin.x+safe.width*backdropArea.center.x)/w,(origin.y+safe.height*backdropArea.center.y)/h,depth));
                backdrop.rotation=viewCamera.transform.rotation;
                var lower=viewCamera.ViewportToWorldPoint(new Vector3(0,0,depth));
                var upper=viewCamera.ViewportToWorldPoint(new Vector3(safe.width/w*backdropArea.width,safe.height/h*backdropArea.height,depth));
                backdrop.localScale=new Vector3(Vector3.Dot(upper-lower,viewCamera.transform.right),Vector3.Dot(upper-lower,viewCamera.transform.up),1);
            }
            var a=viewCamera.ViewportToWorldPoint(new Vector3(0,0,distance));
            var b=viewCamera.ViewportToWorldPoint(new Vector3(safe.width/w*handArea.width,safe.height/h*handArea.height,distance));
            var size=new Vector2(Vector3.Dot(b-a,viewCamera.transform.right),Vector3.Dot(b-a,viewCamera.transform.up));
            if(size.x<=0||size.y<=0||(size-lastSize).sqrMagnitude<.000001f)return;
            lastSize=size;var layout=hand.Layout;layout.Size=size;layout.FitCardsToZone=true;layout.ArcHeight=size.y*.05f;
            hand.SetLayout(layout,hand.MotionSettings,false);
        }
    }
}
