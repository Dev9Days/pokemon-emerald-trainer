using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using PokemonGen3Hack.Emulator;
using PokemonGen3Hack.Pokemon;
using PokemonGen3Hack.Utils;

internal static class RomIconTests {
  private static int assertions;

  private static void Check(bool condition, string name) {
    assertions++;
    if (!condition) throw new InvalidOperationException(name);
  }

  private static void Reject<T>(Action action, string name) where T : Exception {
    bool rejected = false;
    try { action(); }
    catch (T) { rejected = true; }
    Check(rejected, name);
  }

  public static void Run() {
    for (uint value = 0; value < 256; value++) {
      uint personality = (value & 3) | (((value >> 2) & 3) << 8)
        | (((value >> 4) & 3) << 16) | (((value >> 6) & 3) << 24);
      ushort expected = value % 28 == 0 ? (ushort)201 : (ushort)(412 + value % 28);
      Check(RomPokemonIcon.GetIconSpecies(201, personality, false) == expected, "Unown form");
    }
    Check(RomPokemonIcon.GetIconSpecies(201, 1, true) == 412, "Egg overrides Unown form");
    Check(RomPokemonIcon.GetIconSpecies(ushort.MaxValue, 0, false) == 260, "Invalid species fallback");
    Check(RomPokemonIcon.GetIconSpecies(439, 0, false) == 439, "Last icon is valid");

    byte[] rom = new byte[0x5000];
    const int table = 0x100;
    const int palettes = 0x100 + RomPokemonIcon.IconCount * 5;
    for (int i = 0; i < RomPokemonIcon.IconCount; i++) {
      BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(0x100 + i * 4), 0x08002000);
    }
    BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(palettes), 0x08004000);
    BinaryPrimitives.WriteUInt16LittleEndian(rom.AsSpan(palettes + 4), 0xDAC0);
    for (int y = 0; y < 32; y++) {
      for (int x = 0; x < 32; x++) {
        int tileByte = ((y / 8) * 4 + x / 8) * 32 + (y % 8) * 4 + (x % 8) / 2;
        rom[0x2000 + tileByte] |= (byte)(Pixel(x, y) << ((x % 2) * 4));
      }
    }
    ushort[] colors = [0, 31, 31 << 5, 31 << 10, 0x7FFF, 1, 2, 3, 4, 8, 12, 16, 20, 24, 28, 30];
    for (int i = 0; i < colors.Length; i++) {
      BinaryPrimitives.WriteUInt16LittleEndian(rom.AsSpan(0x4000 + i * 2), colors[i]);
    }
    byte[] Read(int offset, int count) => rom.AsSpan(offset, count).ToArray();
    var decoded = Decode(RomPokemonIcon.ReadPng(Read, table, 1));
    for (int y = 0; y < 32; y++) {
      for (int x = 0; x < 32; x++) {
        Check(decoded.Pixels[y * 32 + x] == Pixel(x, y), "Tile order / nibble order");
      }
    }
    Check(decoded.Palette.AsSpan(3, 12).SequenceEqual(new byte[] {
      255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 255, 255,
    }), "BGR555 colors");
    for (int i = 5; i < 16; i++) {
      int expected = colors[i] * 255 / 31;
      Check(Math.Abs(decoded.Palette[i * 3] - expected) <= 1, "5-bit color expansion");
    }

    Array.Fill(rom, (byte)0x33, 0x2400, 512);
    Check(Decode(RomPokemonIcon.ReadPng(Read, table, 410)).Pixels.All(p => p == 3), "Deoxys Speed frame");
    Reject<InvalidDataException>(() => RomPokemonIcon.ReadPng(Read, -1, 1), "Negative table offset");
    Reject<InvalidDataException>(() => RomPokemonIcon.ReadPng(Read, 0x02000000, 1), "Table outside ROM");
    Reject<InvalidDataException>(() => RomPokemonIcon.ReadPng(Read, 0x08000100, 1), "GBA address is not an offset");
    Reject<InvalidDataException>(() => RomPokemonIcon.ReadPng((_, _) => [], table, 1), "Short read");
    rom[0x100 + RomPokemonIcon.IconCount * 4 + 1] = 3;
    Reject<InvalidDataException>(() => RomPokemonIcon.ReadPng(Read, table, 1), "Invalid palette index");
    rom[0x100 + RomPokemonIcon.IconCount * 4 + 1] = 0;
    rom[palettes + 4] = 0;
    Reject<InvalidDataException>(() => RomPokemonIcon.ReadPng(Read, table, 1), "Invalid palette record");
    BinaryPrimitives.WriteUInt16LittleEndian(rom.AsSpan(palettes + 4), 0xDAC0);
    BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(0x104), 0x02000000);
    Reject<InvalidDataException>(() => RomPokemonIcon.ReadPng(Read, table, 1), "RAM icon pointer");
    BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(0x104), 0x09FFFFF0);
    Reject<InvalidDataException>(() => RomPokemonIcon.ReadPng(Read, table, 1), "Icon crosses ROM boundary");
    BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(0x104), 0x08002000);
    BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(palettes), 0);
    Reject<InvalidDataException>(() => RomPokemonIcon.ReadPng(Read, table, 1), "Invalid palette pointer");
    Reject<ArgumentException>(() => GbaIconPng.Encode(new byte[511], new byte[32]), "Invalid tile size");
    Reject<ArgumentException>(() => GbaIconPng.Encode(new byte[512], new byte[31]), "Invalid palette size");
    Console.WriteLine($"PASS: {assertions} icon assertions (including local ROMs when supplied).");
  }

  private static byte Pixel(int x, int y) => (byte)((x + 2 * y + (x / 8) * 3 + (y / 8) * 5) % 16);

  public static void CheckRom(string path, RomProfile profile) {
    using var stream = File.OpenRead(path);
    byte[] Read(int offset, int count) {
      stream.Position = offset;
      byte[] data = new byte[count];
      stream.ReadExactly(data);
      return data;
    }
    string previews = Path.Combine(AppContext.BaseDirectory, "rom-icon-previews", Path.GetFileNameWithoutExtension(path));
    Directory.CreateDirectory(previews);
    for (ushort species = 0; species < RomPokemonIcon.IconCount; species++) {
      byte[] png = RomPokemonIcon.ReadPng(Read, profile.MonIconTableOffset, species);
      var decoded = Decode(png);
      if (species is 1 or 25 or 277 or 410 or 412 or 439) {
        Check(decoded.Pixels.Any(p => p != 0), $"Blank ROM icon {species}");
        File.WriteAllBytes(Path.Combine(previews, $"{species}.png"), png);
      }
    }
    byte[] unown = RomPokemonIcon.ReadPng(Read, profile.MonIconTableOffset, 201, 1);
    byte[] unownB = RomPokemonIcon.ReadPng(Read, profile.MonIconTableOffset, 413);
    Check(unown.SequenceEqual(unownB), "ROM Unown B selection");
    byte[] egg = RomPokemonIcon.ReadPng(Read, profile.MonIconTableOffset, 1, isEgg: true);
    Check(egg.SequenceEqual(RomPokemonIcon.ReadPng(Read, profile.MonIconTableOffset, 412)), "ROM egg selection");
    Console.WriteLine($"PASS: {Path.GetFileName(path)}: all 440 icons decoded; previews: {previews}");
  }

  private static (byte[] Pixels, byte[] Palette) Decode(byte[] png) {
    Check(png.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }), "PNG signature");
    int offset = 8;
    List<string> chunks = [];
    byte[] palette = [];
    using MemoryStream compressed = new();
    while (offset < png.Length) {
      int length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset));
      string type = Encoding.ASCII.GetString(png, offset + 4, 4);
      var payload = png.AsSpan(offset + 8, length);
      uint crc = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset + 8 + length));
      Check(crc == Crc(png.AsSpan(offset + 4, length + 4)), $"{type} CRC");
      chunks.Add(type);
      switch (type) {
        case "IHDR":
          Check(length == 13 && BinaryPrimitives.ReadInt32BigEndian(payload) == 32
            && BinaryPrimitives.ReadInt32BigEndian(payload[4..]) == 32
            && payload[8] == 4 && payload[9] == 3 && payload[10..].ToArray().All(b => b == 0), "PNG header");
          break;
        case "PLTE": palette = payload.ToArray(); break;
        case "tRNS": Check(payload.SequenceEqual(new byte[] { 0 }), "Transparent index 0"); break;
        case "IDAT": compressed.Write(payload); break;
        case "IEND": Check(length == 0 && crc == 0xAE426082, "IEND known CRC"); break;
      }
      offset += length + 12;
    }
    Check(offset == png.Length && chunks.SequenceEqual(new[] { "IHDR", "PLTE", "tRNS", "IDAT", "IEND" }), "PNG chunks");
    Check(palette.Length == 48, "16-color palette");
    compressed.Position = 0;
    using ZLibStream zlib = new(compressed, CompressionMode.Decompress);
    byte[] rows = new byte[32 * 17];
    zlib.ReadExactly(rows);
    Check(zlib.ReadByte() == -1, "PNG image data size");
    byte[] pixels = new byte[32 * 32];
    for (int y = 0; y < 32; y++) {
      Check(rows[y * 17] == 0, "PNG filter");
      for (int x = 0; x < 32; x++) {
        byte pair = rows[y * 17 + 1 + x / 2];
        pixels[y * 32 + x] = (byte)((x % 2 == 0 ? pair >> 4 : pair) & 15);
      }
    }
    return (pixels, palette);
  }

  private static uint Crc(ReadOnlySpan<byte> data) {
    uint[] table = new uint[256];
    for (uint i = 0; i < table.Length; i++) {
      uint value = i;
      for (int bit = 0; bit < 8; bit++) value = (value & 1) == 0 ? value >> 1 : (value >> 1) ^ 0xEDB88320;
      table[i] = value;
    }
    uint crc = uint.MaxValue;
    foreach (byte b in data) crc = (crc >> 8) ^ table[(crc ^ b) & 255];
    return ~crc;
  }
}
