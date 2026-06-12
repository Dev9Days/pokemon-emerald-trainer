using Microsoft.Extensions.Logging;
using PokemonGen3Hack.Services;

namespace PokemonGen3Hack {
  public static class MauiProgram {
    public static MauiApp CreateMauiApp() {
      var builder = MauiApp.CreateBuilder();
      builder
          .UseMauiApp<App>()
          .ConfigureFonts(fonts => {
            fonts.AddFont("NotoSansKR-Regular.ttf", "NotoSansRegular");
            fonts.AddFont("NotoSansKR-Bold.ttf", "NotoSansBold");
          });

      builder.Services.AddMauiBlazorWebView();
      builder.Services.AddSingleton<ToastService>();

#if DEBUG
      builder.Services.AddBlazorWebViewDeveloperTools();
      builder.Logging.AddDebug();
#endif

      return builder.Build();
    }
  }
}
