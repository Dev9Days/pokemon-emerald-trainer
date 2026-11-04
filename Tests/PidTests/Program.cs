using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using PokemonGen3Hack.Components.ViewModel;
using PokemonGen3Hack.Emulator;
using PokemonGen3Hack.Pokemon;
using PokemonGen3Hack.Utils;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
int checks = 0;
void Check(bool condition, string name) {
  checks++;
  if (!condition) throw new InvalidOperationException(name);
}

var fixture = MakeFixture(0x12345678);
var original = ReadInfo(fixture.Raw);
Check(original.GetPokemonData().SequenceEqual(fixture.Raw), "Lossless initial round trip");
Check(!original.Personality.IsShiny, "Fixture starts non-shiny");
byte[] before = original.GetPokemonData();
var ivs = original.Data.IVs;
var ivSnapshot = new Dictionary<string, byte>(ivs);
Check(original.CanGenerateMethod1PID(original.Personality.Nature, original.Personality.Gender, true, ivs), "Shiny PID exists with fixed IVs");
Check(original.GetPokemonData().SequenceEqual(before), "Preview does not mutate data");
Check(original.RegeneratePID(original.Personality.Nature, original.Personality.Gender, true, ivs), "Shiny PID applied");
Check(original.Personality.IsShiny, "Shiny flag recomputed from actual PID");
Check(ivs.All(p => original.Data.IVs[p.Key] == p.Value), "IV and ability bits unchanged");
Check(ivs.All(p => ivSnapshot[p.Key] == p.Value), "Input IV dictionary unchanged");
byte[] serialized = original.GetPokemonData();
Check(serialized.AsSpan(4, 28).SequenceEqual(before.AsSpan(4, 28)), "Trainer IDs, names, checksum and header preserved");
Check(serialized.AsSpan(80).SequenceEqual(before.AsSpan(80)), "Status preserved");
Check(Decrypt(serialized).SequenceEqual(Decrypt(before)), "Only PID/encryption changed, not plaintext data");
Check(IsMethod1(original.Personality.PID, ivs), "Actual Method 1 correlation after update");

for (uint seed = 1; seed <= 24; seed++) {
  var f = MakeFixture(seed * 1234567);
  var info = ReadInfo(f.Raw);
  var target = new Personality(Hex.GetBytes(f.TargetPid), GenderGroup.M7F1, info.TID, info.SID);
  Check(info.RegeneratePID(target.Nature, target.Gender, true, info.Data.IVs), "Known Method 1 seed recovered");
  Check(info.Personality.Nature == target.Nature && info.Personality.Gender == target.Gender
    && info.Personality.IsShiny && info.Personality.Ability == target.Ability, "Only requested personality traits changed");
  Check(Decrypt(info.GetPokemonData()).SequenceEqual(Decrypt(f.Raw)), "Plaintext preserved across PID permutations");
}

var rejected = ReadInfo(fixture.Raw);
Nature impossibleNature = Enumerable.Range(0, 25).Select(n => (Nature)n)
  .First(n => !rejected.CanGenerateMethod1PID(n, rejected.Personality.Gender, true, rejected.Data.IVs));
Check(!rejected.RegeneratePID(impossibleNature, rejected.Personality.Gender, true, rejected.Data.IVs), "Impossible combination rejected");
Check(rejected.GetPokemonData().SequenceEqual(fixture.Raw), "Failed PID update is lossless");
var invalidIvs = new Dictionary<string, byte>(ivs) { ["HP"] = 32 };
Check(!rejected.RegeneratePID(rejected.Personality.Nature, rejected.Personality.Gender, true, invalidIvs), "Out-of-range IV rejected, not masked");
invalidIvs.Remove("HP");
Check(!rejected.CanGenerateMethod1PID(rejected.Personality.Nature, rejected.Personality.Gender, true, invalidIvs), "Missing IV rejected, not defaulted");
Check(rejected.GetPokemonData().SequenceEqual(fixture.Raw), "Invalid inputs do not mutate data");
foreach (var group in new[] { GenderGroup.Male, GenderGroup.Female, GenderGroup.Unknown }) {
  var fixedGender = new Personality(Hex.GetBytes(fixture.TargetPid), group, original.TID, original.SID);
  Gender invalidGender = group == GenderGroup.Female ? Gender.Male : Gender.Female;
  Check(!fixedGender.TryUpdatePersonality(fixedGender.Nature, invalidGender, true, ivs), "Fixed-gender species cannot change sex");
}

// Exercise the production write path against memory allocated in this test process only.
using (var memory = new MemoryFixture(fixture.Raw)) {
  var vm = memory.ViewModel;
  Check(vm.ApplyChanges().Length == 0, "No-op save succeeds");
  Check(memory.ReadSlot().SequenceEqual(fixture.Raw), "No-op save preserves every byte");
}
using (var memory = new MemoryFixture(fixture.Raw)) {
  var vm = memory.ViewModel;
  vm.Pokemon.IsShiny = true;
  Check(vm.GetPendingPIDAssessment()?.Status == PidMatchStatus.Match, "UI preview finds compatible Shiny PID");
  Check(vm.ApplyChanges().Length == 0, "Shiny-only save succeeds");
  byte[] saved = memory.ReadSlot();
  var savedInfo = ReadInfo(saved);
  Check(savedInfo.Personality.IsShiny, "Shiny written to memory");
  Check(saved.AsSpan(4, 28).SequenceEqual(fixture.Raw.AsSpan(4, 28)), "Shiny-only write preserves header");
  Check(saved.AsSpan(80).SequenceEqual(fixture.Raw.AsSpan(80)), "Shiny-only write does not heal HP or change sleep turns");
  Check(Decrypt(saved).SequenceEqual(Decrypt(fixture.Raw)), "Shiny-only write preserves IVs, ability, Pokerus, ribbon ranks and all other data");
  Check(vm.Pokemon.PID == Hex.ToUInt(savedInfo.Personality.PID).ToString("X8"), "UI PID updated after successful write");
  Check(!vm.NeedsNewPID(), "Successful changes become the new comparison baseline");
}
using (var memory = new MemoryFixture(fixture.Raw)) {
  var vm = memory.ViewModel;
  var source = vm.PokemonInfo[0];
  vm.Pokemon.IsShiny = true;
  vm.Pokemon.Nature = impossibleNature;
  vm.Pokemon.Nickname = "NEW";
  Check(vm.GetPendingPIDAssessment()?.Status == PidMatchStatus.NoMatch, "UI preview warns about uncorrelated PID");
  Check(vm.ApplyChanges().Length > 0, "Impossible save rejected");
  Check(ReferenceEquals(source, vm.PokemonInfo[0]), "Failed search retains original object");
  Check(source.GetPokemonData().SequenceEqual(fixture.Raw), "Failed search preserves all local data");
  Check(memory.ReadSlot().SequenceEqual(fixture.Raw), "Failed search does not write memory");
}
using (var memory = new MemoryFixture(fixture.Raw)) {
  var vm = memory.ViewModel;
  var source = vm.PokemonInfo[0];
  vm.Pokemon.IsShiny = true;
  memory.FailWrites();
  Check(vm.ApplyChanges().Length > 0, "Write failure reported");
  Check(ReferenceEquals(source, vm.PokemonInfo[0]), "Failed write retains original object");
  Check(source.GetPokemonData().SequenceEqual(fixture.Raw), "Failed write does not commit local changes");
  Check(memory.ReadSlot().SequenceEqual(fixture.Raw), "Failed write preserves test memory");
}
using (var memory = new MemoryFixture(fixture.Raw)) {
  var vm = memory.ViewModel;
  vm.Pokemon.Nature = impossibleNature;
  vm.Pokemon.IsShiny = true;
  Gender expectedGender = vm.Pokemon.Gender;
  byte expectedAbility = vm.PokemonInfo[0].Personality.Ability;
  Check(vm.GetPendingPIDAssessment()?.Status == PidMatchStatus.NoMatch, "Fallback path starts with no encounter-compatible match");
  Check(vm.ApplyChanges(allowUncorrelatedPid: true).Length == 0, "Confirmed uncorrelated PID can be saved");
  byte[] saved = memory.ReadSlot();
  var savedInfo = ReadInfo(saved);
  Check(savedInfo.Personality.Nature == impossibleNature && savedInfo.Personality.IsShiny,
    "Confirmed fallback applies requested nature and Shiny");
  Check(savedInfo.Personality.Gender == expectedGender && savedInfo.Personality.Ability == expectedAbility,
    "Confirmed fallback preserves gender and ability");
  Check(saved.AsSpan(4, 28).SequenceEqual(fixture.Raw.AsSpan(4, 28)), "Confirmed fallback preserves IDs and header");
  Check(saved.AsSpan(80).SequenceEqual(fixture.Raw.AsSpan(80)), "Confirmed fallback preserves HP and status");
  Check(Decrypt(saved).SequenceEqual(Decrypt(fixture.Raw)), "Confirmed fallback preserves all IVs and plaintext fields");
  Check(!IsMethod1(savedInfo.Personality.PID, savedInfo.Data.IVs), "Fallback does not falsely claim Method 1 correlation");
  vm.Pokemon.IsShiny = false;
  Check(vm.ApplyChanges(allowUncorrelatedPid: true).Length == 0, "Shiny can also be switched off");
  Check(!ReadInfo(memory.ReadSlot()).Personality.IsShiny, "Non-shiny generation excludes shiny PID");
  Check(Decrypt(memory.ReadSlot()).SequenceEqual(Decrypt(fixture.Raw)), "Shiny off still preserves IVs and data");
}
var impossibleGender = new Personality(Hex.GetBytes(fixture.TargetPid), GenderGroup.Male, original.TID, original.SID);
byte[] originalPid = impossibleGender.PID.ToArray();
Check(!impossibleGender.TryUpdatePersonality(impossibleGender.Nature, Gender.Female, true, ivs, allowUncorrelatedPid: true),
  "Fallback handles impossible species gender without an infinite loop");
Check(impossibleGender.PID.SequenceEqual(originalPid), "Failed fallback does not mutate PID");
Check(!rejected.RegeneratePID(rejected.Personality.Nature, rejected.Personality.Gender, true, invalidIvs, allowUncorrelatedPid: true),
  "Fallback does not silently accept missing IV values");

int samples = 0, shinyMatches = 0;
for (uint seed = 1; seed <= 100; seed++) {
  var sample = MakeFixture(seed * 1234567);
  var actual = new Personality(Hex.GetBytes(sample.TargetPid), GenderGroup.M7F1, 21963, 55364);
  if (actual.IsShiny) continue;
  samples++;
  var sampleIvs = ReadInfo(sample.Raw).Data.IVs;
  Check(actual.CanGenerateMethod1PID(actual.Nature, actual.Gender, false, sampleIvs), "Sample starts with genuine Method 1 correlation");
  if (actual.CanGenerateMethod1PID(actual.Nature, actual.Gender, true, sampleIvs)) shinyMatches++;
}
Console.WriteLine($"Method 1 sample: {shinyMatches}/{samples} non-shiny Pokemon permit Shiny with the same IVs, nature, gender, ability and trainer IDs.");
EncounterPidTests.Run(Check);
Console.WriteLine($"PASS: {checks} PID and write-preservation assertions.");

static uint Next(uint seed) => unchecked(seed * 0x41C64E6D + 0x6073);
static PokemonInfo ReadInfo(byte[] raw) {
  var info = new PokemonInfo(raw);
  info.SetPersonality(GenderGroup.M7F1);
  return info;
}
static (byte[] Raw, uint TargetPid) MakeFixture(uint seed) {
  uint s1 = Next(seed), s2 = Next(s1), s3 = Next(s2), s4 = Next(s3);
  uint targetPid = (s2 & 0xFFFF0000) | (s1 >> 16);
  const ushort tid = 21963;
  ushort sid = (ushort)(tid ^ (ushort)targetPid ^ (ushort)(targetPid >> 16));
  uint sourcePid = targetPid;
  for (uint step = 1; ; step++) {
    sourcePid = unchecked(targetPid + 50 * step);
    if (sourcePid % 25 != targetPid % 25) continue;
    if (((sourcePid & 255) < 31) != ((targetPid & 255) < 31)) continue;
    if ((tid ^ sid ^ (ushort)sourcePid ^ (ushort)(sourcePid >> 16)) >= 8) break;
  }
  byte[] plain = new byte[48];
  BinaryPrimitives.WriteUInt16LittleEndian(plain, 277);
  BinaryPrimitives.WriteUInt16LittleEndian(plain.AsSpan(2), 13);
  BinaryPrimitives.WriteUInt32LittleEndian(plain.AsSpan(4), 1000);
  plain[9] = 100; plain[10] = 0xA5; plain[11] = 0x5A;
  for (int i = 0; i < 4; i++) {
    BinaryPrimitives.WriteUInt16LittleEndian(plain.AsSpan(12 + i * 2), (ushort)(i + 1));
    plain[20 + i] = 10;
  }
  for (int i = 0; i < 12; i++) plain[24 + i] = (byte)(i + 1);
  plain[36] = 0xA4;
  plain[37] = 16;
  BinaryPrimitives.WriteUInt16LittleEndian(plain.AsSpan(38), (4 << 11) | (3 << 7) | 5);
  uint iv32 = ((s3 >> 16) & 0x7FFF) | (((s4 >> 16) & 0x7FFF) << 15) | ((targetPid & 1) << 31);
  BinaryPrimitives.WriteUInt32LittleEndian(plain.AsSpan(40), iv32);
  BinaryPrimitives.WriteUInt32LittleEndian(plain.AsSpan(44), 0x40010004);
  byte[] raw = new byte[100];
  BinaryPrimitives.WriteUInt32LittleEndian(raw, sourcePid);
  BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(4), tid);
  BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(6), sid);
  raw[8] = 0xBB; raw[9] = 0xFF; raw[10] = 0xB7;
  raw[18] = 2; raw[19] = 2;
  raw[20] = 0xBC; raw[21] = 0xFF; raw[22] = 0xBA;
  raw[27] = 0xA5; raw[30] = 0xAA; raw[31] = 0x55;
  uint checksum = 0;
  for (int i = 0; i < 48; i += 2) checksum += BinaryPrimitives.ReadUInt16LittleEndian(plain.AsSpan(i));
  BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(28), (ushort)checksum);
  string order = Orders()[sourcePid % 24];
  uint key = sourcePid ^ ((uint)sid << 16) ^ tid;
  for (int block = 0; block < 4; block++) {
    int from = "GAEM".IndexOf(order[block]) * 12;
    for (int i = 0; i < 12; i += 4) {
      uint word = BinaryPrimitives.ReadUInt32LittleEndian(plain.AsSpan(from + i)) ^ key;
      BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(32 + block * 12 + i), word);
    }
  }
  raw[80] = 5; raw[81] = 0xD1; raw[82] = 0xE2; raw[83] = 0xF3;
  raw[84] = 5; raw[85] = 0xFF;
  BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(86), 7);
  for (int i = 0; i < 6; i++) BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(88 + i * 2), (ushort)(30 + i));
  return (raw, targetPid);
}
static string[] Orders() => ["GAEM", "GAME", "GEAM", "GEMA", "GMAE", "GMEA", "AGEM", "AGME", "AEGM", "AEMG", "AMGE", "AMEG",
  "EGAM", "EGMA", "EAGM", "EAMG", "EMGA", "EMAG", "MGAE", "MGEA", "MAGE", "MAEG", "MEGA", "MEAG"];
static byte[] Decrypt(byte[] raw) {
  uint pid = BinaryPrimitives.ReadUInt32LittleEndian(raw);
  uint key = pid ^ BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(4));
  string order = Orders()[pid % 24];
  byte[] plain = new byte[48];
  for (int block = 0; block < 4; block++) {
    int to = "GAEM".IndexOf(order[block]) * 12;
    for (int i = 0; i < 12; i += 4) {
      uint word = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(32 + block * 12 + i)) ^ key;
      BinaryPrimitives.WriteUInt32LittleEndian(plain.AsSpan(to + i), word);
    }
  }
  return plain;
}
static bool IsMethod1(byte[] pidBytes, Dictionary<string, byte> ivs) {
  uint pid = Hex.ToUInt(pidBytes);
  uint iv1 = (uint)(ivs["HP"] | ivs["Attack"] << 5 | ivs["Defense"] << 10);
  uint iv2 = (uint)(ivs["Speed"] | ivs["SpecialAttack"] << 5 | ivs["SpecialDefense"] << 10);
  for (uint low = 0; low <= 65535; low++) {
    uint s1 = (pid << 16) | low, s2 = Next(s1);
    if ((s2 & 0xFFFF0000) != (pid & 0xFFFF0000)) continue;
    uint s3 = Next(s2), s4 = Next(s3);
    if (((s3 >> 16) & 0x7FFF) == iv1 && ((s4 >> 16) & 0x7FFF) == iv2) return true;
  }
  return false;
}

sealed class MemoryFixture : IDisposable {
  private readonly nint buffer = Marshal.AllocHGlobal(0x600000);
  private readonly PokemonGen3Hack.Emulator.Emulator emulator;
  private readonly RomProfile profile = RomProfile.FromMarker(0x8C)!;
  public PokemonMainViewModel ViewModel { get; } = new();
  public MemoryFixture(byte[] raw) {
    Marshal.Copy(new byte[0x600000], 0, buffer, 0x600000);
    Marshal.Copy(raw, 0, buffer + profile.Slot1Offset, raw.Length);
    Marshal.WriteByte(buffer + profile.PartyCountOffset, 1);
    ushort species = new PokemonInfo(raw).PID;
    byte ratio = PKHeX.Core.PersonalTable.E[PKHeX.Core.SpeciesConverter.GetNational3(species)].Gender;
    Marshal.WriteByte(buffer + profile.SpeciesOffset + (species - 1) * 28 + 16, ratio);
    emulator = (PokemonGen3Hack.Emulator.Emulator)typeof(PokemonMainViewModel)
      .GetField("emul", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(ViewModel)!;
    emulator.Process = Process.GetCurrentProcess();
    Set("processID", (nint)Environment.ProcessId);
    Set("pak", buffer); Set("ewram", buffer); Set("iwram", buffer);
    Set("lang", Language.English);
    typeof(PokemonGen3Hack.Emulator.Emulator).GetProperty("Profile", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(emulator, profile);
    var info = new PokemonInfo(raw);
    info.SetPersonality((GenderGroup)ratio);
    ViewModel.PokemonInfo = [info];
    ViewModel.PartyCount = 1;
    ViewModel.SelectPartyMember(0);
  }
  private void Set(string name, object value) => typeof(PokemonGen3Hack.Emulator.Emulator)
    .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(emulator, value);
  public void FailWrites() => Set("processID", (nint)(-1));
  public byte[] ReadSlot() {
    byte[] raw = new byte[100];
    Marshal.Copy(buffer + profile.Slot1Offset, raw, 0, raw.Length);
    return raw;
  }
  public void Dispose() {
    emulator.Process?.Dispose();
    Marshal.FreeHGlobal(buffer);
  }
}
