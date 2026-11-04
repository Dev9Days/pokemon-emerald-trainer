using System.Buffers.Binary;
using PokemonGen3Hack.Utils;

namespace PokemonGen3Hack.Pokemon {
  internal static class RomPokemonIcon {
    internal const int IconCount = 440;
    private const uint RomBase = 0x08000000;
    private const uint RomEnd = 0x0A000000;
    private const int PaletteCount = 3;
    private const ushort EggSpecies = 412;
    private const ushort InvalidIconSpecies = 260;

    public static ushort GetIconSpecies(ushort species, uint personality, bool isEgg) {
      if (isEgg) return EggSpecies;
      if (species == 201) {
        uint letter = (((personality & 0x03000000) >> 18)
          | ((personality & 0x00030000) >> 12)
          | ((personality & 0x00000300) >> 6)
          | (personality & 0x00000003)) % 28;
        return letter == 0 ? species : (ushort)(412 + letter);
      }
      return species < IconCount ? species : InvalidIconSpecies;
    }

    public static byte[] ReadPng(Func<int, int, byte[]> readRom, int tableOffset,
        ushort species, uint personality = 0, bool isEgg = false) {
      ArgumentNullException.ThrowIfNull(readRom);
      if (tableOffset < 0 || tableOffset > RomEnd - RomBase - IconCount * 5 - PaletteCount * 8) {
        throw new InvalidDataException($"Icon table offset is outside ROM: 0x{tableOffset:X}");
      }
      ushort iconSpecies = GetIconSpecies(species, personality, isEgg);
      uint iconAddress = BinaryPrimitives.ReadUInt32LittleEndian(
        ReadOffset(checked(tableOffset + iconSpecies * 4), 4));

      // pokemon_icon.c: 440 pointers, 440 palette indices, then SpritePalette records.
      int indicesOffset = checked(tableOffset + IconCount * 4);
      byte paletteIndex = ReadOffset(checked(indicesOffset + iconSpecies), 1)[0];
      if (paletteIndex >= PaletteCount) {
        throw new InvalidDataException($"Invalid icon palette index: {paletteIndex}");
      }
      int palettesOffset = checked(indicesOffset + IconCount);
      byte[] paletteEntry = ReadOffset(checked(palettesOffset + paletteIndex * 8), 8);
      ushort paletteTag = BinaryPrimitives.ReadUInt16LittleEndian(paletteEntry.AsSpan(4));
      if (paletteTag != 0xDAC0 + paletteIndex) {
        throw new InvalidDataException($"Unexpected icon palette tag: 0x{paletteTag:X4}");
      }
      uint paletteAddress = BinaryPrimitives.ReadUInt32LittleEndian(paletteEntry);

      // Emerald uses the Speed form stored after the two Normal-form Deoxys frames.
      if (iconSpecies == 410) iconAddress = checked(iconAddress + 0x400);
      byte[] tiles = Read(iconAddress, 512);
      byte[] palette = Read(paletteAddress, 32);
      return GbaIconPng.Encode(tiles, palette);

      byte[] Read(uint address, int length) {
        if (address < RomBase || address > RomEnd - length) {
          throw new InvalidDataException($"Icon address is outside ROM: 0x{address:X8}");
        }
        return ReadOffset(checked((int)(address - RomBase)), length);
      }

      byte[] ReadOffset(int offset, int length) {
        if (offset < 0 || offset > RomEnd - RomBase - length) {
          throw new InvalidDataException($"Icon offset is outside ROM: 0x{offset:X}");
        }
        byte[] data = readRom(offset, length);
        if (data.Length != length) {
          throw new InvalidDataException($"Short icon ROM read: {data.Length}/{length}");
        }
        return data;
      }
    }
  }
}
