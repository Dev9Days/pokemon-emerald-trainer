using PokemonGen3Hack.Utils;

int assertions = 0;
void Equal(WindowBounds actual, WindowBounds expected, string name) {
  assertions++;
  if (actual != expected) throw new Exception($"{name}: expected {expected}, got {actual}");
}

var primary = new WindowBounds(0, 0, 1920, 1040);
var left = new WindowBounds(-2560, -200, 2560, 1400);
var right = new WindowBounds(1920, 0, 1920, 1040);
Equal(new WindowBounds(100, 80, 1120, 720).FitInside(primary), new(100, 80, 1120, 720), "Keep normal position");
Equal(new WindowBounds(2500, 200, 1120, 720).FitInside(primary), new(800, 200, 1120, 720), "Removed right monitor");
Equal(new WindowBounds(-2000, -100, 1120, 720).FitInside(left), new(-2000, -100, 1120, 720), "Negative desktop coordinates");
Equal(new WindowBounds(2000, 100, 1120, 720).FitInside(right), new(2000, 100, 1120, 720), "Keep secondary monitor");
Equal(new WindowBounds(1800, 900, 1120, 720).FitInside(primary), new(800, 320, 1120, 720), "Do not cover taskbar");
Equal(new WindowBounds(0, 0, 4000, 3000).FitInside(primary), primary, "Oversized saved window");
Equal(new WindowBounds(-500, -500, -1, 0).FitInside(primary), new(0, 0, 1, 1), "Invalid size");
Equal(new WindowBounds(int.MaxValue, int.MinValue, 1120, 720).FitInside(primary), new(800, 0, 1120, 720), "Extreme saved coordinates");
var topTaskbar = new WindowBounds(60, 48, 1860, 1032);
Equal(new WindowBounds(0, 0, 1120, 720).FitInside(topTaskbar), new(60, 48, 1120, 720), "Top and left work-area origin");

foreach (double density in new[] { 1, 1.25, 1.5, 1.75, 2, 2.5, 3 }) {
  foreach (var screen in new[] { new WindowBounds(0, 0, 1366, 728), primary, left, new WindowBounds(0, 0, 3840, 2080) }) {
    var saved = new WindowBounds(1800, 950, (int)Math.Round(1120 * density), (int)Math.Round(720 * density));
    var fitted = saved.FitInside(screen);
    assertions++;
    if (fitted.X < screen.X || fitted.Y < screen.Y ||
        (long)fitted.X + fitted.Width > (long)screen.X + screen.Width ||
        (long)fitted.Y + fitted.Height > (long)screen.Y + screen.Height) {
      throw new Exception($"Window outside work area at {density * 100}%: {fitted}");
    }
    Equal(fitted.FitInside(screen), fitted, $"Idempotent fit at {density * 100}%");
  }
}
Console.WriteLine($"Window placement: {assertions} assertions passed.");
