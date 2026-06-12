using PokemonGen3Hack.Utils;
using System.Diagnostics;

namespace PokemonGen3Hack.Emulator {
  public class Emulator : IEmulator {
    private readonly Memory scanner = Memory.Instance;

    public Process? Process { get; set; }
    private nint processID;
    public nint ProcessID { get => processID; }
    private nint pak;
    public nint PAK { get => pak; }
    private nint ewram;
    public nint EWRAM { get => ewram; }
    private nint iwram;
    public nint IWRAM { get => iwram; }
    private int blockSize;
    public int BlockSize { get => blockSize; }
    private EmulatorType type;
    public EmulatorType Type { get => type; }
    private Language lang;
    public Language Lang { get => lang; }
    private bool isWorking;
    public bool IsWorking { get => isWorking; }

    public Emulator() {
      Process = null;
    }

    public void SetProcess(Process p) {
      if (p.MainModule == null) return;
      Process = p;
      processID = p.Id;

      type = IEmulator.GetEmulatorType(p.ProcessName);
      blockSize = IEmulator.GetBlockSize(type);

      Stopwatch sw = new();
      sw.Start();
      try {
        GetData();
        ValidateResolvedAddresses();
        lang = GetLanguage();
        if (lang == Language.Unknown) {
          throw new InvalidOperationException($"지원하지 않는 ROM 언어이거나 ROM 영역을 찾지 못했습니다. PAK=0x{PAK:X}, EWRAM=0x{EWRAM:X}, IWRAM=0x{IWRAM:X}");
        }
        Logger.Info($"Found Lang: {Lang}");
      } finally {
        isWorking = false;
        sw.Stop();
        Logger.Info($"Memory read in {sw.ElapsedMilliseconds} ms");
      }
    }

    private void GetData() {
      if (Process == null || Process.MainModule == null) return;
      isWorking = true;
      int p = ProcessID.ToInt32();
      nint baseAddress = Process.MainModule.BaseAddress;
      ResetMemoryAddresses();
      bool resolved = TryResolveKnownPointers(p, baseAddress);
      if (resolved) {
        lang = GetLanguage();
        if (lang == Language.Unknown) {
          Logger.Warning("Known pointer resolved, but ROM language check failed. Falling back to AOB scan...");
          ResetMemoryAddresses();
          resolved = false;
        }
      }
      if (!resolved) {
        ResolveByAobScan(p);
      }
      Logger.Info($"Found PAK: {PAK:X}");
      Logger.Info($"Found EWRAM: {EWRAM:X}");
      Logger.Info($"Found IWRAM: {IWRAM:X}");
    }

    private void ValidateResolvedAddresses() {
      if (PAK == nint.Zero || EWRAM == nint.Zero || IWRAM == nint.Zero) {
        throw new InvalidOperationException($"에뮬레이터 메모리 주소를 찾지 못했습니다. PAK=0x{PAK:X}, EWRAM=0x{EWRAM:X}, IWRAM=0x{IWRAM:X}");
      }
    }

    private void ResetMemoryAddresses() {
      pak = nint.Zero;
      ewram = nint.Zero;
      iwram = nint.Zero;
    }

    private bool TryResolveKnownPointers(int processId, nint baseAddress) {
      try {
        switch (Process?.ProcessName.ToLower()) {
          case "mgba": // v0.10.5
            byte[] baseOffset = scanner.ReadMemory(processId, baseAddress + 0x27389C8, 8);
            byte[] offset1 = scanner.ReadMemory(processId, (nint)BitConverter.ToInt64(baseOffset) + 0x20, 8);
            byte[] offset2 = scanner.ReadMemory(processId, (nint)BitConverter.ToInt64(offset1) + 0x58, 8);
            byte[] offset3 = scanner.ReadMemory(processId, (nint)BitConverter.ToInt64(offset2) + 0x8, 8);
            byte[] offsetPak = scanner.ReadMemory(processId, (nint)BitConverter.ToInt64(offset3) + 0x38, 8);
            byte[] offsetEWRAM = scanner.ReadMemory(processId, (nint)BitConverter.ToInt64(offset3) + 0x28, 8);
            byte[] offsetIWRAM = scanner.ReadMemory(processId, (nint)BitConverter.ToInt64(offset3) + 0x30, 8);

            pak = (nint)BitConverter.ToInt64(offsetPak);
            ewram = (nint)BitConverter.ToInt64(offsetEWRAM);
            iwram = (nint)BitConverter.ToInt64(offsetIWRAM);
            break;
          case "visualboyadvance": // v1.7.2
            offsetPak = scanner.ReadMemory(processId, baseAddress + 0x173BF8, 4);
            offsetEWRAM = scanner.ReadMemory(processId, baseAddress + 0x173BF0, 4);
            offsetIWRAM = scanner.ReadMemory(processId, baseAddress + 0x173BF4, 4);

            pak = BitConverter.ToInt32(offsetPak);
            ewram = BitConverter.ToInt32(offsetEWRAM);
            iwram = BitConverter.ToInt32(offsetIWRAM);
            break;
          case "visualboyadvance-m":
          case "vba-m": // v2.2.2
            offsetPak = scanner.ReadMemory(processId, baseAddress + 0x3FF51F0, 8);
            offsetEWRAM = scanner.ReadMemory(processId, baseAddress + 0x3FF50D0, 8);
            offsetIWRAM = scanner.ReadMemory(processId, baseAddress + 0x3FF5100, 8);

            pak = (nint)BitConverter.ToInt64(offsetPak);
            ewram = (nint)BitConverter.ToInt64(offsetEWRAM);
            iwram = (nint)BitConverter.ToInt64(offsetIWRAM);
            break;
          default:
            return false;
        }

        bool resolved = PAK != nint.Zero && EWRAM != nint.Zero && IWRAM != nint.Zero;
        if (!resolved) {
          Logger.Warning("Known pointer resolution returned empty addresses.");
        }
        return resolved;
      } catch (Exception ex) {
        Logger.Error(ex, "Known pointer resolution failed");
        return false;
      }
    }

    private void ResolveByAobScan(int processId) {
      Logger.Info("Starting AOB Scan...");
      string PakPattern = "50 4F 4B 45 4D 4F 4E 20";
      // 캐릭터가 보일만한 화면에서 감지 됨. 어느 고정된 패턴을 찾아서 변경하면 좋을 듯
      string EWRAMPattern = "01 00 A3 A3 00 08 00 00 00 00 00 02 10";
      string IWRAMPattern = "C0 0F 00 00 00 00 00 02 00 C0";

      var addrs = scanner.AOBScan(processId, [PakPattern, EWRAMPattern, IWRAMPattern], BlockSize);
      if (addrs.Count > 0) {
        foreach (var (pattern, addr) in addrs) {
          if (pattern.Equals(PakPattern)) {
            pak = addr - 0xA0;
          } else if (pattern.Equals(EWRAMPattern)) {
            ewram = addr;
          } else if (pattern.Equals(IWRAMPattern)) {
            iwram = addr;
          }
        }
      }
      Logger.Info("AOB Scan Complete.");
    }
    private Language GetLanguage() {
      byte[] res = ReadPAK(0x128, 1);
      Logger.Debug($"Lang(0x128): {res[0]:X}");
      return IEmulator.GetLanguage(res[0]);
    }
    public byte[] ReadPAK(int offset = 0, int count = 0) => scanner.ReadMemory(ProcessID.ToInt32(), PAK + offset, count);
    public byte[] ReadEWRAM(int offset = 0, int count = 0) => scanner.ReadMemory(ProcessID.ToInt32(), EWRAM + offset, count);
    public byte[] ReadIWRAM(int offset = 0, int count = 0) => scanner.ReadMemory(ProcessID.ToInt32(), IWRAM + offset, count);
    public bool WriteEWRAM(int offset, byte[] data) => scanner.WriteMemory(ProcessID.ToInt32(), EWRAM + offset, data);

  }
}
