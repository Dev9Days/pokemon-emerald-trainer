using System.Text;

namespace PokemonGen3Hack {
  public partial class App : Application {
    public App() {
      InitializeComponent();
      Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    protected override Window CreateWindow(IActivationState? activationState) {
      var window = new Window(new MainPage()) {
        Title = "PokemonGen3Hack",
        Width = 784.17,
        Height = 988.67
      };
      // 저장된 창 크기와 위치 복원
      if (Preferences.ContainsKey("WindowWidth")) {
        window.Width = Preferences.Get("WindowWidth", 784.17);
      }
      if (Preferences.ContainsKey("WindowHeight")) {
        window.Height = Preferences.Get("WindowHeight", 988.67);
      }
      if (Preferences.ContainsKey("WindowX")) {
        window.X = Preferences.Get("WindowX", 0.0);
      }
      if (Preferences.ContainsKey("WindowY")) {
        window.Y = Preferences.Get("WindowY", 0.0);
      }

      // 창 크기/위치 변경 시 저장
      window.SizeChanged += (s, e) => {
        Preferences.Set("WindowWidth", window.Width);
        Preferences.Set("WindowHeight", window.Height);
      };

      window.Destroying += (s, e) => {
        Preferences.Set("WindowWidth", window.Width);
        Preferences.Set("WindowHeight", window.Height);
        Preferences.Set("WindowX", window.X);
        Preferences.Set("WindowY", window.Y);
      };

      return window;
    }
  }
}
