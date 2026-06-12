using PokemonGen3Hack.Utils;
namespace PokemonGen3Hack.Pokemon {
  public class Status {
    private StatusCondition statusCondition;
    private byte level;
    private byte mail;
    private ushort currentHP;
    private ushort maxHP;
    private ushort attack;
    private ushort defense;
    private ushort speed;
    private ushort specialAttack;
    private ushort specialDefense;
    public StatusCondition StatusCond { get => statusCondition; }
    public byte Level { get => level; }
    public byte Mail { get => mail; }
    public ushort CurrentHP { get => currentHP; }
    public ushort MaxHP { get => maxHP; }
    public ushort Attack { get => attack; }
    public ushort Defense { get => defense; }
    public ushort Speed { get => speed; }
    public ushort SpecialAttack { get => specialAttack; }
    public ushort SpecialDefense { get => specialDefense; }
    public Status(byte[] bytes) => SetStatus(bytes);
    public void SetStatus(byte[] bytes) {
      if (bytes.Length != 20) throw new ArgumentException("bytes length must be 20");
      statusCondition = bytes[0] >= 1 && bytes[0] <= 7 ? StatusCondition.Sleep : (StatusCondition)bytes[0];
      level = bytes[4];
      mail = bytes[5];
      currentHP = Hex.ToUShort([.. bytes.Skip(6).Take(2)]);
      maxHP = Hex.ToUShort([.. bytes.Skip(8).Take(2)]);
      attack = Hex.ToUShort([.. bytes.Skip(10).Take(2)]);
      defense = Hex.ToUShort([.. bytes.Skip(12).Take(2)]);
      speed = Hex.ToUShort([.. bytes.Skip(14).Take(2)]);
      specialAttack = Hex.ToUShort([.. bytes.Skip(16).Take(2)]);
      specialDefense = Hex.ToUShort([.. bytes.Skip(18).Take(2)]);
    }
    public static Status CreateStatus(StatusCondition condition, byte level, byte mail, ushort currentHP, ushort maxHP, ushort attack, ushort defense, ushort speed, ushort specialAttack, ushort specialDefense) {
      byte[] statusBytes = new byte[20];
      statusBytes[0] = (byte)condition;
      statusBytes[4] = level;
      statusBytes[5] = mail;
      byte[] hpBytes = Hex.GetBytes(currentHP);
      statusBytes[6] = hpBytes[0];
      statusBytes[7] = hpBytes[1];
      byte[] maxHPBytes = Hex.GetBytes(maxHP);
      statusBytes[8] = maxHPBytes[0];
      statusBytes[9] = maxHPBytes[1];
      byte[] atkBytes = Hex.GetBytes(attack);
      statusBytes[10] = atkBytes[0];
      statusBytes[11] = atkBytes[1];
      byte[] defBytes = Hex.GetBytes(defense);
      statusBytes[12] = defBytes[0];
      statusBytes[13] = defBytes[1];
      byte[] spdBytes = Hex.GetBytes(speed);
      statusBytes[14] = spdBytes[0];
      statusBytes[15] = spdBytes[1];
      byte[] spaBytes = Hex.GetBytes(specialAttack);
      statusBytes[16] = spaBytes[0];
      statusBytes[17] = spaBytes[1];
      byte[] spdBytes2 = Hex.GetBytes(specialDefense);
      statusBytes[18] = spdBytes2[0];
      statusBytes[19] = spdBytes2[1];
      return new Status(statusBytes);
    }
    public byte[] Getbytes() => [
      (byte)statusCondition,
      0, 0, 0,
      level,
      mail,
      .. Hex.GetBytes(currentHP),
      .. Hex.GetBytes(maxHP),
      .. Hex.GetBytes(attack),
      .. Hex.GetBytes(defense),
      .. Hex.GetBytes(speed),
      .. Hex.GetBytes(specialAttack),
      .. Hex.GetBytes(specialDefense),
    ];
    public override string ToString() {
      return $"StatusCondition: {statusCondition}, Level: {level}, Mail: {mail}, CurrentHP: {currentHP}, MaxHP: {maxHP}, Attack: {attack}, Defense: {defense}, Speed: {speed}, SpecialAttack: {specialAttack}, SpecialDefense: {specialDefense}";
    }
  }
}
