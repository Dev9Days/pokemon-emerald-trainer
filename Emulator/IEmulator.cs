using System.Diagnostics;

namespace PokemonGen3Hack.Emulator {
  public enum EmulatorType {
    VBA_M,
    MGBA,
    DeSmuME,
    MelonDS,
    Citra,
    Unknown
  }
  public enum Language {
    English,
    Korean,
    Japan,
    Unknown
  }
  internal interface IEmulator {

    public Process? Process { get; set; }
    public nint ProcessID { get; }
    public nint PAK { get; }
    public nint EWRAM { get; }
    public nint IWRAM { get; }
    public int BlockSize { get; }
    public EmulatorType Type { get; }
    public Language Lang { get; }

    public void SetProcess(Process p);
    internal static EmulatorType GetEmulatorType(string processName) {
      processName = processName.ToLower();
      return processName switch {
        "visualboyadvance" or "visualboyadvance-m" or "vba-m" => EmulatorType.VBA_M,
        "mgba" => EmulatorType.MGBA,
        "desmume" => EmulatorType.DeSmuME,
        "melonds" => EmulatorType.MelonDS,
        "citra" => EmulatorType.Citra,
        _ => EmulatorType.Unknown,
      };
    }
    internal static int GetBlockSize(EmulatorType type) {
      return type switch {
        EmulatorType.VBA_M => 0x40000,
        EmulatorType.MGBA => 0x20000,
        _ => 0x20000,
      };
    }
    internal static Language GetLanguage(byte b) {
      return b switch {
        0x8C => Language.English,
        0x34 => Language.Korean, // 24 한글(모던)
        0x3C => Language.Korean, // 24 한글
        0x78 => Language.Korean, // 26 한글
        0x1C => Language.Japan,
        _ => Language.Unknown,
      };
    }
    byte[] ReadPAK(int offset = 0, int count = 0);
    byte[] ReadEWRAM(int offset = 0, int count = 0);
    byte[] ReadIWRAM(int offset = 0, int count = 0);
  }
}
