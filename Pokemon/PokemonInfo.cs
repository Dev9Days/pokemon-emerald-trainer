using PokemonGen3Hack.Utils;

namespace PokemonGen3Hack.Pokemon {
  public class PokemonInfo {

    private Personality personality;
    private ushort pid;
    private ushort tid;
    private ushort sid;
    private byte[] nickname;
    private PokemonLanguage language;
    private byte misc;
    private byte[] otname;
    private byte markings;
    private ushort checksum;
    private DataStructure data;
    private Status status;
    private byte[] orgData;
    public ushort PID { get => pid; }
    public string Image { get; set; }
    public GenderGroup GenderGroup { get; set; }
    public Personality Personality { get => personality; }
    public ushort TID { get => tid; }
    public ushort SID { get => sid; }
    public string Nickname { get => CharDecoder.Encode(nickname, true); }
    public PokemonLanguage Language { get => language; }
    public byte Misc { get => misc; }
    public string OTName { get => CharDecoder.Encode(otname, true); }
    public byte Markings { get => markings; }
    public DataStructure Data { get => data; }
    public Status Status { get => status; }

    public PokemonInfo(byte[] datas) => SetPokemonData(datas);
    public void SetPokemonData(byte[] data) {
      orgData = data;
      tid = Hex.ToUShort([.. data.Skip(4).Take(2)]);
      sid = Hex.ToUShort([.. data.Skip(6).Take(2)]);
      nickname = [.. data.Skip(8).Take(10)];
      language = (PokemonLanguage)data[18];
      misc = data[19];
      otname = [.. data.Skip(20).Take(7)];
      markings = data[27];
      checksum = Hex.ToUShort([.. data.Skip(28).Take(2)]);
      this.data = new DataStructure([.. data.Take(4)], tid, sid, [.. data.Skip(32).Take(48)]);
      pid = Hex.ToUShort(this.data.Species);
      status = new Status([.. data.Skip(80).Take(20)]);
    }
    public void SetPersonality(GenderGroup threshold) {
      GenderGroup = threshold;
      personality = new Personality([.. orgData.Take(4)], threshold, tid, sid);
    }
    public byte[] GetPokemonData() => [
      ..personality.GetBytes(),
      ..Hex.GetBytes(tid),
      ..Hex.GetBytes(sid),
      ..nickname,
      (byte)language,
      misc,
      ..otname,
      markings,
      ..data.CalcChecksum(),
      0, 0,
      ..data.GetEncryptData(),
      ..status.Getbytes(),
    ];
    public void ChangeGender(Gender gender) => Data.ChangeSubOrder(personality.SetGender(gender).GetBytes());
    public void ChangeNature(Nature nature) => Data.ChangeSubOrder(personality.SetNature(nature).GetBytes());
    public void SetShiny() => Data.ChangeSubOrder(personality.SetShiny().GetBytes());
    public void SetNickname(string nicknameText) {
      byte[] encoded = CharDecoder.Decode(nicknameText);
      nickname = new byte[10];
      Array.Copy(encoded, nickname, Math.Min(encoded.Length, 10));
      if (encoded.Length < 10) {
        nickname[encoded.Length] = 0xFF;
      }
    }
    public void SetOTName(string otNameText) {
      byte[] encoded = CharDecoder.Decode(otNameText);
      otname = new byte[7];
      Array.Copy(encoded, otname, Math.Min(encoded.Length, 7));
      if (encoded.Length < 7) {
        otname[encoded.Length] = 0xFF;
      }
    }
    public void SetMarkings(bool m1, bool m2, bool m3, bool m4) {
      markings = (byte)(
        (m1 ? 0b1 : 0) |
        (m2 ? 0b10 : 0) |
        (m3 ? 0b100 : 0) |
        (m4 ? 0b1000 : 0)
      );
    }
    public void SetStatus(Status newStatus) => status = newStatus;
    public bool CanGenerateMethod1PID(Nature nature, Gender gender, bool isShiny, Dictionary<string, byte> targetIVs)
      => personality.CanGenerateMethod1PID(nature, gender, isShiny, targetIVs);
    public bool RegeneratePID(Nature nature, Gender gender, bool isShiny, Dictionary<string, byte> targetIVs, bool allowIllegalFallback) {
      bool isLegal = personality.TryUpdatePersonality(nature, gender, isShiny, targetIVs, allowIllegalFallback);
      Data.UpdateKey(personality.PID, tid, sid);
      Data.ChangeSubOrder(personality.GetBytes());
      return isLegal;
    }
  }
}
