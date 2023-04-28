using D4HUD.Constants;
using D4HUD.Entities;
using D4HUD.Events;
using D4HUD.Extensions;
using D4HUD.Interfaces;
using GameOverlay.Drawing;
using GameOverlay.Windows;
using Microsoft.Extensions.Logging;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace D4HUD.Services
{
    public class OverlayHandler : IOverlayHandler
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly ILogger _logger;
        private readonly ISettingsManager _settingsManager;
        private readonly IScreenCaptureHandler _screenCaptureHandler;

        private GraphicsWindow? _window = null;
        private Graphics _gfx;
        private readonly Dictionary<string, SolidBrush> _brushes = new Dictionary<string, SolidBrush>();
        private readonly Dictionary<string, Font> _fonts = new Dictionary<string, Font>();
        private readonly Dictionary<string, Image> _images = new Dictionary<string, Image>();
        private object _lockImage = new object();

        private int _coordsMouseX = 0;
        private int _coordsMouseY = 0;
        private List<OverlayObject> _overlayObjects = new List<OverlayObject>();
        IntPtr _windowHandle = IntPtr.Zero;

        // Start of Constructor region

        #region Constructor

        public OverlayHandler(IEventAggregator eventAggregator, ILogger<OverlayHandler> logger, ISettingsManager settingsManager, IScreenCaptureHandler screenCaptureHandler)
        {
            // Init IEventAggregator
            _eventAggregator = eventAggregator;
            _eventAggregator.GetEvent<ConfigPanelEvent>().Subscribe(HandleConfigPanelEvent);
            _eventAggregator.GetEvent<InterfaceLockedEvent>().Subscribe(HandleInterfaceLockedEvent);
            _eventAggregator.GetEvent<MenuLockedEvent>().Subscribe(HandleMenuLockedEvent);
            _eventAggregator.GetEvent<MenuUnlockedEvent>().Subscribe(HandleMenuUnlockedEvent);
            _eventAggregator.GetEvent<MouseUpdatedEvent>().Subscribe(HandleMouseUpdatedEvent);
            _eventAggregator.GetEvent<ROILockedEvent>().Subscribe(HandleROILockedEvent);
            _eventAggregator.GetEvent<ScreenUpdatedEvent>().Subscribe(HandleScreenUpdatedEvent);
            _eventAggregator.GetEvent<WindowHandleUpdatedEvent>().Subscribe(HandleWindowHandleUpdatedEvent);

            // Init logger
            _logger = logger;

            // Init services
            _settingsManager = settingsManager;
            _screenCaptureHandler = screenCaptureHandler;

            // Init overlay objects
            InitOverlayObjects();

            // Load overlay objects
            LoadOverlayObjects();
        }

        #endregion

        // Start of Events region

        #region Events

        #endregion

        // Start of Properties region

        #region Properties

        public int CoordsMouseX { get => _coordsMouseX; set => _coordsMouseX = value; }
        public int CoordsMouseY { get => _coordsMouseY; set => _coordsMouseY = value; }

        #endregion

        // Start of Event handlers region

        #region Event handlers

        private void SetupGraphics(object? sender, SetupGraphicsEventArgs e)
        {
            var gfx = e.Graphics;

            if (e.RecreateResources)
            {
                foreach (var pair in _brushes) pair.Value.Dispose();
                foreach (var pair in _images) pair.Value.Dispose();
            }

            _brushes["black"] = gfx.CreateSolidBrush(0, 0, 0);
            _brushes["white"] = gfx.CreateSolidBrush(255, 255, 255);
            _brushes["red"] = gfx.CreateSolidBrush(255, 0, 0);
            _brushes["red200"] = gfx.CreateSolidBrush(200, 0, 0);
            _brushes["green"] = gfx.CreateSolidBrush(0, 255, 0);
            _brushes["blue"] = gfx.CreateSolidBrush(0, 0, 255);
            _brushes["darkyellow"] = gfx.CreateSolidBrush(255, 204, 0);
            _brushes["background"] = gfx.CreateSolidBrush(25, 25, 25);
            _brushes["border"] = gfx.CreateSolidBrush(75, 75, 75);
            _brushes["text"] = gfx.CreateSolidBrush(200, 200, 200);

            _images["config"] = gfx.CreateImage("./Images/icon_config.png");
            _images["diablo"] = gfx.CreateImage("./Images/icon_diablo.png");
            _images["reload"] = gfx.CreateImage("./Images/icon_reload.png");
            _images["save"] = gfx.CreateImage("./Images/icon_save.png");
            _images["minus"] = gfx.CreateImage("./Images/icon_minus.png");
            _images["plus"] = gfx.CreateImage("./Images/icon_plus.png");

            if (e.RecreateResources) return;

            _fonts["arial"] = gfx.CreateFont("Arial", 12);
            _fonts["consolas"] = gfx.CreateFont("Consolas", 14);
            _fonts["consolasBold"] = gfx.CreateFont("Consolas", 18, true);
        }

        private void DestroyGraphics(object? sender, DestroyGraphicsEventArgs e)
        {
            foreach (var pair in _brushes) pair.Value.Dispose();
            foreach (var pair in _fonts) pair.Value.Dispose();
            foreach (var pair in _images) pair.Value.Dispose();
        }

        private void DrawGraphics(object? sender, DrawGraphicsEventArgs e)
        {
            // Clear
            var gfx = e.Graphics;
            gfx.ClearScene();

            // Menu items
            float stroke = 1; // Border arround menu items
            float captionOffset = 5; // Left margin for menu item caption
            float activationBarSize = 5; // Size for the activation bar

            foreach (OverlayMenuItem menuItem in _overlayObjects.OfType<OverlayMenuItem>())
            {
                if (menuItem.IsVisible)
                {
                    gfx.FillRectangle(_brushes["background"], menuItem.Left, menuItem.Top, menuItem.Left + menuItem.Width, menuItem.Top + menuItem.Height);
                    gfx.DrawRectangle(_brushes["border"], menuItem.Left, menuItem.Top, menuItem.Left + menuItem.Width, menuItem.Top + menuItem.Height, stroke);
                    gfx.DrawText(_fonts["consolasBold"], _brushes[menuItem.CaptionColor], menuItem.Left + captionOffset, menuItem.Top + menuItem.Height - activationBarSize - _fonts["consolasBold"].FontSize - captionOffset, menuItem.Caption);
                    gfx.DrawImage(_images[menuItem.Image], menuItem.Left + (menuItem.Width / 2) - (_images[menuItem.Image].Width / 2), menuItem.Top + (menuItem.Height / 3) - (_images[menuItem.Image].Height / 3));
                    float lockProgressAsWidth = (float)Math.Min(menuItem.LockWatch.ElapsedMilliseconds / OverlayConstants.LockTimer, 1.0) * menuItem.Width;
                    gfx.FillRectangle(_brushes["darkyellow"], menuItem.Left, menuItem.Top + menuItem.Height - activationBarSize, menuItem.Left + lockProgressAsWidth, menuItem.Top + menuItem.Height);                    
                }
            }

            // Regions of interest
            foreach (var roiArea in _overlayObjects.OfType<OverlayROI>())
            {
                if (roiArea.IsVisible)
                {
                    if (roiArea.IsLocked)
                    {
                        gfx.DrawRectangle(_brushes["green"], roiArea.Left, roiArea.Top, roiArea.Left + roiArea.Width, roiArea.Top + roiArea.Height, stroke);
                        gfx.DrawText(_fonts["consolasBold"], _brushes[roiArea.CaptionColor], roiArea.Left + captionOffset, roiArea.Top + (roiArea.Height / 2) - (_fonts["consolasBold"].FontSize / 2), roiArea.Caption);
                    }
                    else 
                    {
                        gfx.DashedRectangle(_brushes["green"], roiArea.Left, roiArea.Top, roiArea.Left + roiArea.Width, roiArea.Top + roiArea.Height, stroke);
                    }
                }
            }

            // Config
            foreach (var config in _overlayObjects.OfType<OverlayConfig>())
            {
                if (config.IsVisible)
                {
                    float currentLeft = 0;
                    float currentTop = 0;
                    float currentHeight = 0;
                    float currentWidth = 0;

                    // Get locked Regions of interest and Interface items
                    var currentROI = _overlayObjects.OfType<OverlayROI>().FirstOrDefault(roi => roi.IsLocked);
                    var currentInterface = _overlayObjects.OfType<OverlayInterface>().FirstOrDefault(intf => intf.IsLocked);
                    if (currentROI != null)
                    {
                        currentLeft = currentROI.Left;
                        currentTop = currentROI.Top;
                        currentHeight = currentROI.Height;
                        currentWidth = currentROI.Width;
                    }
                    else if (currentInterface != null) 
                    {
                        currentLeft = currentInterface.Left;
                        currentTop = currentInterface.Top;
                        currentHeight = currentInterface.Height;
                        currentWidth = currentInterface.Width;
                    }

                    if (currentROI != null || currentInterface != null)
                    {
                        float spacingX = config.Width / 4;
                        float spacingY = config.Height / 4;
                        float textOffsetX = spacingX / 2;
                        float textOffsetY = spacingY / 2;

                        gfx.FillRectangle(_brushes["background"], config.Left, config.Top, config.Left + config.Width, config.Top + config.Height);
                        gfx.DrawRectangle(_brushes["border"], config.Left, config.Top, config.Left + config.Width, config.Top + config.Height, stroke);
                        gfx.DrawText(_fonts["consolasBold"], _brushes[config.CaptionColor], config.Left + textOffsetX + (spacingX * 0), config.Top + textOffsetY + (spacingY * 0) - (_fonts["consolasBold"].FontSize / 2), "Left");
                        gfx.DrawText(_fonts["consolasBold"], _brushes[config.CaptionColor], config.Left + textOffsetX + (spacingX * 0), config.Top + textOffsetY + (spacingY * 1) - (_fonts["consolasBold"].FontSize / 2), "Top");
                        gfx.DrawText(_fonts["consolasBold"], _brushes[config.CaptionColor], config.Left + textOffsetX + (spacingX * 0), config.Top + textOffsetY + (spacingY * 2) - (_fonts["consolasBold"].FontSize / 2), "Height");
                        gfx.DrawText(_fonts["consolasBold"], _brushes[config.CaptionColor], config.Left + textOffsetX + (spacingX * 0), config.Top + textOffsetY + (spacingY * 3) - (_fonts["consolasBold"].FontSize / 2), "Width");                        
                        gfx.DrawImage(_images["minus"], config.Left + textOffsetX + (spacingX * 1), config.Top + textOffsetY + (spacingY * 0) - (_images["minus"].Height / 2));
                        gfx.DrawImage(_images["minus"], config.Left + textOffsetX + (spacingX * 1), config.Top + textOffsetY + (spacingY * 1) - (_images["minus"].Height / 2));
                        gfx.DrawImage(_images["minus"], config.Left + textOffsetX + (spacingX * 1), config.Top + textOffsetY + (spacingY * 2) - (_images["minus"].Height / 2));
                        gfx.DrawImage(_images["minus"], config.Left + textOffsetX + (spacingX * 1), config.Top + textOffsetY + (spacingY * 3) - (_images["minus"].Height / 2));
                        gfx.DrawText(_fonts["consolasBold"], _brushes[config.CaptionColor], config.Left + textOffsetX + (spacingX * 2), config.Top + textOffsetY + (spacingY * 0) - (_fonts["consolasBold"].FontSize / 2), currentLeft.ToString());
                        gfx.DrawText(_fonts["consolasBold"], _brushes[config.CaptionColor], config.Left + textOffsetX + (spacingX * 2), config.Top + textOffsetY + (spacingY * 1) - (_fonts["consolasBold"].FontSize / 2), currentTop.ToString());
                        gfx.DrawText(_fonts["consolasBold"], _brushes[config.CaptionColor], config.Left + textOffsetX + (spacingX * 2), config.Top + textOffsetY + (spacingY * 2) - (_fonts["consolasBold"].FontSize / 2), currentHeight.ToString());
                        gfx.DrawText(_fonts["consolasBold"], _brushes[config.CaptionColor], config.Left + textOffsetX + (spacingX * 2), config.Top + textOffsetY + (spacingY * 3) - (_fonts["consolasBold"].FontSize / 2), currentWidth.ToString());
                        gfx.DrawImage(_images["plus"], config.Left + textOffsetX + (spacingX * 3) - (_images["plus"].Width / 2), config.Top + textOffsetY + (spacingY * 0) - (_images["plus"].Height / 2));
                        gfx.DrawImage(_images["plus"], config.Left + textOffsetX + (spacingX * 3) - (_images["plus"].Width / 2), config.Top + textOffsetY + (spacingY * 1) - (_images["plus"].Height / 2));
                        gfx.DrawImage(_images["plus"], config.Left + textOffsetX + (spacingX * 3) - (_images["plus"].Width / 2), config.Top + textOffsetY + (spacingY * 2) - (_images["plus"].Height / 2));
                        gfx.DrawImage(_images["plus"], config.Left + textOffsetX + (spacingX * 3) - (_images["plus"].Width / 2), config.Top + textOffsetY + (spacingY * 3) - (_images["plus"].Height / 2));
                    }
                }
            }

            // Interface items
            foreach (var interfaceItem in _overlayObjects.OfType<OverlayInterface>())
            {
                if (interfaceItem.IsVisible)
                {
                    if (interfaceItem.Mode.Equals("edit"))
                    {
                        gfx.DrawRectangle(_brushes["green"], interfaceItem.Left, interfaceItem.Top, interfaceItem.Left + interfaceItem.Width, interfaceItem.Top + interfaceItem.Height, stroke);
                        gfx.DrawText(_fonts["consolasBold"], _brushes[interfaceItem.CaptionColor], interfaceItem.Left + captionOffset, interfaceItem.Top + (interfaceItem.Height / 2) - (_fonts["consolasBold"].FontSize / 2), interfaceItem.Caption);
                    }
                    else
                    {
                        string roiId = $"roi{interfaceItem.Id.Substring(interfaceItem.Id.IndexOf("."))}";
                        ROICaptureInfo? roiCaptureInfo = _screenCaptureHandler.GetRoiCaptureInfo(roiId);
                        if (roiCaptureInfo != null && roiCaptureInfo.Bitmap != null) 
                        {
                            //MemoryStream ms = new MemoryStream();
                            //roiCaptureInfo.Bitmap.Save(ms, ImageFormat.Jpeg);
                            //byte[] bmpBytes = ms.ToArray();

                            //Image roiImage = new Image(gfx, bmpBytes);
                            //Image roiImage = new Image(gfx, BitmapToByteArray(roiCaptureInfo.Bitmap));
                            //Image roiImage = new Image(gfx, roiCaptureInfo.Bitmap.ToByteArray(ImageFormat.Bmp));

                            if (_images[interfaceItem.Id] != null)
                            {
                                lock (_lockImage) 
                                {
                                    gfx.DrawImage(_images[interfaceItem.Id], interfaceItem.Left, interfaceItem.Top, interfaceItem.Left + interfaceItem.Width, interfaceItem.Top + interfaceItem.Height);
                                }
                            }
                        }
                        else
                        {
                            gfx.DrawRectangle(_brushes["green"], interfaceItem.Left, interfaceItem.Top, interfaceItem.Left + interfaceItem.Width, interfaceItem.Top + interfaceItem.Height, stroke);
                        }
                    }
                    
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="configPanelEventParams"></param>
        private void HandleConfigPanelEvent(ConfigPanelEventParams configPanelEventParams)
        {
            // First check if there is a locked ROI
            var currentROI = _overlayObjects.OfType<OverlayROI>().FirstOrDefault(roi => roi.IsLocked);
            var currentInterface = _overlayObjects.OfType<OverlayInterface>().FirstOrDefault(intf => intf.IsLocked);
            var overlayObject = currentROI != null ? (OverlayObject)currentROI : currentInterface;
            if (overlayObject != null)
            {
                if (configPanelEventParams.Id.Equals("config.positions"))
                {
                    switch (configPanelEventParams.Property)
                    {
                        case "Left":
                            overlayObject.Left = configPanelEventParams.Increase ? overlayObject.Left + 1 : Math.Max(0, overlayObject.Left - 1);
                            break;
                        case "Top":
                            overlayObject.Top = configPanelEventParams.Increase ? overlayObject.Top + 1 : Math.Max(0, overlayObject.Top - 1);
                            break;
                        case "Height":
                            overlayObject.Height = configPanelEventParams.Increase ? overlayObject.Height + 1 : Math.Max(0, overlayObject.Height - 1);
                            break;
                        case "Width":
                            overlayObject.Width = configPanelEventParams.Increase ? overlayObject.Width + 1 : Math.Max(0, overlayObject.Width - 1);
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Set all other interface items to unlocked state.
        /// Set config visibility to true.
        /// </summary>
        /// <param name="interfaceLockedEventParams"></param>
        private void HandleInterfaceLockedEvent(InterfaceLockedEventParams interfaceLockedEventParams)
        {
            // Only handle event when D4HUD menu is open
            var rootMenu = _overlayObjects.OfType<OverlayMenuItemRoot>().FirstOrDefault();
            if (rootMenu != null && rootMenu.IsLocked)
            {
                foreach (var config in _overlayObjects.OfType<OverlayConfig>())
                {
                    config.IsVisible = true;
                }

                foreach (var interfaceItem in _overlayObjects.Where(m => !m.Id.Equals(interfaceLockedEventParams.Id)).OfType<OverlayInterface>())
                {
                    interfaceItem.IsLocked = false;
                }
            }
        }

        /// <summary>
        /// Set all sibling menu items to unlocked state
        /// </summary>
        /// <param name="menuLockedEventParams"></param>
        private void HandleMenuLockedEvent(MenuLockedEventParams menuLockedEventParams)
        {
            // Exclude root menu item
            if (menuLockedEventParams.Id.Contains(".")) 
            {
                string parentId = menuLockedEventParams.Id.Substring(0, menuLockedEventParams.Id.LastIndexOf(".") + 1);
                foreach (var menuItem in _overlayObjects.Where(m => m.Id.StartsWith(parentId) && !m.Id.Equals(menuLockedEventParams.Id)).OfType<OverlayMenuItem>())
                {
                    menuItem.IsLocked = false;
                }
            }

            // Handle related actions
            HandleMenuItemAction(menuLockedEventParams.Id, true);
        }

        /// <summary>
        /// Set all child menu items to unlocked state.
        /// </summary>
        /// <param name="menuUnlockedEventParams"></param>
        private void HandleMenuUnlockedEvent(MenuUnlockedEventParams menuUnlockedEventParams)
        {
            foreach (var menuItem in _overlayObjects.Where(m => m.Id.StartsWith(menuUnlockedEventParams.Id)).OfType<OverlayMenuItem>())
            {
                menuItem.IsLocked = false;
            }

            // Handle related actions
            HandleMenuItemAction(menuUnlockedEventParams.Id, false);
        }

        private void HandleMouseUpdatedEvent(MouseUpdatedEventParams mouseUpdatedEventParams)
        {
            _coordsMouseX = mouseUpdatedEventParams.CoordsMouseX;
            _coordsMouseY = mouseUpdatedEventParams.CoordsMouseY;
        }

        /// <summary>
        /// Set all other ROIs to unlocked state.
        /// Set config visibility to true.
        /// </summary>
        /// <param name="roiLockedEventParams"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void HandleROILockedEvent(ROILockedEventParams roiLockedEventParams)
        {
            foreach (var config in _overlayObjects.OfType<OverlayConfig>())
            {
                config.IsVisible = true;
            }

            foreach (var roi in _overlayObjects.Where(m => !m.Id.Equals(roiLockedEventParams.Id)).OfType<OverlayROI>())
            {
                roi.IsLocked = false;
            }
        }

        private void HandleScreenUpdatedEvent()
        {
            lock (_lockImage)
            {
                // Skill 1
                string interfaceId = "interface.skill1";
                string roiId = $"roi{interfaceId.Substring(interfaceId.IndexOf("."))}";
                ROICaptureInfo? roiCaptureInfo = _screenCaptureHandler.GetRoiCaptureInfo(roiId);
                if (roiCaptureInfo != null && roiCaptureInfo.Bitmap != null)
                {
                    _images[interfaceId] = _gfx.CreateImage(roiCaptureInfo.Bitmap.ToByteArray(ImageFormat.Bmp));
                }

                // Skill 2
                interfaceId = "interface.skill2";
                roiId = $"roi{interfaceId.Substring(interfaceId.IndexOf("."))}";
                roiCaptureInfo = _screenCaptureHandler.GetRoiCaptureInfo(roiId);
                if (roiCaptureInfo != null && roiCaptureInfo.Bitmap != null)
                {
                    _images[interfaceId] = _gfx.CreateImage(roiCaptureInfo.Bitmap.ToByteArray(ImageFormat.Bmp));
                }

                // Skill 3
                interfaceId = "interface.skill3";
                roiId = $"roi{interfaceId.Substring(interfaceId.IndexOf("."))}";
                roiCaptureInfo = _screenCaptureHandler.GetRoiCaptureInfo(roiId);
                if (roiCaptureInfo != null && roiCaptureInfo.Bitmap != null)
                {
                    _images[interfaceId] = _gfx.CreateImage(roiCaptureInfo.Bitmap.ToByteArray(ImageFormat.Bmp));
                }

                // Skill 4
                interfaceId = "interface.skill4";
                roiId = $"roi{interfaceId.Substring(interfaceId.IndexOf("."))}";
                roiCaptureInfo = _screenCaptureHandler.GetRoiCaptureInfo(roiId);
                if (roiCaptureInfo != null && roiCaptureInfo.Bitmap != null)
                {
                    _images[interfaceId] = _gfx.CreateImage(roiCaptureInfo.Bitmap.ToByteArray(ImageFormat.Bmp));
                }

                // Skill 5
                interfaceId = "interface.skill5";
                roiId = $"roi{interfaceId.Substring(interfaceId.IndexOf("."))}";
                roiCaptureInfo = _screenCaptureHandler.GetRoiCaptureInfo(roiId);
                if (roiCaptureInfo != null && roiCaptureInfo.Bitmap != null)
                {
                    _images[interfaceId] = _gfx.CreateImage(roiCaptureInfo.Bitmap.ToByteArray(ImageFormat.Bmp));
                }

                // Skill 6
                interfaceId = "interface.skill6";
                roiId = $"roi{interfaceId.Substring(interfaceId.IndexOf("."))}";
                roiCaptureInfo = _screenCaptureHandler.GetRoiCaptureInfo(roiId);
                if (roiCaptureInfo != null && roiCaptureInfo.Bitmap != null)
                {
                    _images[interfaceId] = _gfx.CreateImage(roiCaptureInfo.Bitmap.ToByteArray(ImageFormat.Bmp));
                }
            }
        }

        private void HandleWindowHandleUpdatedEvent(WindowHandleUpdatedEventParams windowHandleUpdatedEventParams)
        {
            if (!_windowHandle.Equals(windowHandleUpdatedEventParams.WindowHandle))
            {
                _windowHandle = windowHandleUpdatedEventParams.WindowHandle;
                InitOverlayWindow();
            }
        }

        #endregion

        // Start of Methods region
        #region Methods

        private void InitOverlayObjects()
        {
            // Menu items

            /*
             *   Diablo /
             *   ├─ Config /
             *   │  ├─ Region of Interest /
             *   │  ├─ Interface /
             *   │  ├─ Save /
             *   ├─ Reset /
             *   │  ├─ 1920x1080 /
             *   │  ├─ 2560x1440 /
             *   │  ├─ Custom /
             */

            _overlayObjects.Add(new OverlayMenuItemRoot
            {
                Id = "diablo",
                Left = 10,
                Top = 10,
                Width = 50,
                Height = 50,
                Image = "diablo"
            });

            _overlayObjects.Add(new OverlayMenuItemWithParent
            {
                Id = "diablo.config",
                Left = 60,
                Top = 10,
                Width = 50,
                Height = 50,
                Image = "config",
                Parent = _overlayObjects.FirstOrDefault(m => m.Id.Equals("diablo")) as OverlayMenuItem
            });

            _overlayObjects.Add(new OverlayMenuItemWithParent
            {
                Id = "diablo.config.roi",
                Left = 110,
                Top = 10,
                Width = 100,
                Height = 100,
                Caption = "ROI",
                CaptionColor = "text",
                Image = "config",
                Parent = _overlayObjects.FirstOrDefault(m => m.Id.Equals("diablo.config")) as OverlayMenuItem
            });

            _overlayObjects.Add(new OverlayMenuItemWithParent
            {
                Id = "diablo.config.interface",
                Left = 210,
                Top = 10,
                Width = 100,
                Height = 100,
                Caption = "Interface",
                CaptionColor = "text",
                Image = "config",
                Parent = _overlayObjects.FirstOrDefault(m => m.Id.Equals("diablo.config")) as OverlayMenuItem
            });

            _overlayObjects.Add(new OverlayMenuItemWithParent
            {
                Id = "diablo.config.save",
                Left = 110,
                Top = 110,
                Width = 100,
                Height = 100,
                Caption = "Save",
                CaptionColor = "text",
                Image = "save",
                Parent = _overlayObjects.FirstOrDefault(m => m.Id.Equals("diablo.config")) as OverlayMenuItem
            });

            _overlayObjects.Add(new OverlayMenuItemWithParent
            {
                Id = "diablo.reset",
                Left = 60,
                Top = 60,
                Width = 50,
                Height = 50,
                Image = "reload",
                Parent = _overlayObjects.FirstOrDefault(m => m.Id.Equals("diablo")) as OverlayMenuItem
            });

            _overlayObjects.Add(new OverlayMenuItemWithParent
            {
                Id = "diablo.reset.1920x1080",
                Left = 110,
                Top = 10,
                Width = 100,
                Height = 100,
                Caption = "1920x1080",
                CaptionColor = "text",
                Image = "reload",
                Parent = _overlayObjects.FirstOrDefault(m => m.Id.Equals("diablo.reset")) as OverlayMenuItem
            });

            _overlayObjects.Add(new OverlayMenuItemWithParent
            {
                Id = "diablo.reset.2560x1440",
                Left = 210,
                Top = 10,
                Width = 100,
                Height = 100,
                Caption = "2560x1440",
                CaptionColor = "text",
                Image = "reload",
                Parent = _overlayObjects.FirstOrDefault(m => m.Id.Equals("diablo.reset")) as OverlayMenuItem
            });

            _overlayObjects.Add(new OverlayMenuItemWithParent
            {
                Id = "diablo.reset.custom",
                Left = 310,
                Top = 10,
                Width = 100,
                Height = 100,
                Caption = "Custom",
                CaptionColor = "text",
                Image = "reload",
                Parent = _overlayObjects.FirstOrDefault(m => m.Id.Equals("diablo.reset")) as OverlayMenuItem
            });

            // Regions of interest

            _overlayObjects.Add(new OverlayROI
            {
                Id = "roi.minimap",
                Left = 2140,
                Top = 85,
                Width = 390,
                Height = 275,
                Caption = "minimap",
                CaptionColor = "green"
            });

            _overlayObjects.Add(new OverlayROI
            {
                Id = "roi.exp",
                Left = 1052,
                Top = 1282,
                Width = 487,
                Height = 10,
                Caption = "exp",
                CaptionColor = "green"
            });

            _overlayObjects.Add(new OverlayROI
            {
                Id = "roi.skill1",
                Left = 1043,
                Top = 1302,
                Width = 75,
                Height = 85,
                Caption = "skill1",
                CaptionColor = "green"
            });

            _overlayObjects.Add(new OverlayROI
            {
                Id = "roi.skill2",
                Left = 1127,
                Top = 1302,
                Width = 75,
                Height = 85,
                Caption = "skill2",
                CaptionColor = "green"
            });

            _overlayObjects.Add(new OverlayROI
            {
                Id = "roi.skill3",
                Left = 1211,
                Top = 1302,
                Width = 75,
                Height = 85,
                Caption = "skill3",
                CaptionColor = "green"
            });

            _overlayObjects.Add(new OverlayROI
            {
                Id = "roi.skill4",
                Left = 1295,
                Top = 1302,
                Width = 75,
                Height = 85,
                Caption = "skill4",
                CaptionColor = "green"
            });

            _overlayObjects.Add(new OverlayROI
            {
                Id = "roi.skill5",
                Left = 1379,
                Top = 1302,
                Width = 75,
                Height = 85,
                Caption = "skill5",
                CaptionColor = "green"
            });

            _overlayObjects.Add(new OverlayROI
            {
                Id = "roi.skill6",
                Left = 1463,
                Top = 1302,
                Width = 75,
                Height = 85,
                Caption = "skill6",
                CaptionColor = "green"
            });

            _overlayObjects.Add(new OverlayROI
            {
                Id = "roi.hp",
                Left = 820,
                Top = 1230,
                Width = 5,
                Height = 180,
                Caption = "hp",
                CaptionColor = "green"
            });

            _overlayObjects.Add(new OverlayROI
            {
                Id = "roi.mp",
                Left = 1752,
                Top = 1230,
                Width = 5,
                Height = 180,
                Caption = "mp",
                CaptionColor = "green"
            });

            // Config

            _overlayObjects.Add(new OverlayConfig
            {
                Id = "config.positions",
                Left = 1130,
                Top = 570,
                Width = 300,
                Height = 300
            });

            // Interface items

            _overlayObjects.Add(new OverlayInterface
            {
                Id = "interface.skill1",
                Left = 1043,
                Top = 200,
                Width = 75,
                Height = 85,
                Caption = "skill1",
                CaptionColor = "green"
            });

            _overlayObjects.Add(new OverlayInterface
            {
                Id = "interface.skill2",
                Left = 1127,
                Top = 200,
                Width = 75,
                Height = 85,
                Caption = "skill2",
                CaptionColor = "green"
            });

            _overlayObjects.Add(new OverlayInterface
            {
                Id = "interface.skill3",
                Left = 1211,
                Top = 200,
                Width = 75,
                Height = 85,
                Caption = "skill3",
                CaptionColor = "green"
            });

            _overlayObjects.Add(new OverlayInterface
            {
                Id = "interface.skill4",
                Left = 1295,
                Top = 200,
                Width = 75,
                Height = 85,
                Caption = "skill4",
                CaptionColor = "green"
            });

            _overlayObjects.Add(new OverlayInterface
            {
                Id = "interface.skill5",
                Left = 1379,
                Top = 200,
                Width = 75,
                Height = 85,
                Caption = "skill5",
                CaptionColor = "green"
            });

            _overlayObjects.Add(new OverlayInterface
            {
                Id = "interface.skill6",
                Left = 1463,
                Top = 200,
                Width = 75,
                Height = 85,
                Caption = "skill6",
                CaptionColor = "green"
            });
        }

        private void LoadOverlayObjects()
        {
            foreach (var persist in _settingsManager.Settings.PersistOverlayObjects)
            {
                LoadOverlayObject(persist);
            }
        }

        private void LoadOverlayObject(PersistOverlayObject persist)
        {
            var overlayobject = _overlayObjects.FirstOrDefault(o => o.Id.Equals(persist.Id));
            if (overlayobject != null)
            {
                overlayobject.Left = persist.Left;
                overlayobject.Top = persist.Top;
                overlayobject.Width = persist.Width;
                overlayobject.Height = persist.Height;

                if (overlayobject.GetType().Equals(typeof(OverlayROI)))
                {
                    _eventAggregator.GetEvent<ROIUpdatedEvent>().Publish(new ROIUpdatedEventParams 
                    { 
                        Id = overlayobject.Id,
                        Left = overlayobject.Left,
                        Top = overlayobject.Top,
                        Width = overlayobject.Width,
                        Height = overlayobject.Height
                    });
                }
            }
        }

        private void LoadOverlayObjects1920x1080()
        {
            foreach (var persist in _settingsManager.Settings1920x1080.PersistOverlayObjects)
            {
                LoadOverlayObject(persist);
            }
        }

        private void LoadOverlayObjects2560x1440()
        {
            foreach (var persist in _settingsManager.Settings2560x1440.PersistOverlayObjects)
            {
                LoadOverlayObject(persist);
            }
        }

        private void InitOverlayWindow()
        {
            var gfx = new Graphics()
            {
                MeasureFPS = true,
                PerPrimitiveAntiAliasing = true,
                TextAntiAliasing = true
            };

            _gfx = gfx;

            _window = new GraphicsWindow(gfx)
            {
                FPS = 60,
                IsTopmost = true,
                IsVisible = true
            };
 
            _window.DestroyGraphics += DestroyGraphics;
            _window.DrawGraphics += DrawGraphics;
            _window.SetupGraphics += SetupGraphics;

            Task.Run(() =>
            {
                _window.Create();
                _window.FitTo(_windowHandle);
                _window.Join();
            });
        }

        private void HandleMenuItemAction(string id, bool isLocked)
        {
            switch (id)
            {
                case "diablo":
                    if (isLocked)
                    {
                        // Set mode to "edit" for all OverlayObjects
                        foreach (var overlayObject in _overlayObjects)
                        {
                            overlayObject.Mode = "edit";
                        }

                        // Hide all interfaces
                        foreach (var interfaceItem in _overlayObjects.OfType<OverlayInterface>())
                        {
                            interfaceItem.IsVisible = false;
                        }
                    }
                    else
                    {
                        // Set mode to "play" for all OverlayObjects
                        foreach (var overlayObject in _overlayObjects)
                        {
                            overlayObject.Mode = "play";
                        }

                        // Show all interface items
                        foreach (var interfaceItem in _overlayObjects.OfType<OverlayInterface>())
                        {
                            interfaceItem.IsLocked = false;
                            interfaceItem.IsVisible = true;
                        }

                        // Hide all ROIs
                        foreach (var roi in _overlayObjects.OfType<OverlayROI>())
                        {
                            roi.IsLocked = false;
                            roi.IsVisible = false;
                        }

                        // Hide config
                        foreach (var config in _overlayObjects.OfType<OverlayConfig>())
                        {
                            config.IsVisible = false;
                        }
                    }
                    break;
                case "diablo.config.roi":
                    if (isLocked)
                    {
                        // Hide all interfaces
                        foreach (var interfaceItem in _overlayObjects.OfType<OverlayInterface>())
                        {
                            interfaceItem.IsLocked = false;
                            interfaceItem.IsVisible = false;
                        }

                        // Show all ROIs
                        foreach (var roi in _overlayObjects.OfType<OverlayROI>())
                        {
                            roi.IsVisible = true;
                        }
                    }
                    else
                    {
                        // Hide all ROIs
                        foreach (var roi in _overlayObjects.OfType<OverlayROI>())
                        {
                            roi.IsLocked = false;
                            roi.IsVisible = false;
                        }
                    }
                    break;
                case "diablo.config.interface":
                    if (isLocked)
                    {
                        // Hide all ROIs
                        foreach (var roi in _overlayObjects.OfType<OverlayROI>())
                        {
                            roi.IsLocked = false;
                            roi.IsVisible = false;
                        }

                        // Show all interface items
                        foreach (var interfaceItem in _overlayObjects.OfType<OverlayInterface>())
                        {
                            interfaceItem.IsVisible = true;
                        }
                    }
                    else
                    {
                        // Hide all interface items
                        foreach (var interfaceItem in _overlayObjects.OfType<OverlayInterface>())
                        {
                            interfaceItem.IsLocked = false;
                            interfaceItem.IsVisible = false;
                        }
                    }
                    break;
                case "diablo.config.save":
                    SaveOverlayConfiguration();
                    break;
                case "diablo.reset.custom":
                    LoadOverlayObjects();
                    break;
                case "diablo.reset.1920x1080":
                    LoadOverlayObjects1920x1080();
                    break;
                case "diablo.reset.2560x1440":
                    LoadOverlayObjects2560x1440();
                    break;
            }
        }

        private void SaveOverlayConfiguration()
        {
            _settingsManager.Settings.PersistOverlayObjects.Clear();

            foreach (var overlayObject in _overlayObjects)
            {
                var persistOverlayObject = new PersistOverlayObject
                {
                    Id = overlayObject.Id,
                    Left = overlayObject.Left,
                    Top = overlayObject.Top,
                    Width = overlayObject.Width,
                    Height = overlayObject.Height
                };
                _settingsManager.Settings.PersistOverlayObjects.Add(persistOverlayObject);
            }

            _settingsManager.SaveSettings();
        }

        public List<ROICaptureInfo> GetROIInfo()
        {
            List<ROICaptureInfo> roiCaptureInfoList = new List<ROICaptureInfo>();

            foreach (var roiArea in _overlayObjects.OfType<OverlayROI>())
            {
                var roiCaptureInfo = new ROICaptureInfo
                {
                    Id = roiArea.Id,
                    Left = roiArea.Left,
                    Top = roiArea.Top,
                    Width = roiArea.Width,
                    Height = roiArea.Height
                };
                roiCaptureInfoList.Add(roiCaptureInfo);
            }
            return roiCaptureInfoList;
        }

        public static byte[] BitmapToByteArray(System.Drawing.Bitmap bitmap)
        {
            BitmapData bmpdata = null;

            try
            {
                bmpdata = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
                int numbytes = bmpdata.Stride * bitmap.Height;
                byte[] bytedata = new byte[numbytes];
                IntPtr ptr = bmpdata.Scan0;

                Marshal.Copy(ptr, bytedata, 0, numbytes);

                return bytedata;
            }
            finally
            {
                if (bmpdata != null)
                {
                    bitmap.UnlockBits(bmpdata);
                }
            }
        }

        #endregion
    }

    public abstract class OverlayObject
    {
        protected readonly IEventAggregator _eventAggregator;

        public string Id { get; set; } = string.Empty;
        public float Left { get; set; }
        public float Top { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public string Mode { get; set; } = "play"; // edit or play.

        public OverlayObject()
        {
            _eventAggregator = (IEventAggregator)Prism.Ioc.ContainerLocator.Container.Resolve(typeof(IEventAggregator));
            _eventAggregator.GetEvent<MouseUpdatedEvent>().Subscribe(HandleMouseUpdatedEvent);
        }

        protected virtual void HandleMouseUpdatedEvent(MouseUpdatedEventParams mouseUpdatedEventParams)
        {
        }
    }

    public abstract class OverlayMenuItem : OverlayObject
    {
        public string Caption { get; set; } = string.Empty;
        public string CaptionColor { get; set; } = "text";
        public string Image { get; set; } = string.Empty;

        public bool IsLocked { get; set; } = false;
        public bool IsVisible { get; set; } = false;
        public Stopwatch LockWatch { get; set; } = new Stopwatch();

        protected override void HandleMouseUpdatedEvent(MouseUpdatedEventParams mouseUpdatedEventParams)
        {
            if (!IsVisible) return;

            bool isOnOverlayMenuItem = mouseUpdatedEventParams.CoordsMouseX >= Left && mouseUpdatedEventParams.CoordsMouseX <= Left + Width && 
                mouseUpdatedEventParams.CoordsMouseY >= Top && mouseUpdatedEventParams.CoordsMouseY <= Top + Height;

            if (isOnOverlayMenuItem)
            {
                if (!LockWatch.IsRunning && LockWatch.ElapsedMilliseconds == 0)
                {
                    // Start timer when mouse enters OverlayMenuItem
                    LockWatch.Start();
                }
                else
                {
                    if (LockWatch.IsRunning && LockWatch.ElapsedMilliseconds >= OverlayConstants.LockTimer)
                    {
                        // Stop timer and change lock state when mouse was on OverlayMenuItem for >= OverlayConstants.LockTimer ms
                        LockWatch.Stop();
                        IsLocked = !IsLocked;

                        // Let subscribers know when locked state has changed
                        if (IsLocked)
                        {
                            _eventAggregator.GetEvent<MenuLockedEvent>().Publish(new MenuLockedEventParams { Id = Id });
                        }
                        else
                        {
                            _eventAggregator.GetEvent<MenuUnlockedEvent>().Publish(new MenuUnlockedEventParams { Id = Id });
                        }
                    }
                }
            }
            else
            {
                // Stop and reset timer when mouse leaves OverlayMenuItem
                LockWatch.Reset();
            }
        }
    }

    public class OverlayMenuItemRoot : OverlayMenuItem
    {
        protected override void HandleMouseUpdatedEvent(MouseUpdatedEventParams mouseUpdatedEventParams)
        {
            base.HandleMouseUpdatedEvent(mouseUpdatedEventParams);

            bool isOnOverlayMenuItem = mouseUpdatedEventParams.CoordsMouseX >= Left && mouseUpdatedEventParams.CoordsMouseX <= Left + Width &&
                mouseUpdatedEventParams.CoordsMouseY >= Top && mouseUpdatedEventParams.CoordsMouseY <= Top + Height;

            if (IsLocked == true || isOnOverlayMenuItem)
            {
                IsVisible = true;
            }
            else if (IsLocked == false && isOnOverlayMenuItem == false)
            {
                IsVisible = false;
            }
        }
    }

    public class OverlayMenuItemWithParent : OverlayMenuItemRoot
    {
        public OverlayMenuItem? Parent { get; set; } = null;

        protected override void HandleMouseUpdatedEvent(MouseUpdatedEventParams mouseUpdatedEventParams)
        {
            base.HandleMouseUpdatedEvent(mouseUpdatedEventParams);

            if (Parent != null && Parent.IsLocked == true)
            {
                IsVisible = true;
            }
            else
            {
                IsVisible = false;
            }
        }
    }

    public class OverlayROI : OverlayObject
    {
        public string Caption { get; set; } = string.Empty;
        public string CaptionColor { get; set; } = "text";

        public bool IsLocked { get; set; } = false;
        public bool IsVisible { get; set; } = false;

        protected override void HandleMouseUpdatedEvent(MouseUpdatedEventParams mouseUpdatedEventParams)
        {
            if (!IsVisible) return;

            bool isOnOverlayROI = mouseUpdatedEventParams.CoordsMouseX >= Left && mouseUpdatedEventParams.CoordsMouseX <= Left + Width &&
                mouseUpdatedEventParams.CoordsMouseY >= Top && mouseUpdatedEventParams.CoordsMouseY <= Top + Height;

            if (isOnOverlayROI)
            {
                IsLocked = true;

                // Move ROI when modifier key is pressed.
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    Top = mouseUpdatedEventParams.CoordsMouseY - (Height / 2);
                    Left = mouseUpdatedEventParams.CoordsMouseX - (Width / 2);
                }

                // Notify event subscribers
                _eventAggregator.GetEvent<ROILockedEvent>().Publish(new ROILockedEventParams { Id = Id });
            }
        }
    }

    public class OverlayConfig : OverlayObject
    {
        public string CaptionColor { get; set; } = "text";

        public bool IsVisible { get; set; } = false;

        protected override void HandleMouseUpdatedEvent(MouseUpdatedEventParams mouseUpdatedEventParams)
        {
            if (!IsVisible) return;

            bool isOnOverlayConfig = mouseUpdatedEventParams.CoordsMouseX >= Left && mouseUpdatedEventParams.CoordsMouseX <= Left + Width &&
                mouseUpdatedEventParams.CoordsMouseY >= Top && mouseUpdatedEventParams.CoordsMouseY <= Top + Height;

            if (isOnOverlayConfig)
            {
                float imageHeight = 32;
                float imageWidth = 32;

                float spacingX = Width / 4;
                float spacingY = Height / 4;
                float textOffsetX = spacingX / 2;
                float textOffsetY = spacingY / 2;

                float LeftMinusControlLeft = Left + textOffsetX + (spacingX * 1);
                float LeftMinusControlTop = Top + textOffsetY + (spacingY * 0) - (imageHeight / 2);
                float TopMinusControlLeft = Left + textOffsetX + (spacingX * 1);
                float TopMinusControlTop = Top + textOffsetY + (spacingY * 1) - (imageHeight / 2);
                float HeightMinusControlLeft = Left + textOffsetX + (spacingX * 1);
                float HeightMinusControlTop = Top + textOffsetY + (spacingY * 2) - (imageHeight / 2);
                float WidthMinusControlLeft = Left + textOffsetX + (spacingX * 1);
                float WidthMinusControlTop = Top + textOffsetY + (spacingY * 3) - (imageHeight / 2);

                float LeftPlusControlLeft = Left + textOffsetX + (spacingX * 3) - (imageWidth / 2);
                float LeftPlusControlTop = Top + textOffsetY + (spacingY * 0) - (imageHeight / 2);
                float TopPlusControlLeft = Left + textOffsetX + (spacingX * 3) - (imageWidth / 2);
                float TopPlusControlTop = Top + textOffsetY + (spacingY * 1) - (imageHeight / 2);
                float HeightPlusControlLeft = Left + textOffsetX + (spacingX * 3) - (imageWidth / 2);
                float HeightPlusControlTop = Top + textOffsetY + (spacingY * 2) - (imageHeight / 2);
                float WidthPlusControlLeft = Left + textOffsetX + (spacingX * 3) - (imageWidth / 2);
                float WidthPlusControlTop = Top + textOffsetY + (spacingY * 3) - (imageHeight / 2);

                // Notify event subscribers
                bool isOnOverlayConfigLeftMinus = mouseUpdatedEventParams.CoordsMouseX >= LeftMinusControlLeft && mouseUpdatedEventParams.CoordsMouseX <= LeftMinusControlLeft + imageWidth &&
                mouseUpdatedEventParams.CoordsMouseY >= LeftMinusControlTop && mouseUpdatedEventParams.CoordsMouseY <= LeftMinusControlTop + imageHeight;
                bool isOnOverlayConfigTopMinus = mouseUpdatedEventParams.CoordsMouseX >= TopMinusControlLeft && mouseUpdatedEventParams.CoordsMouseX <= TopMinusControlLeft + imageWidth &&
                mouseUpdatedEventParams.CoordsMouseY >= TopMinusControlTop && mouseUpdatedEventParams.CoordsMouseY <= TopMinusControlTop + imageHeight;
                bool isOnOverlayConfigHeightMinus = mouseUpdatedEventParams.CoordsMouseX >= HeightMinusControlLeft && mouseUpdatedEventParams.CoordsMouseX <= HeightMinusControlLeft + imageWidth &&
                mouseUpdatedEventParams.CoordsMouseY >= HeightMinusControlTop && mouseUpdatedEventParams.CoordsMouseY <= HeightMinusControlTop + imageHeight;
                bool isOnOverlayConfigWidthMinus = mouseUpdatedEventParams.CoordsMouseX >= WidthMinusControlLeft && mouseUpdatedEventParams.CoordsMouseX <= WidthMinusControlLeft + imageWidth &&
                mouseUpdatedEventParams.CoordsMouseY >= WidthMinusControlTop && mouseUpdatedEventParams.CoordsMouseY <= WidthMinusControlTop + imageHeight;

                bool isOnOverlayConfigLeftPlus = mouseUpdatedEventParams.CoordsMouseX >= LeftPlusControlLeft && mouseUpdatedEventParams.CoordsMouseX <= LeftPlusControlLeft + imageWidth &&
                mouseUpdatedEventParams.CoordsMouseY >= LeftPlusControlTop && mouseUpdatedEventParams.CoordsMouseY <= LeftPlusControlTop + imageHeight;
                bool isOnOverlayConfigTopPlus = mouseUpdatedEventParams.CoordsMouseX >= TopPlusControlLeft && mouseUpdatedEventParams.CoordsMouseX <= TopPlusControlLeft + imageWidth &&
                mouseUpdatedEventParams.CoordsMouseY >= TopPlusControlTop && mouseUpdatedEventParams.CoordsMouseY <= TopPlusControlTop + imageHeight;
                bool isOnOverlayConfigHeightPlus = mouseUpdatedEventParams.CoordsMouseX >= HeightPlusControlLeft && mouseUpdatedEventParams.CoordsMouseX <= HeightPlusControlLeft + imageWidth &&
                mouseUpdatedEventParams.CoordsMouseY >= HeightPlusControlTop && mouseUpdatedEventParams.CoordsMouseY <= HeightPlusControlTop + imageHeight;
                bool isOnOverlayConfigWidthPlus = mouseUpdatedEventParams.CoordsMouseX >= WidthPlusControlLeft && mouseUpdatedEventParams.CoordsMouseX <= WidthPlusControlLeft + imageWidth &&
                mouseUpdatedEventParams.CoordsMouseY >= WidthPlusControlTop && mouseUpdatedEventParams.CoordsMouseY <= WidthPlusControlTop + imageHeight;

                string property = string.Empty;
                bool increase = false;
                if (isOnOverlayConfigLeftMinus)
                {
                    // Decrease left position
                    property = "Left";
                    increase = false;
                }
                else if (isOnOverlayConfigTopMinus)
                {
                    // Decrease top position
                    property = "Top";
                    increase = false;
                }
                else if (isOnOverlayConfigHeightMinus)
                {
                    // Decrease height
                    property = "Height";
                    increase = false;
                }
                else if (isOnOverlayConfigWidthMinus)
                {
                    // Decrease width
                    property = "Width";
                    increase = false;
                }
                else if (isOnOverlayConfigLeftPlus)
                {
                    // Increase left position
                    property = "Left";
                    increase = true;
                }
                else if (isOnOverlayConfigTopPlus)
                {
                    // Increase top position
                    property = "Top";
                    increase = true;
                }
                else if (isOnOverlayConfigHeightPlus)
                {
                    // Increase height
                    property = "Height";
                    increase = true;
                }
                else if (isOnOverlayConfigWidthPlus)
                {
                    // Increase width
                    property = "Width";
                    increase = true;
                }

                _eventAggregator.GetEvent<ConfigPanelEvent>().Publish(new ConfigPanelEventParams { Id = Id, Property = property, Increase = increase });
            }
        }
    }

    public class OverlayInterface : OverlayObject
    {
        public string Caption { get; set; } = string.Empty;
        public string CaptionColor { get; set; } = "text";
        public bool IsLocked { get; set; } = false;
        public bool IsVisible { get; set; } = true;

        protected override void HandleMouseUpdatedEvent(MouseUpdatedEventParams mouseUpdatedEventParams)
        {
            if (!IsVisible) return;

            bool isOnOverlayInterface = mouseUpdatedEventParams.CoordsMouseX >= Left && mouseUpdatedEventParams.CoordsMouseX <= Left + Width &&
                mouseUpdatedEventParams.CoordsMouseY >= Top && mouseUpdatedEventParams.CoordsMouseY <= Top + Height;

            if (isOnOverlayInterface)
            {
                IsLocked = true;

                // Move Interface when modifier key is pressed.
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    Top = mouseUpdatedEventParams.CoordsMouseY - (Height / 2);
                    Left = mouseUpdatedEventParams.CoordsMouseX - (Width / 2);
                }

                // Notify event subscribers
                _eventAggregator.GetEvent<InterfaceLockedEvent>().Publish(new InterfaceLockedEventParams { Id = Id });
            }
        }
    }
}
