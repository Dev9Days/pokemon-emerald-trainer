using PokemonGen3Hack.Pokemon;
using PokemonGen3Hack.Utils;
using Core = PKHeX.Core;

internal static class EncounterPidTests {
  public static void Run(Action<bool, string> check) {
    foreach (var method in new[] { Core.PIDType.Method_1, Core.PIDType.Method_2, Core.PIDType.Method_3, Core.PIDType.Method_4 }) {
      var target = FindWildVector(method);
      var source = MakeSource(target);
      var originalBytes = source.GetPokemonData();
      var traits = new Personality(Hex.GetBytes(target.PID), source.GenderGroup, source.TID, source.SID);
      var result = source.AssessPID(traits.Nature, traits.Gender, true, source.Data.IVs);
      check(result.Status == PidMatchStatus.Match, $"{method}: encounter-aware candidate found ({result.Origin})");
      check(result.Origin == "야생" && result.Method == method.ToString().Replace("Method_", "Method "), $"{method}: correct method selected, not forced to Method 1");
      check(source.GetPokemonData().SequenceEqual(originalBytes), $"{method}: assessment does not mutate original");
      check(source.RegeneratePID(traits.Nature, traits.Gender, true, source.Data.IVs), $"{method}: correlated update succeeds");
      var updated = ToCore(source);
      check(Core.MethodFinder.Analyze(updated).Type == method, $"{method}: PKHeX recognizes actual saved correlation");
      check(HasWildFrame(updated, Core.MethodFinder.Analyze(updated)), $"{method}: PKHeX verifies encounter slot and level frame");
      check(Plaintext(updated).SequenceEqual(Plaintext(new Core.PK3(originalBytes.ToArray()))), $"{method}: IVs and every other data field preserved");
      using (var memory = new MemoryFixture(originalBytes)) {
        memory.ViewModel.Pokemon.IsShiny = true;
        check(memory.ViewModel.GetPendingPIDAssessment()?.Status == PidMatchStatus.Match, $"{method}: UI selects encounter-aware candidate");
        check(memory.ViewModel.ApplyChanges(allowUncorrelatedPid: true).Length == 0, $"{method}: production write succeeds without a confirmation gate");
        var saved = new Core.PK3(memory.ReadSlot());
        check(saved.IsShiny && Core.MethodFinder.Analyze(saved).Type == method, $"{method}: correct method actually written to memory");
        check(Plaintext(saved).SequenceEqual(Plaintext(new Core.PK3(originalBytes.ToArray()))), $"{method}: production write preserves everything except PID");
      }

      if (method == Core.PIDType.Method_2) {
        var stationary = target.Clone();
        stationary.Species = 252;
        stationary.MetLevel = 5;
        stationary.CurrentLevel = 10;
        stationary.Stat_Level = 10;
        var starterSource = MakeSource(stationary);
        var starterTraits = new Personality(Hex.GetBytes(stationary.PID), starterSource.GenderGroup, starterSource.TID, starterSource.SID);
        var starterResult = starterSource.AssessPID(starterTraits.Nature, starterTraits.Gender, true, starterSource.Data.IVs);
        check(starterResult.Status == PidMatchStatus.NoMatch && starterResult.Origin == "고정 출현/선물", "Static starter must not accept a wild-only Method 2 candidate");
      }
      if (method == Core.PIDType.Method_4) {
        var evolved = target.Clone();
        evolved.Species = 262;
        evolved.CurrentLevel = 30;
        evolved.Stat_Level = 30;
        var evolvedSource = MakeSource(evolved);
        var evolvedResult = evolvedSource.AssessPID(traits.Nature, traits.Gender, true, evolvedSource.Data.IVs);
        check(evolvedResult.Status == PidMatchStatus.Match && evolvedResult.Origin == "야생", "Evolved Pokemon retains the pre-evolution wild encounter rules");
      }
      Console.WriteLine($"PASS: encounter-specific {method} vector, PID={target.PID:X8}.");
    }

    foreach (bool unhatched in new[] { false, true }) {
      var egg = new Core.PK3 {
        Species = 252, Version = Core.GameVersion.E, Language = 2, Ball = (byte)Core.Ball.Poke,
        MetLocation = unhatched ? (byte)Core.Locations.HatchLocationRSE : (byte)32, MetLevel = 0,
        CurrentLevel = 5, Stat_Level = 5, TID16 = 21963, SID16 = 55364, PID = 123456,
        Nickname = "EGG", OriginalTrainerName = "TEST", IsEgg = unhatched, FlagIsEgg = unhatched,
        IV_HP = 31, IV_ATK = 31, IV_DEF = 31, IV_SPE = 31, IV_SPA = 31, IV_SPD = 31,
      };
      egg.AbilityBit = (egg.PID & 1) != 0;
      var info = FromCore(egg);
      var before = Plaintext(egg);
      var result = info.AssessPID(Nature.Adamant, Gender.Male, true, info.Data.IVs);
      check(result.Status == PidMatchStatus.Match && result.Method == "부화 규칙", $"Egg (unhatched={unhatched}) does not require Method 1");
      check(info.RegeneratePID(Nature.Adamant, Gender.Male, true, info.Data.IVs), "Egg PID updated with all six IVs fixed at 31");
      var saved = ToCore(info);
      check(saved.IsShiny && saved.Nature == Core.Nature.Adamant && saved.Gender == 0, "Egg requested traits applied");
      check(Plaintext(saved).SequenceEqual(before), "Egg IVs, ability, origin flags and all other fields preserved");
    }

    var unknown = FindWildVector(Core.PIDType.Method_1);
    unknown.MetLocation = 255;
    var unknownInfo = MakeSource(unknown);
    var unknownResult = unknownInfo.AssessPID(unknownInfo.Personality.Nature, unknownInfo.Personality.Gender, true, unknownInfo.Data.IVs);
    check(unknownResult.Status == PidMatchStatus.Unsupported, "Unrecognized acquisition metadata is not treated as Method 1 or as an egg");
    byte[] beforeUnknown = unknownInfo.GetPokemonData();
    check(unknownInfo.RegeneratePID(unknownInfo.Personality.Nature, unknownInfo.Personality.Gender, true,
      unknownInfo.Data.IVs, allowUncorrelatedPid: true), "User may still apply edits when acquisition rules cannot be verified");
    check(ToCore(unknownInfo).IsShiny, "Unverified edit still changes the requested trait");
    check(Plaintext(ToCore(unknownInfo)).SequenceEqual(Plaintext(new Core.PK3(beforeUnknown))), "Unverified edit never modifies other fields");
  }

  private static Core.PK3 FindWildVector(Core.PIDType method) {
    for (uint origin = 1; origin < 10000; origin++) {
      uint state = origin;
      uint low = Core.LCRNG.Next16(ref state);
      if (method == Core.PIDType.Method_3) state = Core.LCRNG.Next(state);
      uint high = Core.LCRNG.Next16(ref state);
      uint pid = (high << 16) | low;
      if (method == Core.PIDType.Method_2) state = Core.LCRNG.Next(state);
      uint firstIv = Core.LCRNG.Next15(ref state);
      if (method == Core.PIDType.Method_4) state = Core.LCRNG.Next(state);
      uint secondIv = Core.LCRNG.Next15(ref state);
      var pk = new Core.PK3 {
        Species = 261, Version = Core.GameVersion.E, Language = 2, Ball = (byte)Core.Ball.Poke,
        MetLocation = 16, MetLevel = 2, CurrentLevel = 5, Stat_Level = 5,
        TID16 = 21963, SID16 = (ushort)(21963 ^ (ushort)pid ^ (ushort)(pid >> 16)),
        PID = pid, IV32 = firstIv | (secondIv << 15), Nickname = "POOCHYENA", OriginalTrainerName = "TEST",
        Stat_HPCurrent = 7, Stat_HPMax = 30, Stat_ATK = 12, Stat_DEF = 13, Stat_SPE = 14, Stat_SPA = 15, Stat_SPD = 16,
      };
      pk.AbilityBit = (pid & 1) != 0;
      var pidiv = Core.MethodFinder.Analyze(pk);
      if (pidiv.Type == method && HasWildFrame(pk, pidiv)) return pk;
    }
    throw new InvalidOperationException($"No independent wild vector found for {method}");
  }

  private static bool HasWildFrame(Core.PK3 pk, Core.PIDIV pidiv) {
    var chain = Core.EncounterOrigin.GetOriginChain(pk, 3, Core.EntityContext.Gen3);
    foreach (var match in new Core.EncounterEnumerator3(pk, chain, pk.Version)) {
      if (match.Rating != Core.EncounterMatchRating.Match || match.Encounter is not Core.EncounterSlot3 slot) continue;
      var level = new Core.EvoCriteria { Species = slot.Species, LevelMin = pk.MetLevel, LevelMax = pk.MetLevel };
      if (Core.LeadFinder.GetLeadInfo3(slot, pidiv, level, true, pk.Gender, 3).IsValid) return true;
    }
    return false;
  }

  private static PokemonInfo MakeSource(Core.PK3 target) {
    var source = target.Clone();
    for (uint step = 1; step < 100000; step++) {
      source.PID = unchecked(target.PID + 50 * step);
      if (source.Nature == target.Nature && source.Gender == target.Gender && !source.IsShiny) return FromCore(source);
    }
    throw new InvalidOperationException("No non-shiny source PID for fixture");
  }

  private static PokemonInfo FromCore(Core.PK3 pk) {
    byte[] bytes = new byte[100];
    pk.WriteEncryptedDataParty(bytes);
    var result = new PokemonInfo(bytes);
    result.SetPersonality((GenderGroup)pk.PersonalInfo.Gender);
    return result;
  }

  private static Core.PK3 ToCore(PokemonInfo info) => new(info.GetPokemonData());

  private static byte[] Plaintext(Core.PK3 pk) {
    byte[] data = new byte[100];
    pk.WriteDecryptedDataParty(data);
    // Trainer IDs, names, metadata and the full canonical data body must be unchanged.
    return [.. data.AsSpan(4, 24), .. data.AsSpan(30, 70)];
  }
}
