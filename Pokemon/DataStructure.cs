using PokemonGen3Hack.Utils;

namespace PokemonGen3Hack.Pokemon {
  public class DataStructure {
    private class Growth {
      public ushort Species { get; set; }
      public ushort ItemHeld { get; set; }
      public uint Experience { get; set; }
      public byte PPBonuse1 { get; set; }
      public byte PPBonuse2 { get; set; }
      public byte PPBonuse3 { get; set; }
      public byte PPBonuse4 { get; set; }
      public byte PPBonuses { get => (byte)(PPBonuse4 << 6 | PPBonuse3 << 4 | PPBonuse2 << 2 | PPBonuse1); }
      public byte Friendship { get; set; }
      private byte[] unused = [0, 0];
      public Growth(byte[] data) => SetGrowth(data);
      public void SetGrowth(byte[] data) {
        Species = Hex.ToUShort([.. data.Take(2)]);
        ItemHeld = Hex.ToUShort([.. data.Skip(2).Take(2)]);
        Experience = Hex.ToUInt([.. data.Skip(4).Take(4)]);
        PPBonuse1 = (byte)(data[8] & 0b11);
        PPBonuse2 = (byte)((data[8] >> 2) & 0b11);
        PPBonuse3 = (byte)((data[8] >> 4) & 0b11);
        PPBonuse4 = (byte)((data[8] >> 6) & 0b11);
        Friendship = data[9];
      }
      public byte[] GetBytes() => [
        ..Hex.GetBytes(Species),
        ..Hex.GetBytes(ItemHeld),
        ..Hex.GetBytes(Experience),
        PPBonuses,
        Friendship,
        ..unused
      ];
    }
    private class Attacks {
      public ushort Move1 { get; set; }
      public ushort Move2 { get; set; }
      public ushort Move3 { get; set; }
      public ushort Move4 { get; set; }
      public byte PP1 { get; set; }
      public byte PP2 { get; set; }
      public byte PP3 { get; set; }
      public byte PP4 { get; set; }
      public Attacks(byte[] data) => SetAttacks(data);
      public void SetAttacks(byte[] data) {
        Move1 = Hex.ToUShort([.. data.Take(2)]);
        Move2 = Hex.ToUShort([.. data.Skip(2).Take(2)]);
        Move3 = Hex.ToUShort([.. data.Skip(4).Take(2)]);
        Move4 = Hex.ToUShort([.. data.Skip(6).Take(2)]);
        PP1 = data[8];
        PP2 = data[9];
        PP3 = data[10];
        PP4 = data[11];
      }
      public byte[] GetBytes() => [
        ..Hex.GetBytes(Move1),
        ..Hex.GetBytes(Move2),
        ..Hex.GetBytes(Move3),
        ..Hex.GetBytes(Move4),
        PP1, PP2, PP3, PP4
      ];
    }
    private class EVsAndCondition {
      public byte HP { get; set; }
      public byte Attack { get; set; }
      public byte Defense { get; set; }
      public byte Speed { get; set; }
      public byte SpecialAttack { get; set; }
      public byte SpecialDefense { get; set; }
      public byte Coolness { get; set; }
      public byte Beauty { get; set; }
      public byte Cuteness { get; set; }
      public byte Smartness { get; set; }
      public byte Toughness { get; set; }
      public byte Feel { get; set; }
      public EVsAndCondition(byte[] data) => SetEVsAndCondition(data);
      public void SetEVsAndCondition(byte[] data) {
        HP = data[0];
        Attack = data[1];
        Defense = data[2];
        Speed = data[3];
        SpecialAttack = data[4];
        SpecialDefense = data[5];
        Coolness = data[6];
        Beauty = data[7];
        Cuteness = data[8];
        Smartness = data[9];
        Toughness = data[10];
        Feel = data[11];
      }
      public Dictionary<string, byte> GetEVs() {
        return new Dictionary<string, byte>{
          { "HP", HP },
          { "Attack", Attack },
          { "Defense", Defense },
          { "Speed", Speed },
          { "SpecialAttack", SpecialAttack },
          { "SpecialDefense", SpecialDefense },
        };
      }
      public byte[] GetBytes() => [
        HP,
        Attack,
        Defense,
        Speed,
        SpecialAttack,
        SpecialDefense,
        Coolness,
        Beauty,
        Cuteness,
        Smartness,
        Toughness,
        Feel,
      ];
    }
    private class Miscellaneous {
      public byte Pokerus { get; set; }
      public bool IsPokerus { get => Pokerus != 0; }
      public bool IsCured { get => IsPokerus && (Pokerus & 0xF) == 0; }
      public byte MetLocation { get; set; }
      public ushort OriginsInfo { get; set; }
      public byte MetLevel { get => (byte)(OriginsInfo & 0b1111111); } // 0이면 부화
      public GameVersion MetVersion { get => (GameVersion)((OriginsInfo >> 7) & 0xF); }
      public Pokeball CaughtBall { get => (Pokeball)((OriginsInfo >> 11) & 0xF); }
      public TrainerGender TrainerGender { get => (TrainerGender)((OriginsInfo >> 15) & 0b1); }
      public uint IVs { get; set; }
      public uint RibbonsAndObedience { get; set; }
      // Cool ~ Tough는 0 ~ 4의 값을 가질 수 있음
      public byte RibbonCool { get => (byte)(RibbonsAndObedience & 0b111); }
      public byte RibbonBeauty { get => (byte)((RibbonsAndObedience >> 3) & 0b111); }
      public byte RibbonCute { get => (byte)((RibbonsAndObedience >> 6) & 0b111); }
      public byte RibbonSmart { get => (byte)((RibbonsAndObedience >> 9) & 0b111); }
      public byte RibbonTough { get => (byte)((RibbonsAndObedience >> 12) & 0b111); }
      public byte RibbonChampion { get => (byte)((RibbonsAndObedience >> 15) & 0b1); }
      public byte RibbonWinning { get => (byte)((RibbonsAndObedience >> 16) & 0b1); }
      public byte RibbonVictory { get => (byte)((RibbonsAndObedience >> 17) & 0b1); }
      public byte RibbonArtist { get => (byte)((RibbonsAndObedience >> 18) & 0b1); }
      public byte RibbonEffort { get => (byte)((RibbonsAndObedience >> 19) & 0b1); }
      public byte RibbonBattleChampion { get => (byte)((RibbonsAndObedience >> 20) & 0b1); }
      public byte RibbonRegionalChampion { get => (byte)((RibbonsAndObedience >> 21) & 0b1); }
      public byte RibbonNationalChampion { get => (byte)((RibbonsAndObedience >> 22) & 0b1); }
      public byte RibbonCountry { get => (byte)((RibbonsAndObedience >> 23) & 0b1); }
      public byte RibbonNational { get => (byte)((RibbonsAndObedience >> 24) & 0b1); }
      public byte RibbonEarth { get => (byte)((RibbonsAndObedience >> 25) & 0b1); }
      public byte RibbonWorld { get => (byte)((RibbonsAndObedience >> 26) & 0b1); }
      // 뮤, 테오키스 복종여부. 0이면 불복종, 1이면 복종이고 1인 상태에서 이후 세대로 넘어가면 운명적인 만남이 됨.
      public byte Obedience { get => (byte)((RibbonsAndObedience >> 31) & 0b1); }
      public Miscellaneous(byte[] data) => SetMiscellaneous(data);
      public void SetMiscellaneous(byte[] data) {
        Pokerus = data[0];
        MetLocation = data[1];
        OriginsInfo = Hex.ToUShort([.. data.Skip(2).Take(2)]);
        IVs = Hex.ToUInt([.. data.Skip(4).Take(4)]);
        RibbonsAndObedience = Hex.ToUInt([.. data.Skip(8).Take(4)]);
      }
      public Dictionary<string, byte> GetIVs() {
        byte hp = (byte)(IVs & 0b11111);
        byte attack = (byte)((IVs >> 5) & 0b11111);
        byte defense = (byte)((IVs >> 10) & 0b11111);
        byte speed = (byte)((IVs >> 15) & 0b11111);
        byte specialAttack = (byte)((IVs >> 20) & 0b11111);
        byte specialDefense = (byte)((IVs >> 25) & 0b11111);
        byte egg = (byte)((IVs >> 30) & 0b1);
        byte ability = (byte)((IVs >> 31) & 0b1);

        byte[] ivs = [hp, attack, defense, speed, specialAttack, specialDefense];
        byte typeFlag = 0;
        byte valueFlag = 0;
        for (int i = 0; i < 6; i++) {
          int iv = ivs[i];
          int bit = 1 << i;
          if ((iv & 1) != 0)
            typeFlag |= (byte)bit;
          if (((iv & 3) >> 1) != 0) // % 4 해서 나머지가 2 or 3인 것을 비트 연산으로 변환한 것
            valueFlag |= (byte)bit;
        }
        byte hiddenPowerType = (byte)(typeFlag * 15 / 63);
        byte hiddenPowerValue = (byte)(valueFlag * 40 / 63 + 30);

        return new Dictionary<string, byte>{
          { "HP", hp },
          { "Attack", attack },
          { "Defense", defense },
          { "Speed", speed },
          { "SpecialAttack", specialAttack },
          { "SpecialDefense", specialDefense },
          { "Egg", egg },
          { "Ability", ability },
          { "HiddenPowerType", hiddenPowerType },
          { "HiddenPowerValue", hiddenPowerValue }
        };
      }
      public byte[] GetBytes() => [
        Pokerus,
        MetLocation,
        ..Hex.GetBytes(OriginsInfo),
        ..Hex.GetBytes(IVs),
        ..Hex.GetBytes(RibbonsAndObedience)
      ];
    }

    private Growth G;
    private Attacks A;
    private EVsAndCondition E;
    private Miscellaneous M;
    private SubstructureOrder subOrder;
    private byte[] key;

    public DataStructure(byte[] pv, ushort tid, ushort sid, byte[] datas) => SetDataStructure(pv, tid, sid, datas);
    public void SetDataStructure(byte[] pv, ushort tid, ushort sid, byte[] datas) {
      key = [
        (byte)(pv[0] ^ (tid & 0xFF)),
        (byte)(pv[1] ^ ((tid & 0xFF00) >>> 8)),
        (byte)(pv[2] ^ (sid & 0xFF)),
        (byte)(pv[3] ^ ((sid & 0xFF00) >>> 8)),
      ];
      ChangeSubOrder(pv);
      byte[] decrypted = GetXORData(datas, key);
      byte[][] tempData = [
        [.. decrypted.Take(12)],
        [.. decrypted.Skip(12).Take(12)],
        [.. decrypted.Skip(24).Take(12)],
        [.. decrypted.Skip(36).Take(12)],
      ];
      switch (subOrder) {
        case SubstructureOrder.GAEM: G = new Growth(tempData[0]); A = new Attacks(tempData[1]); E = new EVsAndCondition(tempData[2]); M = new Miscellaneous(tempData[3]); break;
        case SubstructureOrder.GAME: G = new Growth(tempData[0]); A = new Attacks(tempData[1]); E = new EVsAndCondition(tempData[3]); M = new Miscellaneous(tempData[2]); break;
        case SubstructureOrder.GEAM: G = new Growth(tempData[0]); A = new Attacks(tempData[2]); E = new EVsAndCondition(tempData[1]); M = new Miscellaneous(tempData[3]); break;
        case SubstructureOrder.GEMA: G = new Growth(tempData[0]); A = new Attacks(tempData[3]); E = new EVsAndCondition(tempData[1]); M = new Miscellaneous(tempData[2]); break;
        case SubstructureOrder.GMAE: G = new Growth(tempData[0]); A = new Attacks(tempData[2]); E = new EVsAndCondition(tempData[3]); M = new Miscellaneous(tempData[1]); break;
        case SubstructureOrder.GMEA: G = new Growth(tempData[0]); A = new Attacks(tempData[3]); E = new EVsAndCondition(tempData[2]); M = new Miscellaneous(tempData[1]); break;
        case SubstructureOrder.AGEM: G = new Growth(tempData[1]); A = new Attacks(tempData[0]); E = new EVsAndCondition(tempData[2]); M = new Miscellaneous(tempData[3]); break;
        case SubstructureOrder.AGME: G = new Growth(tempData[1]); A = new Attacks(tempData[0]); E = new EVsAndCondition(tempData[3]); M = new Miscellaneous(tempData[2]); break;
        case SubstructureOrder.AEGM: G = new Growth(tempData[2]); A = new Attacks(tempData[0]); E = new EVsAndCondition(tempData[1]); M = new Miscellaneous(tempData[3]); break;
        case SubstructureOrder.AEMG: G = new Growth(tempData[3]); A = new Attacks(tempData[0]); E = new EVsAndCondition(tempData[1]); M = new Miscellaneous(tempData[2]); break;
        case SubstructureOrder.AMGE: G = new Growth(tempData[2]); A = new Attacks(tempData[0]); E = new EVsAndCondition(tempData[3]); M = new Miscellaneous(tempData[1]); break;
        case SubstructureOrder.AMEG: G = new Growth(tempData[3]); A = new Attacks(tempData[0]); E = new EVsAndCondition(tempData[2]); M = new Miscellaneous(tempData[1]); break;
        case SubstructureOrder.EGAM: G = new Growth(tempData[1]); A = new Attacks(tempData[2]); E = new EVsAndCondition(tempData[0]); M = new Miscellaneous(tempData[3]); break;
        case SubstructureOrder.EGMA: G = new Growth(tempData[1]); A = new Attacks(tempData[3]); E = new EVsAndCondition(tempData[0]); M = new Miscellaneous(tempData[2]); break;
        case SubstructureOrder.EAGM: G = new Growth(tempData[2]); A = new Attacks(tempData[1]); E = new EVsAndCondition(tempData[0]); M = new Miscellaneous(tempData[3]); break;
        case SubstructureOrder.EAMG: G = new Growth(tempData[3]); A = new Attacks(tempData[1]); E = new EVsAndCondition(tempData[0]); M = new Miscellaneous(tempData[2]); break;
        case SubstructureOrder.EMGA: G = new Growth(tempData[2]); A = new Attacks(tempData[3]); E = new EVsAndCondition(tempData[0]); M = new Miscellaneous(tempData[1]); break;
        case SubstructureOrder.EMAG: G = new Growth(tempData[3]); A = new Attacks(tempData[2]); E = new EVsAndCondition(tempData[0]); M = new Miscellaneous(tempData[1]); break;
        case SubstructureOrder.MGAE: G = new Growth(tempData[1]); A = new Attacks(tempData[2]); E = new EVsAndCondition(tempData[3]); M = new Miscellaneous(tempData[0]); break;
        case SubstructureOrder.MGEA: G = new Growth(tempData[1]); A = new Attacks(tempData[3]); E = new EVsAndCondition(tempData[2]); M = new Miscellaneous(tempData[0]); break;
        case SubstructureOrder.MAGE: G = new Growth(tempData[2]); A = new Attacks(tempData[1]); E = new EVsAndCondition(tempData[3]); M = new Miscellaneous(tempData[0]); break;
        case SubstructureOrder.MAEG: G = new Growth(tempData[3]); A = new Attacks(tempData[1]); E = new EVsAndCondition(tempData[2]); M = new Miscellaneous(tempData[0]); break;
        case SubstructureOrder.MEGA: G = new Growth(tempData[2]); A = new Attacks(tempData[3]); E = new EVsAndCondition(tempData[1]); M = new Miscellaneous(tempData[0]); break;
        case SubstructureOrder.MEAG: G = new Growth(tempData[3]); A = new Attacks(tempData[2]); E = new EVsAndCondition(tempData[1]); M = new Miscellaneous(tempData[0]); break;
      }
    }
    public DataStructure ChangeSubOrder(byte[] pid) {
      uint uPid = Hex.ToUInt(pid);
      subOrder = (SubstructureOrder)(uPid % 24);
      return this;
    }
    private static byte[] GetXORData(byte[] datas, byte[] key) {
      byte[] result = new byte[datas.Length];
      for (byte i = 0; i < 12; i++) {
        byte j = (byte)(i * 4);
        result[j + 0] = (byte)(datas[j + 0] ^ key[0]);
        result[j + 1] = (byte)(datas[j + 1] ^ key[1]);
        result[j + 2] = (byte)(datas[j + 2] ^ key[2]);
        result[j + 3] = (byte)(datas[j + 3] ^ key[3]);
      }
      return result;
    }
    public byte[] GetEncryptData() {
      byte[] encrypt = GetXORData([.. G.GetBytes(), .. A.GetBytes(), .. E.GetBytes(), .. M.GetBytes()], key);
      byte[] EG = [.. encrypt.Take(12)];
      byte[] EA = [.. encrypt.Skip(12).Take(12)];
      byte[] EE = [.. encrypt.Skip(24).Take(12)];
      byte[] EM = [.. encrypt.Skip(36).Take(12)];
      return subOrder switch {
        SubstructureOrder.GAEM => [.. EG, .. EA, .. EE, .. EM],
        SubstructureOrder.GAME => [.. EG, .. EA, .. EM, .. EE],
        SubstructureOrder.GEAM => [.. EG, .. EE, .. EA, .. EM],
        SubstructureOrder.GEMA => [.. EG, .. EE, .. EM, .. EA],
        SubstructureOrder.GMAE => [.. EG, .. EM, .. EA, .. EE],
        SubstructureOrder.GMEA => [.. EG, .. EM, .. EE, .. EA],
        SubstructureOrder.AGEM => [.. EA, .. EG, .. EE, .. EM],
        SubstructureOrder.AGME => [.. EA, .. EG, .. EM, .. EE],
        SubstructureOrder.AEGM => [.. EA, .. EE, .. EG, .. EM],
        SubstructureOrder.AEMG => [.. EA, .. EE, .. EM, .. EG],
        SubstructureOrder.AMGE => [.. EA, .. EM, .. EG, .. EE],
        SubstructureOrder.AMEG => [.. EA, .. EM, .. EE, .. EG],
        SubstructureOrder.EGAM => [.. EE, .. EG, .. EA, .. EM],
        SubstructureOrder.EGMA => [.. EE, .. EG, .. EM, .. EA],
        SubstructureOrder.EAGM => [.. EE, .. EA, .. EG, .. EM],
        SubstructureOrder.EAMG => [.. EE, .. EA, .. EM, .. EG],
        SubstructureOrder.EMGA => [.. EE, .. EM, .. EG, .. EA],
        SubstructureOrder.EMAG => [.. EE, .. EM, .. EA, .. EG],
        SubstructureOrder.MGAE => [.. EM, .. EG, .. EA, .. EE],
        SubstructureOrder.MGEA => [.. EM, .. EG, .. EE, .. EA],
        SubstructureOrder.MAGE => [.. EM, .. EA, .. EG, .. EE],
        SubstructureOrder.MAEG => [.. EM, .. EA, .. EE, .. EG],
        SubstructureOrder.MEGA => [.. EM, .. EE, .. EG, .. EA],
        SubstructureOrder.MEAG => [.. EM, .. EE, .. EA, .. EG],
        _ => throw new Exception(),
      };
    }
    public void UpdateKey(byte[] newKey, ushort tid, ushort sid) {
      key = [
        (byte)(newKey[0] ^ (tid & 0xFF)),
        (byte)(newKey[1] ^ ((tid & 0xFF00) >>> 8)),
        (byte)(newKey[2] ^ (sid & 0xFF)),
        (byte)(newKey[3] ^ ((sid & 0xFF00) >>> 8)),
      ];
    }
    public byte[] Species => Hex.GetBytes(G.Species);
    public ushort ItemHeld => G.ItemHeld;
    public uint Experience => G.Experience;
    public byte Friendship => G.Friendship;
    public Dictionary<string, byte> IVs => M.GetIVs();
    public Dictionary<string, byte> EVs => E.GetEVs();
    public byte[] Ribbons {
      get => [
        M.RibbonCool,
        M.RibbonBeauty,
        M.RibbonCute,
        M.RibbonSmart,
        M.RibbonTough,
        M.RibbonChampion,
        M.RibbonWinning,
        M.RibbonVictory,
        M.RibbonArtist,
        M.RibbonEffort,
        M.RibbonBattleChampion,
        M.RibbonRegionalChampion,
        M.RibbonNationalChampion,
        M.RibbonCountry,
        M.RibbonNational,
        M.RibbonEarth,
        M.RibbonWorld,
      ];
    }
    public bool IsObedience => M.Obedience == 1;
    public ushort[] Moves => [A.Move1, A.Move2, A.Move3, A.Move4];
    public byte[] PPs => [A.PP1, A.PP2, A.PP3, A.PP4];
    public byte[] PPBonus => [G.PPBonuse1, G.PPBonuse2, G.PPBonuse3, G.PPBonuse4];
    public bool IsPokerus => M.IsPokerus;
    public bool IsCured => M.IsCured;
    public byte Pokerus => M.Pokerus;
    public byte MetLocation => M.MetLocation;
    public Pokeball CaughtBall => M.CaughtBall;
    public byte MetLevel => M.MetLevel;
    private ushort Sum(byte[] bytes) {
      ushort sum = 0;
      for (int i = 0; i < bytes.Length; i += 2) {
        sum += (ushort)((bytes[i + 1] << 8) | bytes[i]);
      }
      return sum;
    }
    public byte[] CalcChecksum() {
      ushort temp = (ushort)((Sum(G.GetBytes()) + Sum(A.GetBytes()) + Sum(E.GetBytes()) + Sum(M.GetBytes())) & 0xFFFF);
      return Hex.GetBytes(temp);
    }
    public void SetExperience(uint experience) => G.Experience = experience;
    public void SetFriendship(byte friendship) => G.Friendship = friendship;
    public void SetItemHeld(ushort itemId) => G.ItemHeld = itemId;
    public void SetMoves(ushort[] moves) {
      if (moves.Length != 4) throw new ArgumentException("Must provide exactly 4 moves");
      A.Move1 = moves[0];
      A.Move2 = moves[1];
      A.Move3 = moves[2];
      A.Move4 = moves[3];
    }
    public void SetPPs(byte[] pps) {
      if (pps.Length != 4) throw new ArgumentException("Must provide exactly 4 PP values");
      A.PP1 = pps[0];
      A.PP2 = pps[1];
      A.PP3 = pps[2];
      A.PP4 = pps[3];
    }
    public void SetPPBonuses(byte[] bonuses) {
      if (bonuses.Length != 4) throw new ArgumentException("Must provide exactly 4 PP bonus values");
      G.PPBonuse1 = bonuses[0];
      G.PPBonuse2 = bonuses[1];
      G.PPBonuse3 = bonuses[2];
      G.PPBonuse4 = bonuses[3];
    }
    public void SetEVs(Dictionary<string, byte> evs) {
      if (evs.TryGetValue("HP", out byte hp)) E.HP = hp;
      if (evs.TryGetValue("Attack", out byte attack)) E.Attack = attack;
      if (evs.TryGetValue("Defense", out byte defense)) E.Defense = defense;
      if (evs.TryGetValue("Speed", out byte speed)) E.Speed = speed;
      if (evs.TryGetValue("SpecialAttack", out byte spa)) E.SpecialAttack = spa;
      if (evs.TryGetValue("SpecialDefense", out byte spd)) E.SpecialDefense = spd;
    }
    public void SetIVs(Dictionary<string, byte> ivs, byte? ability = null) {
      uint flags = M.IVs & 0x40000000;
      if ((ability ?? (byte)((M.IVs >> 31) & 1)) != 0) {
        flags |= 0x80000000;
      }
      byte hp = ivs.TryGetValue("HP", out byte hpVal) ? hpVal : (byte)0;
      byte attack = ivs.TryGetValue("Attack", out byte atkVal) ? atkVal : (byte)0;
      byte defense = ivs.TryGetValue("Defense", out byte defVal) ? defVal : (byte)0;
      byte speed = ivs.TryGetValue("Speed", out byte speedVal) ? speedVal : (byte)0;
      byte spa = ivs.TryGetValue("SpecialAttack", out byte spaVal) ? spaVal : (byte)0;
      byte spd = ivs.TryGetValue("SpecialDefense", out byte spdVal) ? spdVal : (byte)0;
      uint ivsValue = flags | (uint)(
        (hp & 0x1F) |
        ((attack & 0x1F) << 5) |
        ((defense & 0x1F) << 10) |
        ((speed & 0x1F) << 15) |
        ((spa & 0x1F) << 20) |
        ((spd & 0x1F) << 25)
      );
      M.IVs = ivsValue;
    }
    public void SetPokerus(byte pokerusValue) => M.Pokerus = pokerusValue;
    public void SetObedience(bool isObedient) {
      uint ribbons = M.RibbonsAndObedience;
      if (isObedient) {
        ribbons |= 0x80000000;
      } else {
        ribbons &= 0x7FFFFFFF;
      }
      M.RibbonsAndObedience = ribbons;
    }
    public void SetRibbons(List<Ribbon> selectedRibbons) {
      uint ribbons = M.RibbonsAndObedience & 0x80000000;
      foreach (var ribbon in selectedRibbons) {
        switch (ribbon) {
          case Ribbon.Cool: ribbons |= 0x1; break;
          case Ribbon.Beauty: ribbons |= 0x8; break;
          case Ribbon.Cute: ribbons |= 0x40; break;
          case Ribbon.Smart: ribbons |= 0x200; break;
          case Ribbon.Tough: ribbons |= 0x1000; break;
          case Ribbon.Champion: ribbons |= 0x8000; break;
          case Ribbon.Winning: ribbons |= 0x10000; break;
          case Ribbon.Victory: ribbons |= 0x20000; break;
          case Ribbon.Artist: ribbons |= 0x40000; break;
          case Ribbon.Effort: ribbons |= 0x80000; break;
          case Ribbon.BattleChampion: ribbons |= 0x100000; break;
          case Ribbon.RegionalChampion: ribbons |= 0x200000; break;
          case Ribbon.NationalChampion: ribbons |= 0x400000; break;
          case Ribbon.Country: ribbons |= 0x800000; break;
          case Ribbon.National: ribbons |= 0x1000000; break;
          case Ribbon.Earth: ribbons |= 0x2000000; break;
          case Ribbon.World: ribbons |= 0x4000000; break;
        }
      }
      M.RibbonsAndObedience = ribbons;
    }
  }
}
