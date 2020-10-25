using MaterialDesignThemes.Wpf;
using Streamster.ClientCore;
using Streamster.ClientCore.Cross;
using Streamster.ClientCore.Services;
using Streamster.ClientData.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace Streamster.ClientApp.Win.Services
{
    class WindowStateManager : IWindowStateManager, IDisposable
    {
        private Window _window;
        private IDeviceSettings _appSettings;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private AppWindowState _lastState = AppWindowState.Minimized;
        private AppWindowState _stateBeforeFullscreen;

        private IntRect _compactRect;
        private IntRect _normalRect;
        private bool _started;
        private readonly CoreData _coreData;

        private bool _topMostPinned = false;

        public CaptionViewModel Model { get; } = new CaptionViewModel();

        public WindowStateManager(CoreData coreData)
        {
            _coreData = coreData;

            _coreData.Subscriptions.SubscribeForProperties<IDeviceSettings>(s => s.DisableTopMost, (s, c, p) => RefreshButtons(true));
            _coreData.Subscriptions.SubscribeForProperties<IDeviceSettings>(s => s.TopMostExtendedMode, (s, c, p) => RefreshButtons(true));
        }

        private void RefreshTopMost()
        {
            if (_window != null)
            {
                var mode = TopMostModeConverter.ToMode(_coreData.ThisDevice.DeviceSettings);

                bool topMost = false;
                switch (mode)
                {
                    case TopMostMode.Always:
                        topMost = true;
                        break;
                    case TopMostMode.WhenCompact:
                        topMost = _lastState == AppWindowState.Compact;
                        break;
                    case TopMostMode.Never:
                        topMost = false;
                        break;
                    case TopMostMode.Manual:
                        topMost = _topMostPinned;
                        break;
                }

                if (_lastState == AppWindowState.FullScreen || _lastState == AppWindowState.Maximized || _lastState == AppWindowState.Minimized)
                    topMost = false;

                if (topMost != _window.Topmost)
                    _window.Topmost = topMost;
            }
        }

        internal void SetWindow(Window window)
        {
            _window = window;
            window.StateChanged += (s, e) =>
            {
                if (_started)
                {
                    if (_lastState == AppWindowState.FullScreen && _window.WindowState != WindowState.Minimized)
                        _window.WindowStyle = WindowStyle.SingleBorderWindow;
                    RefreshButtons(false);
                }
            };
            window.SizeChanged += (s, e) =>
            {
                if (_started)
                {
                    RefreshButtons(false);
                    _cts.Cancel();
                    _cts = new CancellationTokenSource();
                    _ = ProcessDelayedSizeChange(_cts.Token);
                }
            };
            window.LocationChanged += async (s, e) =>
            {
                if (_started)
                {
                    await Task.Yield();
                    StoreRectangles();
                }
            };

            Model.Buttons.Value = new List<CaptionButtonViewModel>
            {
                Create(CaptionButtonType.Close)
            };
        }

        public void Start()
        {
            var appSettings = _coreData.ThisDevice.DeviceSettings;
            _started = true;
            _appSettings = appSettings;
            _window.ResizeMode = ResizeMode.CanResizeWithGrip;
            _compactRect = appSettings.CompactWnd;
            _normalRect = appSettings.NormalWnd;
            GoToState(appSettings.AppWindowState);

            RefreshButtons(false);
        }

        public void Dispose()
        {
            _coreData.RunOnMainThread(() =>
            {
                var appSettings = _coreData.ThisDevice?.DeviceSettings;
                if (appSettings != null)
                {
                    appSettings.CompactWnd = _compactRect;
                    appSettings.NormalWnd = _normalRect;
                    appSettings.AppWindowState = _lastState;
                }
            });
        }

        private void GoToState(AppWindowState state)
        {
            switch (state)
            {
                case AppWindowState.Normal:
                    GoToNormalCompactState(_normalRect, () => GetDefaultRect(false));
                    break;
                case AppWindowState.Compact:
                    GoToNormalCompactState(_compactRect, () => GetDefaultRect(true));
                    break;
                case AppWindowState.FullScreen:
                    _stateBeforeFullscreen = GetCurrentState();
                    if (_stateBeforeFullscreen == AppWindowState.Maximized)
                        _stateBeforeFullscreen = AppWindowState.Normal;
                    if (_window.WindowState == WindowState.Maximized)
                        _window.WindowState = WindowState.Normal;
                    _window.WindowStyle = WindowStyle.None;
                    SystemCommands.MaximizeWindow(_window);
                    break;
                case AppWindowState.Maximized:
                    SystemCommands.MaximizeWindow(_window);
                    break;
                case AppWindowState.Minimized:
                    SystemCommands.MinimizeWindow(_window);
                    break;
            }
        }

        private IntRect GetDefaultRect(bool compact)
        {
            var wa = MonitorHelper.GetMonitorWorkingArea(_window);
            if (compact)
            {
                int dx = 343;
                int dy = 363;

                return new IntRect
                {
                    L = (int)(wa.Left + wa.Width - dx - 5),
                    T = (int)(wa.Top + wa.Height - dy - 5),
                    W = dx,
                    H = dy
                };
            }
            else
            {
                int dx = 965;
                int dy = 786;

                if (dy > wa.Height)
                    dy = (int)wa.Height;

                return new IntRect
                {
                    L = (int)(wa.Left + (wa.Width - dx) / 2),
                    T = (int)(wa.Top + (wa.Height - dy) / 2),
                    W = dx,
                    H = dy
                };
            }
        }

        private void GoToNormalCompactState(IntRect rect, Func<IntRect> getDefault)
        {
            rect = rect ?? getDefault();

            switch (GetCurrentState())
            {
                case AppWindowState.Normal:
                case AppWindowState.Compact:
                    SetWindowRect(rect);
                    break;
                case AppWindowState.FullScreen:
                case AppWindowState.Maximized:
                    SetWindowRect(rect);
                    SystemCommands.RestoreWindow(_window);
                    break;
            }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private void SetWindowRect(IntRect rect)
        {
            PresentationSource source = PresentationSource.FromVisual(_window);
            var tl = source.CompositionTarget.TransformToDevice.Transform(new Point(rect.L, rect.T));
            var wh = source.CompositionTarget.TransformToDevice.Transform(new Point(rect.W, rect.H));
            IntPtr windowHandle = new WindowInteropHelper(_window).Handle;
            SetWindowPos(windowHandle, new IntPtr(), (int)tl.X, (int)tl.Y, (int)wh.X, (int)wh.Y, 0);
            //_window.Top = rect.T;_window.Left = rect.L;_window.Width = rect.W;_window.Height = rect.H;
        }

        private void RefreshButtons(bool force)
        {
            var current = GetCurrentState();
            if (current != AppWindowState.Minimized && current != _lastState || force)
            {
                Model.IsMaximized.Value = current == AppWindowState.FullScreen || current == AppWindowState.Maximized;
                Model.Buttons.Value = GetButtonsForState(current).Where(s => s != null).ToList();
            }
            _lastState = current;
            RefreshTopMost();
        }

        private CaptionButtonViewModel[] GetButtonsForState(AppWindowState current)
        {
            switch (current)
            {
                case AppWindowState.Normal: return new[]
                    { 
                        Create(CaptionButtonType.Minimize),
                        Create(CaptionButtonType.Maximize),
                        CreatePin(),
                        CreateBox(CaptionButtonType.CompactView, CaptionButtonType.DockLeft, CaptionButtonType.DockRight, CaptionButtonType.EnterFullScreen),
                        Create(CaptionButtonType.Close)
                    };
                case AppWindowState.Compact: return new[]
                    {
                        CreatePin(),
                        CreateBox(CaptionButtonType.NormalView, CaptionButtonType.Minimize, CaptionButtonType.Maximize, CaptionButtonType.DockLeft, CaptionButtonType.DockRight, CaptionButtonType.EnterFullScreen),
                        Create(CaptionButtonType.Close)
                    };
                case AppWindowState.FullScreen: return new[]
                    {
                        Create(CaptionButtonType.Minimize),
                        Create(CaptionButtonType.ExitFullScreen),
                        CreateBox(CaptionButtonType.CompactView, CaptionButtonType.NormalView, CaptionButtonType.DockLeft, CaptionButtonType.DockRight),
                        Create(CaptionButtonType.Close)
                    };
                case AppWindowState.Maximized: return new[]
                    {
                        Create(CaptionButtonType.Minimize),
                        Create(CaptionButtonType.Restore),
                        CreateBox(CaptionButtonType.CompactView, CaptionButtonType.NormalView, CaptionButtonType.DockLeft, CaptionButtonType.DockRight, CaptionButtonType.EnterFullScreen),
                        Create(CaptionButtonType.Close)
                    };

                default: throw new Exception();
            }
        }

        private CaptionButtonViewModel CreatePin()
        {
            if (TopMostModeConverter.ToMode(_coreData.ThisDevice.DeviceSettings) != TopMostMode.Manual)
                return null;

            if (_topMostPinned)
                return CreateButton(CaptionButtonType.PinOff, PackIconKind.PinOff, "Disable 'Top most' mode", () =>
                {
                    _topMostPinned = false;
                    RefreshButtons(true);
                });
            else
            {
                return CreateButton(CaptionButtonType.Pin, PackIconKind.Pin, "Enable 'Top most' mode", () =>
                {
                    _topMostPinned = true;
                    RefreshButtons(true);
                });
            }
        }

        private bool IsCompact()
        {
            return _window.ActualWidth < 500 && _window.ActualHeight < 450;
        }

        private AppWindowState GetCurrentState()
        {
            if (_window.WindowState == WindowState.Minimized)
                return AppWindowState.Minimized;
            else if (_window.WindowState == WindowState.Maximized)
            {
                if (_window.WindowStyle == WindowStyle.None) return AppWindowState.FullScreen;
                else return AppWindowState.Maximized;
            }
            else
            {
                if (IsCompact()) return AppWindowState.Compact;
                return AppWindowState.Normal;
            }
        }

        private CaptionButtonViewModel Create(CaptionButtonType type)
        {
            switch (type)
            {
                case CaptionButtonType.Close:
                    return CreateButton(type, PackIconKind.Close, "Close", () => SystemCommands.CloseWindow(_window));
                case CaptionButtonType.Maximize:
                    return CreateButton(type, PackIconKind.WindowMaximize, "Maximize", () => SystemCommands.MaximizeWindow(_window));
                case CaptionButtonType.Minimize:
                    return CreateButton(type, PackIconKind.WindowMinimize, "Minimize", () => SystemCommands.MinimizeWindow(_window));
                case CaptionButtonType.Restore:
                    return CreateButton(type, PackIconKind.WindowRestore, "Restore", () => SystemCommands.RestoreWindow(_window));
                case CaptionButtonType.EnterFullScreen:
                    return CreateButton(type, PackIconKind.Fullscreen, "Fullscreen", () => GoToState(AppWindowState.FullScreen));
                case CaptionButtonType.ExitFullScreen: 
                    return CreateButton(type, PackIconKind.FullscreenExit, "Exit fullscreen", () => GoToState(_stateBeforeFullscreen));
                case CaptionButtonType.DockLeft:
                    return CreateButton(type, PackIconKind.ArrowCollapseLeft, "Dock left", () => DockLeft()); 
                case CaptionButtonType.DockRight: 
                    return CreateButton(type, PackIconKind.ArrowCollapseRight, "Dock right", () => DockRight());
                case CaptionButtonType.CompactView:
                    return CreateButton(type, PackIconKind.Crop, "Compact View", () => GoToState(AppWindowState.Compact));
                case CaptionButtonType.NormalView:
                    return CreateButton(type, PackIconKind.FilePresentationBox, "Normal View", () => GoToState(AppWindowState.Normal));
                default:
                    throw new Exception();
            }
        }

        private void DockRight()
        {
            var dock = IsDocked();
            if (dock == -1)
                InputSender.Send(CaptionButtonType.DockLeft);
            else if (dock == 1)
            {
            }
            else
                InputSender.Send(CaptionButtonType.DockRight);
        }

        private void DockLeft()
        {
            var dock = IsDocked();
            if (dock == -1)
            {
            }
            else if (dock == 1)
            {
                InputSender.Send(CaptionButtonType.DockRight);
            }
            else
                InputSender.Send(CaptionButtonType.DockLeft);
        }

        private int IsDocked()
        {
            var wa = MonitorHelper.GetMonitorWorkingArea(_window);
            var w = GetWindowRect();

            if (IsAlmostEqual(wa.Height, w.H) && IsAlmostEqual(w.T, wa.Top))
            {
                if (IsAlmostEqual(wa.Left, w.L) && w.W < wa.Width * 4 / 5)
                    return -1;

                if (IsAlmostEqual(wa.Left + wa.Width, w.L + w.W) && w.W < wa.Width * 4 / 5)
                    return 1;
            }

            return 0;
        }

        private bool IsAlmostEqual(double a, double b) => Math.Abs(a - b) < 4;

        private CaptionButtonViewModel CreateBox(params CaptionButtonType[] types) => 
            CreateButton(CaptionButtonType.MultiBox, PackIconKind.Menu, "Further options", null, types.Select(s => Create(s)).ToArray());

        private CaptionButtonViewModel CreateButton(CaptionButtonType type, PackIconKind icon, string name, Action action, CaptionButtonViewModel[] subItems = null)
        {
            return new CaptionButtonViewModel
            {
                Type = type,
                Icon = icon,
                Name = name,
                SubItems = subItems,
                Action = action
            };
        }

        private async Task ProcessDelayedSizeChange(CancellationToken cancellationToken)
        {
            await Task.Delay(1000, cancellationToken);
            StoreRectangles();
        }

        private void StoreRectangles()
        {
            if (_window.WindowState == WindowState.Normal)
            {
                if (IsCompact())
                    _compactRect = GetWindowRect();
                else
                    _normalRect = GetWindowRect();
            }
        }

        private IntRect GetWindowRect()
        {
            return new IntRect
            {
                L = (int)_window.Left,
                T = (int)_window.Top,
                W = (int)_window.ActualWidth,
                H = (int)_window.ActualHeight,
            };
        }
    }

    public class CaptionViewModel
    {
        public Property<List<CaptionButtonViewModel>> Buttons { get; } = new Property<List<CaptionButtonViewModel>>();

        public Property<bool> IsMaximized { get; } = new Property<bool>();
    }

    public class CaptionButtonViewModel : ICommand
    {
        public Action Action { get; set; }

        public PackIconKind Icon { get; set; }

        public string Name { get; set; }

        public CaptionButtonType Type { get; set; }

        public CaptionButtonViewModel[] SubItems { get; set; }

        public event EventHandler CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter) => Action();
    }


    public enum CaptionButtonType
    {
        Close,
        Minimize,
        Maximize,
        Restore,
        EnterFullScreen,
        ExitFullScreen,
        DockLeft,
        DockRight,
        CompactView,
        NormalView,
        MultiBox,
        PinOff,
        Pin
    }
}
