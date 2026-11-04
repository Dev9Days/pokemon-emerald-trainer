using System.Text;

namespace PokemonGen3Hack {
  public partial class App : Application {
    public App() {
      InitializeComponent();
      Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    protected override Window CreateWindow(IActivationState? activationState) {
      const double defaultWidth = 1108;
      const double defaultHeight = 695;
      const string sizePreset = "1108x695";
      bool restoreCompactLayout = Preferences.Get("CompactLayoutWindow", false);
      bool restoreSize = Preferences.Get("WindowSizePreset", "") == sizePreset;
      double width = restoreSize ? Preferences.Get("WindowWidth", defaultWidth) : defaultWidth;
      double height = restoreSize ? Preferences.Get("WindowHeight", defaultHeight) : defaultHeight;
      if (!double.IsFinite(width)) width = defaultWidth;
      if (!double.IsFinite(height)) height = defaultHeight;
      var window = new Window(new MainPage()) {
        Title = "PokemonGen3Hack",
        Width = Math.Clamp(width, 320, 16384),
        Height = Math.Clamp(height, 320, 16384),
      };
      // Apply the new size once, then retain subsequent user resizing and the existing position.
      Preferences.Set("WindowSizePreset", sizePreset);
      Preferences.Set("CompactLayoutWindow", true);
      // The native window must exist before its monitor, DPI and work area are known.
      window.Created += (_, _) => new WinUI.WindowPlacementManager(window).Initialize(restoreCompactLayout);

      return window;
    }
  }
}
