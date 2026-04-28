using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using GameSubRelay.App.ViewModels;

namespace GameSubRelay.App.Overlay;

public partial class OverlayWindow : Window
{
    private const int GwlExstyle = -20;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExLayered = 0x00080000;
    private const int WsExTransparent = 0x00000020;
    private readonly OverlayViewModel _viewModel;
    private bool _isDragging;

    public OverlayWindow(OverlayViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        WindowStartupLocation = WindowStartupLocation.Manual;
        AllowsTransparency = true;
        Opacity = _viewModel.Settings.Opacity;
        Loaded += OnLoaded;
        _viewModel.Settings.PropertyChanged += OnSettingsPropertyChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        LocationChanged += (_, _) => PersistBounds();
        SizeChanged += (_, _) => PersistBounds();
        MouseLeftButtonDown += OnWindowMouseLeftButtonDown;
        MouseDoubleClick += OnWindowMouseDoubleClick;
        PreviewMouseMove += OnWindowMouseMove;
        _viewModel.RefreshCommands();

        _viewModel.ToggleVisibilityCommand = new RelayCommand(_ =>
        {
            _viewModel.ToggleOverlayVisibility();
            UpdateWindowVisibility();
        }, _ => true);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InitializeWindowFlags();
        UpdateWindowVisibility();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        InitializeWindowFlags();
    }

    private void OnWindowMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_viewModel.IsEditMode)
        {
            return;
        }

        if (e.OriginalSource == ResizeGrip)
        {
            return;
        }

        _isDragging = true;
        DragMove();
        _isDragging = false;
        PersistBounds();
    }

    private void OnWindowMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.IsEditMode)
        {
            _viewModel.ToggleEditMode();
            InitializeWindowFlags();
        }
    }

    private void OnWindowMouseMove(object sender, MouseEventArgs e)
    {
        if (!_viewModel.IsEditMode)
        {
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed && e.OriginalSource == ResizeGrip)
        {
            var position = e.GetPosition(this);
            Width = Math.Max(300, position.X);
            Height = Math.Max(120, position.Y);
            PersistBounds();
        }
    }

    public void SetEditMode(bool isEditMode)
    {
        if (_viewModel.IsEditMode == isEditMode)
        {
            return;
        }

        _viewModel.IsEditMode = isEditMode;
        InitializeWindowFlags();
    }

    public void UpdateWindowVisibility()
    {
        Visibility = _viewModel.IsWindowVisible ? Visibility.Visible : Visibility.Hidden;
    }

    public void ApplyFromViewModel()
    {
        UpdateWindowVisibility();
        InitializeWindowFlags();
        Opacity = _viewModel.Settings.Opacity;
        FontSize = _viewModel.Settings.FontSize;
    }

    private void PersistBounds()
    {
        if (_isDragging)
        {
            return;
        }

        _viewModel.Settings.Left = Left;
        _viewModel.Settings.Top = Top;
        _viewModel.Settings.Width = Width;
        _viewModel.Settings.Height = Height;
    }

    private void InitializeWindowFlags()
    {
        if (!IsLoaded)
        {
            return;
        }

        var hWnd = new WindowInteropHelper(this).Handle;
        if (hWnd == IntPtr.Zero)
        {
            return;
        }

        var styles = (IntPtr)GetWindowLongPtr(hWnd, GwlExstyle).ToInt64();
        if (_viewModel.IsEditMode)
        {
            styles = new IntPtr(styles.ToInt64() & ~WsExTransparent);
            ResizeMode = ResizeMode.CanResizeWithGrip;
        }
        else
        {
            styles = new IntPtr(styles.ToInt64() | WsExTransparent);
            ResizeMode = ResizeMode.NoResize;
        }

        styles = new IntPtr(styles.ToInt64() | WsExToolWindow | WsExLayered);
        SetWindowLongPtr(hWnd, GwlExstyle, styles);

        ShowInTaskbar = false;
        Topmost = true;
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        PersistBounds();
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        if (!_viewModel.IsEditMode)
        {
            _viewModel.IsEditMode = false;
        }
    }

    private void InitializeWindowFlagsForMode()
    {
        UpdateStyleForEditMode();
        ApplyFromViewModel();
    }

    private void UpdateStyleForEditMode()
    {
        if (!IsLoaded)
        {
            return;
        }

        var hWnd = new WindowInteropHelper(this).Handle;
        if (hWnd == IntPtr.Zero)
        {
            return;
        }

        int newStyle = GetWindowLong(hWnd, GwlExstyle);
        if (_viewModel.IsEditMode)
        {
            newStyle &= ~WsExTransparent;
        }
        else
        {
            newStyle |= WsExTransparent;
        }

        SetWindowLong(hWnd, GwlExstyle, newStyle);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Settings.PropertyChanged -= OnSettingsPropertyChanged;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnClosed(e);
    }

    public void WindowVisibilityFromVM()
    {
        Visibility = _viewModel.IsVisible ? Visibility.Visible : Visibility.Hidden;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(OverlayViewModel.IsEditMode), StringComparison.Ordinal) ||
            string.Equals(e.PropertyName, nameof(OverlayViewModel.IsVisible), StringComparison.Ordinal) ||
            string.Equals(e.PropertyName, nameof(OverlayViewModel.IsWindowVisible), StringComparison.Ordinal))
        {
            InitializeWindowFlags();
            UpdateWindowVisibility();
        }
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(OverlayRenderSettings.Opacity), StringComparison.Ordinal))
        {
            Opacity = _viewModel.Settings.Opacity;
        }
        else if (string.Equals(e.PropertyName, nameof(OverlayRenderSettings.MaxLines), StringComparison.Ordinal))
        {
            _viewModel.EnsureMaxLines();
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int x, int y, int cx, int cy, uint uFlags);
}
