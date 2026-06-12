using System.Diagnostics;
using System.Runtime.InteropServices;

var baseDirectory = AppContext.BaseDirectory;
var appDirectory = Path.Combine(baseDirectory, "app");
var appExecutable = Path.Combine(appDirectory, "PokemonGen3Hack.exe");
var userDataDirectory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "PokemonGen3Hack",
    "WebView2");

if (!File.Exists(appExecutable))
{
    ShowMessage(
        "PokemonGen3Hack 실행 파일을 찾을 수 없습니다.\n\n" +
        $"필요한 파일: {appExecutable}",
        "PokemonGen3Hack");
    return 1;
}

try
{
    Process.Start(new ProcessStartInfo
    {
        FileName = appExecutable,
        WorkingDirectory = appDirectory,
        UseShellExecute = false,
        Environment =
        {
            ["WEBVIEW2_USER_DATA_FOLDER"] = userDataDirectory
        }
    });

    return 0;
}
catch (Exception exception)
{
    ShowMessage(
        "PokemonGen3Hack 실행에 실패했습니다.\n\n" + exception.Message,
        "PokemonGen3Hack");
    return 1;
}

static void ShowMessage(string text, string caption)
{
    _ = MessageBoxW(IntPtr.Zero, text, caption, 0x00000010);
}

[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
static extern int MessageBoxW(IntPtr hWnd, string lpText, string lpCaption, uint uType);
