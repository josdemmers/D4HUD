using D4HUD.Entities;

namespace D4HUD.Interfaces
{
    public interface ISettingsManager
    {
        public SettingsD4 Settings { get; }
        public SettingsD4 Settings1920x1080 { get; }
        public SettingsD4 Settings2560x1440 { get; }

        public void LoadSettings();
        public void SaveSettings();
    }
}
