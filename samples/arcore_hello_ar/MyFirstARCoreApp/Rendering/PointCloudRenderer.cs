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
using Android.Opengl;

namespace MyFirstARCoreApp
{
    class PointCloudRenderer
    {
        private static readonly string TAG = typeof(PointCloudRenderer).Name;

        private const int BYTES_PER_FLOAT = Java.Lang.Float.Size / 8;
        private const int FLOATS_PER_POINT = 4;  // X,Y,Z,confidence.
        private const int BYTES_PER_POINT = BYTES_PER_FLOAT * FLOATS_PER_POINT;
        private const int INITIAL_BUFFER_POINTS = 1000;

        private int mVbo;
        private int mVboSize;

        private int mProgramName;
        private int mPositionAttribute;
        private int mModelViewProjectionUniform;
        private int mColorUniform;
        private int mPointSizeUniform;

        private int mNumPoints = 0;

        // Keep track of the last point cloud rendered to avoid updating the VBO if point cloud
        // was not changed.
        private PointCloud mLastPointCloud = null;

        public PointCloudRenderer()
        {
        }

        /// <summary>
        /// Allocates and initializes OpenGL resources needed by the plane renderer.Must be 
        /// called on the OpenGL thread, typically in <see cref="GLSurfaceView.IRenderer.OnSurfaceCreated(Javax.Microedition.Khronos.Opengles.IGL10, Javax.Microedition.Khronos.Egl.EGLConfig)"/>.
        /// </summary>
        /// <param name="context">Needed to access shader source.</param>
        public void CreateOnGlThread(Context context)
        {
            ShaderUtil.CheckGLError(TAG, "before create");

            int[] buffers = new int[1];
            GLES20.GlGenBuffers(1, buffers, 0);
            mVbo = buffers[0];
            GLES20.GlBindBuffer(GLES20.GlArrayBuffer, mVbo);

            mVboSize = INITIAL_BUFFER_POINTS * BYTES_PER_POINT;
            GLES20.GlBufferData(GLES20.GlArrayBuffer, mVboSize, null, GLES20.GlDynamicDraw);
            GLES20.GlBindBuffer(GLES20.GlArrayBuffer, 0);

            ShaderUtil.CheckGLError(TAG, "buffer alloc");

            int vertexShader = ShaderUtil.LoadGLShader(TAG, context,
                GLES20.GlVertexShader, Resource.Raw.point_cloud_vertex);
            int passthroughShader = ShaderUtil.LoadGLShader(TAG, context,
                GLES20.GlFragmentShader, Resource.Raw.passthrough_fragment);

            mProgramName = GLES20.GlCreateProgram();
            GLES20.GlAttachShader(mProgramName, vertexShader);
            GLES20.GlAttachShader(mProgramName, passthroughShader);
            GLES20.GlLinkProgram(mProgramName);
            GLES20.GlUseProgram(mProgramName);

            ShaderUtil.CheckGLError(TAG, "program");

            mPositionAttribute = GLES20.GlGetAttribLocation(mProgramName, "a_Position");
            mColorUniform = GLES20.GlGetUniformLocation(mProgramName, "u_Color");
            mModelViewProjectionUniform = GLES20.GlGetUniformLocation(
                mProgramName, "u_ModelViewProjection");
            mPointSizeUniform = GLES20.GlGetUniformLocation(mProgramName, "u_PointSize");

            ShaderUtil.CheckGLError(TAG, "program  params");
        }

        /// <summary>
        /// Updates the OpenGL buffer contents to the provided point.  Repeated calls with the same
        /// point cloud will be ignored.
        /// </summary>
        /// <param name="cloud"></param>
        public void Update(PointCloud cloud)
        {
            if (mLastPointCloud == cloud)
            {
                // Redundant call.
                return;
            }

            ShaderUtil.CheckGLError(TAG, "before update");

            GLES20.GlBindBuffer(GLES20.GlArrayBuffer, mVbo);
            mLastPointCloud = cloud;

            // If the VBO is not large enough to fit the new point cloud, resize it.
            mNumPoints = mLastPointCloud.Points.Remaining() / FLOATS_PER_POINT;
            if (mNumPoints * BYTES_PER_POINT > mVboSize)
            {
                while (mNumPoints * BYTES_PER_POINT > mVboSize)
                {
                    mVboSize *= 2;
                }
                GLES20.GlBufferData(GLES20.GlArrayBuffer, mVboSize, null, GLES20.GlDynamicDraw);
            }
            GLES20.GlBufferSubData(GLES20.GlArrayBuffer, 0, mNumPoints * BYTES_PER_POINT,
                mLastPointCloud.Points);
            GLES20.GlBindBuffer(GLES20.GlArrayBuffer, 0);

            ShaderUtil.CheckGLError(TAG, "after update");
        }

        /// <summary>
        /// Renders the point cloud.
        /// </summary>
        /// <param name="pose">the current point cloud pose, from {@link Frame#getPointCloudPose()}.</param>
        /// <param name="cameraView">the camera view matrix for this frame, typically from <see cref="Frame.GetViewMatrix(float[], int)/>.</param>
        /// <param name="cameraPerspective">the camera projection matrix for this frame, typically from <see cref="Session.GetProjectionMatrix(float[], int, float, float)"/>.</param>
        public void Draw(Pose pose, float[] cameraView, float[] cameraPerspective)
        {
            float[] modelMatrix = new float[16];
            pose.ToMatrix(modelMatrix, 0);

            float[] modelView = new float[16];
            float[] modelViewProjection = new float[16];
            Matrix.MultiplyMM(modelView, 0, cameraView, 0, modelMatrix, 0);
            Matrix.MultiplyMM(modelViewProjection, 0, cameraPerspective, 0, modelView, 0);

            ShaderUtil.CheckGLError(TAG, "Before draw");

            GLES20.GlUseProgram(mProgramName);
            GLES20.GlEnableVertexAttribArray(mPositionAttribute);
            GLES20.GlBindBuffer(GLES20.GlArrayBuffer, mVbo);
            GLES20.GlVertexAttribPointer(
                mPositionAttribute, 4, GLES20.GlFloat, false, BYTES_PER_POINT, 0);
            GLES20.GlUniform4f(mColorUniform, 31.0f / 255.0f, 188.0f / 255.0f, 210.0f / 255.0f, 1.0f);
            GLES20.GlUniformMatrix4fv(mModelViewProjectionUniform, 1, false, modelViewProjection, 0);
            GLES20.GlUniform1f(mPointSizeUniform, 5.0f);

            GLES20.GlDrawArrays(GLES20.GlPoints, 0, mNumPoints);
            GLES20.GlDisableVertexAttribArray(mPositionAttribute);
            GLES20.GlBindBuffer(GLES20.GlArrayBuffer, 0);

            ShaderUtil.CheckGLError(TAG, "Draw");
        }
    }
}
 