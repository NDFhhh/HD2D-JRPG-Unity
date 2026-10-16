using System.Collections.Generic;
using UnityEngine;

// 注意：本文件刻意**不使用可空引用类型注解（`?`）**。
// 本工程 Assembly-CSharp 的编译参数里没有 -nullable，且全工程只有本目录新加的文件
// 是后来引入 Yarn 才出现的，没有开启 nullable 的先例。写了 `SomeType?` 会被判为
// CS8632 警告（如果顺手加了 `#nullable enable`，泛型上的 `T?` 又会被解析成
// Nullable<T> 而与基类签名冲突，报 CS0115/CS0534/CS0453）。
// 因此这里统一走「不用注解」的写法。

/// <summary>
/// 说话人注册表：把 Yarn 脚本里的说话人标识（<c>LocalizedLine.CharacterName</c>）
/// 映射回项目的 <see cref="Speaker_SO"/>，从而拿回姓名颜色、立绘、打字机音效。
///
/// 用法：Yarn 里写
/// <code>
/// traveller: 你好啊，旅行者。
/// </code>
/// → <c>line.CharacterName == "traveller"</c> → 查到 <c>Speaker_Traveller.asset</c>。
///
/// <see cref="Speaker_SO.Id"/> 留空时回落到资产文件名（如 <c>Speaker_Traveller</c>）。
/// </summary>
public static class DialogueSpeakerRegistry
{
    private static Dictionary<string, Speaker_SO> _map;

    /// <summary>失效缓存（编辑器里改了 Speaker 资产后调用；通常重进 Play 即可）</summary>
    public static void Invalidate() => _map = null;

    public static Speaker_SO Get(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        EnsureBuilt();
        return _map.TryGetValue(id, out Speaker_SO so) ? so : null;
    }

    private static void EnsureBuilt()
    {
        if (_map is not null)
            return;

        _map = new Dictionary<string, Speaker_SO>();

        // 与 DialoguePanelController.ResolveFont 同思路：运行时扫描已加载资产，
        // 避免在场景里手工维护一份 Speaker 清单。
        Speaker_SO[] all = Resources.FindObjectsOfTypeAll<Speaker_SO>();
        for (int i = 0; i < all.Length; i++)
        {
            Speaker_SO so = all[i];
            if (so is null)
                continue;

            string key = string.IsNullOrEmpty(so.Id) ? so.name : so.Id;
            if (string.IsNullOrEmpty(key))
                continue;

            // 先到先得：Id 显式重复时保留第一个，避免随机覆盖
            if (!_map.ContainsKey(key))
                _map[key] = so;
        }
    }
}
