using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Reflection;
using HarmonyLib;
using PeterHan.PLib.Core;
using PeterHan.PLib.Database;
using PeterHan.PLib.Options;
using UnityEngine;
using static Localization;
using static ModI18n.Patches;
using System.Diagnostics;

namespace ModI18n
{
    

    public class Utils
    {
        public static readonly string modsDir = KMod.Manager.GetDirectory();
        public static readonly string stringsFolder = Path.Combine(modsDir, "i18n");
        public static readonly int maxPrintCount = 3;
        public static Dictionary<string, string> translations = null;
        public static string templatesFolder = Path.Combine(modsDir, "strings_templates", "ModI18n");

        public static void InitTranslations()
        {
            var _sw = Stopwatch.StartNew();

            if (translations != null)
            {
                Debug.Log("[ModI18n] Translations have already been initialized.");
                return;
            }

            I18nOptions options = POptions.ReadSettings<I18nOptions>() ?? new I18nOptions();
            string code = LangAttribute.GetAttr(options.PreferedLanguage).code;
            bool localOnly = options.LocalOnly;

            Directory.CreateDirectory(stringsFolder);
            string filename = $"{code}.po";
            string path = Path.Combine(stringsFolder, filename);

            try
            {
                translations = LoadStringsFile(path, false);
                Debug.Log($"[ModI18n] Translation init successfully: {path}");
            }
            catch (FileNotFoundException)
            {
                translations = new Dictionary<string, string>();
                Debug.LogWarning($"[ModI18n] Failed to load localization file: {filename}");
            }

            _sw.Stop();
            Debug.Log($"[ModI18n][Perf] InitTranslations 耗时: {_sw.Elapsed.TotalSeconds:F3}s");
        }

        public static void LoadStrings()
        {
            var _sw = Stopwatch.StartNew();
            Debug.Log("[ModI18n][Perf] 开始 LoadStrings()");

            var _t1 = Stopwatch.StartNew();
            GenerateStringsTemplate(typeof(STRINGS), templatesFolder);
            _t1.Stop();
            Debug.Log($"[ModI18n][Perf] GenerateStringsTemplate 耗时: {_t1.Elapsed.TotalSeconds:F3}s");

            var _t2 = Stopwatch.StartNew();
            RegisterForTranslation(typeof(STRINGS));
            _t2.Stop();
            Debug.Log($"[ModI18n][Perf] RegisterForTranslation 耗时: {_t2.Elapsed.TotalSeconds:F3}s");

            var _t3 = Stopwatch.StartNew();
            InitTranslations();
            OverloadStrings(translations);
            _t3.Stop();
            Debug.Log($"[ModI18n][Perf] 翻译加载+覆盖 耗时: {_t3.Elapsed.TotalSeconds:F3}s");

            var _t4 = Stopwatch.StartNew();
            int count = 0;
            foreach (KeyValuePair<string, string> e in translations)
            {
                Strings.Add(e.Key, e.Value);
                count++;
            }
            _t4.Stop();
            Debug.Log($"[ModI18n][Perf] Strings.Add 共 {count} 条 耗时: {_t4.Elapsed.TotalSeconds:F3}s");

            _sw.Stop();
            Debug.Log($"[ModI18n][Perf] 结束 LoadStrings 总耗时: {_sw.Elapsed.TotalSeconds:F3}s");
        }

        public static void LoadStringsWithPrefix(string prefix)
        {
            var _sw = Stopwatch.StartNew();
            int count = 0;
            foreach (KeyValuePair<string, string> e in translations)
            {
                if (!e.Key.StartsWith(prefix)) continue;
                Strings.Add(e.Key, e.Value);
                count++;
            }
            _sw.Stop();
            Debug.Log($"[ModI18n][Perf] LoadStringsWithPrefix({prefix}) 共 {count} 条 耗时: {_sw.Elapsed.TotalSeconds:F3}s");
        }

        public static object GetField(Type type, object instance, string fieldName)
        {
            BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            FieldInfo field = type.GetField(fieldName, bindFlags);
            return field.GetValue(instance);
        }

        public static List<KeyValuePair<string, string>> CollectStrings()
        {
            var _sw = Stopwatch.StartNew();
            Debug.Log("[ModI18n][Perf] 开始 CollectStrings()");

            string vanillaStringsTemplate = Path.Combine(UnityEngine.Application.streamingAssetsPath, "strings", "strings_template.pot");
            Dictionary<string, string> vanillaStrings = LoadStringsFile(vanillaStringsTemplate, true);

            var _ref = Stopwatch.StartNew();
            StringTable RootTable = (StringTable)GetField(typeof(Strings), null, "RootTable");
            Dictionary<int, string> RootTableKeyNames = (Dictionary<int, string>)GetField(typeof(StringTable), RootTable, "KeyNames");
            Dictionary<int, StringEntry> RootTableEntries = (Dictionary<int, StringEntry>)GetField(typeof(StringTable), RootTable, "Entries");
            _ref.Stop();
            Debug.Log($"[ModI18n][Perf] 反射 RootTable 耗时: {_ref.Elapsed.TotalSeconds:F3}s");

            var _loop = Stopwatch.StartNew();
            SortedSet<string> keys = new SortedSet<string>();
            foreach (var kv in RootTableKeyNames)
                keys.Add(kv.Value);

            List<KeyValuePair<string, string>> dict = new List<KeyValuePair<string, string>>();
            foreach (var k in keys)
            {
                if (!vanillaStrings.ContainsKey(k)
                    && !k.StartsWith("PeterHan.PLib.")
                    && !k.StartsWith("ModI18n.")
                    && RootTableEntries[k.GetHashCode()].String?.Length > 0)
                    dict.Add(new KeyValuePair<string, string>(k, RootTableEntries[k.GetHashCode()].String));
            }
            _loop.Stop();
            Debug.Log($"[ModI18n][Perf] 遍历 RootTable 共 {dict.Count} 条 耗时: {_loop.Elapsed.TotalSeconds:F3}s");

            _sw.Stop();
            Debug.Log($"[ModI18n][Perf] 结束 CollectStrings 总耗时: {_sw.Elapsed.TotalSeconds:F3}s");
            return dict;
        }

        public static void GenerateStringsTemplateForAll(string file_path)
        {
            var _sw = Stopwatch.StartNew();
            Debug.Log("[ModI18n][Perf] 开始 GenerateStringsTemplateForAll()");

            Directory.CreateDirectory(Path.GetDirectoryName(file_path));
            using (StreamWriter streamWriter = new StreamWriter(file_path, append: false,
                new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                streamWriter.WriteLine("msgid \"\"");
                streamWriter.WriteLine("msgstr \"\"");
                streamWriter.WriteLine("\"Application: Oxygen Not Included\"");
                streamWriter.WriteLine("\"POT Version: 2.0\"");
                streamWriter.WriteLine("");

                var _collect = Stopwatch.StartNew();
                var allStrings = CollectStrings();
                _collect.Stop();
                Debug.Log($"[ModI18n][Perf] CollectStrings 完成 耗时: {_collect.Elapsed.TotalSeconds:F3}s");

                var _write = Stopwatch.StartNew();
                foreach (var kv in allStrings)
                {
                    string msgctxt = kv.Key;
                    string msgid = kv.Value.Replace("\"", "\\\"").Replace("\n", "\\n");
                    streamWriter.WriteLine($"#. {msgctxt}");
                    streamWriter.WriteLine($"msgctxt \"{msgctxt}\"");
                    streamWriter.WriteLine($"msgid \"{msgid}\"");
                    streamWriter.WriteLine($"msgstr \"\"");
                    streamWriter.WriteLine("");
                }
                _write.Stop();
                Debug.Log($"[ModI18n][Perf] 写入模板 {allStrings.Count} 条 耗时: {_write.Elapsed.TotalSeconds:F3}s");
            }

            _sw.Stop();
            Debug.Log($"[ModI18n][Perf] 结束 GenerateStringsTemplateForAll 总耗时: {_sw.Elapsed.TotalSeconds:F3}s");
        }
    }

    public class Patches
    {
        // 静态标志位，记录 OverloadStrings 是否已经执行过
        // 避免在多个 Mod 或多次调用中重复执行，提升性能
        private static bool hasOverloaded = false;


        [HarmonyPatch(typeof(LegacyModMain), "Load")]
        public class LegacyModMain_Patch
        {
            [HarmonyPriority(int.MinValue)]
            public static void Postfix()
            {
                var _sw = Stopwatch.StartNew();
                Utils.LoadStrings();
                Utils.GenerateStringsTemplateForAll(Path.Combine(Utils.templatesFolder, "curr_mods_templates.pot"));
                _sw.Stop();
                Debug.Log($"[ModI18n][Perf] LegacyModMain.Load 阶段耗时: {_sw.Elapsed.TotalSeconds:F3}s");
            }
        }

        [HarmonyPatch(typeof(Localization), "Initialize")]
        public class LocalizationInitializePatch
        {
            [HarmonyPriority(int.MaxValue)]
            public static void Postfix()
            {
                var _sw = Stopwatch.StartNew();
                Utils.InitTranslations();
                _sw.Stop();
                Debug.Log($"[ModI18n][Perf] Localization.Initialize Patch 耗时: {_sw.Elapsed.TotalSeconds:F3}s");
            }
        }

        [HarmonyPatch(typeof(Localization), "RegisterForTranslation")]
        public class LocalizationRegisterForTranslationPatch
        {
            [HarmonyPriority(int.MinValue)]
            public static void Postfix()
            {
                // 创建一个 Stopwatch，用于统计本 Patch 执行耗时
                var _sw = Stopwatch.StartNew();

                // 如果已经执行过 OverloadStrings，就直接跳过，防止重复消耗时间
                //if (hasOverloaded) return; // ⚡ 性能优化

                

                // 获取调用栈中第二层方法的名称，判断是否来自 OnAllModsLoaded
                // 如果是 OnAllModsLoaded 调用，则不执行任何操作
                if (new System.Diagnostics.StackFrame(2).GetMethod().Name == "OnAllModsLoaded") { }
                // 否则，如果 translations 字典已初始化
                else if (Utils.translations != null)
                {
                    
                    // 调用 OverloadStrings，将 translations 中的翻译字符串覆盖到游戏中
                    OverloadStrings(Utils.translations);

                    // ⚡ 标记为已执行，确保下次 Patch 调用直接跳过
                    hasOverloaded = true;

                    
                }


                // 停止计时
                _sw.Stop();

                // 打印本 Patch 执行耗时日志，便于分析性能
                Debug.Log($"[ModI18n][Perf] RegisterForTranslation Patch 耗时: {_sw.Elapsed.TotalSeconds:F3}s");


            }
        }


        [HarmonyPatch(typeof(Localization), "OverloadStrings")]
        [HarmonyPatch(new Type[] { typeof(Dictionary<string, string>) })] // 指定重载方法签名
        public class OverloadStringsPatch
        {
            [HarmonyPriority(int.MinValue)]
            public static void Prefix(ref Dictionary<string, string> translated_strings)
            {
                // 创建 Stopwatch 用于测量本 Patch 的执行耗时
                var _sw = Stopwatch.StartNew();

                // 如果 translations 字典未初始化，则直接返回，避免空引用
                if (Utils.translations == null) return;

                // 如果传入的字典正好是 translations 本身，则直接返回，避免重复操作
                if (Utils.translations == translated_strings) return;


                // 遍历 ModI18n 的 translations 字典，将每条翻译写入传入的 translated_strings
                // ⚡ 注意：字典条目越多，耗时越长
                foreach (var kv in Utils.translations)
                {
                    translated_strings[kv.Key] = kv.Value;
                }
                    
                // 停止计时
                _sw.Stop();

                // 输出本 Patch 耗时日志，方便分析性能瓶颈
                Debug.Log($"[ModI18n][Perf] OverloadStrings Patch 合并耗时: {_sw.Elapsed.TotalSeconds:F3}s");
            }
        }


        [HarmonyPatch(typeof(Assets), "SubstanceListHookup")]
        public class SubstanceListHookupPatch
        {
            [HarmonyPriority(int.MinValue)]
            public static void Prefix()
            {
                Utils.LoadStringsWithPrefix("STRINGS.ELEMENTS.");
            }
        }

        [HarmonyPatch(typeof(ModUtil), "AddBuildingToPlanScreen")]
        [HarmonyPatch(new Type[] {typeof(HashedString),typeof(string),typeof(string),typeof(string),typeof(ModUtil.BuildingOrdering)
        })]
        public class AddBuildingToPlanScreenPatch
        {
            [HarmonyPriority(int.MinValue)]
            public static void Prefix() => Utils.LoadStringsWithPrefix("STRINGS.BUILDINGS.");
        }

        [HarmonyPatch(typeof(GeneratedBuildings), "LoadGeneratedBuildings")]
        public class LoadGeneratedBuildingsPatch
        {
            [HarmonyPriority(int.MinValue)]
            public static void Prefix() => Utils.LoadStringsWithPrefix("STRINGS.BUILDINGS.");
        }

        [HarmonyPatch(typeof(EntityConfigManager), "LoadGeneratedEntities")]
        public class LoadGeneratedEntitiesPatch
        {
            [HarmonyPriority(int.MinValue)]
            public static void Prefix() => Utils.LoadStrings();
        }

        [HarmonyPatch(typeof(EntityTemplates), "CreateLooseEntity")]
        public class CreateLooseEntityPatch
        {
            [HarmonyPriority(int.MinValue)]
            public static void Prefix(string id, ref string name, ref string desc)
            {
                string foodNameCode = $"STRINGS.ITEMS.FOOD.{id.ToUpperInvariant()}.NAME";
                string foodDescCode = $"STRINGS.ITEMS.FOOD.{id.ToUpperInvariant()}.DESC";
                if (Utils.translations.ContainsKey(foodNameCode))
                    name = Utils.translations[foodNameCode];
                if (Utils.translations.ContainsKey(foodDescCode))
                    desc = Utils.translations[foodDescCode];
            }
        }
    }

    public class I18nUserMod : KMod.UserMod2
    {
        

        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
            PUtil.InitLibrary(false);
            new PLocalization().Register();
            new POptions().RegisterOptions(this, typeof(I18nOptions));
        }

        public override void OnAllModsLoaded(Harmony harmony, IReadOnlyList<KMod.Mod> mods)
        {
            base.OnAllModsLoaded(harmony, mods);
            var _sw = Stopwatch.StartNew();
            Debug.Log("[ModI18n][Perf] >>> 开始 OnAllModsLoaded()");


            // 读取白名单文件，每行一个启用的命名空间
            string whitelistFile = Path.Combine(Utils.stringsFolder, "enableList.txt");
            HashSet<string> enabledNamespaces = new HashSet<string>();
            if (File.Exists(whitelistFile))
            {
                foreach (var line in File.ReadAllLines(whitelistFile))
                {

                    // 去掉首尾空格并统一小写
                    string ns = line.Trim().ToLowerInvariant();

                    // 忽略空行或以 # 开头的注释行
                    if (string.IsNullOrEmpty(ns) || ns.StartsWith("#")) continue;

                    // 去掉 _template.pot 后缀
                    if (ns.EndsWith("_template.pot")) ns = ns.Substring(0, ns.Length - "_template.pot".Length);

                    enabledNamespaces.Add(ns);
                    Debug.Log($"[ModI18n][Perf] 添加白名单 {ns}");

                    
                }
            }
            else
            {
                Debug.LogWarning($"[ModI18n] 白名单文件不存在: {whitelistFile}，不会注册任何命名空间");
            }

            // 已注册的命名空间
            HashSet<string> registeredNamespaces = new HashSet<string>(); 

            foreach (KMod.Mod mod in mods)
            {
                if (mod.title == "ModI18nReborn") continue;
                if (!mod.IsActive()) continue;

                
                foreach (Assembly assem in mod.loaded_mod_data.dlls)
                {
                    
                    foreach (Type t in assem.GetTypes())
                    {
                        // 如果命名空间已注册，跳过
                        if (registeredNamespaces.Contains(t.Namespace)) continue;
                        // 标记为已注册
                        registeredNamespaces.Add(t.Namespace);
                        try
                        {
                            GenerateStringsTemplate(t, Utils.templatesFolder);
                            Debug.Log($"[ModI18n][Perf] 已导出命名空间 【{mod.title}】{t.Namespace}");
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[ModI18n] Error type 【{mod.title}】{t.FullName}: {e.Message}");
                        }

                        // 当前命名空间不在白名单中，跳过
                        string ns = t.Namespace?.ToLowerInvariant(); // ⚡ 转小写防止 null
                        if (!enabledNamespaces.Contains(ns)) continue;
                        
                        try
                        {
                            RegisterForTranslation(t);
                            Debug.Log($"[ModI18n][Perf] 已注册命名空间 【{mod.title}】{t.Namespace}");
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[ModI18n] Error type 【{mod.title}】{t.FullName}: {e.Message}");
                        }

   
                    }
                    
                }
                
            }

            var _loadsw = Stopwatch.StartNew();
            Utils.LoadStrings();
            _loadsw.Stop();
            Debug.Log($"[ModI18n][Perf] LoadStrings 耗时: {_loadsw.Elapsed.TotalSeconds:F3}s");

            _sw.Stop();
            Debug.Log($"[ModI18n][Perf] <<< OnAllModsLoaded 总耗时: {_sw.Elapsed.TotalSeconds:F3}s");
        }
    }
}
