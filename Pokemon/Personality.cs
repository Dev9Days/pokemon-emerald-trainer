using PokemonGen3Hack.Utils;
namespace PokemonGen3Hack.Pokemon {
  public class Personality {
    private static readonly Random rnd = new();
    private const uint GbaRngMultiplier = 0x41C64E6D;
    private const uint GbaRngIncrement = 0x6073;
    private const uint GbaRngInverse = 0xEEB9EB65;
    private Gender gender;
    private GenderGroup genderGroup;
    private byte ability;
    private Nature nature;
    private bool isShiny;
    private byte[] pid;
    private ushort tid;
    private ushort sid;
    public Gender Gender { get => gender; }
    public byte Ability { get => ability; } // 0: 무조건 첫 번째 능력, 1: 두 번째 능력을 가질 수 있음
    public Nature Nature { get => nature; }
    public bool IsShiny { get => isShiny; }
    public byte[] PID { get => pid; }
    public Personality(byte[] bytes, GenderGroup threshold, ushort tid, ushort sid) => SetPersonality(bytes, threshold, tid, sid);
    public void SetPersonality(byte[] bytes, GenderGroup threshold, ushort tid, ushort sid) {
      if (bytes.Length != 4) throw new ArgumentException("bytes length must be 4");
      pid = bytes;
      if (threshold is GenderGroup.Female or GenderGroup.Male or GenderGroup.Unknown) {
        gender = (Gender)threshold;
      } else {
        gender = bytes[0] < (byte)threshold ? Gender.Female : Gender.Male;
      }
      genderGroup = threshold;
      ability = (byte)(bytes[0] & 0x0000_0001);
      uint pvInt = Hex.ToUInt(bytes);
      nature = (Nature)(pvInt % 25);
      ushort p1 = Hex.ToUShort([.. bytes.Skip(2).Take(2)]);
      ushort p2 = Hex.ToUShort([.. bytes.Take(2)]);
      int xor = (tid ^ sid ^ p1 ^ p2);
      isShiny = xor < 8;
      this.tid = tid;
      this.sid = sid;
    }
    public Personality SetGender(Gender gender) {
      this.gender = gender;
      //SetNewPID(gender);
      pid = GeneratePID(gender);
      return this;
    }
    public Personality SetNature(Nature nature) {
      this.nature = nature;
      //SetNewPID(null);
      pid = GeneratePID(null);
      return this;
    }
    // 정말 랜덤 돌려서 조건에 맞는 값을 뽑는 PKHeX 방식
    //private void SetNewPID(Gender? gender) {
    //  while (true) {
    //    uint pid = (uint)rnd.Next();
    //    if (pid % 25 != (int)nature) continue;
    //    if (ability != (byte)(pid & 0x0000_0001)) continue;
    //    if (genderGroup is GenderGroup.Unknown or GenderGroup.Female or GenderGroup.Male) {
    //      this.pid = Hex.GetBytes(pid);
    //      break;
    //    } else {
    //      Gender newGender = (pid & 0xFF) < (byte)genderGroup ? Gender.Female : Gender.Male;
    //      if ((gender != null && gender != newGender) || this.gender != newGender) continue;
    //      this.pid = Hex.GetBytes(pid);
    //      break;
    //    }
    //  }
    //}
    public byte[] GeneratePID(Gender? targetGender = null, bool? targetShiny = null) {
      var rng = Random.Shared;
      Gender? desiredGender = targetGender ?? gender;
      bool needShiny = targetShiny ?? false;

      if (needShiny) {
        return GenerateShinyPID(rng, desiredGender);
      } else {
        return GenerateNonShinyPID(rng, desiredGender);
      }
    }

    private byte[] GenerateShinyPID(Random rng, Gender? desiredGender) {
      while (true) {
        ushort upper = (ushort)rng.Next(0x0000, 0x10000);
        ushort xorBase = (ushort)(tid ^ sid ^ upper);

        for (int shinyOffset = 0; shinyOffset < 8; shinyOffset++) {
          ushort lower = (ushort)(shinyOffset ^ xorBase);

          for (int i = 0; i <= ushort.MaxValue; i++) {
            ushort cand = (ushort)(lower + i * 8);

            if (!IsValidShiny(cand, upper)) continue;
            if (!IsValidPID(cand, upper, desiredGender)) continue;

            return BuildPIDBytes(cand, upper);
          }
        }
      }
    }

    private byte[] GenerateNonShinyPID(Random rng, Gender? desiredGender) {
      ushort upper = (ushort)rng.Next(0x0000, 0x10000);
      ushort lower = (ushort)rng.Next(0x0000, 0x10000);

      for (int i = 0; i <= ushort.MaxValue; i++) {
        ushort cand = (ushort)(lower + i);

        if (!IsValidPID(cand, upper, desiredGender)) continue;

        return BuildPIDBytes(cand, upper);
      }
      throw new Exception("Valid PID not found (should not happen).");
    }

    private bool IsValidShiny(ushort cand, ushort upper) {
      int xor = tid ^ sid ^ cand ^ upper;
      return xor < 8;
    }

    private bool IsValidPID(ushort cand, ushort upper, Gender? desiredGender) {
      uint pidForNature = ((uint)upper << 16) | cand;
      if (pidForNature % 25 != (uint)nature) return false;
      if ((cand & 1) != ability) return false;

      // Gender 검사
      if (genderGroup is GenderGroup.Unknown or GenderGroup.Female or GenderGroup.Male) {
        return true;
      }

      Gender newGender = (cand & 0xFF) < (byte)genderGroup ? Gender.Female : Gender.Male;
      return desiredGender == null || newGender == desiredGender;
    }

    private static byte[] BuildPIDBytes(ushort cand, ushort upper) => [
      (byte)(cand & 0xFF),
      (byte)(cand >> 8),
      (byte)(upper & 0xFF),
      (byte)(upper >> 8)
    ];
    // 정말 랜덤 돌려서 조건에 맞는 값을 뽑는 PKHeX 방식
    //public void SetShiny() {
    //  if (isShiny) return;
    //  do {
    //    ushort p1 = Hex.ToUShort([.. pid.Skip(2).Take(2)]);
    //    ushort p2 = Hex.ToUShort([.. pid.Take(2)]);
    //    int xor = (tid ^ sid ^ p1 ^ p2);
    //    isShiny = xor < 8;
    //    pid = GeneratePID(null);
    //  } while (!isShiny);
    //}
    public Personality SetShiny() {
      if (isShiny) return this;
      byte[] pidBytes = GeneratePID(null);
      ushort upper = (ushort)(pidBytes[2] | (pidBytes[3] << 8));
      ushort xorBase = (ushort)(tid ^ sid ^ upper);
      ushort shinyOffset = (ushort)(Random.Shared.Next(0, 8));
      ushort newLower = (ushort)(shinyOffset ^ xorBase);
      pidBytes[0] = (byte)(newLower & 0xFF);
      pidBytes[1] = (byte)(newLower >> 8);
      pid = pidBytes;
      isShiny = true;
      return this;
    }
    public Personality UpdatePersonality(Nature targetNature, Gender targetGender, bool targetShiny) {
      // 현재 값과 동일하면 아무것도 하지 않음
      if (nature == targetNature && gender == targetGender && isShiny == targetShiny) {
        return this;
      }
      // Nature, Gender, Shiny를 모두 만족하는 PID 한 번에 생성
      nature = targetNature;
      gender = targetGender;
      isShiny = targetShiny;
      pid = GeneratePID(targetGender, targetShiny);
      return this;
    }
    public bool CanGenerateMethod1PID(Nature targetNature, Gender targetGender, bool targetShiny, Dictionary<string, byte> targetIVs) {
      Nature originalNature = nature;
      Gender originalGender = gender;
      bool originalIsShiny = isShiny;

      nature = targetNature;
      gender = targetGender;
      isShiny = targetShiny;
      bool canGenerate = TryGenerateMethod1PID(targetIVs, targetGender, targetShiny, out _);

      nature = originalNature;
      gender = originalGender;
      isShiny = originalIsShiny;
      return canGenerate;
    }
    public bool TryUpdatePersonality(Nature targetNature, Gender targetGender, bool targetShiny, Dictionary<string, byte> targetIVs, bool allowIllegalFallback) {
      Nature originalNature = nature;
      Gender originalGender = gender;
      bool originalIsShiny = isShiny;
      byte[] originalPid = pid;

      nature = targetNature;
      gender = targetGender;
      isShiny = targetShiny;

      if (!TryGenerateMethod1PID(targetIVs, targetGender, targetShiny, out byte[] method1Pid)) {
        if (allowIllegalFallback) {
          pid = GeneratePID(targetGender, targetShiny);
          return false;
        }

        nature = originalNature;
        gender = originalGender;
        isShiny = originalIsShiny;
        pid = originalPid;
        return false;
      }

      pid = method1Pid;
      return true;
    }
    private bool TryGenerateMethod1PID(Dictionary<string, byte> targetIVs, Gender? desiredGender, bool targetShiny, out byte[] method1Pid) {
      method1Pid = [];

      ushort iv1 = PackIVWord(targetIVs, "HP", "Attack", "Defense");
      ushort iv2 = PackIVWord(targetIVs, "Speed", "SpecialAttack", "SpecialDefense");

      for (int iv1HighBit = 0; iv1HighBit <= 1; iv1HighBit++) {
        ushort rngIv1 = (ushort)(iv1 | (iv1HighBit << 15));
        for (int iv2HighBit = 0; iv2HighBit <= 1; iv2HighBit++) {
          ushort rngIv2 = (ushort)(iv2 | (iv2HighBit << 15));

          for (uint low = 0; low <= ushort.MaxValue; low++) {
            uint state3 = ((uint)rngIv1 << 16) | low;
            uint state4 = NextSeed(state3);
            if ((ushort)(state4 >> 16) != rngIv2) continue;

            uint state2 = PreviousSeed(state3);
            uint state1 = PreviousSeed(state2);
            ushort lower = (ushort)(state1 >> 16);
            ushort upper = (ushort)(state2 >> 16);

            if (targetShiny != IsValidShiny(lower, upper)) continue;
            if (!IsValidPID(lower, upper, desiredGender)) continue;

            method1Pid = BuildPIDBytes(lower, upper);
            return true;
          }
        }
      }

      return false;
    }
    private static ushort PackIVWord(Dictionary<string, byte> ivs, string first, string second, string third) {
      byte iv1 = ivs.TryGetValue(first, out byte firstValue) ? firstValue : (byte)0;
      byte iv2 = ivs.TryGetValue(second, out byte secondValue) ? secondValue : (byte)0;
      byte iv3 = ivs.TryGetValue(third, out byte thirdValue) ? thirdValue : (byte)0;
      return (ushort)((iv1 & 0x1F) | ((iv2 & 0x1F) << 5) | ((iv3 & 0x1F) << 10));
    }
    private static uint NextSeed(uint seed) => unchecked(seed * GbaRngMultiplier + GbaRngIncrement);
    private static uint PreviousSeed(uint seed) => unchecked((seed - GbaRngIncrement) * GbaRngInverse);
    // FR, LG에서 사용한다고 함. 현재는 에메랄드만 다루기 때문에 미사용
    public static byte GetUnownForm3(uint pid) {
      var value = ((pid & 0x3000000) >> 18) | ((pid & 0x30000) >> 12) | ((pid & 0x300) >> 6) | (pid & 0x3);
      return (byte)(value % 28);
    }
    public byte[] GetBytes() => pid;
    public override string ToString() {
      return $"Gender: {gender}, Ability: {ability}, Nature: {nature}, IsShiny: {isShiny}";
    }
  }
}
