using D4HUD.Interfaces;
using D4HUD.Services;
using Microsoft.Extensions.Logging;
using Prism.Commands;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace D4HUD.ViewModels
{
    public class MainWindowViewModel
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly ILogger _logger;
        private readonly ISettingsManager _settingsManager;
        private readonly IScreenCaptureHandler _screenCaptureHandler;
        private readonly IOverlayHandler _overlayHandler;

        private string _windowTitle = $"Diablo IV HUD v{Assembly.GetExecutingAssembly().GetName().Version}";

        // Start of Constructor region

        #region Constructor

        public MainWindowViewModel(IEventAggregator eventAggregator, ILogger<MainWindowViewModel> logger, ISettingsManager settingsManager, IScreenCaptureHandler screenCaptureHandler, IOverlayHandler overlayHandler)
        {
            // Init IEventAggregator
            _eventAggregator = eventAggregator;

            // Init logger
            _logger = logger;

            // Init services
            _settingsManager = settingsManager;
            _screenCaptureHandler = screenCaptureHandler;
            _overlayHandler = overlayHandler;

            // Init View commands
            ApplicationLoadedCmd = new DelegateCommand(ApplicationLoaded);
        }

        #endregion

        // Start of Properties region

        #region Properties

        public string WindowTitle { get => _windowTitle; set => _windowTitle = value; }
        public DelegateCommand ApplicationLoadedCmd { get; }

        #endregion

        // Start of Events region

        #region Events

        private void ApplicationLoaded()
        {
            _logger.LogInformation(WindowTitle);
            _screenCaptureHandler.Init();
        }

        #endregion

        // Start of Methods region

        #region Methods

        #endregion
    }
}
