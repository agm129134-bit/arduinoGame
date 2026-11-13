using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 一鍵完整重建 Windows 發行版並打包 zip。
/// 每次都清空輸出資料夾再整包建置，避免「只替換 DLL、場景資料仍是舊版」造成
/// 序列化欄位錯位（v0.9.9 開始遊戲崩潰的原因）。
/// 選單：Build > Release Windows (Clean Build + Zip)
/// 命令列：Unity -batchmode -quit -projectPath . -executeMethod ReleaseBuilder.BuildWindowsRelease
/// </summary>
public static class ReleaseBuilder
{
    [MenuItem("Build/Release Windows (Clean Build + Zip)")]
    public static void BuildWindowsRelease()
    {
        string version = PlayerSettings.bundleVersion;
        string name = $"RollingBall_v{version}";
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds"));
        string outDir = Path.Combine(root, name);
        string zipPath = Path.Combine(root, name + "_Windows.zip");

        if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        if (File.Exists(zipPath)) File.Delete(zipPath);
        Directory.CreateDirectory(outDir);

        var opts = new BuildPlayerOptions
        {
            scenes = System.Array.ConvertAll(
                System.Array.FindAll(EditorBuildSettings.scenes, s => s.enabled), s => s.path),
            locationPathName = Path.Combine(outDir, PlayerSettings.productName + ".exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(opts);
        bool ok = report.summary.result == BuildResult.Succeeded;
        Debug.Log($"[ReleaseBuilder] {name}: {report.summary.result}, errors={report.summary.totalErrors}");

        if (ok)
        {
            // 不打包除錯用資料夾
            foreach (var d in Directory.GetDirectories(outDir, "*_BurstDebugInformation_DoNotShip"))
                Directory.Delete(d, true);
            ZipFile.CreateFromDirectory(outDir, zipPath, System.IO.Compression.CompressionLevel.Optimal, true);
            Debug.Log($"[ReleaseBuilder] Zip: {zipPath}");
        }

        if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
    }
}
