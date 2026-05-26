using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace VisionEditCV.Shared.Helpers;

public static class ImageHelper
{
    public static Bitmap MatToBitmap(Mat mat)
    {
        if (mat.NumberOfChannels == 3)
        {
            using var bgra = new Mat();
            CvInvoke.CvtColor(mat, bgra, ColorConversion.Bgr2Bgra);
            return MatToBitmapInternal(bgra);
        }
        return MatToBitmapInternal(mat);
    }

    private static Bitmap MatToBitmapInternal(Mat mat)
    {
        var bitmap = new WriteableBitmap(
            new PixelSize(mat.Width, mat.Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using (var fb = bitmap.Lock())
        {
            using var matBgra = new Mat(mat.Height, mat.Width, DepthType.Cv8U, 4, fb.Address, fb.RowBytes);
            mat.CopyTo(matBgra);
        }

        return bitmap;
    }

    public static Mat BitmapToMat(Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms);
        ms.Seek(0, SeekOrigin.Begin);
        
        // Emgu.CV doesn't have a direct way to load from Stream easily without temporary file 
        // in some versions, but we can use Imread with temporary file or byte array.
        var bytes = ms.ToArray();
        Mat mat = new Mat();
        CvInvoke.Imdecode(bytes, ImreadModes.AnyColor, mat);
        return mat;
    }
}
