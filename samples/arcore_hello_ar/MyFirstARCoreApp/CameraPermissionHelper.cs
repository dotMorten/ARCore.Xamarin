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
using Android;
using Android.Content.PM;
using Android.Support.V4.Content;
using Android.Support.V4.App;

namespace MyFirstARCoreApp
{
    /**
     * Helper to ask camera permission.
     */
    class CameraPermissionHelper
    {
        private static readonly string CAMERA_PERMISSION = Manifest.Permission.Camera;
        private static readonly int CAMERA_PERMISSION_CODE = 0;

        /**
          * Check to see we have the necessary permissions for this app.
          */
        public static bool HasCameraPermission(Activity activity)
        {
            
            return ContextCompat.CheckSelfPermission(activity, CAMERA_PERMISSION) == Permission.Granted;
        }

        /**
         * Check to see we have the necessary permissions for this app, and ask for them if we don't.
         */
        public static void RequestCameraPermission(Activity activity)
        {
            ActivityCompat.RequestPermissions(activity, new String[] { CAMERA_PERMISSION }, CAMERA_PERMISSION_CODE);
        }
    }
}