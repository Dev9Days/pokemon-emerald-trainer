using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Runtime.InteropServices;

var baseDirectory = AppContext.BaseDirectory;
var appDirectory = Path.Combine(baseDirectory, "app");
var appExecutable = Path.Combine(appDirectory, "PokemonGen3Hack.exe");
var userDataDirectory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "PokemonGen3Hack",
    "WebView2");

try
{
    var update = await UpdateChecker.CheckForUpdateAsync(baseDirectory);
    if (update is not null)
    {
        var updaterScript = await UpdateChecker.DownloadAndPrepareUpdateAsync(baseDirectory, update);
        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{updaterScript}\" -LauncherPid {Environment.ProcessId}",
            UseShellExecute = false,
            CreateNoWindow = true
        });
        return 0;
    }
}
catch (Exception exception)
{
    ShowMessage(
        "업데이트 확인에 실패했습니다. 현재 버전으로 실행합니다.\n\n" + exception.Message,
        "PokemonGen3Hack 업데이트");
}

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

static int ShowMessage(string text, string caption, uint type = 0x00000010)
{
    return MessageBoxW(IntPtr.Zero, text, caption, type);
}

[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
static extern int MessageBoxW(IntPtr hWnd, string lpText, string lpCaption, uint uType);

internal sealed record ReleaseUpdate(string Version, string DownloadUrl);

internal static class UpdateChecker
{
    private const string Owner = "Dev9Days";
    private const string Repository = "pokemon-emerald-trainer";
    private const string RuntimeIdentifier = "win-x64";
    private const string VersionFileName = "version.txt";

    public static string GetCurrentVersion(string baseDirectory)
    {
        var versionFile = Path.Combine(baseDirectory, VersionFileName);
        return File.Exists(versionFile) ? File.ReadAllText(versionFile).Trim() : "0.0.0";
    }

    public static async Task<ReleaseUpdate?> CheckForUpdateAsync(string baseDirectory)
    {
        using var http = CreateHttpClient();
        using var response = await http.GetAsync($"https://api.github.com/repos/{Owner}/{Repository}/releases/latest");
        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(stream);
        var root = json.RootElement;
        var latestVersion = NormalizeVersion(root.GetProperty("tag_name").GetString() ?? string.Empty);
        var currentVersion = NormalizeVersion(GetCurrentVersion(baseDirectory));
        if (!IsNewerVersion(latestVersion, currentVersion))
            return null;

        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? string.Empty;
            if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
            if (!name.Contains(RuntimeIdentifier, StringComparison.OrdinalIgnoreCase)) continue;

            var url = asset.GetProperty("browser_download_url").GetString();
            if (!string.IsNullOrWhiteSpace(url))
                return new ReleaseUpdate(latestVersion, url);
        }

        return null;
    }

    public static async Task<string> DownloadAndPrepareUpdateAsync(string baseDirectory, ReleaseUpdate update)
    {
        var updateRoot = Path.Combine(Path.GetTempPath(), "PokemonGen3HackUpdate", Guid.NewGuid().ToString("N"));
        var zipPath = Path.Combine(updateRoot, "update.zip");
        var extractRoot = Path.Combine(updateRoot, "extract");
        Directory.CreateDirectory(updateRoot);

        using var http = CreateHttpClient();
        await using (var download = await http.GetStreamAsync(update.DownloadUrl))
        await using (var file = File.Create(zipPath))
        {
            await download.CopyToAsync(file);
        }

        ZipFile.ExtractToDirectory(zipPath, extractRoot);
        var packageRoot = Directory.Exists(Path.Combine(extractRoot, "app"))
            && File.Exists(Path.Combine(extractRoot, "PokemonGen3Hack.exe"))
            ? extractRoot
            : Directory.GetDirectories(extractRoot).FirstOrDefault() ?? extractRoot;
        var backupRoot = Path.Combine(updateRoot, "backup");
        var scriptPath = Path.Combine(updateRoot, "apply-update.ps1");
        var launcherPath = Path.Combine(baseDirectory, "PokemonGen3Hack.exe");

        File.WriteAllText(scriptPath, $$"""
param([int]$LauncherPid)
$ErrorActionPreference = "Stop"
Wait-Process -Id $LauncherPid -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 300
$baseDirectory = "{{baseDirectory}}"
$packageRoot = "{{packageRoot}}"
$backupRoot = "{{backupRoot}}"
$launcherPath = "{{launcherPath}}"

function Copy-Package([string]$source, [string]$destination) {
    Copy-Item -Path (Join-Path $source "*") -Destination $destination -Recurse -Force
}

try {
    New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
    Copy-Package $baseDirectory $backupRoot

    Copy-Package $packageRoot $baseDirectory

    if (!(Test-Path (Join-Path $baseDirectory "PokemonGen3Hack.exe"))) {
        throw "Launcher executable is missing after update."
    }
    if (!(Test-Path (Join-Path $baseDirectory "version.txt"))) {
        throw "version.txt is missing after update."
    }
    if (!(Test-Path (Join-Path $baseDirectory "app\PokemonGen3Hack.exe"))) {
        throw "App executable is missing after update."
    }
} catch {
    try {
        if (Test-Path $backupRoot) {
            Copy-Package $backupRoot $baseDirectory
        }
    } catch {
    }
    Add-Type -AssemblyName PresentationFramework
    [System.Windows.MessageBox]::Show("업데이트에 실패해서 기존 버전으로 복구했습니다.`n`n$($_.Exception.Message)", "PokemonGen3Hack 업데이트") | Out-Null
}

Start-Process -FilePath $launcherPath -WorkingDirectory $baseDirectory
""");

        return scriptPath;
    }

    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PokemonGen3Hack", "1.0"));
        return http;
    }

    private static string NormalizeVersion(string version)
    {
        version = version.Trim();
        return version.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? version[1..] : version;
    }

    private static bool IsNewerVersion(string latestVersion, string currentVersion)
    {
        return Version.TryParse(latestVersion, out var latest)
            && Version.TryParse(currentVersion, out var current)
            && latest > current;
    }
}
