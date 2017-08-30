using System;
using System.Collections.Generic;
using System.Linq;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Opengl;
using Java.Lang;
using Java.IO;
using Android.Util;

namespace MyFirstARCoreApp
{
    internal class ShaderUtil
    {
        /// <summary>
        /// Converts a raw text file, saved as a resource, into an OpenGL ES shader.
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="context"></param>
        /// <param name="type">The type of shader we will be creating.</param>
        /// <param name="resId">The resource ID of the raw text file about to be turned into a shader.</param>
        /// <returns>The shader object handler.</returns>
        public static int LoadGLShader(string tag, Context context, int type, int resId)
        {
            string code = ReadRawTextFile(context, resId);
            int shader = GLES20.GlCreateShader(type);
            GLES20.GlShaderSource(shader, code);
            GLES20.GlCompileShader(shader);

            // Get the compilation status.
            int[] compileStatus = new int[1];
            GLES20.GlGetShaderiv(shader, GLES20.GlCompileStatus, compileStatus, 0);

            // If the compilation failed, delete the shader.
            if (compileStatus[0] == 0)
            {
                Log.Error(tag, "Error compiling shader: " + GLES20.GlGetShaderInfoLog(shader));
                GLES20.GlDeleteShader(shader);
                shader = 0;
            }

            if (shader == 0)
            {
                throw new RuntimeException("Error creating shader.");
            }

            return shader;
        }

        /// <summary>
        /// Checks if we've had an error inside of OpenGL ES, and if so what that error is.
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="label"> Label to report in case of error.</param>
        /// <exception cref="RuntimeException">If an OpenGL error is detected</exception>
        public static void CheckGLError(string tag, string label)
        {
            int error;
            while ((error = GLES20.GlGetError()) != GLES20.GlNoError)
            {
                Log.Error(tag, label + ": glError " + error);
                throw new RuntimeException(label + ": glError " + error);
            }
        }

        /// <summary>
        /// Converts a raw text file into a string.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="resId">The resource ID of the raw text file about to be turned into a shader.</param>
        /// <returns>The context of the text file, or null in case of error.</returns>
        private static string ReadRawTextFile(Context context, int resId)
        {
            System.IO.Stream inputStream = context.Resources.OpenRawResource(resId);
            try
            {
                BufferedReader reader = new BufferedReader(new InputStreamReader(inputStream));
                StringBuilder sb = new StringBuilder();
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    sb.Append(line).Append("\n");
                }
                reader.Close();
                return sb.ToString();
            }
            catch (IOException e)
            {
                e.PrintStackTrace();
            }
            return null;
        }
    }
}