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
    class ShaderUtil
    { /**
     * Converts a raw text file, saved as a resource, into an OpenGL ES shader.
     *
     * @param type The type of shader we will be creating.
     * @param resId The resource ID of the raw text file about to be turned into a shader.
     * @return The shader object handler.
     */
        public static int loadGLShader(string tag, Context context, int type, int resId)
        {
            string code = readRawTextFile(context, resId);
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

        /**
         * Checks if we've had an error inside of OpenGL ES, and if so what that error is.
         *
         * @param label Label to report in case of error.
         * @throws RuntimeException If an OpenGL error is detected.
         */
        public static void checkGLError(string tag, string label)
        {
            int error;
            while ((error = GLES20.GlGetError()) != GLES20.GlNoError)
            {
                Log.Error(tag, label + ": glError " + error);
                throw new RuntimeException(label + ": glError " + error);
            }
        }

        /**
         * Converts a raw text file into a string.
         *
         * @param resId The resource ID of the raw text file about to be turned into a shader.
         * @return The context of the text file, or null in case of error.
         */
        private static string readRawTextFile(Context context, int resId)
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