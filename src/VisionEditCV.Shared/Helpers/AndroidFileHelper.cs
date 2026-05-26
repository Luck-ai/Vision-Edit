using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace VisionEditCV.Desktop.Helpers;

public static class AndroidFileHelper
{
    public static async Task<string> EnsureLocalFilePathAsync(IStorageFile file)
    {
        if (file == null)
            throw new ArgumentNullException(nameof(file));

        var uri = file.Path;
        
        // If it's a standard file scheme, we can try to use LocalPath directly
        if (uri.IsFile)
        {
            return uri.LocalPath;
        }

        // For content:// URIs or any other non-file scheme (e.g. on Android),
        // we copy the stream to a temporary local file so OpenCV can consume it.
        string tempDir = Path.Combine(Path.GetTempPath(), "VisionEditCV_Cache");
        if (!Directory.Exists(tempDir))
        {
            Directory.CreateDirectory(tempDir);
        }

        // Clean up old cached files to save space
        try
        {
            var directory = new DirectoryInfo(tempDir);
            foreach (var existingFile in directory.GetFiles())
            {
                if (existingFile.LastWriteTime < DateTime.Now.AddDays(-1))
                {
                    existingFile.Delete();
                }
            }
        }
        catch
        {
            // Ignore clean up errors
        }

        // Ensure we preserve the original extension so OpenCV/Imread can decode it properly
        string fileName = file.Name;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "temp_image.png";
        }
        else
        {
            // Make filename unique to avoid conflicts but keep extension
            string ext = Path.GetExtension(fileName);
            fileName = $"{Guid.NewGuid()}{ext}";
        }

        string cachedPath = Path.Combine(tempDir, fileName);

        using (var stream = await file.OpenReadAsync())
        using (var fileStream = File.Create(cachedPath))
        {
            await stream.CopyToAsync(fileStream);
        }

        return cachedPath;
    }
}
