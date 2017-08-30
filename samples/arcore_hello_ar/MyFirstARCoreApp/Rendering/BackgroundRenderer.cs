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
using Java.Nio;
using Android.Opengl;
using Com.Google.AR.Core;

namespace MyFirstARCoreApp
{
    class BackgroundRenderer
    {
        private static readonly String TAG = typeof(BackgroundRenderer).FullName;

        private static readonly int COORDS_PER_VERTEX = 3;
        private static readonly int TEXCOORDS_PER_VERTEX = 2;
        private static readonly int FLOAT_SIZE = 4;

        private FloatBuffer mQuadVertices;
        private FloatBuffer mQuadTexCoord;
        private FloatBuffer mQuadTexCoordTransformed;

        private int mQuadProgram;

        private int mQuadPositionParam;
        private int mQuadTexCoordParam;
        private int mTextureId = -1;
        private int mTextureTarget = GLES11Ext.GlTextureExternalOes;


        public int getTextureId()
        {
            return mTextureId;
        }

        /**
         * Allocates and initializes OpenGL resources needed by the background renderer.  Must be
         * called on the OpenGL thread, typically in
         * {@link GLSurfaceView.Renderer#onSurfaceCreated(GL10, EGLConfig)}.
         *
         * @param context Needed to access shader source.
         */
        public void createOnGlThread(Context context)
        {
            // Generate the background texture.
            int[] textures = new int[1];
            GLES20.GlGenTextures(1, textures, 0);
            mTextureId = textures[0];
            GLES20.GlBindTexture(mTextureTarget, mTextureId);
            GLES20.GlTexParameteri(mTextureTarget, GLES20.GlTextureWrapS, GLES20.GlClampToEdge);
            GLES20.GlTexParameteri(mTextureTarget, GLES20.GlTextureWrapT, GLES20.GlClampToEdge);
            GLES20.GlTexParameteri(mTextureTarget, GLES20.GlTextureMinFilter, GLES20.GlNearest);
            GLES20.GlTexParameteri(mTextureTarget, GLES20.GlTextureMagFilter, GLES20.GlNearest);

            int numVertices = 4;
            if (numVertices != QUAD_COORDS.Length / COORDS_PER_VERTEX)
            {
                throw new Java.Lang.RuntimeException("Unexpected number of vertices in BackgroundRenderer.");
            }

            ByteBuffer bbVertices = ByteBuffer.AllocateDirect(QUAD_COORDS.Length * FLOAT_SIZE);
            bbVertices.Order(ByteOrder.NativeOrder());
            mQuadVertices = bbVertices.AsFloatBuffer();
            mQuadVertices.Put(QUAD_COORDS);
            mQuadVertices.Position(0);

            ByteBuffer bbTexCoords = ByteBuffer.AllocateDirect(
                    numVertices * TEXCOORDS_PER_VERTEX * FLOAT_SIZE);
            bbTexCoords.Order(ByteOrder.NativeOrder());
            mQuadTexCoord = bbTexCoords.AsFloatBuffer();
            mQuadTexCoord.Put(QUAD_TEXCOORDS);
            mQuadTexCoord.Position(0);

            ByteBuffer bbTexCoordsTransformed = ByteBuffer.AllocateDirect(
                numVertices * TEXCOORDS_PER_VERTEX * FLOAT_SIZE);
            bbTexCoordsTransformed.Order(ByteOrder.NativeOrder());
            mQuadTexCoordTransformed = bbTexCoordsTransformed.AsFloatBuffer();

            int vertexShader = ShaderUtil.loadGLShader(TAG, context,
                    GLES20.GlVertexShader,  Resource.Raw.screenquad_vertex);
            int fragmentShader = ShaderUtil.loadGLShader(TAG, context,
                    GLES20.GlFragmentShader, Resource.Raw.screenquad_fragment_oes);

            mQuadProgram = GLES20.GlCreateProgram();
            GLES20.GlAttachShader(mQuadProgram, vertexShader);
            GLES20.GlAttachShader(mQuadProgram, fragmentShader);
            GLES20.GlLinkProgram(mQuadProgram);
            GLES20.GlUseProgram(mQuadProgram);

            ShaderUtil.checkGLError(TAG, "Program creation");

            mQuadPositionParam = GLES20.GlGetAttribLocation(mQuadProgram, "a_Position");
            mQuadTexCoordParam = GLES20.GlGetAttribLocation(mQuadProgram, "a_TexCoord");

            ShaderUtil.checkGLError(TAG, "Program parameters");
        }

        /**
         * Draws the AR background image.  The image will be drawn such that virtual content rendered
         * with the matrices provided by {@link Frame#getViewMatrix(float[], int)} and
         * {@link Session#getProjectionMatrix(float[], int, float, float)} will accurately follow
         * static physical objects.  This must be called <b>before</b> drawing virtual content.
         *
         * @param frame The last {@code Frame} returned by {@link Session#update()}.
         */
        public void draw(Frame frame)
        {
            // If display rotation changed (also includes view size change), we need to re-query the uv
            // coordinates for the screen rect, as they may have changed as well.
            if (frame.IsDisplayRotationChanged)
            {
                frame.TransformDisplayUvCoords(mQuadTexCoord, mQuadTexCoordTransformed);
            }

            // No need to test or write depth, the screen quad has arbitrary depth, and is expected
            // to be drawn first.
            GLES20.GlDisable(GLES20.GlDepthTest);
            GLES20.GlDepthMask(false);

            GLES20.GlBindTexture(GLES11Ext.GlTextureExternalOes, mTextureId);

            GLES20.GlUseProgram(mQuadProgram);

            // Set the vertex positions.
            GLES20.GlVertexAttribPointer(
                mQuadPositionParam, COORDS_PER_VERTEX, GLES20.GlFloat, false, 0, mQuadVertices);

            // Set the texture coordinates.
            GLES20.GlVertexAttribPointer(mQuadTexCoordParam, TEXCOORDS_PER_VERTEX,
                    GLES20.GlFloat, false, 0, mQuadTexCoordTransformed);

            // Enable vertex arrays
            GLES20.GlEnableVertexAttribArray(mQuadPositionParam);
            GLES20.GlEnableVertexAttribArray(mQuadTexCoordParam);

            GLES20.GlDrawArrays(GLES20.GlTriangleStrip, 0, 4);

            // Disable vertex arrays
            GLES20.GlDisableVertexAttribArray(mQuadPositionParam);
            GLES20.GlDisableVertexAttribArray(mQuadTexCoordParam);

            // Restore the depth state for further drawing.
            GLES20.GlDepthMask(true);
            GLES20.GlEnable(GLES20.GlDepthTest);

            ShaderUtil.checkGLError(TAG, "Draw");
        }

        public static readonly float[] QUAD_COORDS = new float[]{
            -1.0f, -1.0f, 0.0f,
            -1.0f, +1.0f, 0.0f,
            +1.0f, -1.0f, 0.0f,
            +1.0f, +1.0f, 0.0f,
        };
    
        public static readonly float[] QUAD_TEXCOORDS = new float[]{
            0.0f, 1.0f,
            0.0f, 0.0f,
            1.0f, 1.0f,
            1.0f, 0.0f,
        };
    }
}