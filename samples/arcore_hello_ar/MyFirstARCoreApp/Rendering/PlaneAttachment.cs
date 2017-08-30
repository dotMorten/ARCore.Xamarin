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
using Google.AR.Core;

namespace MyFirstARCoreApp
{
    internal class PlaneAttachment
    {
        private readonly Plane mPlane;

        // Allocate temporary storage to avoid multiple allocations per frame.
        private float[] mPoseTranslation = new float[3];
        private float[] mPoseRotation = new float[4];

        public PlaneAttachment(Plane plane, Anchor anchor)
        {
            mPlane = plane;
            Anchor = anchor;
        }

        public bool IsTracking
        {
            get
            {
                return /*true if*/
                    mPlane.GetTrackingState() == Plane.TrackingState.Tracking &&
                    Anchor.GetTrackingState() == Anchor.TrackingState.Tracking;
            }
        }

        public Pose Pose
        {
            get
            {
                Pose pose = Anchor.Pose;
                pose.GetTranslation(mPoseTranslation, 0);
                pose.GetRotationQuaternion(mPoseRotation, 0);
                mPoseTranslation[1] = mPlane.CenterPose.Ty();
                return new Pose(mPoseTranslation, mPoseRotation);
            }
        }

        public Anchor Anchor { get; }
    }
}