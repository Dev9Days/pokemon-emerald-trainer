using PokemonGen3Hack.Emulator;

int assertions = 0;
void Equal<T>(T expected, T actual, string name) {
  assertions++;
  if (!EqualityComparer<T>.Default.Equals(expected, actual)) {
    throw new InvalidOperationException($"{name}: expected {expected}, got {actual}");
  }
}

void Check(byte marker, Language language, int partyCount, int[] slots,
    int species, int items, int tmhm, byte itemSize, byte nameSize, int iconTable) {
  var profile = RomProfile.FromMarker(marker)
    ?? throw new InvalidOperationException($"Missing profile for 0x{marker:X2}");
  Equal(language, profile.Language, $"0x{marker:X2} language");
  Equal(language, IEmulator.GetLanguage(marker), $"0x{marker:X2} language detection");
  Equal(partyCount, profile.PartyCountOffset, "Party count");
  for (byte i = 1; i <= 6; i++) {
    Equal(slots[i - 1], profile.GetSlotOffset(i), $"Slot {i}");
  }
  Equal(species, profile.SpeciesOffset, "Species");
  Equal(items, profile.ItemsOffset, "Items");
  Equal(tmhm, profile.TMHMDescriptionOffset, "TMHM description");
  Equal(iconTable, profile.MonIconTableOffset, "Mon icon table offset");
  Equal(itemSize, profile.ItemDataSize, "Item data size");
  Equal(nameSize, profile.ItemNameSize, "Item name size");
  foreach (byte slot in new byte[] { 0, 7, 255 }) {
    bool rejected = false;
    try { profile.GetSlotOffset(slot); }
    catch (ArgumentOutOfRangeException) { rejected = true; }
    Equal(true, rejected, $"Invalid slot {slot}");
  }
}

int[] koreanSlots = [0x2464C, 0x246B0, 0x24714, 0x24778, 0x247DC, 0x24840];
Check(0x34, Language.Korean, 0x24649, koreanSlots, 0x3041DC, 0x562E44, 0x300698, 48, 18, 0x55B5A0);
Check(0x78, Language.Korean, 0x24649, koreanSlots, 0x301D20, 0x560988, 0x2FE1DC, 48, 18, 0x5590E4);
Check(0x3C, Language.Korean, 0x24649, koreanSlots, 0x3041E4, 0x562E4C, 0x3006A0, 48, 18, 0x55B5A8);
Check(0x8C, Language.English, 0x244E9,
  [0x244EC, 0x24550, 0x245B4, 0x24618, 0x2467C, 0x246E0],
  0x3203E8, 0x5839CC, 0x300698, 44, 14, 0x57BCA8);
Check(0x1C, Language.Japan, 0x2418D,
  [0x24190, 0x241F4, 0x24258, 0x242BC, 0x24320, 0x24384],
  0x2F0D70, 0x55CF18, 0x2ED22C, 48, 18, 0x556804);

for (int marker = 0; marker <= byte.MaxValue; marker++) {
  bool supported = marker is 0x34 or 0x78 or 0x3C or 0x8C or 0x1C;
  Equal(supported, RomProfile.FromMarker((byte)marker) != null, $"Marker 0x{marker:X2}");
  if (!supported) {
    Equal(Language.Unknown,
      IEmulator.GetLanguage((byte)marker), $"Unsupported language marker 0x{marker:X2}");
  }
}

// Optional local ROM paths verify the marker-to-profile mapping without shipping ROM data.
foreach (string path in args) {
  using var stream = File.OpenRead(path);
  stream.Position = 0x128;
  int marker = stream.ReadByte();
  if (marker < 0) throw new InvalidDataException($"ROM is too short: {path}");
  var profile = RomProfile.FromMarker((byte)marker)
    ?? throw new InvalidOperationException($"Unsupported ROM marker: 0x{marker:X2}");
  Console.WriteLine($"{Path.GetFileName(path)} -> {profile.Name}");
  RomIconTests.CheckRom(path, profile);
}

Console.WriteLine($"PASS: {assertions} ROM profile assertions.");
RomIconTests.Run();
