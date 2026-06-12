namespace PokemonGen3Hack.Utils {
  internal class Hex {
    public static byte[] ReadBytes(string filename, int lineNumber, int startOffset, int count) {
      string[] lines = File.ReadAllLines(filename);

      if (lineNumber < 0 || lineNumber >= lines.Length)
        throw new IndexOutOfRangeException("라인 번호가 파일 범위를 벗어남");

      string[] tokens = lines[lineNumber].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

      if (startOffset < 0 || startOffset + count > tokens.Length)
        throw new IndexOutOfRangeException("요청한 바이트 범위가 라인의 길이를 벗어남");

      byte[] result = new byte[count];

      for (int i = 0; i < count; i++) {
        result[i] = Convert.ToByte(tokens[startOffset + i], 16);
      }

      return result;
    }
    public static string BytesToString(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", " ");
    public static byte[] HexStringToBytes(string hexString) {
      // 공백을 제거한 후, Span<byte>를 사용하여 한 번에 처리
      int byteCount = hexString.Length / 3 + (hexString.Length % 3 == 0 ? 0 : 1);  // 각 2자리마다 공백이 있기 때문에, byteCount를 계산
      byte[] byteArray = new byte[byteCount];

      Span<byte> span = new(byteArray);
      int byteIndex = 0;

      // 문자열을 순회하면서 직접 byte[]에 값을 넣는다
      for (int i = 0; i < hexString.Length; i += 3) { // 한 번에 2자리씩 변환 + 공백을 건너뛰기 위해 3칸씩 이동
        span[byteIndex++] = Convert.ToByte(hexString.Substring(i, 2), 16);
      }

      return byteArray;
    }
    public static ushort ToUShort(byte[] bytes) => (ushort)((bytes[1] << 8) | bytes[0]);
    public static uint ToUInt(byte[] bytes) => (uint)((bytes[3] << 24) | (bytes[2] << 16) | (bytes[1] << 8) | bytes[0]);
    public static byte[] GetBytes(ushort value) => [(byte)(value & 0xFF), (byte)((value & 0xFF00) >> 8)];
    public static byte[] GetBytes(uint value) => [
      (byte)(value & 0xFF),
      (byte)((value & 0xFF00) >> 8),
      (byte)((value & 0xFF0000) >> 16),
      (byte)((value & 0xFF000000) >>> 24),
    ];
  }
}
