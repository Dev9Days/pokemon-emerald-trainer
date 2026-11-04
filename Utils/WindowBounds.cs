namespace PokemonGen3Hack.Utils;

internal readonly record struct WindowBounds(int X, int Y, int Width, int Height) {
  public WindowBounds FitInside(WindowBounds workArea) {
    if (workArea.Width <= 0 || workArea.Height <= 0) {
      throw new ArgumentOutOfRangeException(nameof(workArea));
    }
    int width = Math.Clamp(Width, 1, workArea.Width);
    int height = Math.Clamp(Height, 1, workArea.Height);
    int x = (int)Math.Clamp((long)X, workArea.X, (long)workArea.X + workArea.Width - width);
    int y = (int)Math.Clamp((long)Y, workArea.Y, (long)workArea.Y + workArea.Height - height);
    return new(x, y, width, height);
  }
}
