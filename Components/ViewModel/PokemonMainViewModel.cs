using Force.DeepCloner;
using PokemonGen3Hack.Emulator;
using PokemonGen3Hack.Pokemon;
using PokemonGen3Hack.Services;
using PokemonGen3Hack.Utils;
using System.Diagnostics;

namespace PokemonGen3Hack.Components.ViewModel {
  public class PokemonMainViewModel {
    // Model Classes
    public class PokemonModel {
      private IConst? _lang;

      public void SetLanguage(IConst lang) {
        _lang = lang;
      }

      public int Level { get; set; }
      public Gender Gender { get; set; }
      public Nature Nature { get; set; }
      public string Nickname { get; set; } = string.Empty;
      public bool IsShiny { get; set; }
      public string Type1 { get; set; } = string.Empty;
      public string Type2 { get; set; } = string.Empty;
      public string Type1Text { get; set; } = string.Empty;
      public string Type2Text { get; set; } = string.Empty;
      public string OT { get; set; } = string.Empty;
      public string Language { get; set; } = string.Empty;
      public string PID { get; set; } = string.Empty;
      public string TID { get; set; } = string.Empty;
      public string SID { get; set; } = string.Empty;
      public List<StatModel> Stats { get; set; } = [];
      public int SumBase => Stats.Sum(s => s.Base);
      public int SumIV => Stats.Sum(s => s.IV);
      public int SumEV => Stats.Sum(s => s.EV);
      public int SumStat => Stats.Sum(s => s.RealStat);
      public HiddenPowerType HiddenPowerType {
        get {
          int[] ivs = [.. Stats.Select(s => s.IV)];
          byte typeFlag = 0;
          for (int i = 0; i < ivs.Length; i++) {
            int iv = ivs[i];
            int bit = 1 << i;
            if ((iv & 1) != 0)
              typeFlag |= (byte)bit;
          }
          return (HiddenPowerType)(typeFlag * 15 / 63);
        }
      }
      public string HiddenPowerTypeEN => Const.EN.HiddenPowerTypes[HiddenPowerType] ?? string.Empty;
      public string HiddenPowerTypeText => _lang?.HiddenPowerTypes[HiddenPowerType] ?? string.Empty;
      public int HiddenPowerValue {
        get {
          int[] ivs = [.. Stats.Select(s => s.IV)];
          byte valueFlag = 0;
          for (int i = 0; i < ivs.Length; i++) {
            int iv = ivs[i];
            int bit = 1 << i;
            if (((iv & 3) >> 1) != 0)
              valueFlag |= (byte)bit;
          }
          return valueFlag * 40 / 63 + 30;
        }
      }
      public List<MoveModel> Moves { get; set; } = [new MoveModel(), new MoveModel(), new MoveModel(), new MoveModel()];
      public bool IsObedience { get; set; }
      public StatusCondition Status { get; set; }
      public ushort HeldItem { get; set; }
      public int Friendship { get; set; }
      public bool IsPokerus { get; set; }
      public int PokerusDaysLeft { get; set; }
      public string PokerusDetail {
        get => PokerusDaysLeft > 0 ? $"Infected ({PokerusDaysLeft} days left)" : "Cured";
      }
      public bool Marking1 { get; set; }
      public bool Marking2 { get; set; }
      public bool Marking3 { get; set; }
      public bool Marking4 { get; set; }
      public List<Ribbon> SelectedRibbons { get; set; } = [];

      public string EncounterLocation { get; set; } = "Petalburg Forest";
      public int EncounterLevel { get; set; } = 5;
      public string EncounterBall { get; set; } = "Poké Ball";

      public int Experience { get; set; }
      public int NextLevel { get; set; } = 60;
    }

    public class StatModel {
      public string EN { get; set; } = string.Empty;
      public string Name { get; set; } = string.Empty;
      public int Base { get; set; }
      public int IV { get; set; }
      public int EV { get; set; }
      public int RealStat { get; set; }
    }

    public class MoveModel {
      public int Number { get; set; }

      public int PP { get; set; }
      public int PPBonus { get; set; }
    }

    // Properties
    private Emulator.Emulator emul = new();
    private PokemonDex dex;
    public int PartyCount { get; set; } = 0;
    public PokemonInfo[] PokemonInfo { get; set; } = [];
    public bool ShowProcessModal { get; set; } = true;
    public PokemonModel Pokemon { get; set; } = new PokemonModel();
    public PokemonModel?[] PokemonTemp = new PokemonModel?[6];
    public int SelectedPartyIndex { get; set; } = 0;
    public bool IsEnglish { get => emul.Lang is Language.English; }
    public bool IsAttached { get => emul.Process != null && emul.PAK != nint.Zero && emul.EWRAM != nint.Zero && emul.IWRAM != nint.Zero && emul.Lang != Language.Unknown; }
    private IConst EmulLang { get => IsEnglish ? Const.EN : Const.KO; }

    private void UpdatePokemonLanguage(PokemonModel pokemon) {
      pokemon.SetLanguage(EmulLang);
      EnsureDefaultStats(pokemon);
      for (int i = 0; i < pokemon.Stats.Count && i < EmulLang.StatNames.Length; i++) {
        pokemon.Stats[i].Name = EmulLang.StatNames[i];
      }
    }

    private void EnsureDefaultStats(PokemonModel pokemon) {
      while (pokemon.Stats.Count < Const.EN.StatNames.Length) {
        int index = pokemon.Stats.Count;
        pokemon.Stats.Add(new StatModel {
          EN = Const.EN.StatNames[index],
          Name = EmulLang.StatNames[index],
          Base = 0,
          IV = 0,
          EV = 0,
          RealStat = 0,
        });
      }
    }

    private Dictionary<string, byte> PokemonModelStatsToIVs(List<StatModel> stats) {
      var ivs = new Dictionary<string, byte>();
      foreach (var stat in stats) {
        ivs[stat.EN] = (byte)stat.IV;
      }
      return ivs;
    }

    private Dictionary<string, byte> PokemonModelStatsToEVs(List<StatModel> stats) {
      var evs = new Dictionary<string, byte>();
      foreach (var stat in stats) {
        evs[stat.EN] = (byte)stat.EV;
      }
      return evs;
    }

    private static bool SameStats(Dictionary<string, byte> left, Dictionary<string, byte> right) {
      string[] keys = ["HP", "Attack", "Defense", "Speed", "SpecialAttack", "SpecialDefense"];
      foreach (string key in keys) {
        left.TryGetValue(key, out byte leftValue);
        right.TryGetValue(key, out byte rightValue);
        if (leftValue != rightValue) return false;
      }
      return true;
    }

    public bool NeedsNewPID() {
      if (PokemonInfo.Length == 0 || SelectedPartyIndex < 0 || SelectedPartyIndex >= PokemonInfo.Length) {
        return false;
      }

      var info = PokemonInfo[SelectedPartyIndex];
      Dictionary<string, byte> ivs = PokemonModelStatsToIVs(Pokemon.Stats);
      return
        Pokemon.Nature != info.Personality.Nature ||
        Pokemon.Gender != info.Personality.Gender ||
        Pokemon.IsShiny != info.Personality.IsShiny ||
        !SameStats(ivs, info.Data.IVs);
    }

    public bool IsPKHeXMethod1Compatible() {
      if (PokemonInfo.Length == 0 || SelectedPartyIndex < 0 || SelectedPartyIndex >= PokemonInfo.Length) {
        return true;
      }

      if (!NeedsNewPID()) return true;

      var info = PokemonInfo[SelectedPartyIndex];
      return info.CanGenerateMethod1PID(Pokemon.Nature, Pokemon.Gender, Pokemon.IsShiny, PokemonModelStatsToIVs(Pokemon.Stats));
    }

    private byte CalculatePokerusValue(bool isPokerus, int daysLeft) {
      if (!isPokerus) return 0;
      if (daysLeft <= 0) return 0x10;
      return (byte)(0x10 | (daysLeft & 0xF));
    }

    // Options
    public Dictionary<Gender, string> GenderOptions { get => EmulLang.Gender; }
    public Dictionary<Nature, string> NatureOptions { get => EmulLang.Nature; }
    public Dictionary<int, string> MoveOptions {
      get => new Dictionary<int, string>() { { 0, "-" } }.Concat(EmulLang.Moves).ToDictionary();
    }
    public Dictionary<StatusCondition, string> StatusOptions { get => EmulLang.StatusCondition; }
    public Dictionary<int, string> ItemOptions {
      get {
        Dictionary<int, string> result = [];
        result.Add(0, "-");
        if (!IsAttached) return result;

        try {
          var items = dex.GetItemsData();
          for (int i = 1; i <= items.Length; i++) {
            byte[] itemName = [.. items[i - 1].Take(dex.ITEM_NAME_COUNTS)];
            string name = CharDecoder.Encode(itemName);
            if (name.StartsWith("????????")) continue;
            result[i] = name;
          }
        } catch (Exception ex) {
          Logger.Warning($"아이템 목록 로딩 실패: {ex.Message}");
        }
        return result;
      }
    }
    public Dictionary<Ribbon, string> RibbonOptions { get => EmulLang.Ribbons; }

    public PokemonMainViewModel() {
      dex = new(emul);
      UpdatePokemonLanguage(Pokemon);
    }

    // Methods
    public void OnRibbonChange(Ribbon[] selectedRibbons) {
      Pokemon.SelectedRibbons = selectedRibbons?.ToList() ?? [];
    }

    public async Task ReloadPokemonAsync() {
      byte count = dex.GetPartyCount(false);
      if (SelectedPartyIndex >= count) {
        SelectedPartyIndex = 0;
      }
      PokemonInfo = new PokemonInfo[count];
      for (int i = 0; i < count; i++) {
        PokemonInfo[i] = new(dex.GetPokemonData((byte)(i + 1)));
        var slot = PokemonInfo[i];
        ushort pid = Hex.ToUShort(slot.Data.Species);
        byte[] speciesData = GetSpeciesOrDefault(pid);
        if (pid == 0) {
          slot.Image = string.Empty;
        } else try {
          slot.Image = await PokemonDex.GetPocketmonImage(pid >= Const.PokemonImage.Length ? (ushort)(Const.PokemonImage.Length - 1) : pid);
        } catch (Exception ex) {
          Logger.Warning($"포켓몬 이미지 로딩 실패: Species={pid}, {ex.Message}");
          slot.Image = string.Empty;
        }
        GenderGroup genderGroup = (GenderGroup)speciesData[16];
        slot.SetPersonality(genderGroup);
        // byte[] encrypted = slot.Data.GetEncryptData();
      }
      PartyCount = count;
      PokemonTemp = new PokemonModel[6];
      SelectPartyMember(SelectedPartyIndex);
    }
    private string[] Validate() {
      List<string> errMsgs = [];
      var p = Pokemon;
      if (!IsAttached || PokemonInfo.Length == 0 || SelectedPartyIndex < 0 || SelectedPartyIndex >= PokemonInfo.Length) {
        return ["먼저 에뮬레이터 프로세스를 연결하고 포켓몬 데이터를 불러오세요."];
      }
      if (p.Stats.Count < 6) {
        return ["능력치 데이터가 아직 준비되지 않았습니다. 다시 불러오기를 실행하세요."];
      }
      if (p.Level < 1 || p.Level > 100) {
        errMsgs.Add("레벨을 1~100 사이의 값으로 입력하세요.");
      }
      if (p.Experience < 1 || p.Experience > 0x100_0000) {
        errMsgs.Add("경험치를 1~16777216 사이의 값으로 입력하세요.");
      }
      int nicknameMaxLength = IsEnglish ? 10 : 5;
      if (p.Nickname.Length < 1 || p.Nickname.Length > nicknameMaxLength) {
        errMsgs.Add($"닉네임을 {nicknameMaxLength}자리 이하로 입력하세요.");
      }
      for (int i = 0; i < p.Stats.Count; i++) {
        var stat = p.Stats[i];
        if (stat.IV < 0 || stat.IV > 31) {
          errMsgs.Add($"{stat.Name}-IV를 0~31 사이의 값으로 입력하세요.");
        }
        if (stat.EV < 0 || stat.EV > 255) {
          errMsgs.Add($"{stat.Name}-EV를 0~255 사이의 값으로 입력하세요.");
        }
        if (stat.RealStat < 1 || stat.RealStat > 999) {
          errMsgs.Add($"{stat.Name}-RealStat을 1~999 사이의 값으로 입력하세요.");
        }
      }
      if (p.SumEV < 0 || p.SumEV > 510) {
        errMsgs.Add("EV의 총합을 0~510 사이의 값으로 맞춰주세요.");
      }
      for (int i = 0; i < 4; i++) {
        var move = p.Moves[i];
        if (move.Number != 0 && (move.PP < 0 || move.PP > 64)) {
          errMsgs.Add($"Move{i + 1}의 PP를 0~64 사이의 값으로 입력하세요.");
        }
      }
      if (p.Friendship < 0 || p.Friendship > 255) {
        errMsgs.Add("친밀도를 0~255 사이의 값으로 입력하세요.");
      }
      return [.. errMsgs];
    }

    public string[] ApplyChanges(bool allowIllegalPKHeX = false) {
      string[] errMsgs = Validate();
      if (errMsgs.Length > 0) return errMsgs;
      var info = PokemonInfo[SelectedPartyIndex];
      var p = Pokemon;
      try {
        // 1. 기본 속성 업데이트
        info.SetNickname(p.Nickname);
        info.SetOTName(p.OT);
        info.SetMarkings(p.Marking1, p.Marking2, p.Marking3, p.Marking4);
        Dictionary<string, byte> ivs = PokemonModelStatsToIVs(p.Stats);
        // 2. 새 PID 생성 (DataStructure 업데이트 전에 필수!)
        if (NeedsNewPID()) {
          bool isLegal = info.RegeneratePID(p.Nature, p.Gender, p.IsShiny, ivs, allowIllegalPKHeX);
          if (!isLegal && !allowIllegalPKHeX) {
            return ["현재 성격/성별/색이 다른 여부/IV 조합으로는 Gen 3 Method 1 PID를 찾지 못했습니다."];
          }
        }
        // 3. DataStructure 필드 업데이트
        info.Data.SetExperience((uint)p.Experience);
        info.Data.SetFriendship((byte)p.Friendship);
        info.Data.SetItemHeld(p.HeldItem);
        info.Data.SetIVs(ivs, info.Personality.Ability);
        info.Data.SetEVs(PokemonModelStatsToEVs(p.Stats));
        ushort[] moves = [.. p.Moves.Select(m => (ushort)m.Number)];
        info.Data.SetMoves(moves);
        byte[] pps = [.. p.Moves.Select(m => (byte)m.PP)];
        info.Data.SetPPs(pps);
        byte[] ppBonuses = [.. p.Moves.Select(m => (byte)m.PPBonus)];
        info.Data.SetPPBonuses(ppBonuses);
        byte pokerusValue = CalculatePokerusValue(p.IsPokerus, p.PokerusDaysLeft);
        info.Data.SetPokerus(pokerusValue);
        info.Data.SetRibbons(p.SelectedRibbons);
        info.Data.SetObedience(p.IsObedience);
        // 4. Status 업데이트 (새 인스턴스 생성)
        Status newStatus = Status.CreateStatus(
          p.Status,
          (byte)p.Level,
          info.Status.Mail,
          (ushort)p.Stats[0].RealStat,
          (ushort)p.Stats[0].RealStat,
          (ushort)p.Stats[1].RealStat,
          (ushort)p.Stats[2].RealStat,
          (ushort)p.Stats[3].RealStat,
          (ushort)p.Stats[4].RealStat,
          (ushort)p.Stats[5].RealStat
        );
        info.SetStatus(newStatus);
        // 5. 100바이트로 직렬화
        byte[] pokemonData = info.GetPokemonData();
        // 6. 에뮬레이터 메모리에 쓰기
        byte slotNumber = (byte)(SelectedPartyIndex + 1);
        bool writeSuccess = dex.WritePokemonData(slotNumber, pokemonData);
        if (!writeSuccess) {
          return ["에뮬레이터가 실행 중인지 확인하세요."];
        }
        // 7. 임시 저장 초기화
        PokemonTemp[SelectedPartyIndex] = null;
        Logger.Info($"[{p.Nickname}] 변경 완료");
        return [];
      } catch (Exception ex) {
        Logger.Error(ex, "포켓몬 변경 사항 적용 중 오류 발생");
        return [$"오류 발생: {ex.Message}"];
      }
    }

    public void AttachProcess() {
      ShowProcessModal = true;
    }

    public void TempSave() {
      if (SelectedPartyIndex < 0 || SelectedPartyIndex >= PokemonTemp.Length) return;
      PokemonTemp[SelectedPartyIndex] = Pokemon.DeepClone();
    }

    public async Task HandleProcessSelectedAsync(Process process) {
      await Task.Run(() => emul.SetProcess(process));
      Logger.Debug($"Emulator attached to process: {process.ProcessName}");
      await ReloadPokemonAsync();
    }

    public void SelectPartyMember(int index) {
      if (index < 0 || index >= PokemonTemp.Length) return;
      SelectedPartyIndex = index;
      PokemonModel? temp = PokemonTemp[index];
      if (temp != null) {
        Pokemon = temp.DeepClone();
        UpdatePokemonLanguage(Pokemon);
        return;
      }
      if (PokemonInfo != null && index >= 0 && index < PokemonInfo.Length) {
        var slot = PokemonInfo[index];
        UpdatePokemonLanguage(Pokemon);
        Pokemon.Level = slot.Status.Level;
        Pokemon.Experience = (int)slot.Data.Experience;
        Pokemon.Gender = slot.Personality.Gender;
        Pokemon.Nature = slot.Personality.Nature;
        Pokemon.Nickname = slot.Nickname;
        Pokemon.IsShiny = slot.Personality.IsShiny;
        byte[] p = GetSpeciesOrDefault(slot.PID);
        Pokemon.Type1 = Const.EN.PokemonTypes[(PokemonType)p[6]];
        Pokemon.Type1Text = EmulLang.PokemonTypes[(PokemonType)p[6]];
        Pokemon.Type2 = p[6] != p[7] ? Const.EN.PokemonTypes[(PokemonType)p[7]] : string.Empty;
        Pokemon.Type2Text = p[6] != p[7] ? EmulLang.PokemonTypes[(PokemonType)p[7]] : string.Empty;
        Pokemon.OT = slot.OTName;
        Pokemon.Language = EmulLang.Language[slot.Language];
        byte[] tempPID = [.. slot.Personality.PID];
        tempPID = [.. tempPID.Reverse()];
        Pokemon.PID = Hex.BytesToString(tempPID).Replace(" ", "");
        Pokemon.TID = slot.TID.ToString();
        Pokemon.SID = slot.SID.ToString();
        Dictionary<string, byte> ivs = slot.Data.IVs;
        Dictionary<string, byte> evs = slot.Data.EVs;
        int[] realStats = [slot.Status.MaxHP, slot.Status.Attack, slot.Status.Defense, slot.Status.Speed, slot.Status.SpecialAttack, slot.Status.SpecialDefense];
        Pokemon.Stats.Clear();
        for (int i = 0; i < EmulLang.StatNames.Length; i++) {
          ivs.TryGetValue(Const.EN.StatNames[i], out byte iv);
          evs.TryGetValue(Const.EN.StatNames[i], out byte ev);
          Pokemon.Stats.Add(new StatModel() {
            EN = Const.EN.StatNames[i],
            Name = EmulLang.StatNames[i],
            IV = iv,
            EV = ev,
            RealStat = realStats[i],
            Base = p[i],
          });
        }
        Pokemon.HeldItem = slot.Data.ItemHeld;
        Pokemon.Friendship = slot.Data.Friendship;
        Pokemon.IsPokerus = slot.Data.IsPokerus;
        Pokemon.PokerusDaysLeft = slot.Data.IsPokerus ? slot.Data.Pokerus & 0xF : 0;
        Pokemon.Marking1 = (slot.Markings & 0b1) != 0;
        Pokemon.Marking2 = (slot.Markings & 0b10) != 0;
        Pokemon.Marking3 = (slot.Markings & 0b100) != 0;
        Pokemon.Marking4 = (slot.Markings & 0b1000) != 0;
        Pokemon.Status = (byte)slot.Status.StatusCond >= 1 && (byte)slot.Status.StatusCond <= 7 ? StatusCondition.Sleep : slot.Status.StatusCond;
        Pokemon.EncounterLocation = EmulLang.Locations[slot.Data.MetLocation];
        Pokemon.EncounterLevel = slot.Data.MetLevel;
        if ((int)slot.Data.CaughtBall > EmulLang.Pokeball.Count) {
          Pokemon.EncounterBall = EmulLang.Pokeball[Pokeball.PokeBall];
        } else {
          Pokemon.EncounterBall = EmulLang.Pokeball[slot.Data.CaughtBall];
        }
        for (int i = 0; i < 4; i++) {
          Pokemon.Moves[i].Number = slot.Data.Moves[i];
          Pokemon.Moves[i].PP = slot.Data.PPs[i];
          Pokemon.Moves[i].PPBonus = slot.Data.PPBonus[i];
        }
        Pokemon.IsObedience = slot.Data.IsObedience;
        Pokemon.SelectedRibbons = [.. slot.Data.Ribbons.Select((r, i) => r > 0 ? (Ribbon)i : Ribbon.None).Where(r => r != Ribbon.None)];
      } else {
        UpdatePokemonLanguage(Pokemon);
      }
    }

    private byte[] GetSpeciesOrDefault(ushort speciesId) {
      if (speciesId == 0) {
        return new byte[PokemonDex.SPECIES_COUNTS];
      }

      try {
        byte[] species = dex.GetSpecies(speciesId);
        if (species.Length == PokemonDex.SPECIES_COUNTS) {
          return species;
        }
        Logger.Warning($"종족 데이터 길이가 올바르지 않습니다: Species={speciesId}, Length={species.Length}");
      } catch (Exception ex) {
        Logger.Warning($"종족 데이터 로딩 실패: Species={speciesId}, {ex.Message}");
      }

      return new byte[PokemonDex.SPECIES_COUNTS];
    }
  }
}
