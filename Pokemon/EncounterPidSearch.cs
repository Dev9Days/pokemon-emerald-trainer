using PokemonGen3Hack.Utils;
using Core = PKHeX.Core;

namespace PokemonGen3Hack.Pokemon {
  public enum PidMatchStatus { Match, NoMatch, Unsupported }

  public sealed record PidAssessment(PidMatchStatus Status, uint? CandidatePid, string Origin, string Method);

  internal static class EncounterPidSearch {
    public static PidAssessment Find(PokemonInfo info, Nature nature, Gender gender, bool shiny, Dictionary<string, byte> ivs) {
      string[] keys = ["HP", "Attack", "Defense", "Speed", "SpecialAttack", "SpecialDefense"];
      if ((uint)nature >= 25 || keys.Any(k => !ivs.TryGetValue(k, out byte value) || value > 31)) {
        return new(PidMatchStatus.NoMatch, null, "입력값 오류", "");
      }
      try {
        var original = new Core.PK3(info.GetPokemonData());
        if (original.Version is not (Core.GameVersion.R or Core.GameVersion.S or Core.GameVersion.E
            or Core.GameVersion.FR or Core.GameVersion.LG)) {
          return new(PidMatchStatus.Unsupported, null, "지원하지 않는 출신 게임", "");
        }
        var chain = Core.EncounterOrigin.GetOriginChain(original, 3, Core.EntityContext.Gen3);
        var candidate = original.Clone();
        candidate.IV_HP = ivs["HP"];
        candidate.IV_ATK = ivs["Attack"];
        candidate.IV_DEF = ivs["Defense"];
        candidate.IV_SPE = ivs["Speed"];
        candidate.IV_SPA = ivs["SpecialAttack"];
        candidate.IV_SPD = ivs["SpecialDefense"];
        var frames = GetFrames(ivs);
        string? knownOrigin = null;
        bool unsupported = false;
        foreach (var match in new Core.EncounterEnumerator3(original, chain, original.Version)) {
          var encounter = match.Encounter;
          if (match.Rating != Core.EncounterMatchRating.Match || !MatchesEncounter(encounter, original, chain)) continue;
          switch (encounter) {
            case Core.EncounterEgg3:
              knownOrigin = "부화/알";
              if (info.Personality.TryGenerateUncorrelatedPID(nature, gender, shiny, out byte[] eggPid, value => {
                candidate.PID = value;
                if (!MatchesTraits(value, info, nature, gender, shiny)) return false;
                if (!Core.Daycare3.IsValidProcPID(value, candidate.Version)) return false;
                if (Core.Breeding.IsGenderSpeciesDetermination(encounter.Species)
                    && !Core.Breeding.IsValidSpeciesBit34(value, candidate.Gender)) return false;
                return ((Core.IRandomCorrelation)encounter).IsCompatible(Core.MethodFinder.Analyze(candidate).Type, candidate)
                  == Core.RandomCorrelationRating.Match;
              })) {
                return new(PidMatchStatus.Match, Hex.ToUInt(eggPid), knownOrigin, "부화 규칙");
              }
              break;
            case Core.EncounterTrade3 trade:
              knownOrigin = "게임 내 교환";
              candidate.PID = trade.PID;
              if (MatchesTraits(candidate.PID, info, nature, gender, shiny) && MatchesEncounter(encounter, candidate, chain)) {
                return new(PidMatchStatus.Match, candidate.PID, knownOrigin, "고정 PID");
              }
              break;
            case Core.EncounterSlot3:
            case Core.EncounterStatic3:
              knownOrigin = encounter is Core.EncounterSlot3 ? "야생" : "고정 출현/선물";
              foreach (var frame in frames) {
                bool unown = encounter is Core.EncounterSlot3 && encounter.Species == (ushort)Core.Species.Unown;
                candidate.PID = unown ? (frame.Pid << 16) | (frame.Pid >> 16) : frame.Pid;
                if (!MatchesTraits(candidate.PID, info, nature, gender, shiny)) continue;
                if (!MatchesEncounter(encounter, candidate, chain)) continue;
                var type = unown ? ToUnown(frame.Type) : frame.Type;
                if (((Core.IRandomCorrelation)encounter).IsCompatible(type, candidate) != Core.RandomCorrelationRating.Match) continue;
                if (encounter is Core.EncounterSlot3 slot) {
                  var level = new Core.EvoCriteria {
                    Species = slot.Species, LevelMin = candidate.MetLevel, LevelMax = candidate.MetLevel,
                  };
                  byte leadGender = candidate.Species is (ushort)Core.Species.Marill or (ushort)Core.Species.Azumarill
                    ? Core.EntityGender.GetFromPID(candidate.PID, Core.EntityGender.MM) : candidate.Gender;
                  var pidiv = new Core.PIDIV(type, frame.OriginSeed);
                  if (!Core.LeadFinder.GetLeadInfo3(slot, pidiv, level, candidate.E, leadGender, 3).IsValid) continue;
                }
                return new(PidMatchStatus.Match, candidate.PID, knownOrigin, MethodName(type));
              }
              break;
            default:
              unsupported = true;
              break;
          }
        }
        return unsupported || knownOrigin == null
          ? new(PidMatchStatus.Unsupported, null, "획득 정보 또는 특수 이벤트 미확인", "")
          : new(PidMatchStatus.NoMatch, null, knownOrigin, "");
      } catch (Exception ex) {
        Logger.Warning($"PID 획득 규칙 분석 실패: {ex.Message}");
        return new(PidMatchStatus.Unsupported, null, "획득 정보 분석 실패", "");
      }
    }

    private static bool MatchesTraits(uint pid, PokemonInfo info, Nature nature, Gender gender, bool shiny) {
      var traits = new Personality(Hex.GetBytes(pid), info.GenderGroup, info.TID, info.SID);
      if (traits.Nature != nature || traits.Gender != gender || traits.IsShiny != shiny
          || traits.Ability != info.Personality.Ability) return false;
      // Unown's form is another PID-derived value that the user did not edit.
      return info.PID != 201 || Personality.GetUnownForm3(pid) == Personality.GetUnownForm3(Hex.ToUInt(info.Personality.PID));
    }

    private static bool MatchesEncounter(Core.IEncounterable encounter, Core.PK3 pk, Core.EvoCriteria[] chain) {
      if (encounter is Core.IFixedBall ball && ball.FixedBall != Core.Ball.None && pk.Ball != (byte)ball.FixedBall) return false;
      if (encounter is not Core.EncounterSlot3 && pk.Ball == (byte)Core.Ball.Safari) return false;
      if (encounter is Core.IEncounterMatch matcher) {
        if (!chain.Any(evo => evo.Species == encounter.Species && matcher.IsMatchExact(pk, evo))) return false;
        if (matcher.GetMatchRating(pk) != Core.EncounterMatchRating.Match) return false;
      }
      return Core.EncounterVerifier.GetEncounterVerifierMethod(3)(pk, encounter).Valid;
    }

    private readonly record struct Frame(uint Pid, uint OriginSeed, Core.PIDType Type);

    private static List<Frame> GetFrames(Dictionary<string, byte> ivs) {
      uint first = (uint)(ivs["HP"] | ivs["Attack"] << 5 | ivs["Defense"] << 10) << 16;
      uint second = (uint)(ivs["Speed"] | ivs["SpecialAttack"] << 5 | ivs["SpecialDefense"] << 10) << 16;
      Span<uint> seeds = stackalloc uint[Core.LCRNG.MaxCountSeedsIV];
      int count = Core.LCRNGReversal.GetSeedsIVs(seeds, first, second);
      List<Frame> result = [];
      foreach (uint seed in seeds[..count]) {
        // The reversal returns the state immediately before the first IV roll.
        uint origin1 = Core.LCRNG.Prev2(seed);
        result.Add(new(Core.ClassicEraRNG.GetSequentialPID(origin1), origin1, Core.PIDType.Method_1));
        uint origin2 = Core.LCRNG.Prev3(seed);
        result.Add(new(Core.ClassicEraRNG.GetSequentialPID(origin2), origin2, Core.PIDType.Method_2));
        uint state = origin2;
        uint low = Core.LCRNG.Next16(ref state);
        state = Core.LCRNG.Next(state);
        uint high = Core.LCRNG.Next16(ref state);
        result.Add(new((high << 16) | low, origin2, Core.PIDType.Method_3));
      }
      count = Core.LCRNGReversalSkip.GetSeedsIVs(seeds, first, second);
      foreach (uint seed in seeds[..count]) {
        uint origin4 = Core.LCRNG.Prev2(seed);
        result.Add(new(Core.ClassicEraRNG.GetSequentialPID(origin4), origin4, Core.PIDType.Method_4));
      }
      return result;
    }

    private static Core.PIDType ToUnown(Core.PIDType type) => type switch {
      Core.PIDType.Method_1 => Core.PIDType.Method_1_Unown,
      Core.PIDType.Method_2 => Core.PIDType.Method_2_Unown,
      Core.PIDType.Method_3 => Core.PIDType.Method_3_Unown,
      Core.PIDType.Method_4 => Core.PIDType.Method_4_Unown,
      _ => type,
    };

    private static string MethodName(Core.PIDType type) => type.ToString().Replace("Method_", "Method ").Replace("_Unown", " (Unown)");
  }
}
