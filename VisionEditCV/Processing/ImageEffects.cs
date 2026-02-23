using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace VisionEditCV.Processing
{
    /// <summary>
    /// EmguCV implementations of all image effects from functions.py.
    /// All methods that take a mask will resize it to match the image dimensions
    /// (preserving the Python behavior: cv2.resize(mask, (image.shape[1], image.shape[0]))).
    /// </summary>
    public static class ImageEffects
    {
        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Converts a System.Drawing.Bitmap to an EmguCV Mat (BGR).
        /// </summary>
        public static Mat BitmapToMat(Bitmap bmp)
        {
            Bitmap src = bmp;
            bool disposeSrc = false;
            if (bmp.PixelFormat != PixelFormat.Format24bppRgb &&
                bmp.PixelFormat != PixelFormat.Format32bppArgb)
            {
                src = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format24bppRgb);
                using var g = Graphics.FromImage(src);
                g.DrawImage(bmp, 0, 0);
                disposeSrc = true;
            }

            BitmapData bd = src.LockBits(
                new Rectangle(0, 0, src.Width, src.Height),
                ImageLockMode.ReadOnly,
                src.PixelFormat);

            int channels = src.PixelFormat == PixelFormat.Format32bppArgb ? 4 : 3;
            Mat mat = new Mat(src.Height, src.Width,
                channels == 4 ? DepthType.Cv8U : DepthType.Cv8U,
                channels);
            int stride = src.Width * channels;
            byte[] data = new byte[src.Height * stride];

            for (int y = 0; y < src.Height; y++)
            {
                Marshal.Copy(
                    bd.Scan0 + y * bd.Stride,
                    data, y * stride, stride);
            }
            src.UnlockBits(bd);
            if (disposeSrc) src.Dispose();

            mat.SetTo(data);

            // WinForms Bitmap is BGR already when loaded via GDI; if ARGB convert channels
            if (channels == 4)
            {
                // Convert BGRA -> RGBA won't be needed; we stay BGR for processing
                // Caller should be aware source is BGR order
            }
            return mat;
        }

        /// <summary>
        /// Converts an EmguCV Mat (BGR or BGRA) to a System.Drawing.Bitmap.
        /// </summary>
        public static Bitmap MatToBitmap(Mat mat)
        {
            int ch = mat.NumberOfChannels;
            PixelFormat pf = ch == 4 ? PixelFormat.Format32bppArgb : PixelFormat.Format24bppRgb;
            var bmp = new Bitmap(mat.Width, mat.Height, pf);
            BitmapData bd = bmp.LockBits(
                new Rectangle(0, 0, bmp.Width, bmp.Height),
                ImageLockMode.WriteOnly, pf);

            int stride = mat.Width * ch;
            byte[] data = new byte[mat.Height * stride];
            mat.CopyTo(data);

            for (int y = 0; y < bmp.Height; y++)
                Marshal.Copy(data, y * stride, bd.Scan0 + y * bd.Stride, stride);

            bmp.UnlockBits(bd);
            return bmp;
        }

        /// <summary>
        /// Resizes a float[,] mask to (targetH, targetW) using nearest-neighbour and
        /// returns it as a byte[,] binary mask (255 where value > threshold, 0 elsewhere).
        /// This mirrors: mask = cv2.resize(mask, (image.shape[1], image.shape[0]))
        /// </summary>
        public static byte[,] ResizeAndThresholdMask(float[,] mask, int targetW, int targetH,
            float threshold = 0.5f)
        {
            int srcH = mask.GetLength(0);
            int srcW = mask.GetLength(1);

            // Build a single-channel float Mat from the mask
            float[] flat = new float[srcH * srcW];
            for (int r = 0; r < srcH; r++)
                for (int c = 0; c < srcW; c++)
                    flat[r * srcW + c] = mask[r, c];

            Mat srcMat = new Mat(srcH, srcW, DepthType.Cv32F, 1);
            srcMat.SetTo(flat.SelectMany(BitConverter.GetBytes).ToArray());

            Mat dstMat = new Mat();
            CvInvoke.Resize(srcMat, dstMat, new Size(targetW, targetH),
                interpolation: Inter.Linear);

            byte[] dstData = new byte[targetH * targetW * 4]; // Cv32F → 4 bytes each
            dstMat.CopyTo(dstData);

            var binary = new byte[targetH, targetW];
            for (int r = 0; r < targetH; r++)
                for (int c = 0; c < targetW; c++)
                {
                    float val = BitConverter.ToSingle(dstData, (r * targetW + c) * 4);
                    binary[r, c] = val > threshold ? (byte)255 : (byte)0;
                }

            srcMat.Dispose();
            dstMat.Dispose();
            return binary;
        }

        // ── Effect 1: Color Grading ───────────────────────────────────────────

        /// <summary>
        /// Port of Color_Grading() from functions.py.
        /// Applies tint, brightness, contrast and optional B&W conversion
        /// to either the foreground (mask) or background (inverse mask).
        /// </summary>
        public static Bitmap ColorGrading(
            Bitmap image,
            float[,] mask,
            Color tintColor,
            float tintStrength,        // 0..1
            int brightness,            // -255..255
            float contrast,            // 0.1..3.0
            bool blackAndWhite,
            bool targetBackground)
        {
            using Mat img = BitmapToMat(image);
            using Mat imgBgr = new Mat();

            // Ensure BGR 3-channel
            if (img.NumberOfChannels == 4)
                CvInvoke.CvtColor(img, imgBgr, ColorConversion.Bgra2Bgr);
            else
                img.CopyTo(imgBgr);

            byte[,] binaryMask = ResizeAndThresholdMask(mask, imgBgr.Width, imgBgr.Height);

            // Build binary mask Mat and its inverse
            using Mat maskMat = BuildMaskMat(binaryMask, imgBgr.Width, imgBgr.Height);
            using Mat inverseMask = new Mat();
            CvInvoke.BitwiseNot(maskMat, inverseMask);

            // Split into foreground / background
            using Mat foreground = new Mat();
            using Mat background = new Mat();
            CvInvoke.BitwiseAnd(imgBgr, imgBgr, foreground, maskMat);
            CvInvoke.BitwiseAnd(imgBgr, imgBgr, background, inverseMask);

            Mat workingLayer = new Mat();
            Mat staticLayer = new Mat();
            Mat workingMask = new Mat();

            if (targetBackground)
            {
                background.CopyTo(workingLayer);
                foreground.CopyTo(staticLayer);
                inverseMask.CopyTo(workingMask);
            }
            else
            {
                foreground.CopyTo(workingLayer);
                background.CopyTo(staticLayer);
                maskMat.CopyTo(workingMask);
            }

            // B&W conversion
            if (blackAndWhite)
            {
                using Mat gray = new Mat();
                using Mat grayBgr = new Mat();
                CvInvoke.CvtColor(workingLayer, gray, ColorConversion.Bgr2Gray);
                CvInvoke.CvtColor(gray, grayBgr, ColorConversion.Gray2Bgr);
                workingLayer.Dispose();
                workingLayer = grayBgr.Clone();
            }

            // Brightness & contrast: convertScaleAbs(alpha=contrast, beta=brightness)
            using Mat adjusted = new Mat();
            CvInvoke.ConvertScaleAbs(workingLayer, adjusted, contrast, brightness);
            workingLayer.Dispose();
            workingLayer = adjusted.Clone();

            // Tint
            if (tintStrength > 0f)
            {
                using Mat tintLayer = new Mat(workingLayer.Rows, workingLayer.Cols,
                    workingLayer.Depth, workingLayer.NumberOfChannels);
                tintLayer.SetTo(new MCvScalar(tintColor.B, tintColor.G, tintColor.R));

                using Mat tinted = new Mat();
                CvInvoke.AddWeighted(workingLayer, 1.0 - tintStrength,
                    tintLayer, tintStrength, 0, tinted);
                workingLayer.Dispose();
                workingLayer = tinted.Clone();
            }

            // Reapply mask
            using Mat maskedWorking = new Mat();
            CvInvoke.BitwiseAnd(workingLayer, workingLayer, maskedWorking, workingMask);
            workingLayer.Dispose();
            workingMask.Dispose();

            // Combine
            using Mat finalResult = new Mat();
            CvInvoke.Add(maskedWorking, staticLayer, finalResult);

            staticLayer.Dispose();

            return MatToBitmap(finalResult);
        }

        // ── Effect 2: Artistic Style ──────────────────────────────────────────

        /// <summary>
        /// Applies cv2.stylization on the masked region.
        /// sigmaS: spatial filter extent (1–200). sigmaR: color range (0.01–1.0).
        /// </summary>
        public static Bitmap StylizeMasked(Bitmap image, float[,] mask, int sigmaS, float sigmaR)
        {
            using Mat img = BitmapToMat(image);
            using Mat imgBgr = new Mat();

            if (img.NumberOfChannels == 4)
                CvInvoke.CvtColor(img, imgBgr, ColorConversion.Bgra2Bgr);
            else
                img.CopyTo(imgBgr);

            byte[,] binaryMask = ResizeAndThresholdMask(mask, imgBgr.Width, imgBgr.Height);
            using Mat maskMat = BuildMaskMat(binaryMask, imgBgr.Width, imgBgr.Height);

            using Mat styled = new Mat();
            CvInvoke.Stylization(imgBgr, styled, sigmaS, sigmaR);

            using Mat result = imgBgr.Clone();
            styled.CopyTo(result, maskMat);

            return MatToBitmap(result);
        }

        /// <summary>
        /// Applies cv2.pencilSketch on the masked region.
        /// sigmaS: spatial filter extent (1–200). shadeFactor: shade intensity (0.0–0.1+).
        /// </summary>
        public static Bitmap PencilSketchMasked(Bitmap image, float[,] mask, int sigmaS, float shadeFactor)
        {
            using Mat img = BitmapToMat(image);
            using Mat imgBgr = new Mat();

            if (img.NumberOfChannels == 4)
                CvInvoke.CvtColor(img, imgBgr, ColorConversion.Bgra2Bgr);
            else
                img.CopyTo(imgBgr);

            byte[,] binaryMask = ResizeAndThresholdMask(mask, imgBgr.Width, imgBgr.Height);
            using Mat maskMat = BuildMaskMat(binaryMask, imgBgr.Width, imgBgr.Height);

            using Mat gray = new Mat();
            using Mat colorSketch = new Mat();
            CvInvoke.PencilSketch(imgBgr, gray, colorSketch, sigmaS, 0.07f, shadeFactor);

            using Mat result = imgBgr.Clone();
            colorSketch.CopyTo(result, maskMat);

            return MatToBitmap(result);
        }

        // ── Effect 3: Sticker Generation ─────────────────────────────────────

        /// <summary>
        /// Extracts the masked foreground as a BGRA bitmap with transparent background.
        /// Border contour and drop shadow are drawn on top.
        /// </summary>
        public static Bitmap ExtractSticker(
            Bitmap image,
            float[,] mask,
            float threshold,
            int contourThickness,
            int shadowBlur,
            Color borderColor,
            float scaleFactor,
            float rotationAngle)
        {
            using Mat imgBgr = BitmapToMat(image);
            using Mat img3 = new Mat();
            if (imgBgr.NumberOfChannels == 4)
                CvInvoke.CvtColor(imgBgr, img3, ColorConversion.Bgra2Bgr);
            else
                imgBgr.CopyTo(img3);

            int w = img3.Width, h = img3.Height;
            byte[,] binaryMask = ResizeAndThresholdMask(mask, w, h, threshold);

            // Build BGRA sticker (transparent bg)
            Mat sticker = new Mat(h, w, DepthType.Cv8U, 4);
            sticker.SetTo(new MCvScalar(0, 0, 0, 0));

            using Mat maskMono = BuildMaskMat(binaryMask, w, h);

            // Shadow
            int blurKv = shadowBlur % 2 == 0 ? shadowBlur + 1 : shadowBlur;
            if (blurKv < 1) blurKv = 1;
            using Mat shadowMask = new Mat();
            CvInvoke.GaussianBlur(maskMono, shadowMask, new Size(blurKv, blurKv), 0);
            using Mat shadowShifted = new Mat();
            float[] mShadow = new float[] { 1, 0, 20, 0, 1, 20 };
            using Mat mShadowMat = new Mat(2, 3, DepthType.Cv32F, 1);
            mShadowMat.SetTo(mShadow.SelectMany(BitConverter.GetBytes).ToArray());
            CvInvoke.WarpAffine(shadowMask, shadowShifted, mShadowMat, new Size(w, h));

            var stickerImg = sticker.ToImage<Bgra, byte>();
            var shadowImg = shadowShifted.ToImage<Gray, byte>();
            for (int r = 0; r < h; r++)
                for (int c = 0; c < w; c++)
                    stickerImg[r, c] = new Bgra(0, 0, 0, (byte)(shadowImg[r, c].Intensity * 0.5));
            sticker = stickerImg.Mat.Clone();

            // Copy foreground pixels (no contour yet — drawn after transform)
            var stickerImg2 = sticker.ToImage<Bgra, byte>();
            var srcImg = img3.ToImage<Bgr, byte>();
            for (int r = 0; r < h; r++)
                for (int c = 0; c < w; c++)
                    if (binaryMask[r, c] == 255)
                    {
                        var px = srcImg[r, c];
                        stickerImg2[r, c] = new Bgra(px.Blue, px.Green, px.Red, 255);
                    }
            sticker = stickerImg2.Mat.Clone();

            // Scale + Rotate via WarpAffine around mask centroid
            if (Math.Abs(scaleFactor - 1.0f) > 0.001f || Math.Abs(rotationAngle) > 0.001f)
            {
                // Find centroid of the mask region
                float cx = 0, cy = 0;
                int count = 0;
                for (int r = 0; r < binaryMask.GetLength(0); r++)
                    for (int c = 0; c < binaryMask.GetLength(1); c++)
                        if (binaryMask[r, c] == 255)
                        {
                            cx += c;
                            cy += r;
                            count++;
                        }
                if (count > 0) { cx /= count; cy /= count; }
                else { cx = w / 2f; cy = h / 2f; }

                // Scale coordinates to current sticker dimensions
                float cxS = cx * w / Math.Max(1, binaryMask.GetLength(1));
                float cyS = cy * h / Math.Max(1, binaryMask.GetLength(0));

                // Build combined rotation + scale matrix around the mask centroid
                using Mat transform = new Mat();
                CvInvoke.GetRotationMatrix2D(new PointF(cxS, cyS), rotationAngle, scaleFactor, transform);
                Mat warped = new Mat();
                CvInvoke.WarpAffine(sticker, warped, transform, new Size(w, h),
                    Inter.Linear, Warp.Default, BorderType.Constant, new MCvScalar(0, 0, 0, 0));
                sticker.Dispose();
                sticker = warped;
            }

            // Draw contour border AFTER rotate/scale so it follows the final shape
            if (contourThickness > 0)
            {
                // Extract alpha channel as a mask, find contours, draw border
                using Mat alphaCh = new Mat();
                CvInvoke.ExtractChannel(sticker, alphaCh, 3);
                using Mat alphaBin = new Mat();
                CvInvoke.Threshold(alphaCh, alphaBin, 128, 255, ThresholdType.Binary);
                using VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
                using Mat hierarchy = new Mat();
                CvInvoke.FindContours(alphaBin, contours, hierarchy,
                    RetrType.External, ChainApproxMethod.ChainApproxSimple);
                var borderScalar = new MCvScalar(borderColor.B, borderColor.G, borderColor.R, 255);
                CvInvoke.DrawContours(sticker, contours, -1, borderScalar, contourThickness);
            }

            var result = MatToBitmap(sticker);
            sticker.Dispose();
            return result;
        }

        /// <summary>
        /// Composites a sticker (BGRA, transparent bg) onto a background.
        /// The sticker is center-stamped at its natural size — no stretching.
        /// </summary>
        public static Bitmap CompositeSticker(Bitmap stickerBgra, Bitmap background)
        {
            int outW = background.Width, outH = background.Height;

            // Convert background to BGRA Mat
            using Mat bgMat = BitmapToMat(background);
            using Mat bgBgra = new Mat();
            if (bgMat.NumberOfChannels == 3)
                CvInvoke.CvtColor(bgMat, bgBgra, ColorConversion.Bgr2Bgra);
            else
                bgMat.CopyTo(bgBgra);

            using Mat stickerMat = BitmapToMat(stickerBgra);
            int sw = stickerMat.Width, sh = stickerMat.Height;

            // Compute top-left offset to center the sticker on the background
            int offX = (outW - sw) / 2;
            int offY = (outH - sh) / 2;

            // Alpha-blend only the overlapping region
            var outImg  = bgBgra.ToImage<Bgra, byte>();
            var stickerI = stickerMat.ToImage<Bgra, byte>();

            for (int r = 0; r < sh; r++)
            {
                int br = r + offY;
                if (br < 0 || br >= outH) continue;
                for (int c = 0; c < sw; c++)
                {
                    int bc = c + offX;
                    if (bc < 0 || bc >= outW) continue;
                    var sp = stickerI[r, c];
                    double a = sp.Alpha / 255.0;
                    if (a > 0)
                    {
                        var op = outImg[br, bc];
                        outImg[br, bc] = new Bgra(
                            op.Blue  * (1 - a) + sp.Blue  * a,
                            op.Green * (1 - a) + sp.Green * a,
                            op.Red   * (1 - a) + sp.Red   * a,
                            255);
                    }
                }
            }

            var result = outImg.Mat.Clone();
            return MatToBitmap(result);
        }

        /// <summary>Creates a solid-color background bitmap at the given size.</summary>
        public static Bitmap SolidColorBackground(Color color, int width, int height)
        {
            var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            using var g = Graphics.FromImage(bmp);
            g.Clear(color);
            return bmp;
        }

        // ── Effect 4: Portrait (background blur with feathered mask) ─────────

        /// <summary>
        /// Port of apply_portrait_effect() from functions.py.
        /// Keeps the masked foreground sharp and applies a Gaussian blur to the
        /// background, with an optional feathered alpha blend at the mask edges.
        ///
        ///   blurStrength  – kernel size for background blur (will be forced odd, ≥3)
        ///   featherAmount – kernel size for mask edge softening (0 = hard edge)
        /// </summary>
        public static Bitmap PortraitEffect(
            Bitmap image,
            float[,] mask,
            int blurStrength  = 51,
            int featherAmount = 21)
        {
            using Mat img = BitmapToMat(image);
            using Mat imgBgr = new Mat();
            if (img.NumberOfChannels == 4)
                CvInvoke.CvtColor(img, imgBgr, ColorConversion.Bgra2Bgr);
            else
                img.CopyTo(imgBgr);

            int w = imgBgr.Width, h = imgBgr.Height;

            // --- build float mask (0..1) resized to image dimensions ---
            int srcH = mask.GetLength(0), srcW = mask.GetLength(1);
            float[] flatMask = new float[srcH * srcW];
            for (int r = 0; r < srcH; r++)
                for (int c = 0; c < srcW; c++)
                    flatMask[r * srcW + c] = mask[r, c] > 0.5f ? 1f : 0f;

            Mat floatMaskMat = new Mat(srcH, srcW, DepthType.Cv32F, 1);
            floatMaskMat.SetTo(flatMask.SelectMany(BitConverter.GetBytes).ToArray());

            Mat resizedMask = new Mat();
            CvInvoke.Resize(floatMaskMat, resizedMask, new Size(w, h), interpolation: Inter.Linear);
            floatMaskMat.Dispose();

            // --- feather ---
            Mat alphaMask = new Mat();
            if (featherAmount > 0)
            {
                int fk = featherAmount % 2 == 0 ? featherAmount + 1 : featherAmount;
                if (fk < 1) fk = 1;
                CvInvoke.GaussianBlur(resizedMask, alphaMask, new Size(fk, fk), 0);
            }
            else
            {
                resizedMask.CopyTo(alphaMask);
            }
            resizedMask.Dispose();

            // --- blur the full image for background ---
            int bk = blurStrength % 2 == 0 ? blurStrength + 1 : blurStrength;
            if (bk < 3) bk = 3;
            using Mat blurred = new Mat();
            CvInvoke.GaussianBlur(imgBgr, blurred, new Size(bk, bk), 0);

            // --- blend: sharp_fg * alpha + blurred_bg * (1 - alpha) ---
            // Work in float32 per-channel
            using Mat imgF = new Mat();
            using Mat blurF = new Mat();
            imgBgr.ConvertTo(imgF, DepthType.Cv32F);
            blurred.ConvertTo(blurF, DepthType.Cv32F);

            // Expand alpha to 3 channels
            using Mat alpha3 = new Mat();
            CvInvoke.CvtColor(alphaMask, alpha3, ColorConversion.Gray2Bgr);
            alphaMask.Dispose();

            using Mat alpha3F = new Mat();
            alpha3.ConvertTo(alpha3F, DepthType.Cv32F);

            // Clamp alpha3F to 0..1 (it might exceed 1 after GaussianBlur on binary)
            // We scale it: originally binary 0/1 stored as float, GaussianBlur output already in 0..1 range.
            // But CvtColor on a Cv32F 1ch → 3ch doesn't rescale, so values are still 0..1. Good.

            using Mat oneMinusAlpha = new Mat();
            Mat ones = new Mat(alpha3F.Rows, alpha3F.Cols, DepthType.Cv32F, 3);
            ones.SetTo(new MCvScalar(1, 1, 1));
            CvInvoke.Subtract(ones, alpha3F, oneMinusAlpha);
            ones.Dispose();

            using Mat sharpFg = new Mat();
            using Mat blurBg  = new Mat();
            CvInvoke.Multiply(imgF,  alpha3F,       sharpFg);
            CvInvoke.Multiply(blurF, oneMinusAlpha, blurBg);

            using Mat finalF = new Mat();
            CvInvoke.Add(sharpFg, blurBg, finalF);

            using Mat finalU8 = new Mat();
            finalF.ConvertTo(finalU8, DepthType.Cv8U);

            return MatToBitmap(finalU8);
        }

        // ── Effect 4a: Pixelate (masked, with target) ─────────────────────────

        // ── Effect 4a: Pixelate (masked) ──────────────────────────────────────

        /// <summary>
        /// Port of pixelate_image() from functions.py, applied only to the masked region.
        /// </summary>
        public static Bitmap PixelateMasked(Bitmap image, float[,] mask, int pixelSize)
        {
            if (pixelSize <= 1) return (Bitmap)image.Clone();

            using Mat img = BitmapToMat(image);
            using Mat imgBgr = new Mat();
            if (img.NumberOfChannels == 4)
                CvInvoke.CvtColor(img, imgBgr, ColorConversion.Bgra2Bgr);
            else
                img.CopyTo(imgBgr);

            int w = imgBgr.Width, h = imgBgr.Height;
            byte[,] binaryMask = ResizeAndThresholdMask(mask, w, h);
            using Mat maskMat = BuildMaskMat(binaryMask, w, h);

            // Downsample then upsample
            using Mat small = new Mat();
            using Mat pixelated = new Mat();
            CvInvoke.Resize(imgBgr, small,
                new Size(Math.Max(1, w / pixelSize), Math.Max(1, h / pixelSize)),
                interpolation: Inter.Linear);
            CvInvoke.Resize(small, pixelated, new Size(w, h), interpolation: Inter.Nearest);

            using Mat result = imgBgr.Clone();
            pixelated.CopyTo(result, maskMat);

            return MatToBitmap(result);
        }

        // ── Effect 4b: Blur (masked) ──────────────────────────────────────────

        /// <summary>
        /// Port of blur_image() from functions.py, applied only to the masked region.
        /// </summary>
        public static Bitmap BlurMasked(Bitmap image, float[,] mask, int kernelSize)
        {
            int k = kernelSize % 2 == 0 ? kernelSize + 1 : kernelSize;
            if (k < 1) k = 1;

            using Mat img = BitmapToMat(image);
            using Mat imgBgr = new Mat();
            if (img.NumberOfChannels == 4)
                CvInvoke.CvtColor(img, imgBgr, ColorConversion.Bgra2Bgr);
            else
                img.CopyTo(imgBgr);

            int w = imgBgr.Width, h = imgBgr.Height;
            byte[,] binaryMask = ResizeAndThresholdMask(mask, w, h);
            using Mat maskMat = BuildMaskMat(binaryMask, w, h);

            using Mat blurred = new Mat();
            CvInvoke.GaussianBlur(imgBgr, blurred, new Size(k, k), 10);

            using Mat result = imgBgr.Clone();
            blurred.CopyTo(result, maskMat);

            return MatToBitmap(result);
        }

        // ── Mask overlay renderer ─────────────────────────────────────────────

        /// <summary>
        /// Renders semi-transparent colored mask overlays on top of the image.
        /// Returns a new Bitmap with overlays composited.
        /// </summary>
        public static Bitmap RenderMaskOverlays(
            Bitmap image,
            IList<float[,]> masks,
            IList<Color> colors,
            IList<bool> selected,
            IList<float> scores,
            float alpha = 0.45f)
        {
            if (masks == null || masks.Count == 0) return (Bitmap)image.Clone();

            using Mat img = BitmapToMat(image);
            using Mat imgBgr = new Mat();
            if (img.NumberOfChannels == 4)
                CvInvoke.CvtColor(img, imgBgr, ColorConversion.Bgra2Bgr);
            else
                img.CopyTo(imgBgr);

            using Mat overlay = imgBgr.Clone();

            bool anySelected = selected.Any(s => s);

            for (int i = 0; i < masks.Count; i++)
            {
                // Once a mask is chosen, hide all unselected masks
                if (anySelected && !selected[i]) continue;

                byte[,] binaryMask = ResizeAndThresholdMask(masks[i], imgBgr.Width, imgBgr.Height);
                using Mat maskMat = BuildMaskMat(binaryMask, imgBgr.Width, imgBgr.Height);

                Color c = colors[i];
                float maskAlpha = selected[i] ? alpha : alpha * 0.35f;

                using Mat coloredMask = new Mat(imgBgr.Rows, imgBgr.Cols, DepthType.Cv8U, 3);
                coloredMask.SetTo(new MCvScalar(0, 0, 0));
                coloredMask.SetTo(new MCvScalar(c.B, c.G, c.R), maskMat);

                CvInvoke.AddWeighted(overlay, 1.0, coloredMask, maskAlpha, 0, overlay);

                // Contour
                using VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
                using Mat hier = new Mat();
                CvInvoke.FindContours(maskMat, contours, hier,
                    RetrType.External, ChainApproxMethod.ChainApproxSimple);

                var contourColor = selected[i]
                    ? new MCvScalar(0, 255, 255)   // bright cyan for selected
                    : new MCvScalar(c.B, c.G, c.R); // match mask color when unselected
                CvInvoke.DrawContours(overlay, contours, -1, contourColor,
                    selected[i] ? 4 : 3);

                // Mask label tag
                if (contours.Size > 0)
                {
                    // Find the topmost contour bounding rect
                    Rectangle topRect = default;
                    for (int ci = 0; ci < contours.Size; ci++)
                    {
                        using var cn = contours[ci];
                        if (cn.Size == 0) continue;
                        var r = CvInvoke.BoundingRectangle(cn);
                        if (topRect.IsEmpty || r.Y < topRect.Y)
                            topRect = r;
                    }

                    if (!topRect.IsEmpty)
                    {
                        string numStr = (i + 1).ToString();
                        string scoreStr = scores != null && i < scores.Count
                            ? $"{(scores[i] * 100):F0}%"
                            : "";

                        double fontScale = Math.Max(0.5, Math.Min(imgBgr.Width, imgBgr.Height) / 900.0);
                        int thick = fontScale >= 0.7 ? 2 : 1;

                        // Measure both parts
                        int bl1 = 0, bl2 = 0;
                        var numSize = CvInvoke.GetTextSize(numStr, FontFace.HersheySimplex,
                            fontScale * 0.8, thick, ref bl1);
                        var scoreSize = scoreStr.Length > 0
                            ? CvInvoke.GetTextSize(scoreStr, FontFace.HersheySimplex,
                                fontScale * 0.7, thick, ref bl2)
                            : new Size(0, 0);

                        int pad = (int)(6 * fontScale);
                        int circleD = (int)(numSize.Height * 1.6);
                        int tagW = circleD + pad + scoreSize.Width + pad * 2;
                        int tagH = circleD + pad;

                        // Position above the top of the mask
                        int tagX = topRect.X + topRect.Width / 2 - tagW / 2;
                        int tagY = topRect.Y - tagH - (int)(4 * fontScale);
                        tagX = Math.Clamp(tagX, 2, imgBgr.Width - tagW - 2);
                        tagY = Math.Max(2, tagY);

                        // Rounded tag background
                        var tagRect = new Rectangle(tagX, tagY, tagW, tagH);
                        CvInvoke.Rectangle(overlay, tagRect,
                            new MCvScalar(20, 20, 20), -1);
                        CvInvoke.Rectangle(overlay, tagRect,
                            new MCvScalar(c.B, c.G, c.R), (int)Math.Max(1, fontScale));

                        // Colored circle with mask number
                        int circleX = tagX + pad;
                        int circleY = tagY + (tagH - circleD) / 2;
                        var circleCenter = new Point(circleX + circleD / 2, circleY + circleD / 2);
                        CvInvoke.Circle(overlay, circleCenter, circleD / 2,
                            new MCvScalar(c.B, c.G, c.R), -1);
                        // Number text centered in circle
                        int numX = circleX + (circleD - numSize.Width) / 2;
                        int numY = circleY + (circleD + numSize.Height) / 2;
                        CvInvoke.PutText(overlay, numStr,
                            new Point(numX, numY),
                            FontFace.HersheySimplex, fontScale * 0.8,
                            new MCvScalar(255, 255, 255), thick);

                        // Score text
                        if (scoreStr.Length > 0)
                        {
                            int scoreX = circleX + circleD + pad;
                            int scoreY = tagY + (tagH + scoreSize.Height) / 2;
                            CvInvoke.PutText(overlay, scoreStr,
                                new Point(scoreX, scoreY),
                                FontFace.HersheySimplex, fontScale * 0.7,
                                new MCvScalar(220, 220, 220), thick);
                        }
                    }
                }
            }

            return MatToBitmap(overlay);
        }

        // ── Internal helpers ──────────────────────────────────────────────────

        private static Mat BuildMaskMat(byte[,] binaryMask, int w, int h)
        {
            byte[] flat = new byte[h * w];
            for (int r = 0; r < h; r++)
                for (int c = 0; c < w; c++)
                    flat[r * w + c] = binaryMask[r, c];

            Mat m = new Mat(h, w, DepthType.Cv8U, 1);
            m.SetTo(flat);
            return m;
        }
    }
}
