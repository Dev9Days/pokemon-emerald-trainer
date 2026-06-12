namespace PokemonGen3Hack.Utils {
  /// <summary>
  /// 전역 로깅 시스템
  /// </summary>
  public static class Logger {
    private static readonly List<string> _logs = [];
    private static readonly Lock _lock = new();
    public enum LogLevel {
      Debug,
      Info,
      Warning,
      Error
    }
    public static event Action<string, LogLevel>? OnLog;
    public static void Debug(string message) => Log(message, LogLevel.Debug);
    public static void Info(string message) => Log(message, LogLevel.Info);
    public static void Warning(string message) => Log(message, LogLevel.Warning);
    public static void Error(string message) => Log(message, LogLevel.Error);
    public static void Error(Exception ex, string message) => Log($"{message}: {ex.Message}", LogLevel.Error);
    private static void Log(string message, LogLevel level) {
      string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
      string logMessage = $"[{timestamp}] [{level}] {message}";
      lock (_lock) {
        _logs.Add(logMessage);
        // 최대 1000개의 로그만 유지
        if (_logs.Count > 1000) {
          _logs.RemoveAt(0);
        }
      }
      OnLog?.Invoke(logMessage, level);
      // 디버그 출력 (개발 중에만)
      System.Diagnostics.Debug.WriteLine(logMessage);
    }
    public static List<string> GetLogs() {
      lock (_lock) {
        return [.. _logs];
      }
    }
    public static void Clear() {
      lock (_lock) {
        _logs.Clear();
      }
    }
  }
}