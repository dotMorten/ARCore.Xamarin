using Android.App;
using Android.Widget;
using Android.OS;
using Android.Opengl;
using Com.Google.AR.Core;
using Javax.Microedition.Khronos.Egl;
using Javax.Microedition.Khronos.Opengles;
using Android.Views;
using static Com.Google.AR.Core.Frame;
using Android.Util;
using Android.Support.Design.Widget;
using System.Collections.Generic;
using Java.Util;

namespace MyFirstARCoreApp
{
    [Activity(Label = "MyFirstARCoreApp", MainLauncher = true, Theme = "@style/Theme.AppCompat.NoActionBar")]
    public class MainActivity : Android.Support.V7.App.AppCompatActivity, GLSurfaceView.IRenderer, Android.Views.GestureDetector.IOnGestureListener, View.IOnTouchListener
    {
        const string TAG = "MainActivity";
        // Rendering. The Renderers are created here, and initialized when the GL surface is created.
        private GLSurfaceView mSurfaceView;

        private Com.Google.AR.Core.Config mDefaultConfig;
        private Session mSession;


        private BackgroundRenderer mBackgroundRenderer = new BackgroundRenderer();
        private GestureDetector mGestureDetector;
        private Snackbar mLoadingMessageSnackbar = null;
        
        private ObjectRenderer mVirtualObject = new ObjectRenderer();
        private ObjectRenderer mVirtualObjectShadow = new ObjectRenderer();
        private PlaneRenderer mPlaneRenderer = new PlaneRenderer();
        private PointCloudRenderer mPointCloud = new PointCloudRenderer();

            // Temporary matrix allocated here to reduce number of allocations for each frame.
        private readonly float[] mAnchorMatrix = new float[16];

        // Tap handling and UI.
        private Queue<MotionEvent> mQueuedSingleTaps = new Queue<MotionEvent>(16);
        private List<PlaneAttachment> mTouches = new List<PlaneAttachment>();

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Main);

            mSurfaceView = FindViewById<GLSurfaceView>(Resource.Id.surfaceview);

            mSession = new Session(this);

            // Create default config, check is supported, create session from that config.
            mDefaultConfig = Com.Google.AR.Core.Config.CreateDefaultConfig();
            if (!mSession.IsSupported(mDefaultConfig))
            {
                Toast.MakeText(this, "This device does not support AR", ToastLength.Long).Show();
                Finish();
                return;
            }

            // Set up tap listener.
            mGestureDetector = new GestureDetector(this, this);
            mSurfaceView.SetOnTouchListener(this);

            // Set up renderer.
            mSurfaceView.PreserveEGLContextOnPause = true;
            mSurfaceView.SetEGLContextClientVersion(2);
            mSurfaceView.SetEGLConfigChooser(8, 8, 8, 8, 16, 0); // Alpha used for plane blending.
            mSurfaceView.SetRenderer(this);
            mSurfaceView.RenderMode = Rendermode.Continuously;

        }
        public bool OnSingleTapUp(MotionEvent e)
        {
            // Queue tap if there is space. Tap is lost if queue is full.
            if(mQueuedSingleTaps.Count < 16)
                mQueuedSingleTaps.Enqueue(e);
            return true;
        }


        protected override void OnResume()
        {
            base.OnResume();
            // ARCore requires camera permissions to operate. If we did not yet obtain runtime
            // permission on Android M and above, now is a good time to ask the user for it.
            if (CameraPermissionHelper.hasCameraPermission(this))
            {
                showLoadingMessage();
                // Note that order matters - see the note in onPause(), the reverse applies here.
                mSession.Resume(mDefaultConfig);
                mSurfaceView.OnResume();
            }
            else
            {
                CameraPermissionHelper.requestCameraPermission(this);
            }

        }

        public void OnSurfaceCreated(IGL10 gl, Javax.Microedition.Khronos.Egl.EGLConfig config)
        {
            GLES20.GlClearColor(0.1f, 0.1f, 0.1f, 1.0f);

            // Create the texture and pass it to ARCore session to be filled during update().
            mBackgroundRenderer.createOnGlThread(/*context=*/this);
            mSession.SetCameraTextureName(mBackgroundRenderer.getTextureId());

            // Prepare the other rendering objects.
            try
            {
                mVirtualObject.createOnGlThread(/*context=*/this, "andy.obj", "andy.png");
                mVirtualObject.setMaterialProperties(0.0f, 3.5f, 1.0f, 6.0f);

                mVirtualObjectShadow.createOnGlThread(/*context=*/this,
                    "andy_shadow.obj", "andy_shadow.png");
                mVirtualObjectShadow.setBlendMode(ObjectRenderer.BlendMode.Shadow);
                mVirtualObjectShadow.setMaterialProperties(1.0f, 0.0f, 0.0f, 1.0f);
            }
            catch (Java.IO.IOException e)
            {
                Log.Error(TAG, "Failed to read obj file");
            }
            try
            {
                mPlaneRenderer.createOnGlThread(/*context=*/this, "trigrid.png");
            }
            catch (Java.IO.IOException e)
            {
                Log.Error(TAG, "Failed to read plane texture");
            }
            mPointCloud.createOnGlThread(/*context=*/this);
        }

        public void OnSurfaceChanged(IGL10 gl, int width, int height)
        {
            GLES20.GlViewport(0, 0, width, height);
            // Notify ARCore session that the view size changed so that the perspective matrix and
            // the video background can be properly adjusted.
            mSession.SetDisplayGeometry(width, height);

        }



        public void OnDrawFrame(IGL10 gl)
        {
            // Clear screen to notify driver it should not load any pixels from previous frame.
            GLES20.GlClear(GLES20.GlColorBufferBit | GLES20.GlDepthBufferBit);

            try
            {
                // Obtain the current frame from ARSession. When the configuration is set to
                // UpdateMode.BLOCKING (it is by default), this will throttle the rendering to the
                // camera framerate.
                Frame frame = mSession.Update();

                // Handle taps. Handling only one tap per frame, as taps are usually low frequency
                // compared to frame rate.
                MotionEvent tap = mQueuedSingleTaps.Count == 0 ? null : mQueuedSingleTaps.Dequeue();
                if (tap != null && frame.GetTrackingState() == TrackingState.Tracking)
                {
                    foreach (HitResult hit in frame.HitTest(tap))
                    {
                        // Check if any plane was hit, and if it was hit inside the plane polygon.
                        if (hit is PlaneHitResult && ((PlaneHitResult)hit).IsHitInPolygon)
                        {
                            // Cap the number of objects created. This avoids overloading both the
                            // rendering system and ARCore.
                            if (mTouches.Count >= 16)
                            {
                                mSession.RemoveAnchors(new List<Anchor>() { mTouches[0].Anchor });
                                mTouches.RemoveAt(0);
                            }
                            // Adding an Anchor tells ARCore that it should track this position in
                            // space. This anchor will be used in PlaneAttachment to place the 3d model
                            // in the correct position relative both to the world and to the plane.
                            mTouches.Add(new PlaneAttachment(
                                ((PlaneHitResult)hit).Plane,
                                mSession.AddAnchor(hit.HitPose)));

                            // Hits are sorted by depth. Consider only closest hit on a plane.
                            break;
                        }
                    }
                }


                // Draw background.
                mBackgroundRenderer.draw(frame);

                // If not tracking, don't draw 3d objects.
                if (frame.GetTrackingState() == TrackingState.NotTracking)
                {
                    return;
                }

                // Get projection matrix.
                float[] projmtx = new float[16];
                mSession.GetProjectionMatrix(projmtx, 0, 0.1f, 100.0f);

                // Get camera matrix and draw.
                float[] viewmtx = new float[16];
                frame.GetViewMatrix(viewmtx, 0);

                // Compute lighting from average intensity of the image.
                float lightIntensity = frame.LightEstimate.PixelIntensity;

                // Visualize tracked points.
                mPointCloud.update(frame.PointCloud);
                mPointCloud.draw(frame.PointCloudPose, viewmtx, projmtx);

                // Check if we detected at least one plane. If so, hide the loading message.
                if (mLoadingMessageSnackbar != null)
                {
                    foreach (Plane plane in mSession.AllPlanes)
                    {
                        if (plane.GetType() == Com.Google.AR.Core.Plane.Type.HorizontalUpwardFacing &&
                                plane.GetTrackingState() == Plane.TrackingState.Tracking)
                        {
                            hideLoadingMessage();
                            break;
                        }
                    }
                }

                // Visualize planes.
                mPlaneRenderer.drawPlanes(mSession.AllPlanes, frame.Pose, projmtx);

                // Visualize anchors created by touch.
                float scaleFactor = 1.0f;
                foreach (PlaneAttachment planeAttachment in mTouches)
                {
                    if (!planeAttachment.isTracking())
                    {
                        continue;
                    }
                    // Get the current combined pose of an Anchor and Plane in world space. The Anchor
                    // and Plane poses are updated during calls to session.update() as ARCore refines
                    // its estimate of the world.
                    planeAttachment.Pose.ToMatrix(mAnchorMatrix, 0);

                    // Update and draw the model and its shadow.
                    mVirtualObject.updateModelMatrix(mAnchorMatrix, scaleFactor);
                    mVirtualObjectShadow.updateModelMatrix(mAnchorMatrix, scaleFactor);
                    mVirtualObject.draw(viewmtx, projmtx, lightIntensity);
                    mVirtualObjectShadow.draw(viewmtx, projmtx, lightIntensity);
                }

            }
            catch (Java.Lang.Throwable t)
            {
                // Avoid crashing the application due to unhandled exceptions.
                Log.Error(TAG, "Exception on the OpenGL thread", t);
            }
        }

        
        private void showLoadingMessage()
        {
            RunOnUiThread(() => {
                mLoadingMessageSnackbar = Snackbar.Make(//mSurfaceView.Parent as View,
                    this.FindViewById(Android.Resource.Id.Content),
                    "Searching for surfaces...", Snackbar.LengthIndefinite);
                mLoadingMessageSnackbar.View.SetBackgroundColor(Android.Graphics.Color.Argb(0xbf, 0x32, 0x32, 0x32));
                mLoadingMessageSnackbar.Show();
            });
    }

        private void hideLoadingMessage()
        {
            RunOnUiThread(() =>
            {

                mLoadingMessageSnackbar.Dismiss();
                mLoadingMessageSnackbar = null;
            });
        }

        public bool OnDown(MotionEvent e)
        {
            return true;
        }

        public bool OnFling(MotionEvent e1, MotionEvent e2, float velocityX, float velocityY)
        {
            return false;
        }

        public void OnLongPress(MotionEvent e)
        {
        }

        public bool OnScroll(MotionEvent e1, MotionEvent e2, float distanceX, float distanceY)
        {
            return false;
        }

        public void OnShowPress(MotionEvent e)
        {
        }

        public bool OnTouch(View v, MotionEvent e)
        {
            return mGestureDetector.OnTouchEvent(e);
        }
    }
}

