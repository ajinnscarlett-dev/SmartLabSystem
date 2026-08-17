using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace SmartLab.Client
{
    // ==========================================================
    // SMARTLAB SCREEN CAPTURE SERVICE
    // ==========================================================
    //
    // Purpose:
    // - Capture the desktop.
    // - Resize it to a lab-monitoring thumbnail size.
    // - Encode as JPEG with controlled quality.
    //
    // This keeps thumbnail uploads much smaller than sending the
    // full native desktop resolution.
    //
    // ==========================================================

    public class ScreenCaptureService
    {
        private const int SM_XVIRTUALSCREEN = 76;
        private const int SM_YVIRTUALSCREEN = 77;
        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(
            int nIndex);

        // ----------------------------------------------------------
        // THUMBNAIL SETTINGS
        // ----------------------------------------------------------

        // 640x360 is enough for a monitoring tile while keeping
        // the upload small.
        private const int MaxWidth = 640;
        private const int MaxHeight = 360;

        // JPEG quality for monitoring thumbnails.
        // This is intentionally lower than a normal photo export.
        private const long JpegQuality = 55L;

        // ==========================================================
        // CAPTURE SCREEN
        // ==========================================================

        public byte[] CaptureScreen()
        {
            int left =
                GetSystemMetrics(
                    SM_XVIRTUALSCREEN);

            int top =
                GetSystemMetrics(
                    SM_YVIRTUALSCREEN);

            int width =
                GetSystemMetrics(
                    SM_CXVIRTUALSCREEN);

            int height =
                GetSystemMetrics(
                    SM_CYVIRTUALSCREEN);

            if (width <= 0 ||
                height <= 0)
            {
                throw new InvalidOperationException(
                    "Unable to determine screen size.");
            }

            using Bitmap fullScreen =
                new Bitmap(
                    width,
                    height,
                    PixelFormat.Format24bppRgb);

            using Graphics graphics =
                Graphics.FromImage(
                    fullScreen);

            graphics.CopyFromScreen(
                left,
                top,
                0,
                0,
                new Size(
                    width,
                    height),
                CopyPixelOperation.SourceCopy);

            Size targetSize =
                CalculateThumbnailSize(
                    width,
                    height);

            using Bitmap thumbnail =
                new Bitmap(
                    targetSize.Width,
                    targetSize.Height,
                    PixelFormat.Format24bppRgb);

            using Graphics thumbnailGraphics =
                Graphics.FromImage(
                    thumbnail);

            thumbnailGraphics.CompositingMode =
                CompositingMode.SourceCopy;

            thumbnailGraphics.CompositingQuality =
                CompositingQuality.HighSpeed;

            thumbnailGraphics.InterpolationMode =
                InterpolationMode.HighQualityBilinear;

            thumbnailGraphics.SmoothingMode =
                SmoothingMode.HighSpeed;

            thumbnailGraphics.PixelOffsetMode =
                PixelOffsetMode.HighSpeed;

            thumbnailGraphics.DrawImage(
                fullScreen,
                new Rectangle(
                    0,
                    0,
                    targetSize.Width,
                    targetSize.Height));

            using MemoryStream stream =
                new MemoryStream();

            ImageCodecInfo? jpegCodec =
                GetJpegCodec();

            if (jpegCodec == null)
            {
                thumbnail.Save(
                    stream,
                    ImageFormat.Jpeg);

                return stream.ToArray();
            }

            using EncoderParameters encoderParameters =
                new EncoderParameters(1);

            encoderParameters.Param[0] =
                new EncoderParameter(
                    Encoder.Quality,
                    JpegQuality);

            thumbnail.Save(
                stream,
                jpegCodec,
                encoderParameters);

            return stream.ToArray();
        }

        // ==========================================================
        // CALCULATE THUMBNAIL SIZE
        // ==========================================================

        private static Size CalculateThumbnailSize(
            int width,
            int height)
        {
            double scaleX =
                (double)MaxWidth /
                width;

            double scaleY =
                (double)MaxHeight /
                height;

            double scale =
                Math.Min(
                    1.0,
                    Math.Min(
                        scaleX,
                        scaleY));

            int targetWidth =
                Math.Max(
                    1,
                    (int)Math.Round(
                        width * scale));

            int targetHeight =
                Math.Max(
                    1,
                    (int)Math.Round(
                        height * scale));

            return new Size(
                targetWidth,
                targetHeight);
        }

        // ==========================================================
        // FIND JPEG CODEC
        // ==========================================================

        private static ImageCodecInfo? GetJpegCodec()
        {
            foreach (ImageCodecInfo codec
                in ImageCodecInfo.GetImageEncoders())
            {
                if (codec.MimeType.Equals(
                    "image/jpeg",
                    StringComparison.OrdinalIgnoreCase))
                {
                    return codec;
                }
            }

            return null;
        }
    }
}
