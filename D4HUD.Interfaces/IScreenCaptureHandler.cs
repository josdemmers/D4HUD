using D4HUD.Entities;

namespace D4HUD.Interfaces
{
    public interface IScreenCaptureHandler
    {
        void Init();
        ROICaptureInfo? GetRoiCaptureInfo(string id);
    }
}