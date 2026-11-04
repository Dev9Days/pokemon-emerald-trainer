namespace PokemonGen3Hack.Utils {
  internal static class ImageUtils {
    public static string GetImageBase64Url(byte[] imageBytes, string mimeType = "image/png") {
      if (imageBytes == null || imageBytes.Length == 0) {
        return string.Empty;
      }
      string base64 = Convert.ToBase64String(imageBytes);
      return $"data:{mimeType};base64,{base64}";
    }
  }
}
