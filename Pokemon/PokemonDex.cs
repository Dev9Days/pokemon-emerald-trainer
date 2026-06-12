using PokemonGen3Hack.Emulator;
using PokemonGen3Hack.Utils;

namespace PokemonGen3Hack.Pokemon {
  internal class PokemonDex(Emulator.Emulator emul) {
    private const string IMG_PATH = "https://archives.bulbagarden.net/media/upload/";
    private readonly Dictionary<string, int> En = new() {
      {"PARTY_COUNT", 0x244E9},
      {"SLOT_1", 0x244EC},
      {"SLOT_2", 0x24550},
      {"SLOT_3", 0x245B4},
      {"SLOT_4", 0x24618},
      {"SLOT_5", 0x2467C},
      {"SLOT_6", 0x246E0},
      {"SPECIES", 0x3203E8},
      {"ITEMS", 0x5839CC}, // 44 bytes
      {"TMHM_DESC", 0x300698},
    };
    private readonly Dictionary<string, int> Ko = new() {
      {"PARTY_COUNT", 0x24649},
      {"SLOT_1", 0x2464C},
      {"SLOT_2", 0x246B0},
      {"SLOT_3", 0x24714},
      {"SLOT_4", 0x24778},
      {"SLOT_5", 0x247DC},
      {"SLOT_6", 0x24840},
      {"SPECIES", 0x3041DC},
      {"ITEMS", 0x562E44}, // 48 bytes
      {"TMHM_DESC", 0x300698},
    };
    private const byte SLOT_COUNTS = 100;
    public const byte SPECIES_COUNTS = 28;
    private const short TOTAL_ITEMS_COUNTS = 376;
    private const byte TMHM_DESC_COUNTS = 12;
    private byte PartyCount = 0;
    private byte ITEM_DATA_COUNTS { get => (byte)(emul.Lang is Language.English ? 44 : 48); }
    public byte ITEM_NAME_COUNTS { get => (byte)(emul.Lang is Language.English ? 14 : 18); }
    private Dictionary<string, int> OffsetList { get => emul.Lang is Language.English ? En : Ko; }
    public byte GetPartyCount(bool useCache = true) {
      if (PartyCount == 0 || !useCache) {
        OffsetList.TryGetValue("PARTY_COUNT", out int countOffset);
        byte rawCount = emul.ReadEWRAM(countOffset, 1)[0];
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
        OffsetList.TryGetValue($"SLOT_{slotNumber}", out int slotOffset);
        byte[] data = emul.ReadEWRAM(slotOffset, SLOT_COUNTS);
        if (data.Length != SLOT_COUNTS) {
          throw new InvalidOperationException($"Invalid pokemon slot data length: {data.Length}/{SLOT_COUNTS}");
        }
        return data;
      }
      throw new ArgumentOutOfRangeException(nameof(slotNumber), $"Slot {slotNumber} is outside current party count {PartyCount}.");
    }
    public byte[] GetSpecies(ushort pokemonId) => ReadTable("SPECIES", pokemonId, SPECIES_COUNTS);
    public byte[] GetTMHMDescription(ushort id) => ReadTable("TMHM_DESC", id, TMHM_DESC_COUNTS);
    public byte[] GetItemData(short itemNumber) => ReadTable("ITEMS", itemNumber, ITEM_DATA_COUNTS);
    public byte[][] GetTMHMDescriptions() {
      // AOB Read 상태일 땐 속도가 느려서 Timeout 10s 설정
      byte wait = 100;
      while (emul.IsWorking && wait-- > 0) Thread.Sleep(100);
      if (emul.IsWorking && wait <= 0) {
        Logger.Warning("작업이 지연되고 있습니다.");
        throw new TimeoutException("에뮬레이터 메모리 작업 시간이 초과되었습니다.");
      }
      OffsetList.TryGetValue("TMHM_DESC", out int offset);
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
      OffsetList.TryGetValue("ITEMS", out int itemOffset);
      byte[] items = emul.ReadPAK(itemOffset, TOTAL_ITEMS_COUNTS * ITEM_DATA_COUNTS);
      byte[][] result = new byte[TOTAL_ITEMS_COUNTS][];
      for (int i = 0; i < TOTAL_ITEMS_COUNTS; i++) {
        int j = i * ITEM_DATA_COUNTS;
        result[i] = [.. items.Take(new Range(j, ITEM_DATA_COUNTS + j))];
      }
      // 이름 필드 영어: 14바이트, 한글: 18바이트. 아이템 번호 영어[14] 한글[18]
      return result;
    }
    private byte[] ReadTable(string key, int id, int itemSize) {
      if (id < 1) {
        throw new ArgumentOutOfRangeException(nameof(id), $"Table id must be greater than 0: {id}");
      }
      if (!OffsetList.TryGetValue(key, out int baseOffset)) {
        throw new KeyNotFoundException($"Offset not found: {key}");
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
        OffsetList.TryGetValue($"SLOT_{slotNumber}", out int slotOffset);
        return emul.WriteEWRAM(slotOffset, data);
      }
      return false;
    }

    public static async Task<string> GetPocketmonImage(ushort id) => ImageUtils.GetImageBase64Url(await ImageUtils.LoadImageFromUrlAsync(IMG_PATH + Const.PokemonImage[id]));
  }
}
