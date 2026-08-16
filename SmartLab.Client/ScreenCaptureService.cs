using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace SmartLab.Client
{
    public class ScreenCaptureService
    {
        // ==========================================
        // WINDOWS SCREEN INFORMATION
        // ==========================================

        private const int SM_XVIRTUALSCREEN = 76;
        private const int SM_YVIRTUALSCREEN = 77;
        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);


        // ==========================================
        // CAPTURE ENTIRE DESKTOP
        // ==========================================

        public byte[] CaptureScreen()
        {
            int left =
                GetSystemMetrics(SM_XVIRTUALSCREEN);

            int top =
                GetSystemMetrics(SM_YVIRTUALSCREEN);

            int width =
                GetSystemMetrics(SM_CXVIRTUALSCREEN);

            int height =
                GetSystemMetrics(SM_CYVIRTUALSCREEN);


            if (width <= 0 || height <= 0)
            {
                throw new InvalidOperationException(
                    "Unable to determine screen size."
                );
            }


            using Bitmap bitmap =
                new Bitmap(
                    width,
                    height,
                    PixelFormat.Format24bppRgb
                );


            using Graphics graphics =
                Graphics.FromImage(bitmap);


            graphics.CopyFromScreen(
                left,
                top,
                0,
                0,
                new Size(width, height),
                CopyPixelOperation.SourceCopy
            );


            using MemoryStream stream =
                new MemoryStream();


            bitmap.Save(
                stream,
                ImageFormat.Jpeg
            );


            return stream.ToArray();
        }
    }
}