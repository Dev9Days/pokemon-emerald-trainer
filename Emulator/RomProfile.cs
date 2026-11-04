namespace PokemonGen3Hack.Emulator {
  internal sealed record RomProfile {
    public required string Name { get; init; }
    public required Language Language { get; init; }
    public required int PartyCountOffset { get; init; }
    public required int Slot1Offset { get; init; }
    public required int Slot2Offset { get; init; }
    public required int Slot3Offset { get; init; }
    public required int Slot4Offset { get; init; }
    public required int Slot5Offset { get; init; }
    public required int Slot6Offset { get; init; }
    public required int SpeciesOffset { get; init; }
    public required int ItemsOffset { get; init; }
    public required int TMHMDescriptionOffset { get; init; }
    public required int MonIconTableOffset { get; init; }
    public required byte ItemDataSize { get; init; }
    public required byte ItemNameSize { get; init; }

    private static readonly RomProfile English = new() {
      Name = "English",
      Language = Language.English,
      PartyCountOffset = 0x244E9,
      Slot1Offset = 0x244EC,
      Slot2Offset = 0x24550,
      Slot3Offset = 0x245B4,
      Slot4Offset = 0x24618,
      Slot5Offset = 0x2467C,
      Slot6Offset = 0x246E0,
      SpeciesOffset = 0x3203E8,
      ItemsOffset = 0x5839CC,
      TMHMDescriptionOffset = 0x300698,
      MonIconTableOffset = 0x57BCA8,
      ItemDataSize = 44,
      ItemNameSize = 14,
    };

    // Korean revisions share party layout; only the ROM tables move.
    private static readonly RomProfile KoreanModern = new() {
      Name = "Korean 2024 Modern (0x34)",
      Language = Language.Korean,
      PartyCountOffset = 0x24649,
      Slot1Offset = 0x2464C,
      Slot2Offset = 0x246B0,
      Slot3Offset = 0x24714,
      Slot4Offset = 0x24778,
      Slot5Offset = 0x247DC,
      Slot6Offset = 0x24840,
      SpeciesOffset = 0x3041DC,
      ItemsOffset = 0x562E44,
      TMHMDescriptionOffset = 0x300698,
      MonIconTableOffset = 0x55B5A0,
      ItemDataSize = 48,
      ItemNameSize = 18,
    };

    private static readonly RomProfile Korean2024 = KoreanModern with {
      Name = "Korean 2024 (0x3C)",
      SpeciesOffset = 0x3041E4,
      ItemsOffset = 0x562E4C,
      TMHMDescriptionOffset = 0x3006A0,
      MonIconTableOffset = 0x55B5A8,
    };

    private static readonly RomProfile Korean260613 = KoreanModern with {
      Name = "Korean 260613 (0x78)",
      SpeciesOffset = 0x301D20,
      ItemsOffset = 0x560988,
      TMHMDescriptionOffset = 0x2FE1DC,
      MonIconTableOffset = 0x5590E4,
    };

    private static readonly RomProfile Japanese = new() {
      Name = "Japanese (0x1C)",
      Language = Language.Japan,
      PartyCountOffset = 0x2418D,
      Slot1Offset = 0x24190,
      Slot2Offset = 0x241F4,
      Slot3Offset = 0x24258,
      Slot4Offset = 0x242BC,
      Slot5Offset = 0x24320,
      Slot6Offset = 0x24384,
      SpeciesOffset = 0x2F0D70,
      ItemsOffset = 0x55CF18,
      TMHMDescriptionOffset = 0x2ED22C,
      MonIconTableOffset = 0x556804,
      ItemDataSize = 48,
      ItemNameSize = 18,
    };

    public static RomProfile? FromMarker(byte marker) => marker switch {
      0x8C => English,
      0x34 => KoreanModern,
      0x3C => Korean2024,
      0x78 => Korean260613,
      0x1C => Japanese,
      _ => null,
    };

    public int GetSlotOffset(byte slotNumber) => slotNumber switch {
      1 => Slot1Offset,
      2 => Slot2Offset,
      3 => Slot3Offset,
      4 => Slot4Offset,
      5 => Slot5Offset,
      6 => Slot6Offset,
      _ => throw new ArgumentOutOfRangeException(nameof(slotNumber), "Slot must be between 1 and 6."),
    };
  }
}
