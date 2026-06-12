using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
namespace PokemonGen3Hack.Utils {
  internal class Memory {
    private static readonly Memory _instance = new();
    public static Memory Instance => _instance;
    private Memory() { }
    // ---------------------------------------------
    // 1. Windows API P/Invoke 정의
    // ---------------------------------------------
    private static readonly uint PAGE_NOACCESS = 0x01;
    private static readonly uint PAGE_READONLY = 0x02;
    private static readonly uint PAGE_READWRITE = 0x04;
    private static readonly uint PAGE_WRITECOPY = 0x08;
    private static readonly uint PAGE_EXECUTE = 0x10;
    private static readonly uint PAGE_EXECUTE_READ = 0x20;
    private static readonly uint PAGE_EXECUTE_READWRITE = 0x40;
    private static readonly uint PAGE_EXECUTE_WRITECOPY = 0x80;
    private static readonly uint PAGE_GUARD = 0x100;
    private static readonly uint PAGE_NOCACHE = 0x200;
    private static readonly uint PAGE_WRITECOMBINE = 0x400;
    private static readonly uint MEM_COMMIT = 0x1000;
    private static readonly uint MEM_RESERVE = 0x2000;
    private static readonly uint MEM_FREE = 0x10000;
    private static readonly uint MEM_IMAGE = 0x1000000;
    private static readonly uint MEM_MAPPED = 0x40000;
    private static readonly uint MEM_PRIVATE = 0x20000;
    [Flags]
    public enum ProcessAccessFlags : uint {
      QueryInformation = 0x0400,
      Read = 0x0010,
      VMRead = 0x0010,
      VMOperation = 0x0008,
      VMWrite = 0x0020,
      All = 0x1F0FFF
    }
    // MEMORY_BASIC_INFORMATION 구조체 정의
    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORY_BASIC_INFORMATION {
      public nint BaseAddress;
      public nint AllocationBase;
      public uint AllocationProtect;
      public ushort PartitionId;
      public ulong RegionSize;
      public uint State;
      public uint Protect;
      public uint Type;
    }
    // OpenProcess 함수 선언
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);
    // VirtualQueryEx 함수 선언
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int VirtualQueryEx(nint hProcess, nint lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadProcessMemory(nint hProcess, nint lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WriteProcessMemory(nint hProcess, nint lpBaseAddress, byte[] lpBuffer, int nSize, out int lpNumberOfBytesWritten);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(nint hObject);
    public byte[] ReadMemory(int processId, nint address, int count) {
      //Console.WriteLine($"{address:X}");
      nint hProcess = nint.Zero;
      byte[] buffer = new byte[count];
      try {
        hProcess = OpenProcess((uint)(ProcessAccessFlags.VMRead | ProcessAccessFlags.QueryInformation), false, processId);
        if (hProcess == nint.Zero)
          throw new Win32Exception(Marshal.GetLastWin32Error(), $"OpenProcess 실패: {Marshal.GetLastWin32Error()}");
        if (!ReadProcessMemory(hProcess, address, buffer, count, out int bytesRead)) {
          throw new Win32Exception(Marshal.GetLastWin32Error(), $"ReadProcessMemory 실패: {Marshal.GetLastWin32Error()}");
        }
        if (bytesRead < count) {
          throw new InvalidOperationException($"Partial read: {bytesRead}/{count} bytes at 0x{address:X}");
        }
        return buffer;
      } catch (Exception e) {
        Logger.Error(e, $"메모리 읽기 오류 발생: PID={processId}, Address=0x{address:X}, Count={count}");
        throw;
      } finally {
        if (hProcess != nint.Zero) {
          CloseHandle(hProcess);
        }
      }
    }
    public bool WriteMemory(int processId, nint address, byte[] data) {
      nint hProcess = nint.Zero;
      try {
        hProcess = OpenProcess((uint)(ProcessAccessFlags.VMWrite | ProcessAccessFlags.VMOperation), false, processId);
        if (hProcess == nint.Zero)
          throw new Win32Exception(Marshal.GetLastWin32Error());
        if (!WriteProcessMemory(hProcess, address, data, data.Length, out int bytesWritten)) {
          throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        if (bytesWritten < data.Length) {
          Logger.Warning($"Partial write: {bytesWritten}/{data.Length} bytes");
          return false;
        }
        return true;
      } catch (Exception e) {
        Logger.Error(e, "메모리 쓰기 오류");
        return false;
      } finally {
        if (hProcess != nint.Zero) {
          CloseHandle(hProcess);
        }
      }
    }
    public List<(nint Base, long Size)> CollectRegions(nint hProcess) {
      var list = new List<(nint, long)>();
      nint addr = nint.Zero;
      ulong maxAddr = 0x7FFF_FFFF_FFFF;
      while ((ulong)addr < maxAddr) {
        MEMORY_BASIC_INFORMATION mbi;
        int result = VirtualQueryEx(hProcess, addr, out mbi, (uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>());
        if (result == 0) {
          int err = Marshal.GetLastWin32Error();
          if (err == 87)
            Logger.Debug("메모리 영역 열거 완료");
          else if (err == 6) Logger.Warning("잘못된 핸들");
          else Logger.Warning($"VirtualQueryEx 실패, GetLastError = {err}");
          break;
        }
        // 안전하게 RegionSize를 long으로 처리
        long regionSize = (long)mbi.RegionSize;
        if (regionSize <= 0) {
          addr += 0x1000; // 최소 페이지 크기 이동
          continue;
        }
        if (IsReadableRegion(mbi)) {
          list.Add((mbi.BaseAddress, regionSize));
        }
        addr = mbi.BaseAddress + (nint)regionSize;
      }
      return list;
    }
    public bool IsReadableRegion(MEMORY_BASIC_INFORMATION mbi) {
      // 1) State check
      if (mbi.State != MEM_COMMIT)
        return false;
      // 2) Protection check
      uint prot = mbi.Protect;
      bool readable = prot == PAGE_READONLY || prot == PAGE_READWRITE || prot == PAGE_WRITECOPY || prot == PAGE_EXECUTE_READ || prot == PAGE_EXECUTE_READWRITE || prot == PAGE_EXECUTE_WRITECOPY;
      if (!readable)
        return false;
      // 3) Optional: skip PAGE_GUARD, NOACCESS for safety/perf
      if ((prot & PAGE_GUARD) != 0 || prot == PAGE_NOACCESS)
        return false;
      // 4) Optional: If scanning only code
      // return mbi.Type == MEM_IMAGE;
      return true;
    }
    public List<(string, nint)> AOBScan(int processId, string pattern, int blockSize = 0x20000) {
      return AOBScan(processId, [pattern], blockSize);
    }
    public List<(string, nint)> AOBScan(int processId, string[] patterns, int blockSize = 0x20000) {
      ConcurrentBag<(string, nint)> results = new();
      nint hProcess = nint.Zero;
      try {
        hProcess = OpenProcess((uint)(ProcessAccessFlags.Read | ProcessAccessFlags.QueryInformation), false, processId);
        if (hProcess == nint.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        // 모든 패턴 객체 생성
        AOBPattern[] aobs = patterns.Select(p => new AOBPattern(p)).ToArray();
        var regions = CollectRegions(hProcess);
        long totalBytes = regions.Sum(r => r.Size);
        Logger.Info($"총 스캔 영역 수: {regions.Count}, 총 바이트: {totalBytes / 1024.0 / 1024.0:F1} MB");
        Parallel.ForEach(regions, region => {
          ScanRegion(hProcess, region.Base, region.Size, aobs, blockSize, results);
        });
      } catch (Exception ex) {
        Logger.Error(ex, "AOB 스캔 중 오류 발생");
      } finally {
        if (hProcess != nint.Zero) {
          CloseHandle(hProcess); hProcess = nint.Zero;
        }
      }
      return results.ToList();
    }
    public void ScanRegion(nint hProcess, nint baseAddr, long regionSize, AOBPattern[] patterns, int blockSize, ConcurrentBag<(string, nint)> results) {
      int maxPatternLength = patterns.Max(p => p.Pattern.Length);
      int overlap = maxPatternLength - 1;
      byte[] buffer = new byte[blockSize + overlap];
      long offset = 0;
      while (offset < regionSize) {
        int mainRead = (int)Math.Min(blockSize, regionSize - offset);
        int totalRead = mainRead;
        if (offset + mainRead < regionSize)
          totalRead += overlap;
        if (ReadProcessMemory(hProcess, nint.Add(baseAddr, (int)offset), buffer, totalRead, out int bytesRead)) {
          // 모든 패턴에 대해 검사
          foreach (var pat in patterns) {
            int idx = FindPatternBMH(buffer, bytesRead, pat);
            if (idx != -1) {
              results.Add(new(pat.ToString(), nint.Add(baseAddr, (int)(offset + idx))));
            }
          }
        }
        offset += mainRead;
      }
    }
    public int FindPatternBMH(byte[] buffer, int length, AOBPattern p) {
      int m = p.Pattern.Length;
      int end = length - m;
      byte[] pat = p.Pattern;
      bool[] mask = p.Mask;
      int[] skip = p.SkipTable;
      int i = 0;
      while (i <= end) {
        int j = m - 1;
        while (j >= 0) {
          if (mask[j] && buffer[i + j] != pat[j])
            break;
          j--;
        }
        if (j < 0) return i;
        byte next = buffer[i + m - 1];
        i += skip[next];
      }
      return -1;
    }
    public class AOBPattern {
      public byte[] Pattern;
      public bool[] Mask;
      public int[] SkipTable;
      public AOBPattern(string pattern) {
        Parse(pattern, out Pattern, out Mask);
        SkipTable = BuildSkipTable(Pattern, Mask);
      }
      private void Parse(string pattern, out byte[] bytes, out bool[] mask) {
        var parts = pattern.Split(' ');
        bytes = new byte[parts.Length];
        mask = new bool[parts.Length];
        for (int i = 0; i < parts.Length; i++) {
          if (parts[i] == "??") {
            bytes[i] = 0;
            mask[i] = false; // false = wildcard
          } else {
            bytes[i] = Convert.ToByte(parts[i], 16);
            mask[i] = true;
          }
        }
      }
      private int[] BuildSkipTable(byte[] pattern, bool[] mask) {
        const int TABLE_SIZE = 256;
        int m = pattern.Length;
        int[] table = new int[TABLE_SIZE];
        for (int i = 0; i < TABLE_SIZE; i++)
          table[i] = m;
        for (int i = 0; i < m - 1; i++) {
          if (mask[i]) // wildcard는 스킵 테이블 만들지 않음
            table[pattern[i]] = m - 1 - i;
        }
        return table;
      }
      public override string ToString() {
        return Hex.BytesToString(Pattern);
      }
    }
  }
}
