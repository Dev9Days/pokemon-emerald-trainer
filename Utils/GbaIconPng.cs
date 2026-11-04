using System.Buffers.Binary;
using System.IO.Compression;

namespace PokemonGen3Hack.Utils {
  internal static class GbaIconPng {
    public static byte[] Encode(ReadOnlySpan<byte> tiles, ReadOnlySpan<byte> palette) {
      if (tiles.Length != 512) throw new ArgumentException("Expected one 32x32 4bpp frame.", nameof(tiles));
      if (palette.Length != 32) throw new ArgumentException("Expected a 16-color BGR555 palette.", nameof(palette));

      // Each PNG scanline has filter 0 and 16 packed pixel pairs, high nibble first.
      byte[] scanlines = new byte[32 * 17];
      for (int y = 0; y < 32; y++) {
        for (int pair = 0; pair < 16; pair++) {
          int tile = (y / 8) * 4 + pair / 4;
          byte pixels = tiles[tile * 32 + (y % 8) * 4 + pair % 4];
          scanlines[y * 17 + 1 + pair] = (byte)((pixels << 4) | (pixels >> 4));
        }
      }
      byte[] rgb = new byte[16 * 3];
      for (int i = 0; i < 16; i++) {
        ushort color = BinaryPrimitives.ReadUInt16LittleEndian(palette.Slice(i * 2, 2));
        for (int channel = 0; channel < 3; channel++) {
          int component = (color >> (channel * 5)) & 31;
          rgb[i * 3 + channel] = (byte)((component << 3) | (component >> 2));
        }
      }

      using MemoryStream png = new();
      png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
      byte[] header = new byte[13];
      BinaryPrimitives.WriteUInt32BigEndian(header, 32);
      BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), 32);
      header[8] = 4;
      header[9] = 3;
      WriteChunk(png, "IHDR"u8, header);
      WriteChunk(png, "PLTE"u8, rgb);
      WriteChunk(png, "tRNS"u8, [0]);
      using MemoryStream compressed = new();
      using (ZLibStream zlib = new(compressed, CompressionLevel.Fastest, leaveOpen: true)) {
        zlib.Write(scanlines);
      }
      WriteChunk(png, "IDAT"u8, compressed.ToArray());
      WriteChunk(png, "IEND"u8, []);
      return png.ToArray();
    }

    private static void WriteChunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data) {
      Span<byte> value = stackalloc byte[4];
      BinaryPrimitives.WriteInt32BigEndian(value, data.Length);
      stream.Write(value);
      stream.Write(type);
      stream.Write(data);
      uint crc = UpdateCrc(uint.MaxValue, type);
      crc = UpdateCrc(crc, data);
      BinaryPrimitives.WriteUInt32BigEndian(value, ~crc);
      stream.Write(value);
    }

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> bytes) {
      foreach (byte b in bytes) {
        crc ^= b;
        for (int bit = 0; bit < 8; bit++) {
          crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xEDB88320u : 0);
        }
      }
      return crc;
    }
  }
}
