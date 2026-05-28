using System;
using System.Threading.Tasks;

namespace VisionEditCV.Shared.Helpers
{
    public interface IUpdateService
    {
        bool IsInstalled { get; }
        /// <summary>
        /// Checks for updates. Returns the version string if an update is available, or null if no update.
        /// </summary>
        Task<string?> CheckForUpdatesAsync();
        Task DownloadUpdatesAsync();
        void ApplyUpdatesAndRestart();
    }
}
