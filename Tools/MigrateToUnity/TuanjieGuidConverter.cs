// TuanjieGuidConverter.cs —— 在【团结引擎】里运行，把资产身份归一化成 Unity 能认的格式。
//
// 用法：把本文件放到工程的 Assets/Editor/ 下，团结引擎编译后在菜单栏出现 "Tools/迁移到Unity"。
//       按 1 -> 2 -> 3 顺序执行，最后按 4 验收。迁移完成后删除本文件。
//
// 为什么需要它：团结引擎 1.x 把 .meta 里的 guid 写成了 56 位 base64（XGUID）格式，
// 标准 Unity 不认，会给资产重新分配 GUID，导致场景/prefab/SO 里的引用全部断链。
// 但团结引擎的场景/prefab/SO YAML 里引用用的仍然是 32 位 hex GUID，
// 所以只要把 .meta 的 guid 改回"YAML 里正在用的那个 hex 值"即可无缝保留引用。

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class TuanjieGuidConverter
{
    const string LogPath = "guid_convert_log.txt";
    static readonly Regex GuidLine = new Regex(@"^guid:\s*(\S+)", RegexOptions.Multiline);
    static readonly Regex YamlTag = new Regex(@"%TAG !u! tag:yousandi\.cn,2023:");
    static readonly Regex RefGuid = new Regex(@"guid: ([0-9a-f]{32})");
    static readonly string[] YamlExt = { ".unity", ".prefab", ".asset", ".controller", ".mat",
                                         ".playable", ".anim", ".overrideController", ".lighting", ".preset" };

    [MenuItem("Tools/迁移到Unity/1 - 体检：统计 XGUID 与引用健康度")]
    public static void Diagnose()
    {
        int hex = 0, xguid = 0;
        foreach (var p in AssetDatabase.GetAllAssetPaths())
        {
            if (!p.StartsWith("Assets/")) continue;
            var meta = p + ".meta";
            if (!File.Exists(meta)) continue;
            var m = GuidLine.Match(File.ReadAllText(meta));
            if (!m.Success) continue;
            if (m.Groups[1].Value.Length == 32) hex++; else xguid++;
        }
        var (refs, missing) = Verify();
        Debug.Log($"[迁移体检] meta: Unity格式 {hex} / 团结XGUID {xguid}\n" +
                  $"YAML 引用 {refs} 个不同 GUID，其中无法兑现(断链) {missing.Count} 个\n" +
                  $"明细见 {LogPath}");
    }

    [MenuItem("Tools/迁移到Unity/2 - 把 .meta 的 GUID 归一化为 Unity 32 位 hex")]
    public static void Convert()
    {
        var sb = new StringBuilder();
        int changed = 0, skipped = 0;
        var paths = AssetDatabase.GetAllAssetPaths();
        try
        {
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < paths.Length; i++)
            {
                var p = paths[i];
                if (!p.StartsWith("Assets/")) continue;
                EditorUtility.DisplayProgressBar("归一化 GUID", p, (float)i / paths.Length);

                var meta = p + ".meta";
                if (!File.Exists(meta)) continue;

                var text = File.ReadAllText(meta);
                var m = GuidLine.Match(text);
                if (!m.Success) continue;

                var cur = m.Groups[1].Value;
                if (cur.Length == 32) continue;                    // 已经是 Unity 格式

                var real = AssetDatabase.AssetPathToGUID(p);
                if (string.IsNullOrEmpty(real) || real.Length != 32)
                {
                    skipped++;
                    sb.AppendLine("[跳过·无GUID] " + p + "  原值 " + cur);
                    continue;
                }
                File.WriteAllText(meta, GuidLine.Replace(text, "guid: " + real, 1));
                sb.AppendLine(p + "\t" + cur + "\t" + real);
                changed++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
        }
        File.WriteAllText(LogPath, sb.ToString());
        Debug.Log($"[归一化完成] 改写 {changed} 个 .meta，跳过 {skipped} 个。\n" +
                  $"请接着执行菜单 3，然后执行菜单 4 验收。日志: {LogPath}");
    }

    [MenuItem("Tools/迁移到Unity/3 - 修正 YAML 的 %TAG 命名空间")]
    public static void FixTag()
    {
        int n = 0;
        foreach (var f in Directory.GetFiles("Assets", "*.*", SearchOption.AllDirectories))
        {
            if (System.Array.IndexOf(YamlExt, Path.GetExtension(f).ToLowerInvariant()) < 0) continue;
            var text = File.ReadAllText(f);
            if (!YamlTag.IsMatch(text)) continue;
            File.WriteAllText(f, YamlTag.Replace(text, "%TAG !u! tag:unity3d.com,2011:"));
            n++;
        }
        AssetDatabase.Refresh();
        Debug.Log($"[%TAG 修正] 处理 {n} 个 YAML 文件");
    }

    [MenuItem("Tools/迁移到Unity/4 - 验收：列出所有仍然断链的引用")]
    public static void VerifyMenu()
    {
        var (refs, missing) = Verify();
        var sb = new StringBuilder();
        foreach (var kv in missing)
        {
            sb.AppendLine(kv.Key + "  <- " + kv.Value.Count + " 个文件, 例: " + First(kv.Value));
        }
        File.WriteAllText("guid_verify_report.txt", sb.ToString());
        Debug.Log($"[验收] YAML 引用 {refs} 个，无法兑现 {missing.Count} 个。\n" +
                  $"预期结果：剩下的是第三方插件内部的历史断链和 Unity 内置资源占位 GUID。\n" +
                  $"若数字仍然很大，说明菜单 2 写入的值与 YAML 用的值不是一个变体，" +
                  $"此时把每个 hex 做一次\"每字节高低半字节互换\"再重跑菜单 2 即可。\n明细: guid_verify_report.txt");
    }

    // 统计 YAML 引用里有哪些 GUID 在当前工程/PackageCache 中找不到对应资产
    static (int, SortedDictionary<string, List<string>>) Verify()
    {
        var known = new HashSet<string>();
        foreach (var p in AssetDatabase.GetAllAssetPaths())
        {
            if (p.StartsWith("Assets/") || p.StartsWith("Packages/"))
            {
                var g = AssetDatabase.AssetPathToGUID(p);
                if (!string.IsNullOrEmpty(g)) known.Add(g);
            }
        }
        var refs = new HashSet<string>();
        var missing = new SortedDictionary<string, List<string>>();
        foreach (var f in Directory.GetFiles("Assets", "*.*", SearchOption.AllDirectories))
        {
            if (System.Array.IndexOf(YamlExt, Path.GetExtension(f).ToLowerInvariant()) < 0) continue;
            var text = File.ReadAllText(f);
            foreach (Match m in RefGuid.Matches(text))
            {
                var g = m.Groups[1].Value;
                refs.Add(g);
                if (!known.Contains(g))
                {
                    if (!missing.ContainsKey(g)) missing[g] = new List<string>();
                    if (missing[g].Count < 5 && !missing[g].Contains(f)) missing[g].Add(f);
                }
            }
        }
        return (refs.Count, missing);
    }

    static string First(List<string> l) { return l.Count > 0 ? l[0] : ""; }
}
