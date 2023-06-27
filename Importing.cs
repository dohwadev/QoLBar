using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using Dalamud.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace QoLBar;

public class QoLSerializer : DefaultSerializationBinder
{
    private static readonly Type exportType = typeof(Importing.ExportInfo);
    private static readonly Type barType = typeof(BarConfig);
    private static readonly Type shortcutType = typeof(Shortcut);
    private static readonly Type barType2 = typeof(BarCfg);
    private static readonly Type shortcutType2 = typeof(ShCfg);
    private static readonly Type conditionSetType = typeof(CndSetCfg);
    private static readonly Type conditionType = typeof(CndCfg);
    private static readonly Type vector2Type = typeof(Vector2);
    private static readonly Type vector4Type = typeof(Vector4);
    private const string exportShortName = "e";
    private const string barShortName = "b";
    private const string shortcutShortName = "s";
    private const string barShortName2 = "b2";
    private const string shortcutShortName2 = "s2";
    private const string conditionSetShortName = "cs";
    private const string conditionShortName = "c";
    private const string vector2ShortName = "2";
    private const string vector4ShortName = "4";
    private static readonly Dictionary<string, Type> types = new()
    {
        [exportType.FullName!] = exportType,
        [exportShortName] = exportType,

        [barType.FullName!] = barType,
        [barShortName] = barType,

        [shortcutType.FullName!] = shortcutType,
        [shortcutShortName] = shortcutType,

        [barType2.FullName!] = barType2,
        [barShortName2] = barType2,

        [shortcutType2.FullName!] = shortcutType2,
        [shortcutShortName2] = shortcutType2,

        [conditionSetType.FullName!] = conditionSetType,
        [conditionSetShortName] = conditionSetType,

        [conditionType.FullName!] = conditionType,
        [conditionShortName] = conditionType,

        [vector2Type.FullName!] = vector2Type,
        [vector2ShortName] = vector2Type,

        [vector4Type.FullName!] = vector4Type,
        [vector4ShortName] = vector4Type
    };
    private static readonly Dictionary<Type, string> typeNames = new()
    {
        [exportType] = exportShortName,
        [barType] = barShortName,
        [shortcutType] = shortcutShortName,
        [barType2] = barShortName2,
        [shortcutType2] = shortcutShortName2,
        [conditionSetType] = conditionSetShortName,
        [conditionType] = conditionShortName,
        [vector2Type] = vector2ShortName,
        [vector4Type] = vector4ShortName
    };

    public override Type BindToType(string assemblyName, string typeName)
    {
        if (types.ContainsKey(typeName))
            return types[typeName];
        else
            return base.BindToType(assemblyName, typeName);
    }

    public override void BindToName(Type serializedType, out string assemblyName, out string typeName)
    {
        if (typeNames.ContainsKey(serializedType))
        {
            assemblyName = null;
            typeName = typeNames[serializedType];
        }
        else
            base.BindToName(serializedType, out assemblyName, out typeName);
    }
}

public static class Importing
{
    public class ImportInfo
    {
        public BarCfg bar;
        public ShCfg shortcut;
        public CndSetCfg conditionSet;
    }

    public class ExportInfo
    {
        public BarConfig b1;
        public BarCfg b2;
        public Shortcut s1;
        public ShCfg s2;
        public CndSetCfg cs;
        public string v = QoLBar.Config.PluginVersion;
    }

    private static readonly QoLSerializer qolSerializer = new();

    private static void CleanBarConfig(BarCfg bar)
    {
        if (bar.DockSide == BarCfg.BarDock.Undocked)
        {
            bar.Alignment = bar.GetDefaultValue(x => x.Alignment);
            bar.RevealAreaScale = bar.GetDefaultValue(x => x.RevealAreaScale);
            bar.Hint = bar.GetDefaultValue(x => x.Hint);
        }
        else
        {
            bar.RevealAreaScale = bar.GetDefaultValue(x => x.RevealAreaScale);

            if (bar.Visibility == BarCfg.BarVisibility.Always)
            {
                bar.RevealAreaScale = bar.GetDefaultValue(x => x.RevealAreaScale);
                bar.Hint = bar.GetDefaultValue(x => x.Hint);
            }
        }

        CleanShortcut(bar.ShortcutList);
    }

    private static void CleanShortcut(List<ShCfg> shortcuts)
    {
        foreach (var sh in shortcuts)
            CleanShortcut(sh);
    }

    private static void CleanShortcut(ShCfg sh)
    {
        if (sh.Type != ShCfg.ShortcutType.Category)
        {
            sh.SubList = sh.GetDefaultValue(x => x.SubList);
            sh.CategoryColumns = sh.GetDefaultValue(x => x.CategoryColumns);
            sh.CategoryStaysOpen = sh.GetDefaultValue(x => x.CategoryStaysOpen);
            sh.CategoryWidth = sh.GetDefaultValue(x => x.CategoryWidth);
            sh.CategorySpacing = sh.GetDefaultValue(x => x.CategorySpacing);
            sh.CategoryScale = sh.GetDefaultValue(x => x.CategoryScale);
            sh.CategoryFontScale = sh.GetDefaultValue(x => x.CategoryFontScale);
            sh.CategoryNoBackground = sh.GetDefaultValue(x => x.CategoryNoBackground);
            sh.CategoryOnHover = sh.GetDefaultValue(x => x.CategoryOnHover);
            sh.CategoryHoverClose = sh.GetDefaultValue(x => x.CategoryHoverClose);
        }
        else
        {
            if (sh.Mode != ShCfg.ShortcutMode.Default)
                sh.Command = sh.GetDefaultValue(x => x.Command);
            CleanShortcut(sh.SubList);
        }

        if (sh.Type == ShCfg.ShortcutType.Spacer)
        {
            sh.Command = sh.GetDefaultValue(x => x.Command);
            sh.Mode = sh.GetDefaultValue(x => x.Mode);
        }

        if (!sh.Name.Contains("::"))
        {
            sh.IconZoom = sh.GetDefaultValue(x => x.IconZoom);
            sh.IconOffset = sh.GetDefaultValue(x => x.IconOffset);
            sh.CooldownAction = sh.GetDefaultValue(x => x.CooldownAction);
            sh.CooldownStyle = sh.GetDefaultValue(x => x.CooldownStyle);
        }
        //else if (sh.ColorAnimation == 0)
        //    sh.ColorBg = sh.GetDefaultValue(x => x.ColorBg);

        sh._i = sh.GetDefaultValue(x => x._i);
    }

    public static T CopyObject<T>(T o)
    {
        var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Objects, SerializationBinder = qolSerializer };
        return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(o, settings), settings);
    }

    public static string SerializeObject(object o, bool saveAllValues) => !saveAllValues
        ? JsonConvert.SerializeObject(o, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Objects,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore,
            SerializationBinder = qolSerializer
        })
        : JsonConvert.SerializeObject(o, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Objects
        });

    public static T DeserializeObject<T>(string o) => JsonConvert.DeserializeObject<T>(o, new JsonSerializerSettings
    {
        TypeNameHandling = TypeNameHandling.Objects,
        SerializationBinder = qolSerializer
    });

    public static string CompressString(string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        using var ms = new MemoryStream();
        using (var gs = new GZipStream(ms, CompressionMode.Compress))
            gs.Write(bytes, 0, bytes.Length);
        return Convert.ToBase64String(ms.ToArray());
    }

    public static string DecompressString(string s)
    {
        var data = Convert.FromBase64String(s);
        using var ms = new MemoryStream(data);
        using var gs = new GZipStream(ms, CompressionMode.Decompress);
        using var r = new StreamReader(gs);
        return r.ReadToEnd();
    }

    public static string ExportObject(object o, bool saveAllValues) => CompressString(SerializeObject(o, saveAllValues));

    public static T ImportObject<T>(string import) => DeserializeObject<T>(DecompressString(import));

    public static string ExportBar(BarCfg bar, bool saveAllValues)
    {
        if (!saveAllValues)
        {
            bar = CopyObject(bar);
            CleanBarConfig(bar);
        }
        return ExportObject(new ExportInfo { b2 = bar }, saveAllValues);
    }

    public static bool allowImportConditions = false;
    public static bool allowImportHotkeys = false;
    public static BarConfig ImportBar(string import)
    {
        var bar = ImportObject<BarConfig>(import);

        if (!allowImportConditions)
            bar.ConditionSet = bar.GetDefaultValue(x => x.ConditionSet);

        if (!allowImportHotkeys)
        {
            static void removeHotkeys(Shortcut sh)
            {
                sh.Hotkey = sh.GetDefaultValue(x => x.Hotkey);
                sh.KeyPassthrough = sh.GetDefaultValue(x => x.KeyPassthrough);
                if (sh.SubList is not { Count: > 0 }) return;

                foreach (var sub in sh.SubList)
                    removeHotkeys(sub);
            }
            foreach (var sh in bar.ShortcutList)
                removeHotkeys(sh);
        }

        return bar;
    }

    public static string ExportShortcut(ShCfg sh, bool saveAllValues)
    {
        if (!saveAllValues)
        {
            sh = CopyObject(sh);
            CleanShortcut(sh);
        }
        return ExportObject(new ExportInfo { s2 = sh }, saveAllValues);
    }

    public static Shortcut ImportShortcut(string import)
    {
        var sh = ImportObject<Shortcut>(import);

        if (!allowImportHotkeys)
        {
            static void removeHotkeys(Shortcut sh)
            {
                sh.Hotkey = sh.GetDefaultValue(x => x.Hotkey);
                sh.KeyPassthrough = sh.GetDefaultValue(x => x.KeyPassthrough);
                if (sh.SubList is not { Count: > 0 }) return;

                foreach (var sub in sh.SubList)
                    removeHotkeys(sub);
            }
            removeHotkeys(sh);
        }

        return sh;
    }

    public static bool allowExportingSensitiveConditionSets = false;
    public static string ExportConditionSet(CndSetCfg cndSet) => ExportObject(new ExportInfo { cs = cndSet }, false);

    public static ImportInfo TryImport(string import, bool printError = false)
    {
        ExportInfo imported;
        try { imported = ImportObject<ExportInfo>(import); }
        catch (Exception e)
        {
            // If we failed to import the ExportInfo then this is an old version
            imported = ImportLegacy(import);
            if (imported == null && printError)
            {
                PluginLog.LogError($"Invalid import string!\n{e}");
                switch (e)
                {
                    case FormatException:
                        QoLBar.PrintError("Failed to import from clipboard! Import string is invalid or incomplete.");
                        break;
                    case JsonSerializationException:
                        QoLBar.PrintError("Failed to import from clipboard! Import string does not contain an importable object.");
                        break;
                    default:
                        QoLBar.PrintError($"Failed to import from clipboard!\n{e}");
                        break;
                }
            }
        }

        if (imported != null)
        {
            Legacy.UpdateImport(imported);

            if (imported.b1 != null)
                imported.b2 = imported.b1.Upgrade();
            else if (imported.s1 != null)
                imported.s2 = imported.s1.Upgrade(new BarConfig(), false);

            var conditionRemoved = false;
            if (!allowImportConditions && imported.b2 != null)
            {
                var d = imported.b2.GetDefaultValue(x => x.ConditionSet);
                if (imported.b2.ConditionSet != d)
                {
                    imported.b2.ConditionSet = d;
                    conditionRemoved = true;
                }
            }

            var pieRemoved = false;
            var hotkeyRemoved = false;
            if (!allowImportHotkeys)
            {
                void removeHotkeys(ShCfg sh)
                {
                    var d = sh.GetDefaultValue(x => x.Hotkey);
                    if (sh.Hotkey != d)
                    {
                        sh.Hotkey = d;
                        sh.KeyPassthrough = sh.GetDefaultValue(x => x.KeyPassthrough);
                        hotkeyRemoved = true;
                    }

                    if (sh.SubList is not { Count: > 0 }) return;

                    foreach (var sub in sh.SubList)
                        removeHotkeys(sub);
                }

                if (imported.b2 != null)
                {
                    var d = imported.b2.GetDefaultValue(x => x.Hotkey);
                    if (imported.b2.Hotkey != d)
                    {
                        imported.b2.Hotkey = d;
                        hotkeyRemoved = true;
                        pieRemoved = true;
                    }
                    foreach (var sh in imported.b2.ShortcutList)
                        removeHotkeys(sh);
                }

                if (imported.s2 != null)
                    removeHotkeys(imported.s2);
            }

            if (printError)
            {
                var msg = "This import contained {0} automatically removed, please enable \"{1}\" inside the \"Settings\" tab on the plugin config and then try importing again if you did not intend to do this.";
                if (conditionRemoved)
                    QoLBar.PrintEcho(string.Format(msg, "a condition set that was", "Allow importing conditions"));
                if (hotkeyRemoved)
                    QoLBar.PrintEcho(string.Format(msg, "one or more hotkeys that were", "Allow importing hotkeys"));
                if (pieRemoved)
                    QoLBar.PrintEcho("It appears that this bar was meant to be used as a pie. You should add a hotkey to it by right clicking on the bar and clicking the \"Pie Hotkey\" input box.");
            }
        }

        return new ImportInfo
        {
            bar = imported?.b2,
            shortcut = imported?.s2,
            conditionSet = imported?.cs
        };
    }

    private static ExportInfo ImportLegacy(string import)
    {
        var imported = new ExportInfo();
        try { imported.b1 = ImportBar(import); }
        catch
        {
            try { imported.s1 = ImportShortcut(import); }
            catch { return null; }
        }
        imported.v = "1.3.2.0";
        return imported;
    }
}