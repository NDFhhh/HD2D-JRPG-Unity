using UnityEditor;
using UnityEngine;

/// <summary>
/// 音频系统编辑器菜单：一键创建 SoundLibrary 资产、在当前场景摆好 AudioManager
/// 菜单路径：CodePath/Audio/...
/// </summary>
public static class AudioSystemSetupMenu
{
    private const string LibraryFolder = "Assets/Game Data/Audio";
    private const string LibraryPath = LibraryFolder + "/SoundLibrary.asset";

    [MenuItem("CodePath/Audio/1. Create SoundLibrary Asset")]
    public static void CreateSoundLibrary()
    {
        if (!AssetDatabase.IsValidFolder(LibraryFolder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Game Data"))
                AssetDatabase.CreateFolder("Assets", "Game Data");
            AssetDatabase.CreateFolder("Assets/Game Data", "Audio");
        }

        var existing = AssetDatabase.LoadAssetAtPath<SoundLibrary_SO>(LibraryPath);
        if (existing != null)
        {
            Debug.LogWarning($"[Audio] SoundLibrary 已存在：{LibraryPath}");
            Selection.activeObject = existing;
            return;
        }

        var library = ScriptableObject.CreateInstance<SoundLibrary_SO>();
        AssetDatabase.CreateAsset(library, LibraryPath);
        AssetDatabase.SaveAssets();

        Selection.activeObject = library;
        EditorGUIUtility.PingObject(library);
        Debug.Log($"[Audio] 已创建 SoundLibrary：{LibraryPath}，接下来为每个 E_SoundId 配置 AudioCue");
    }

    [MenuItem("CodePath/Audio/2. Add AudioManager To Scene")]
    public static void AddAudioManagerToScene()
    {
        var existing = Object.FindObjectOfType<AudioManager>();
        if (existing != null)
        {
            Debug.LogWarning($"[Audio] 场景中已存在 AudioManager：{existing.gameObject.name}", existing);
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        // 确保库资产存在（不存在则顺手创建）
        var library = AssetDatabase.LoadAssetAtPath<SoundLibrary_SO>(LibraryPath);
        if (library == null)
        {
            CreateSoundLibrary();
            library = AssetDatabase.LoadAssetAtPath<SoundLibrary_SO>(LibraryPath);
        }

        var go = new GameObject("AudioManager");
        Undo.RegisterCreatedObjectUndo(go, "Create AudioManager");

        var manager = go.AddComponent<AudioManager>();

        // 通过 SerializedObject 赋值，保证场景被正确标记为 dirty
        if (library != null)
        {
            var so = new SerializedObject(manager);
            so.FindProperty("library").objectReferenceValue = library;
            so.ApplyModifiedProperties();
        }

        Selection.activeGameObject = go;
        Debug.Log("[Audio] 已在场景创建 AudioManager（DontDestroyOnLoad，只需在首个场景放置一次）。" +
                  "建议选中后拖成 Prefab 放入启动场景。");
    }
}
