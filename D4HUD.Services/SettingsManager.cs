using D4HUD.Entities;
using D4HUD.Interfaces;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace D4HUD.Services
{
    public class SettingsManager : ISettingsManager
    {
        private readonly IEventAggregator _eventAggregator;

        private SettingsD4 _settings = new SettingsD4();
        private SettingsD4 _settings1920x1080 = new SettingsD4();
        private SettingsD4 _settings2560x1440 = new SettingsD4();

        // Start of Constructor region

        #region Constructor

        public SettingsManager(IEventAggregator eventAggregator)
        {
            // Init IEventAggregator
            _eventAggregator = eventAggregator;

            // Load defaults
            LoadSettings1920x1080();
            LoadSettings2560x1440();
        }

        #endregion

        // Start of Properties region

        #region Properties

        public SettingsD4 Settings { get => _settings; set => _settings = value; }
        public SettingsD4 Settings1920x1080 { get => _settings1920x1080; set => _settings1920x1080 = value; }
        public SettingsD4 Settings2560x1440 { get => _settings2560x1440; set => _settings2560x1440 = value; }

        #endregion

        // Start of Events region

        #region Events

        #endregion

        // Start of Methods region

        #region Methods

        private void LoadSettings1920x1080()
        {
            string fileName = "Config/Settings.1920x1080.json";
            if (File.Exists(fileName))
            {
                using FileStream stream = File.OpenRead(fileName);
                _settings1920x1080 = JsonSerializer.Deserialize<SettingsD4>(stream) ?? new SettingsD4();
            }
        }

        private void LoadSettings2560x1440()
        {
            string fileName = "Config/Settings.2560x1440.json";
            if (File.Exists(fileName))
            {
                using FileStream stream = File.OpenRead(fileName);
                _settings2560x1440 = JsonSerializer.Deserialize<SettingsD4>(stream) ?? new SettingsD4();
            }
        }

        public void LoadSettings()
        {
            string fileName = "Config/Settings.json";
            if (File.Exists(fileName))
            {
                using FileStream stream = File.OpenRead(fileName);
                _settings = JsonSerializer.Deserialize<SettingsD4>(stream) ?? new SettingsD4();
            }
        }

        public void SaveSettings()
        {
            string fileName = "Config/Settings.json";
            string path = Path.GetDirectoryName(fileName) ?? string.Empty;
            Directory.CreateDirectory(path);

            using FileStream stream = File.Create(fileName);
            var options = new JsonSerializerOptions { WriteIndented = true };
            JsonSerializer.Serialize(stream, _settings, options);
        }

        #endregion

    }
}
