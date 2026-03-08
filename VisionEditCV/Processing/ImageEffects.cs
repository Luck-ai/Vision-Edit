using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace VisionEditCV.Processing
{
    public static class ImageEffects
    {
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

            if (channels == 4)
            {
            }
            return mat;
        }

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

        public static byte[,] ResizeAndThresholdMask(float[,] mask, int targetW, int targetH,
            float threshold = 0.5f)
        {
            int srcH = mask.GetLength(0);
            int srcW = mask.GetLength(1);
            int srcBytes = srcH * srcW * sizeof(float);

            var rawBytes = new byte[srcBytes];
            var gcHandle = GCHandle.Alloc(mask, GCHandleType.Pinned);
            try { Marshal.Copy(gcHandle.AddrOfPinnedObject(), rawBytes, 0, srcBytes); }
            finally { gcHandle.Free(); }

            using Mat srcMat = new Mat(srcH, srcW, DepthType.Cv32F, 1);
            srcMat.SetTo(rawBytes);

            using Mat dstMat = new Mat();
            CvInvoke.Resize(srcMat, dstMat, new Size(targetW, targetH),
                interpolation: Inter.Linear);

            using Mat dstU8 = new Mat();
            CvInvoke.Threshold(dstMat, dstU8, threshold, 255, ThresholdType.Binary);

            using Mat dstByte = new Mat();
            dstU8.ConvertTo(dstByte, DepthType.Cv8U);

            byte[] dstData = new byte[targetH * targetW];
            dstByte.CopyTo(dstData);

            var binary = new byte[targetH, targetW];
            Buffer.BlockCopy(dstData, 0, binary, 0, dstData.Length);
            return binary;
        }

        public static Bitmap ColorGrading(
            Bitmap image,
            float[,] mask,
            Color tintColor,
            float tintStrength,        
            int brightness,            
            float contrast,            
            bool blackAndWhite)
        {
            using Mat img = BitmapToMat(image);
            using Mat imgBgr = new Mat();

            if (img.NumberOfChannels == 4)
                CvInvoke.CvtColor(img, imgBgr, ColorConversion.Bgra2Bgr);
            else
                img.CopyTo(imgBgr);

            byte[,] binaryMask = ResizeAndThresholdMask(mask, imgBgr.Width, imgBgr.Height);
            using Mat maskMat = BuildMaskMat(binaryMask, imgBgr.Width, imgBgr.Height);

            Mat processed = imgBgr.Clone();

            if (blackAndWhite)
            {
                using Mat gray = new Mat();
                using Mat grayBgr = new Mat();
                CvInvoke.CvtColor(processed, gray, ColorConversion.Bgr2Gray);
                CvInvoke.CvtColor(gray, grayBgr, ColorConversion.Gray2Bgr);
                processed.Dispose();
                processed = grayBgr.Clone();
            }

            using Mat adjusted = new Mat();
            CvInvoke.ConvertScaleAbs(processed, adjusted, contrast, brightness);
            processed.Dispose();
            processed = adjusted.Clone();

            if (tintStrength > 0f)
            {
                using Mat tintLayer = new Mat(processed.Rows, processed.Cols,
                    processed.Depth, processed.NumberOfChannels);
                tintLayer.SetTo(new MCvScalar(tintColor.B, tintColor.G, tintColor.R));

                using Mat tinted = new Mat();
                CvInvoke.AddWeighted(processed, 1.0 - tintStrength,
                    tintLayer, tintStrength, 0, tinted);
                processed.Dispose();
                processed = tinted.Clone();
            }

            using Mat result = imgBgr.Clone();
            processed.CopyTo(result, maskMat);
            processed.Dispose();

            return MatToBitmap(result);
        }

        private static Rectangle MaskBoundingRect(byte[,] binaryMask, int w, int h, int pad = 8)
        {
            int minX = w, minY = h, maxX = 0, maxY = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (binaryMask[y, x] != 0)
                    {
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
            if (minX > maxX) return Rectangle.Empty;
            int rx = Math.Max(0, minX - pad);
            int ry = Math.Max(0, minY - pad);
            int rw = Math.Min(w, maxX + pad + 1) - rx;
            int rh = Math.Min(h, maxY + pad + 1) - ry;
            return new Rectangle(rx, ry, rw, rh);
        }

        public static Bitmap StylizeMasked(Bitmap image, float[,] mask, int sigmaS, float sigmaR)
        {
            using Mat img = BitmapToMat(image);
            using Mat imgBgr = new Mat();

            if (img.NumberOfChannels == 4)
                CvInvoke.CvtColor(img, imgBgr, ColorConversion.Bgra2Bgr);
            else
                img.CopyTo(imgBgr);

            byte[,] binaryMask = ResizeAndThresholdMask(mask, imgBgr.Width, imgBgr.Height);
            Rectangle roi = MaskBoundingRect(binaryMask, imgBgr.Width, imgBgr.Height);
            if (roi.IsEmpty) return MatToBitmap(imgBgr);

            using Mat maskMat = BuildMaskMat(binaryMask, imgBgr.Width, imgBgr.Height);

            using Mat crop = new Mat(imgBgr, roi);
            using Mat styledCrop = new Mat();
            CvInvoke.Stylization(crop, styledCrop, sigmaS, sigmaR);

            using Mat result = imgBgr.Clone();
            using Mat maskRoi = new Mat(maskMat, roi);
            styledCrop.CopyTo(new Mat(result, roi), maskRoi);

            return MatToBitmap(result);
        }

        public static Bitmap PencilSketchMasked(Bitmap image, float[,] mask, int sigmaS, float shadeFactor)
        {
            using Mat img = BitmapToMat(image);
            using Mat imgBgr = new Mat();

            if (img.NumberOfChannels == 4)
                CvInvoke.CvtColor(img, imgBgr, ColorConversion.Bgra2Bgr);
            else
                img.CopyTo(imgBgr);

            byte[,] binaryMask = ResizeAndThresholdMask(mask, imgBgr.Width, imgBgr.Height);
            Rectangle roi = MaskBoundingRect(binaryMask, imgBgr.Width, imgBgr.Height);
            if (roi.IsEmpty) return MatToBitmap(imgBgr);

            using Mat maskMat = BuildMaskMat(binaryMask, imgBgr.Width, imgBgr.Height);

            using Mat crop = new Mat(imgBgr, roi);
            using Mat gray = new Mat();
            using Mat colorSketch = new Mat();
            CvInvoke.PencilSketch(crop, gray, colorSketch, sigmaS, 0.07f, shadeFactor);

            using Mat result = imgBgr.Clone();
            using Mat maskRoi = new Mat(maskMat, roi);
            colorSketch.CopyTo(new Mat(result, roi), maskRoi);

            return MatToBitmap(result);
        }

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
            using Mat maskMono = BuildMaskMat(binaryMask, w, h);

            Mat sticker = new Mat(h, w, DepthType.Cv8U, 4);
            sticker.SetTo(new MCvScalar(0, 0, 0, 0));

            if (shadowBlur > 0)
            {
                int blurKv = shadowBlur % 2 == 0 ? shadowBlur + 1 : shadowBlur;
                using Mat shadowBlurred = new Mat();
                CvInvoke.GaussianBlur(maskMono, shadowBlurred, new Size(blurKv, blurKv), 0);

                float[] mShadow = new float[] { 1, 0, 20, 0, 1, 20 };
                using Mat mShadowMat = new Mat(2, 3, DepthType.Cv32F, 1);
                mShadowMat.SetTo(mShadow.SelectMany(BitConverter.GetBytes).ToArray());
                using Mat shadowShifted = new Mat();
                CvInvoke.WarpAffine(shadowBlurred, shadowShifted, mShadowMat, new Size(w, h));

                using Mat shadowF = new Mat();
                shadowShifted.ConvertTo(shadowF, DepthType.Cv32F, 1.0 / 255.0);
                using Mat shadowAlpha = new Mat();
                CvInvoke.Multiply(shadowF, new ScalarArray(new MCvScalar(0.5)), shadowAlpha);
                using Mat shadowAlpha8 = new Mat();
                shadowAlpha.ConvertTo(shadowAlpha8, DepthType.Cv8U, 255.0);

                using Mat zeros = new Mat(h, w, DepthType.Cv8U, 1);
                zeros.SetTo(new MCvScalar(0));
                using var channels = new Emgu.CV.Util.VectorOfMat(zeros, zeros, zeros, shadowAlpha8);
                CvInvoke.Merge(channels, sticker);
            }

            using Mat srcBgra = new Mat();
            CvInvoke.CvtColor(img3, srcBgra, ColorConversion.Bgr2Bgra);

            using Mat srcAlpha = new Mat();
            CvInvoke.ExtractChannel(srcBgra, srcAlpha, 3);
            CvInvoke.BitwiseOr(srcAlpha, maskMono, srcAlpha);  

            using var fgChannels = new Emgu.CV.Util.VectorOfMat();
            CvInvoke.Split(srcBgra, fgChannels);

            using var fgWithAlpha = new Emgu.CV.Util.VectorOfMat(
                fgChannels[0], fgChannels[1], fgChannels[2], maskMono);
            using Mat fgBgra = new Mat();
            CvInvoke.Merge(fgWithAlpha, fgBgra);

            fgBgra.CopyTo(sticker, maskMono);

            if (Math.Abs(scaleFactor - 1.0f) > 0.001f || Math.Abs(rotationAngle) > 0.001f)
            {
                using Mat maskF = new Mat();
                maskMono.ConvertTo(maskF, DepthType.Cv32F);
                var moments = CvInvoke.Moments(maskF, false);
                float cx, cy;
                if (moments.M00 > 0)
                { cx = (float)(moments.M10 / moments.M00); cy = (float)(moments.M01 / moments.M00); }
                else
                { cx = w / 2f; cy = h / 2f; }

                using Mat transform = new Mat();
                CvInvoke.GetRotationMatrix2D(new PointF(cx, cy), rotationAngle, scaleFactor, transform);
                Mat warped = new Mat();
                CvInvoke.WarpAffine(sticker, warped, transform, new Size(w, h),
                    Inter.Linear, Warp.Default, BorderType.Constant, new MCvScalar(0, 0, 0, 0));
                sticker.Dispose();
                sticker = warped;
            }

            if (contourThickness > 0)
            {
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

        public static Bitmap CompositeSticker(Bitmap stickerBgra, Bitmap background)
        {
            int outW = background.Width, outH = background.Height;

            using Mat bgMat = BitmapToMat(background);
            using Mat bgBgra = new Mat();
            if (bgMat.NumberOfChannels == 3)
                CvInvoke.CvtColor(bgMat, bgBgra, ColorConversion.Bgr2Bgra);
            else
                bgMat.CopyTo(bgBgra);

            using Mat stickerMat = BitmapToMat(stickerBgra);
            int sw = stickerMat.Width, sh = stickerMat.Height;

            int offX = (outW - sw) / 2;
            int offY = (outH - sh) / 2;

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

        public static Bitmap SolidColorBackground(Color color, int width, int height)
        {
            var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            using var g = Graphics.FromImage(bmp);
            g.Clear(color);
            return bmp;
        }

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

            int srcH = mask.GetLength(0), srcW = mask.GetLength(1);
            float[] flatMask = new float[srcH * srcW];
            for (int r = 0; r < srcH; r++)
                for (int c = 0; c < srcW; c++)
                    flatMask[r * srcW + c] = mask[r, c] > 0.5f ? 1f : 0f;

            Mat floatMaskMat = new Mat(srcH, srcW, DepthType.Cv32F, 1);
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(flatMask,
                System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                System.Runtime.InteropServices.Marshal.Copy(
                    flatMask, 0, floatMaskMat.DataPointer, flatMask.Length);
            }
            finally { handle.Free(); }

            Mat resizedMask = new Mat();
            CvInvoke.Resize(floatMaskMat, resizedMask, new Size(w, h), interpolation: Inter.Linear);
            floatMaskMat.Dispose();

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

            int bk = blurStrength % 2 == 0 ? blurStrength + 1 : blurStrength;
            if (bk < 3) bk = 3;
            using Mat blurred = new Mat();
            CvInvoke.GaussianBlur(imgBgr, blurred, new Size(bk, bk), 0);

            using Mat imgF = new Mat();
            using Mat blurF = new Mat();
            imgBgr.ConvertTo(imgF, DepthType.Cv32F);
            blurred.ConvertTo(blurF, DepthType.Cv32F);

            using Mat alpha3 = new Mat();
            CvInvoke.CvtColor(alphaMask, alpha3, ColorConversion.Gray2Bgr);
            alphaMask.Dispose();

            using Mat alpha3F = new Mat();
            alpha3.ConvertTo(alpha3F, DepthType.Cv32F);

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
                if (anySelected && !selected[i]) continue;

                byte[,] binaryMask = ResizeAndThresholdMask(masks[i], imgBgr.Width, imgBgr.Height);
                using Mat maskMat = BuildMaskMat(binaryMask, imgBgr.Width, imgBgr.Height);

                Color c = colors[i];
                float maskAlpha = selected[i] ? alpha : alpha * 0.35f;

                using Mat coloredMask = new Mat(imgBgr.Rows, imgBgr.Cols, DepthType.Cv8U, 3);
                coloredMask.SetTo(new MCvScalar(0, 0, 0));
                coloredMask.SetTo(new MCvScalar(c.B, c.G, c.R), maskMat);

                CvInvoke.AddWeighted(overlay, 1.0, coloredMask, maskAlpha, 0, overlay);

                using VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
                using Mat hier = new Mat();
                CvInvoke.FindContours(maskMat, contours, hier,
                    RetrType.External, ChainApproxMethod.ChainApproxSimple);

                if (!selected[i])
                {
                    CvInvoke.DrawContours(overlay, contours, -1,
                        new MCvScalar(c.B, c.G, c.R), 3);
                }

                if (contours.Size > 0)
                {
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

                        int tagX = topRect.X + topRect.Width / 2 - tagW / 2;
                        int tagY = topRect.Y - tagH - (int)(4 * fontScale);
                        tagX = Math.Clamp(tagX, 2, imgBgr.Width - tagW - 2);
                        tagY = Math.Max(2, tagY);

                        var tagRect = new Rectangle(tagX, tagY, tagW, tagH);
                        CvInvoke.Rectangle(overlay, tagRect,
                            new MCvScalar(20, 20, 20), -1);
                        CvInvoke.Rectangle(overlay, tagRect,
                            new MCvScalar(c.B, c.G, c.R), (int)Math.Max(1, fontScale));

                        int circleX = tagX + pad;
                        int circleY = tagY + (tagH - circleD) / 2;
                        var circleCenter = new Point(circleX + circleD / 2, circleY + circleD / 2);
                        CvInvoke.Circle(overlay, circleCenter, circleD / 2,
                            new MCvScalar(c.B, c.G, c.R), -1);

                        int numX = circleX + (circleD - numSize.Width) / 2;
                        int numY = circleY + (circleD + numSize.Height) / 2;
                        CvInvoke.PutText(overlay, numStr,
                            new Point(numX, numY),
                            FontFace.HersheySimplex, fontScale * 0.8,
                            new MCvScalar(255, 255, 255), thick);

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

        public static float[,] TransformMaskForDisplay(
            float[,] mask,
            int imageW, int imageH,
            float scaleFactor,
            float rotationAngle,
            float threshold = 0.5f,
            int previewMaxDim = 0)
        {
            if (Math.Abs(scaleFactor - 1.0f) <= 0.001f && Math.Abs(rotationAngle) <= 0.001f)
            {
                var copy = new float[mask.GetLength(0), mask.GetLength(1)];
                Array.Copy(mask, copy, mask.Length);
                return copy;
            }

            int workW = imageW, workH = imageH;
            if (previewMaxDim > 0 && Math.Max(imageW, imageH) > previewMaxDim)
            {
                float s = (float)previewMaxDim / Math.Max(imageW, imageH);
                workW = Math.Max(1, (int)(imageW * s));
                workH = Math.Max(1, (int)(imageH * s));
            }

            byte[,] binary = ResizeAndThresholdMask(mask, workW, workH, threshold);
            using Mat maskMono = BuildMaskMat(binary, workW, workH);

            using Mat maskF = new Mat();
            maskMono.ConvertTo(maskF, DepthType.Cv32F);
            var moments = CvInvoke.Moments(maskF, false);
            float cx, cy;
            if (moments.M00 > 0)
            { cx = (float)(moments.M10 / moments.M00); cy = (float)(moments.M01 / moments.M00); }
            else
            { cx = workW / 2f; cy = workH / 2f; }

            using Mat transform = new Mat();
            CvInvoke.GetRotationMatrix2D(new PointF(cx, cy), rotationAngle, scaleFactor, transform);
            using Mat warped = new Mat();
            CvInvoke.WarpAffine(maskMono, warped, transform, new Size(workW, workH),
                Inter.Linear, Warp.Default, BorderType.Constant, new MCvScalar(0));

            Mat finalMono = warped;
            Mat? upscaled = null;
            if (workW != imageW || workH != imageH)
            {
                upscaled = new Mat();
                CvInvoke.Resize(warped, upscaled, new Size(imageW, imageH), interpolation: Inter.Nearest);
                finalMono = upscaled;
            }

            byte[] data = new byte[imageH * imageW];
            finalMono.CopyTo(data);
            upscaled?.Dispose();

            var result = new float[imageH, imageW];
            for (int r = 0; r < imageH; r++)
                for (int c = 0; c < imageW; c++)
                    result[r, c] = data[r * imageW + c] > 128 ? 1f : 0f;
            return result;
        }

        private static Mat BuildMaskMat(byte[,] binaryMask, int w, int h)
        {
            byte[] flat = new byte[h * w];
            Buffer.BlockCopy(binaryMask, 0, flat, 0, flat.Length);
            Mat m = new Mat(h, w, DepthType.Cv8U, 1);
            m.SetTo(flat);
            return m;
        }
    }
}
