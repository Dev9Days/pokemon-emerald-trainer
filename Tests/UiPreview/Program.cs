using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.FileProviders;
using PokemonGen3Hack.Components.Pages;
using PokemonGen3Hack.Components.ViewModel;
using PokemonGen3Hack.Emulator;
using PokemonGen3Hack.Pokemon;
using PokemonGen3Hack.Services;
using PokemonGen3Hack.Utils;
using Core = PKHeX.Core;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
var builder = WebApplication.CreateBuilder(args);
builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Warning);
builder.Services.AddSingleton<ToastService>();
var app = builder.Build();
string workspace = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "../.."));
string romPath = builder.Configuration["rom"] ?? throw new ArgumentException("Supply --rom with a local Korean 260613 ROM path.");
using var fixture = new PreviewFixture(File.ReadAllBytes(romPath));
app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(Path.Combine(workspace, "wwwroot")) });
app.MapGet("/preview.styles.css", () => Results.File(Path.Combine(app.Environment.ContentRootPath, "obj/Debug/net10.0/scopedcss/bundle/UiPreview.styles.css"), "text/css"));
app.MapGet("/", async (HttpContext context) => {
  string mode = context.Request.Query["case"].ToString();
  PreviewState.Model = fixture.CreateModel(mode);
  PreviewState.Mode = mode;
  var toasts = app.Services.GetRequiredService<ToastService>();
  toasts.DismissAll();
  if (mode == "toast") toasts.ShowError("테스트 알림: 데이터를 가져오지 못했습니다.", 0);
  await using var renderer = new HtmlRenderer(app.Services, app.Services.GetRequiredService<ILoggerFactory>());
  string html = await renderer.Dispatcher.InvokeAsync(async () => {
    var root = await renderer.RenderComponentAsync<PreviewPokemonMain>();
    return root.ToHtmlString();
  });
  return Results.Content("<!doctype html><html lang=ko><head><meta charset=utf-8><meta name=viewport content=\"width=device-width,initial-scale=1\"><link rel=stylesheet href=/app.css><link rel=stylesheet href=/preview.styles.css></head><body>" + html + "</body></html>", "text/html");
});
await app.RunAsync();

static class PreviewState {
  public static PokemonMainViewModel Model { get; set; } = null!;
  public static string Mode { get; set; } = "";
}
sealed class PreviewPokemonMain : PokemonMain {
  public PreviewPokemonMain() {
    typeof(PokemonMain).GetField("viewModel", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(this, PreviewState.Model);
    if (PreviewState.Mode == "loading") typeof(PokemonMain).GetField("showLoading", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(this, true);
  }
}

sealed class PreviewFixture : IDisposable {
  private readonly byte[] rom;
  private readonly nint pak;
  private readonly nint ewram = Marshal.AllocHGlobal(0x40000);
  public PreviewFixture(byte[] rom) {
    this.rom = rom;
    pak = Marshal.AllocHGlobal(rom.Length);
    Marshal.Copy(rom, 0, pak, rom.Length);
    Marshal.Copy(new byte[0x40000], 0, ewram, 0x40000);
  }
  public PokemonMainViewModel CreateModel(string mode) {
    var vm = new PokemonMainViewModel { ShowProcessModal = mode == "empty" };
    if (mode == "empty") return vm;
    var emulator = (PokemonGen3Hack.Emulator.Emulator)typeof(PokemonMainViewModel).GetField("emul", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(vm)!;
    emulator.Process = Process.GetCurrentProcess();
    Set(emulator, "processID", (nint)Environment.ProcessId);
    Set(emulator, "pak", pak); Set(emulator, "ewram", ewram); Set(emulator, "iwram", ewram);
    Set(emulator, "lang", mode == "english" ? Language.English : Language.Korean);
    Type profileType = emulator.GetType().Assembly.GetType("PokemonGen3Hack.Emulator.RomProfile")!;
    object profile = profileType.GetMethod("FromMarker")!.Invoke(null, new object[] { (byte)0x78 })!;
    emulator.GetType().GetProperty("Profile", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(emulator, profile);
    ushort[] species = [259, 261, 261, 183, 183];
    string[] names = ["늪짱이", "포차나", "포차나", "마릴", "마릴"];
    vm.PokemonInfo = new PokemonInfo[species.Length];
    for (int i = 0; i < species.Length; i++) {
      var pk = new Core.PK3 {
        Species = species[i], Version = Core.GameVersion.E, Language = 2, PID = 0xF405B290,
        TID16 = 21963, SID16 = 55364, Nickname = "POOCHYENA", OriginalTrainerName = "TEST",
        CurrentLevel = 10, Stat_Level = 10, MetLocation = 21, MetLevel = 7, Ball = (byte)Core.Ball.Poke,
        IV_HP = 30, IV_ATK = 16, IV_DEF = 5, IV_SPE = 24, IV_SPA = 15, IV_SPD = 7,
        Stat_HPCurrent = 14, Stat_HPMax = 30, Stat_ATK = 17, Stat_DEF = 10, Stat_SPE = 14, Stat_SPA = 12, Stat_SPD = 12,
        OriginalTrainerFriendship = 81, Move1 = 33, Move1_PP = 35, Move2 = 336, Move2_PP = 40, Move3 = 28, Move3_PP = 15,
      };
      byte[] raw = new byte[100];
      pk.WriteEncryptedDataParty(raw);
      string nickname = mode == "english" ? "WWWWWWWWWW" : names[i];
      byte[] encoded = CharDecoder.Decode(nickname);
      Array.Fill(raw, (byte)0xFF, 8, 10);
      Array.Copy(encoded, 0, raw, 8, Math.Min(10, encoded.Length));
      var info = new PokemonInfo(raw);
      info.SetPersonality((GenderGroup)pk.PersonalInfo.Gender);
      Type icons = emulator.GetType().Assembly.GetType("PokemonGen3Hack.Pokemon.RomPokemonIcon")!;
      Func<int, int, byte[]> read = (offset, count) => rom.AsSpan(offset, count).ToArray();
      byte[] png = (byte[])icons.GetMethod("ReadPng")!.Invoke(null, new object[] {
        read, 0x5590E4, Core.SpeciesConverter.GetInternal3(species[i]), (uint)0, false,
      })!;
      info.Image = "data:image/png;base64," + Convert.ToBase64String(png);
      vm.PokemonInfo[i] = info;
    }
    vm.PartyCount = species.Length;
    vm.SelectPartyMember(mode == "dualtype" ? 0 : 2);
    vm.Pokemon.OT = "인주";
    if (mode == "warning") vm.Pokemon.IsShiny = !vm.Pokemon.IsShiny;
    if (mode == "pokerus") { vm.Pokemon.IsPokerus = true; vm.Pokemon.PokerusDaysLeft = 4; }
    if (mode == "hatch") vm.Pokemon.EncounterLevel = 0;
    return vm;
  }
  private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(obj, value);
  public void Dispose() { Marshal.FreeHGlobal(pak); Marshal.FreeHGlobal(ewram); }
}
