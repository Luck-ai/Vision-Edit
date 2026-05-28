using System;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;
using VisionEditCV.Shared.Helpers;

namespace VisionEditCV.Desktop
{
    public class DesktopUpdateService : IUpdateService
    {
        private const string UpdateRepoUrl = "https://github.com/Luck-ai/Vision-Edit";
        private readonly UpdateManager? _updateManager;
        private UpdateInfo? _pendingUpdate;

        public DesktopUpdateService()
        {
            try
            {
                _updateManager = new UpdateManager(new GithubSource(UpdateRepoUrl, accessToken: null, prerelease: false));
            }
            catch
            {
                _updateManager = null;
            }
        }

        public bool IsInstalled => _updateManager?.IsInstalled == true;

        public async Task<string?> CheckForUpdatesAsync()
        {
            if (_updateManager is null) return null;
            _pendingUpdate = await _updateManager.CheckForUpdatesAsync();
            return _pendingUpdate?.TargetFullRelease?.Version?.ToString();
        }

        public async Task DownloadUpdatesAsync()
        {
            if (_updateManager is null || _pendingUpdate is null) return;
            await _updateManager.DownloadUpdatesAsync(_pendingUpdate);
        }

        public void ApplyUpdatesAndRestart()
        {
            if (_updateManager is null || _pendingUpdate is null) return;
            _updateManager.ApplyUpdatesAndRestart(_pendingUpdate);
        }
    }
}
