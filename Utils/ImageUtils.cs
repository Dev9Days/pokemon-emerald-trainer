namespace PokemonGen3Hack.Utils {
  internal static class ImageUtils {
    public static async Task<byte[]> LoadImageFromUrlAsync(string url) {
      if (string.IsNullOrWhiteSpace(url)) {
        throw new ArgumentException("URL이 비어있거나 잘못되었습니다.", nameof(url));
      }
      using HttpClient client = new();

      // Bulbagarden Archives blocks some direct image requests unless they look like file-page navigation.
      client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
      client.DefaultRequestHeaders.Add("Accept", "image/*,*/*;q=0.8");
      client.DefaultRequestHeaders.Referrer = GetBulbagardenFileReferrer(url);

      byte[] imageBytes = await client.GetByteArrayAsync(url);
      return imageBytes;
    }

    private static Uri? GetBulbagardenFileReferrer(string url) {
      if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || uri.Host != "archives.bulbagarden.net") {
        return null;
      }

      string fileName = Path.GetFileName(uri.LocalPath);
      if (string.IsNullOrWhiteSpace(fileName)) {
        return null;
      }

      return new Uri($"https://archives.bulbagarden.net/wiki/File:{Uri.EscapeDataString(fileName)}");
    }

    public static string GetImageBase64Url(byte[] imageBytes, string mimeType = "image/png") {
      if (imageBytes == null || imageBytes.Length == 0) {
        return string.Empty;
      }
      string base64 = Convert.ToBase64String(imageBytes);
      return $"data:{mimeType};base64,{base64}";
    }
  }
}
