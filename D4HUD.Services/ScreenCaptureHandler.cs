using D4HUD.Entities;
using D4HUD.Events;
using D4HUD.Helpers;
using D4HUD.Interfaces;
using Microsoft.Extensions.Logging;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;

namespace D4HUD.Services
{
    public class ScreenCaptureHandler : IScreenCaptureHandler
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly ILogger _logger;

        private Bitmap? _currentScreen = null;
        private double _delayUpdateScreen = 100;
        private double _delayUpdateMouse = 100;
        private ScreenCapture _screenCapture = new ScreenCapture();
        private int _coordsMouseX = 0;
        private int _coordsMouseY = 0;
        private List<ROICaptureInfo> _roiCaptures = new List<ROICaptureInfo>();

        // Start of Constructor region

        #region Constructor

        public ScreenCaptureHandler(IEventAggregator eventAggregator, ILogger<ScreenCaptureHandler> logger)
        {
            // Init IEventAggregator
            _eventAggregator = eventAggregator;
            _eventAggregator.GetEvent<ROIUpdatedEvent>().Subscribe(HandleROIUpdatedEvent);

            // Init logger
            _logger = logger;
        }

        #endregion

        // Start of Events region

        #region Events

        #endregion

        // Start of Properties region

        #region Properties

        #endregion

        // Start of Event handlers region

        #region Event handlers

        private void HandleROIUpdatedEvent(ROIUpdatedEventParams roiUpdatedEventParams)
        {
            var roiCaptureInfo = _roiCaptures.FirstOrDefault(roi => roi.Id.Equals(roiUpdatedEventParams.Id));

            if (roiCaptureInfo != null) 
            {
                roiCaptureInfo.Left = roiUpdatedEventParams.Left;
                roiCaptureInfo.Top = roiUpdatedEventParams.Top;
                roiCaptureInfo.Width = roiUpdatedEventParams.Width;
                roiCaptureInfo.Height = roiUpdatedEventParams.Height;
            }
            else
            {
                _roiCaptures.Add(new ROICaptureInfo
                {
                    Id = roiUpdatedEventParams.Id,
                    Left = roiUpdatedEventParams.Left,
                    Top = roiUpdatedEventParams.Top,
                    Width = roiUpdatedEventParams.Width,
                    Height = roiUpdatedEventParams.Height
                });
            }
        }

        #endregion

        // Start of Methods region

        #region Methods

        public void Init()
        {
            _ = StartScreenTask();
            _ = StartMouseTask();
        }

        private async Task StartScreenTask()
        {
            while (true)
            {
                await Task.Run(() =>
                {
                    try
                    {
                        UpdateScreen();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, MethodBase.GetCurrentMethod()?.Name);
                        _delayUpdateScreen = 10000;
                    }
                });
                await Task.Delay(TimeSpan.FromMilliseconds(_delayUpdateScreen));
            }
        }

        private async Task StartMouseTask()
        {
            while (true)
            {
                await Task.Run(() =>
                {
                    try
                    {
                        Application.Current.Dispatcher.Invoke((Action)delegate
                        {
                            UpdateMouse();
                        });
                        
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, MethodBase.GetCurrentMethod()?.Name);
                        _delayUpdateMouse = 10000;
                    }
                });
                await Task.Delay(TimeSpan.FromMilliseconds(_delayUpdateMouse));
            }
        }

        private void UpdateScreen()
        {
            // Debug mode - using firefox
            IntPtr windowHandle = IntPtr.Zero;
            Process[] processes = Process.GetProcessesByName("firefox");
            foreach (Process p in processes)
            {
                windowHandle = p.MainWindowHandle;
                if (p.MainWindowTitle.StartsWith("Screenshot"))
                {
                    break;
                }
            }

            // Release mode
            /*IntPtr windowHandle = IntPtr.Zero;
            Process[] processes = Process.GetProcessesByName("Diablo IV");
            foreach (Process p in processes)
            {
                windowHandle = p.MainWindowHandle;
                if (p.MainWindowTitle.StartsWith("Screenshot"))
                {
                    break;
                }
            }*/

            if (windowHandle.ToInt64() > 0)
            {
                _eventAggregator.GetEvent<WindowHandleUpdatedEvent>().Publish(new WindowHandleUpdatedEventParams { WindowHandle = windowHandle });

                foreach (var roiCaptureInfo in _roiCaptures)
                {
                    var bitmap = _screenCapture.GetScreenCaptureArea(windowHandle, roiCaptureInfo.Left, roiCaptureInfo.Top, roiCaptureInfo.Width, roiCaptureInfo.Height);
                    if (bitmap != null) 
                    {
                        roiCaptureInfo.Bitmap = bitmap;
                    }
                }
                _currentScreen = _screenCapture.GetScreenCapture(windowHandle) ?? _currentScreen;
                _eventAggregator.GetEvent<ScreenUpdatedEvent>().Publish();
                

                _delayUpdateScreen = 100;

                //ScreenCapture.WriteBitmapToFile($"Logging/Screen_{DateTime.Now.ToFileTimeUtc()}.png", _currentScreen);
            }
            else
            {
                _logger.LogWarning($"{MethodBase.GetCurrentMethod()?.Name}: Invalid windowHandle. Diablo IV processes found: {processes.Length}. Retry in 10 seconds.");
                _delayUpdateScreen = 10000;
            }
        }

        public ROICaptureInfo? GetRoiCaptureInfo(string id)
        {
            return _roiCaptures.FirstOrDefault(roi => roi.Id.Equals(id));
        }

        private void UpdateMouse()
        {
            PInvoke.User32.CURSORINFO cursorInfo = new PInvoke.User32.CURSORINFO();
            cursorInfo.cbSize = Marshal.SizeOf(cursorInfo);
            PInvoke.User32.GetCursorInfo(ref cursorInfo);

            //var monitor = PInvoke.User32.MonitorFromPoint(cursorInfo.ptScreenPos, PInvoke.User32.MonitorOptions.MONITOR_DEFAULTTONEAREST);
            //var dpi = PInvoke.User32.GetDpiForMonitor(monitor, PInvoke.User32.MonitorDpiType.EFFECTIVE_DPI, out int dpiX, out int dpiY);
            var dpi = PInvoke.User32.GetDpiForSystem();
            var dpiScaling = Math.Round(dpi / (double)96, 2);

            string mouseCoordinates = $"X: {cursorInfo.ptScreenPos.x}, Y: {cursorInfo.ptScreenPos.y}";
            string mouseCoordinatesScaled = $"X: {(int)(cursorInfo.ptScreenPos.x / dpiScaling)}, Y: {(int)(cursorInfo.ptScreenPos.y / dpiScaling)}";

            //_logger.LogInformation(mouseCoordinates);
            //_logger.LogInformation(mouseCoordinatesScaled);

            _coordsMouseX = cursorInfo.ptScreenPos.x;
            _coordsMouseY = cursorInfo.ptScreenPos.y;

            _eventAggregator.GetEvent<MouseUpdatedEvent>().Publish(new MouseUpdatedEventParams { CoordsMouseX = _coordsMouseX, CoordsMouseY = _coordsMouseY});

            _delayUpdateMouse = 100;
        }

        #endregion
    }
}