using PokemonGen3Hack.Emulator;
using PokemonGen3Hack.Utils;

namespace PokemonGen3Hack.Pokemon {
  internal class PokemonDex(Emulator.Emulator emul) {
    private const byte SLOT_COUNTS = 100;
    public const byte SPECIES_COUNTS = 28;
    private const short TOTAL_ITEMS_COUNTS = 376;
    private const byte TMHM_DESC_COUNTS = 12;
    private byte PartyCount = 0;
    private RomProfile Profile => emul.Profile
      ?? throw new InvalidOperationException("ROM address profile has not been resolved.");
    private byte ITEM_DATA_COUNTS => Profile.ItemDataSize;
    public byte ITEM_NAME_COUNTS => Profile.ItemNameSize;
    public byte GetPartyCount(bool useCache = true) {
      if (PartyCount == 0 || !useCache) {
        byte rawCount = emul.ReadEWRAM(Profile.PartyCountOffset, 1)[0];
        if (rawCount > 6) {
          throw new InvalidOperationException($"Invalid party count: {rawCount}");
        }
        PartyCount = rawCount;
      }
      return PartyCount;
    }
    public byte[] GetPokemonData(byte slotNumber) {
      if (slotNumber < 1 || slotNumber > 6) throw new ArgumentOutOfRangeException("slotNumber must be between 1 and 6.");
      // AOB Read 상태일 땐 속도가 느려서 Timeout 10s 설정
      byte wait = 100;
      while (emul.IsWorking && wait-- > 0) Thread.Sleep(100);
      if (emul.IsWorking && wait <= 0) {
        Logger.Warning("작업이 지연되고 있습니다.");
        throw new TimeoutException("에뮬레이터 메모리 작업 시간이 초과되었습니다.");
      }
      if (slotNumber <= GetPartyCount()) {
        int slotOffset = Profile.GetSlotOffset(slotNumber);
        byte[] data = emul.ReadEWRAM(slotOffset, SLOT_COUNTS);
        if (data.Length != SLOT_COUNTS) {
          throw new InvalidOperationException($"Invalid pokemon slot data length: {data.Length}/{SLOT_COUNTS}");
        }
        return data;
      }
      throw new ArgumentOutOfRangeException(nameof(slotNumber), $"Slot {slotNumber} is outside current party count {PartyCount}.");
    }
    public byte[] GetSpecies(ushort pokemonId) => ReadTable(Profile.SpeciesOffset, pokemonId, SPECIES_COUNTS);
    public byte[] GetTMHMDescription(ushort id) => ReadTable(Profile.TMHMDescriptionOffset, id, TMHM_DESC_COUNTS);
    public byte[] GetItemData(short itemNumber) => ReadTable(Profile.ItemsOffset, itemNumber, ITEM_DATA_COUNTS);
    public byte[][] GetTMHMDescriptions() {
      // AOB Read 상태일 땐 속도가 느려서 Timeout 10s 설정
      byte wait = 100;
      while (emul.IsWorking && wait-- > 0) Thread.Sleep(100);
      if (emul.IsWorking && wait <= 0) {
        Logger.Warning("작업이 지연되고 있습니다.");
        throw new TimeoutException("에뮬레이터 메모리 작업 시간이 초과되었습니다.");
      }
      int offset = Profile.TMHMDescriptionOffset;
      byte[] items = emul.ReadPAK(offset, TMHM_DESC_COUNTS * ITEM_DATA_COUNTS);
      byte[][] result = new byte[TOTAL_ITEMS_COUNTS][];
      for (int i = 0; i < TOTAL_ITEMS_COUNTS; i++) {
        int j = i * ITEM_DATA_COUNTS;
        result[i] = [.. items.Take(new Range(j, ITEM_DATA_COUNTS + j))];
      }
      // 이름 필드 영어: 14바이트, 한글: 18바이트. 아이템 번호 영어[14] 한글[18]
      return result;
    }
    public byte[][] GetItemsData() {
      // AOB Read 상태일 땐 속도가 느려서 Timeout 10s 설정
      byte wait = 100;
      while (emul.IsWorking && wait-- > 0) Thread.Sleep(100);
      if (emul.IsWorking && wait <= 0) {
        Logger.Warning("작업이 지연되고 있습니다.");
        throw new TimeoutException("에뮬레이터 메모리 작업 시간이 초과되었습니다.");
      }
      int itemOffset = Profile.ItemsOffset;
      byte[] items = emul.ReadPAK(itemOffset, TOTAL_ITEMS_COUNTS * ITEM_DATA_COUNTS);
      byte[][] result = new byte[TOTAL_ITEMS_COUNTS][];
      for (int i = 0; i < TOTAL_ITEMS_COUNTS; i++) {
        int j = i * ITEM_DATA_COUNTS;
        result[i] = [.. items.Take(new Range(j, ITEM_DATA_COUNTS + j))];
      }
      // 이름 필드 영어: 14바이트, 한글: 18바이트. 아이템 번호 영어[14] 한글[18]
      return result;
    }
    private byte[] ReadTable(int baseOffset, int id, int itemSize) {
      if (id < 1) {
        throw new ArgumentOutOfRangeException(nameof(id), $"Table id must be greater than 0: {id}");
      }
      return emul.ReadPAK(baseOffset + (id - 1) * itemSize, itemSize);
    }
    public bool WritePokemonData(byte slotNumber, byte[] data) {
      if (slotNumber < 1 || slotNumber > 6)
        throw new ArgumentOutOfRangeException("slotNumber must be between 1 and 6.");
      if (data.Length != SLOT_COUNTS)
        throw new ArgumentException($"Data must be exactly {SLOT_COUNTS} bytes");
      byte wait = 100;
      while (emul.IsWorking && wait-- > 0) Thread.Sleep(100);
      if (emul.IsWorking && wait <= 0) {
        Logger.Warning("작업이 지연되고 있습니다.");
        return false;
      }
      if (slotNumber <= GetPartyCount()) {
        int slotOffset = Profile.GetSlotOffset(slotNumber);
        return emul.WriteEWRAM(slotOffset, data);
      }
      return false;
    }

    public string GetPocketmonImage(ushort id, uint personality = 0, bool isEgg = false) =>
      ImageUtils.GetImageBase64Url(RomPokemonIcon.ReadPng(emul.ReadPAK,
        Profile.MonIconTableOffset, id, personality, isEgg));
  }
}
