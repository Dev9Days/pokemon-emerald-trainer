using Microsoft.Maui.Platform;
using Microsoft.UI.Windowing;
using PokemonGen3Hack.Utils;
using Windows.Graphics;
using MauiWindow = Microsoft.Maui.Controls.Window;

namespace PokemonGen3Hack.WinUI;

internal sealed class WindowPlacementManager(MauiWindow window) {
  private readonly Microsoft.UI.Xaml.Window platform = (Microsoft.UI.Xaml.Window)window.Handler!.PlatformView!;
  private AppWindow appWindow = null!;
  private DisplayAreaWatcher watcher = null!;
  private WindowBounds normalBounds;
  private WindowBounds currentWorkArea;
  private double normalDensity = 1;
  private bool adjusting;
  private bool queued;
  private bool disposed;

  public void Initialize(bool restore) {
    appWindow = platform.GetAppWindow()!;
    double width = window.Width;
    double height = window.Height;
    if (restore) {
      if (Preferences.ContainsKey("WindowPhysicalX") && Preferences.ContainsKey("WindowPhysicalY")) {
        appWindow.Move(new PointInt32(Preferences.Get("WindowPhysicalX", 0), Preferences.Get("WindowPhysicalY", 0)));
      } else if (Preferences.ContainsKey("WindowX") && Preferences.ContainsKey("WindowY")) {
        // Migrate legacy MAUI coordinates once; future positions use physical desktop pixels.
        double density = platform.GetDisplayDensity();
        double x = Preferences.Get("WindowX", 0.0) * density;
        double y = Preferences.Get("WindowY", 0.0) * density;
        if (double.IsFinite(x) && double.IsFinite(y)) {
          appWindow.Move(new PointInt32((int)Math.Clamp(x, int.MinValue, int.MaxValue), (int)Math.Clamp(y, int.MinValue, int.MaxValue)));
        }
      }
    }
    FitToDisplay(width, height);
    RememberNormalBounds();
    appWindow.Changed += OnWindowChanged;
    window.DisplayDensityChanged += OnDensityChanged;
    window.Destroying += OnDestroying;
    watcher = DisplayArea.CreateWatcher();
    watcher.Updated += OnDisplayUpdated;
    watcher.Removed += OnDisplayRemoved;
    watcher.Start();
  }

  private void FitToDisplay(double? width = null, double? height = null) {
    var area = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
    if (area.Width <= 0 || area.Height <= 0) return;
    currentWorkArea = new WindowBounds(area.X, area.Y, area.Width, area.Height);
    double density = platform.GetDisplayDensity();
    if (!double.IsFinite(density) || density <= 0) density = 1;
    adjusting = true;
    try {
      window.MinimumWidth = Math.Min(760, area.Width / density);
      window.MinimumHeight = Math.Min(560, area.Height / density);
      // Reapply even when the DIP values are unchanged, since native limits are physical pixels.
      platform.UpdateMinimumSize(window);
      if (!IsRestored()) return;
      int pixelsWide = width.HasValue
        ? (int)Math.Clamp(Math.Round(Math.Max(window.MinimumWidth, width.Value) * density), 1, area.Width)
        : appWindow.Size.Width;
      int pixelsHigh = height.HasValue
        ? (int)Math.Clamp(Math.Round(Math.Max(window.MinimumHeight, height.Value) * density), 1, area.Height)
        : appWindow.Size.Height;
      var bounds = new WindowBounds(appWindow.Position.X, appWindow.Position.Y, pixelsWide, pixelsHigh)
        .FitInside(currentWorkArea);
      if (bounds != CurrentBounds()) appWindow.MoveAndResize(new RectInt32(bounds.X, bounds.Y, bounds.Width, bounds.Height));
    } finally {
      adjusting = false;
    }
    RememberNormalBounds();
  }

  private bool IsRestored() => appWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Restored };
  private WindowBounds CurrentBounds() => new(appWindow.Position.X, appWindow.Position.Y, appWindow.Size.Width, appWindow.Size.Height);

  private void RememberNormalBounds() {
    if (adjusting || queued || !IsRestored() || appWindow.Size.Width <= 0 || appWindow.Size.Height <= 0) return;
    normalBounds = CurrentBounds();
    normalDensity = Math.Max(1, platform.GetDisplayDensity());
  }

  private void QueueFit() {
    if (disposed || queued) return;
    queued = true;
    window.Dispatcher.Dispatch(() => {
      queued = false;
      if (!disposed) FitToDisplay();
    });
  }

  private void OnDensityChanged(object? sender, DisplayDensityChangedEventArgs args) => QueueFit();
  private void OnDisplayUpdated(DisplayAreaWatcher sender, DisplayArea area) => QueueFit();
  private void OnDisplayRemoved(DisplayAreaWatcher sender, DisplayArea area) => QueueFit();
  private void OnWindowChanged(AppWindow sender, AppWindowChangedEventArgs args) {
    if (adjusting || disposed) return;
    if (args.DidPositionChange) {
      var area = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
      if (currentWorkArea != new WindowBounds(area.X, area.Y, area.Width, area.Height)) {
        QueueFit();
        return;
      }
    }
    if (args.DidPresenterChange && IsRestored()) QueueFit();
    RememberNormalBounds();
  }

  private void OnDestroying(object? sender, EventArgs args) {
    disposed = true;
    watcher.Stop();
    watcher.Updated -= OnDisplayUpdated;
    watcher.Removed -= OnDisplayRemoved;
    appWindow.Changed -= OnWindowChanged;
    window.DisplayDensityChanged -= OnDensityChanged;
    window.Destroying -= OnDestroying;
    if (normalBounds.Width <= 0 || normalBounds.Height <= 0) return;
    Preferences.Set("WindowWidth", normalBounds.Width / normalDensity);
    Preferences.Set("WindowHeight", normalBounds.Height / normalDensity);
    Preferences.Set("WindowPhysicalX", normalBounds.X);
    Preferences.Set("WindowPhysicalY", normalBounds.Y);
  }
}
