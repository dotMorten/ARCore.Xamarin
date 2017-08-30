using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Com.Google.AR.Core;

namespace MyFirstARCoreApp
{
    class PlaneAttachment
    {
        private readonly Plane mPlane;
        private readonly Anchor mAnchor;

        // Allocate temporary storage to avoid multiple allocations per frame.
        private readonly float[] mPoseTranslation = new float[3];
        private readonly float[] mPoseRotation = new float[4];

        public PlaneAttachment(Plane plane, Anchor anchor)
        {
            mPlane = plane;
            mAnchor = anchor;
        }

        public bool isTracking()
        {
            return /*true if*/
                mPlane.GetTrackingState() == Plane.TrackingState.Tracking &&
                mAnchor.GetTrackingState() == Com.Google.AR.Core.Anchor.TrackingState.Tracking;
        }

        public Pose Pose
        {
            get
            {
                Pose pose = mAnchor.Pose;
                pose.GetTranslation(mPoseTranslation, 0);
                pose.GetRotationQuaternion(mPoseRotation, 0);
                mPoseTranslation[1] = mPlane.CenterPose.Ty();
                return new Pose(mPoseTranslation, mPoseRotation);
            }
        }

        public Anchor Anchor => mAnchor;
    }
}