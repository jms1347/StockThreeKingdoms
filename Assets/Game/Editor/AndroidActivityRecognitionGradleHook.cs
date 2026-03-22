#if UNITY_EDITOR
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor.Android;
using UnityEngine;

/// <summary>
/// Assets/Plugins/Android/AndroidManifest.xml 대신 사용합니다.
/// 플러그인 매니페스트 병합이 런처(MAIN/LAUNCHER)를 깨뜨리는 경우가 있어,
/// Gradle 생성 후 unityLibrary 쪽 매니페스트에만 ACTIVITY_RECOGNITION 한 줄을 넣습니다.
/// </summary>
public sealed class AndroidActivityRecognitionGradleHook : IPostGenerateGradleAndroidProject
{
    const string PermissionLine = "  <uses-permission android:name=\"android.permission.ACTIVITY_RECOGNITION\" />";
    const string PermissionMarker = "android.permission.ACTIVITY_RECOGNITION";

    public int callbackOrder => 999;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        if (string.IsNullOrEmpty(path))
            return;

        // Unity 버전에 따라 path 가 unityLibrary 루트이거나, 그 상위(Gradle 루트)일 수 있음
        TryInject(Path.Combine(path, "src", "main", "AndroidManifest.xml"));
        TryInject(Path.Combine(path, "unityLibrary", "src", "main", "AndroidManifest.xml"));
    }

    static void TryInject(string manifestPath)
    {
        if (!File.Exists(manifestPath))
            return;

        string xml;
        try
        {
            xml = File.ReadAllText(manifestPath, Encoding.UTF8);
        }
        catch (IOException e)
        {
            Debug.LogWarning($"[ActivityRecognition] 매니페스트 읽기 실패: {manifestPath}\n{e.Message}");
            return;
        }

        if (xml.Contains(PermissionMarker))
            return;

        var m = Regex.Match(xml, @"<manifest\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!m.Success)
        {
            Debug.LogWarning($"[ActivityRecognition] <manifest> 를 찾지 못함: {manifestPath}");
            return;
        }

        int pos = m.Index + m.Length;
        xml = xml.Insert(pos, "\n" + PermissionLine + "\n");

        try
        {
            File.WriteAllText(manifestPath, xml, new UTF8Encoding(false));
        }
        catch (IOException e)
        {
            Debug.LogWarning($"[ActivityRecognition] 매니페스트 쓰기 실패: {manifestPath}\n{e.Message}");
            return;
        }

        Debug.Log($"[ActivityRecognition] ACTIVITY_RECOGNITION 권한 주입: {manifestPath}");
    }
}
#endif
