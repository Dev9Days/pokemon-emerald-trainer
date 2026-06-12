using System.Text;

namespace PokemonGen3Hack.Utils {
  public class CharDecoder {
    // 한글 자모 코드 범위 정의
    private static readonly List<char> ksx1001HangulTable = BuildHangulTable();
    // 0x3701~0x40C7 구간: KS X 1001 한글 테이블 기반 완성형 매핑
    private const int SyllableBaseCode = 0x3701; // '가'
    private const int SyllableEndCode = 0x40C7; // '힝'
    private static readonly Dictionary<int, char> syllableMap = BuildSyllableMap();
    private static readonly Dictionary<char, int> syllableReverseMap = BuildSyllableReverseMap();
    // 0x4101~ 구간: 자모 테이블 (초성 19개 + 모음 15개)
    // 예외: 'ㅛ'=0x4120,'ㅜ'=0x4121,'ㅠ'=0x4125,'ㅡ'=0x4126,'ㅣ'=0x4128
    private const int JamoBaseCode = 0x4101;
    private static readonly char[] jamoTable = {
      'ㄱ','ㄲ','ㄴ','ㄷ','ㄸ','ㄹ','ㅁ','ㅂ','ㅃ','ㅅ','ㅆ','ㅇ','ㅈ','ㅉ','ㅊ','ㅋ','ㅌ','ㅍ','ㅎ',
      'ㅏ','ㅐ','ㅑ','ㅒ','ㅓ','ㅔ','ㅕ','ㅖ','ㅗ'
    };
    private static readonly Dictionary<char, int> jamoReverseMap = BuildJamoReverseMap();
    // 0x00 페이지: 단일 바이트 기호/숫자/알파벳 매핑용 역매핑 테이블
    private static readonly Dictionary<char, byte> charTo0PageMap = Build0PageReverseMap();
    public static string Encode(byte[] codeBytes, bool readUntilEOL = false) {
      ArgumentNullException.ThrowIfNull(codeBytes);
      StringBuilder sb = new();
      // 2바이트 코드(0x37~0x40, 0x41로 시작)와 1바이트 코드(그 외)를 혼합해서 처리한다.
      // - 0x37~0x40, 0x41: 뒤의 한 바이트와 합쳐서 2바이트 코드로 처리
      // - 그 외: 단일 바이트 코드 0x00xx 로 처리하여 0x00 페이지 매핑을 사용
      for (int i = 0; i < codeBytes.Length;) {
        int code;
        byte first = codeBytes[i];
        // 완성형/자모 영역의 2바이트 코드 시작 바이트인지 확인
        if ((first >= 0x37 && first <= 0x40) || first == 0x41) {
          if (i + 1 >= codeBytes.Length) {
            // 잘못된 꼬리 바이트: 남은 바이트가 하나뿐이면 그냥 단일 바이트로 처리
            code = first;
            i += 1;
          } else {
            code = (first << 8) | codeBytes[i + 1];
            i += 2;
          }
        } else {
          // 단일 바이트 심볼/ASCII 코드
          code = first; // 상위 바이트 0x00, 하위 바이트는 해당 값
          i += 1;
        }
        // 예약 영역은 스킵한다.
        if (IsReservedCode(code)) continue;
        int hiPage = (code >> 8) & 0xFF;
        int loByte = code & 0xFF; // 각 페이지의 0x??00 코드는 사용하지 않는다.
        if (hiPage >= 0x37 && hiPage <= 0x40 && loByte == 0x00) {
          continue;
        }
        // 0x00 페이지: 단일 바이트 기호/숫자/알파벳 매핑
        if (hiPage == 0x00) {
          char mapped;
          // A1~AA : '0'~'9'
          if (loByte >= 0xA1 && loByte <= 0xAA) {
            mapped = (char)('0' + (loByte - 0xA1));
          } else if (loByte == 0x1B) {
            // 1B : é
            mapped = 'é';
          } else if (loByte == 0xAB) { // AB~AF : ! ? . - ·
            mapped = '!';
          } else if (loByte == 0xAC) {
            mapped = '?';
          } else if (loByte == 0xAD) {
            mapped = '.';
          } else if (loByte == 0xAE) {
            mapped = '-';
          } else if (loByte == 0xAF) {
            mapped = '·';
          } else if (loByte == 0xB1) { // B1~B4 : 따옴표 (열기/닫기)
            mapped = '“';
          } else if (loByte == 0xB2) {
            mapped = '”';
          } else if (loByte == 0xB3) {
            mapped = '‘';
          } else if (loByte == 0xB4) {
            mapped = '’';
          } else if (loByte == 0xB5) { // B5, B6 : 성별 기호
            mapped = '♂';
          } else if (loByte == 0xB6) {
            mapped = '♀';
          } else if (loByte == 0xB7) {
            mapped = '₽';
          } else if (loByte == 0xB8) { // B8 : 콤마
            mapped = ',';
          } else if (loByte == 0xBA) { // BA : 슬래시
            mapped = '/';
          } else if (loByte >= 0xBB && loByte <= 0xD4) { // BB~D4 : A~Z
            mapped = (char)('A' + (loByte - 0xBB));
          } else if (loByte >= 0xD5 && loByte <= 0xEE) { // D5~EE : a~z
            mapped = (char)('a' + (loByte - 0xD5));
          } else if (loByte == 0x00) { // 00 : 공백
            mapped = ' ';
          } else if (loByte == 0xFA) { // FA : 남은 문장이 있을 때(개행 1)
            mapped = '▼';
            sb.Append(mapped);
            mapped = '\n';
          } else if (loByte == 0xFB) { // FB : 남은 문장이 있을 때(개행 2)
            mapped = '▼';
            sb.Append(mapped);
            mapped = '\n';
            sb.Append(mapped);
            mapped = '\n';
          } else if (loByte == 0xFC) {
            mapped = '=';
            // } else if (loByte == 0xFD) { // FD : 0xFD01은 플레이어 이름
          } else if (loByte == 0xFE) { // FE : 줄 바꿈
            mapped = '\n';
          } else if (loByte == 0xFF) { // FF : 문장 종료
            if (readUntilEOL) {
              return sb.ToString();
            }
            //mapped = '\n';
            //sb.Append(mapped);
            mapped = '\n';
          } else {
            mapped = (char)code;
          }
          sb.Append(mapped);
          continue;
        }
        // 한글 커스텀 완성형 영역 이전 코드는 그대로 출력
        if (code < SyllableBaseCode) {
          sb.Append((char)code);
          continue;
        }
        // 0x3701~0x40C7: 완성형 한글 (KS X 1001 순서)
        if (code >= SyllableBaseCode && code <= SyllableEndCode) {
          if (syllableMap.TryGetValue(code, out char syllable)) {
            sb.Append(syllable);
            continue;
          }
          // 매핑이 없으면 그대로 출력
          sb.Append((char)code);
          continue;
        }
        // 0x4101~ : 자모 코드 매핑
        if (code >= JamoBaseCode) {
          if (code == 0x4120) { sb.Append('ㅛ'); continue; }
          if (code == 0x4121) { sb.Append('ㅜ'); continue; }
          if (code == 0x4125) { sb.Append('ㅠ'); continue; }
          if (code == 0x4126) { sb.Append('ㅡ'); continue; }
          if (code == 0x4128) { sb.Append('ㅣ'); continue; }
          int jamoIndex = code - JamoBaseCode;
          if (jamoIndex >= 0 && jamoIndex < jamoTable.Length) {
            sb.Append(jamoTable[jamoIndex]);
            continue;
          }
        }
        if (code == 0xFD08) sb.Append("AQUA");
        if (code == 0xFD09) sb.Append("MAGMA");
        // 그 외 코드는 그대로 출력
        sb.Append((char)code);
      }
      return sb.ToString();
    }
    public static byte[] Decode(string text) {
      if (text == null) { throw new ArgumentNullException(nameof(text)); }
      List<byte> result = [];
      foreach (char ch in text) {
        // 0x00 페이지 매핑 우선
        if (charTo0PageMap.TryGetValue(ch, out byte code0)) {
          result.Add(code0);
          continue;
        }
        // 완성형 한글
        if (syllableReverseMap.TryGetValue(ch, out int syllableCode)) {
          result.Add((byte)((syllableCode >> 8) & 0xFF));
          result.Add((byte)(syllableCode & 0xFF));
          continue;
        }
        // 자모
        if (jamoReverseMap.TryGetValue(ch, out int jamoCode)) {
          result.Add((byte)((jamoCode >> 8) & 0xFF));
          result.Add((byte)(jamoCode & 0xFF));
          continue;
        }
        // 그 외 문자: 가능한 경우 단일 바이트로 그대로 기록, 아니면 '?'로 대체
        if (ch <= 0xFF) {
          result.Add((byte)ch);
        } else {
          result.Add((byte)'?');
        }
      }
      return [.. result];
    }
    // 예약된 코드인지 확인하는 함수
    private static bool IsReservedCode(int code) {
      // 0x37FF ~ 0x3FFF 사이의 코드들은 예약된 코드로 간주하고 무시합니다.
      return (code >= 0x37F0 && code <= 0x3FFF) && (code % 0x100 >= 0xF0 && code % 0x100 <= 0xFF);
    }
    private static List<char> BuildHangulTable() {
      var list = new List<char>();
      // EUC-KR(51949) 코드페이지는 KS X 1001 한글 영역을 포함하고 있어
      // KS X 1001 테이블 순서를 얻는 용도로 사용할 수 있다.
      Encoding ksx1001 = Encoding.GetEncoding(51949);
      for (int row = 0xB0; row <= 0xC8; row++) {
        for (int cell = 0xA1; cell <= 0xFE; cell++) {
          byte[] bytes = [(byte)row, (byte)cell];
          string s = ksx1001.GetString(bytes);
          if (s.Length == 1) {
            char ch = s[0];
            if (ch >= '가' && ch <= '힣') {
              list.Add(ch);
            }
          }
        }
      }
      return list;
    }
    private static Dictionary<int, char> BuildSyllableMap() {
      var map = new Dictionary<int, char>(); int index = 0;
      for (int code = SyllableBaseCode; code <= SyllableEndCode; code++) {
        int page = (code >> 8) & 0xFF; int low = code & 0xFF;
        if (IsReservedCode(code)) {
          continue;
        }
        // 각 페이지의 0x??00 코드는 사용하지 않는다.
        if (page >= 0x37 && page <= 0x40 && low == 0x00) {
          continue;
        }
        if (index >= ksx1001HangulTable.Count) {
          break;
        }
        map[code] = ksx1001HangulTable[index]; index++;
      }
      return map;
    }
    private static Dictionary<char, int> BuildSyllableReverseMap() {
      var reverse = new Dictionary<char, int>();
      foreach (var kvp in syllableMap) {        // 같은 글자가 여러 코드에 매핑될 일은 없다고 가정
        if (!reverse.ContainsKey(kvp.Value)) {
          reverse[kvp.Value] = kvp.Key;
        }
      }
      return reverse;
    }
    private static Dictionary<char, int> BuildJamoReverseMap() {
      var reverse = new Dictionary<char, int>();
      for (int i = 0; i < jamoTable.Length; i++) {
        char ch = jamoTable[i];
        int code = JamoBaseCode + i;
        if (!reverse.ContainsKey(ch)) {
          reverse[ch] = code;
        }
      }
      // 예외 자모 코드 매핑 (연속 구간 밖에 있는 코드들)
      // 'ㅛ'=0x4120,'ㅜ'=0x4121,'ㅠ'=0x4125,'ㅡ'=0x4126,'ㅣ'=0x4128
      if (!reverse.ContainsKey('ㅛ')) reverse['ㅛ'] = 0x4120;
      if (!reverse.ContainsKey('ㅜ')) reverse['ㅜ'] = 0x4121;
      if (!reverse.ContainsKey('ㅠ')) reverse['ㅠ'] = 0x4125;
      if (!reverse.ContainsKey('ㅡ')) reverse['ㅡ'] = 0x4126;
      if (!reverse.ContainsKey('ㅣ')) reverse['ㅣ'] = 0x4128;
      return reverse;
    }
    private static Dictionary<char, byte> Build0PageReverseMap() {
      var map = new Dictionary<char, byte>();
      // 숫자 '0'~'9' : 0xA1~0xAA
      for (int i = 0; i < 10; i++) {
        map[(char)(i + 48)] = (byte)(0xA1 + i);
      }
      // 1B : é
      map['é'] = 0x1B;
      map['!'] = 0xAB;
      map['?'] = 0xAC;
      map['.'] = 0xAD;
      map['-'] = 0xAE;
      map['·'] = 0xAF;
      map['“'] = 0xB1;
      map['”'] = 0xB2;
      map['‘'] = 0xB3;
      map['’'] = 0xB4;
      map['♂'] = 0xB5;
      map['♀'] = 0xB6;
      map[','] = 0xB8;
      map['/'] = 0xBA;
      // A~Z : 0xBB~0xD4
      for (int i = 0; i < 26; i++) {
        map[(char)('A' + i)] = (byte)(0xBB + i);
      }
      // a~z : 0xD5~0xEE
      for (int i = 0; i < 26; i++) {
        map[(char)('a' + i)] = (byte)(0xD5 + i);
      }
      // 공백, 개행, 기타 심볼
      map[' '] = 0x00;
      map['='] = 0xFC;
      map['\n'] = 0xFE;
      return map;
    }
  }
}
