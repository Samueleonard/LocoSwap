#nullable enable
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Xml.Linq;
using LocoSwap.Properties;

namespace LocoSwap
{
    static class Utilities
    {
        private static readonly XNamespace Namespace = "http://www.kuju.com/TnT/2003/Delta";

        private static readonly FrozenDictionary<string, string> LanguageConversionTable = new Dictionary<string, string>
        {
            { "en", "English" },
            { "fr", "French" },
            { "it", "Italian" },
            { "de", "German" },
            { "es", "Spanish" },
            { "nl", "Dutch" },
            { "pl", "Polish" },
            { "ru", "Russian" },
        }.ToFrozenDictionary();

        public static string GetTempDir()
        {
            // Anchored to the install directory rather than the current working directory,
            // which serz.exe and file dialogs can move out from under us.
            return Path.Combine(AppContext.BaseDirectory, "temp");
        }
        public static string GetSerzPath()
        {
            return Path.Combine(Properties.Settings.Default.TsPath, "serz.exe");
        }
        public static void RemoveFile(string path)
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }
        }
        public static bool ChangeTsPath()
        {
            var valid = false;
            while (!valid)
            {
                var dialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = Language.Resources.select_ts_path
                };

                if (dialog.ShowDialog() != true)
                {
                    return false;
                }
                var path = dialog.FolderName;
                var tsExe = Path.Combine(path, "RailWorks.exe");
                if (!File.Exists(tsExe))
                {
                    MessageBox.Show(Language.Resources.msg_ts_path_invalid, Language.Resources.msg_error, MessageBoxButton.OK, MessageBoxImage.Warning);
                    continue;
                }
                Settings.Default.TsPath = path;
                Settings.Default.Save();
                valid = true;
            }
            return true;
        }

        public static void CopyUserLocalisedString(XElement? dest, XElement? orig)
        {
            if (dest == null || orig == null) return;
            var names = new string[] { "English", "French", "Italian", "German", "Spanish", "Dutch", "Polish", "Russian", "Key" };
            foreach (var name in names)
            {
                XElement? destName = dest.Element(name);
                XElement? origName = orig.Element(name);
                if (destName != null && origName != null)
                {
                    destName.Value = origName.Value;
                }
            }
            dest.Element("Other")?.Elements().Remove();
        }

        public static string DetermineDisplayName(XElement? localisedString)
        {
            if (localisedString == null) return "";

            var lang = Settings.Default.Language;
            if (lang == "") lang = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            var convertedLang = LanguageConversionTable.GetValueOrDefault(lang, "English");
            XElement? preferredElement = localisedString.Element(convertedLang);
            if (preferredElement != null)
            {
                if (preferredElement.Value != "") return preferredElement.Value;
            }

            var result = "";
            foreach (XElement localisedName in localisedString.Elements())
            {
                if (localisedName.Name == "Other" || localisedName.Name == "Key") continue;
                if (localisedName.Value != "")
                {
                    result = localisedName.Value;
                    break;
                }
            }
            return result;
        }

        public static (ulong Low, ulong High) GetUUIDLongs(Guid guid)
        {
            return (
                BitConverter.ToUInt64(guid.ToByteArray(), 0),
                BitConverter.ToUInt64(guid.ToByteArray(), 8));
        }

        public static XElement GenerateCGUID()
        {
            var guid = Guid.NewGuid();
            var ulongs = GetUUIDLongs(guid);
            var cGUID = new XElement("cGUID");
            var UUID = new XElement("UUID");

            var e1 = new XElement("e");
            e1.SetAttributeValue(Namespace + "type", "sUInt64");
            e1.SetValue(ulongs.Low);

            var e2 = new XElement("e");
            e2.SetAttributeValue(Namespace + "type", "sUInt64");
            e2.SetValue(ulongs.High);

            UUID.Add(e1, e2);

            var devString = new XElement("DevString");
            devString.SetAttributeValue(Namespace + "type", "cDeltaString");
            devString.SetValue(guid.ToString());

            cGUID.Add(UUID, devString);
            return cGUID;
        }

        public static XElement GenerateEntityContainerItem()
        {
            XElement newNode = new XElement("e");
            newNode.SetAttributeValue(Namespace + "numElements", "16");
            newNode.SetAttributeValue(Namespace + "elementType", "sFloat32");
            newNode.SetAttributeValue(Namespace + "precision", "string");
            newNode.SetValue("1.0000000 0.0000000 0.0000000 0.0000000 0.0000000 1.0000000 0.0000000 0.0000000 0.0000000 0.0000000 1.0000000 0.0000000 0.0000000 0.0000000 0.0000000 1.0000000");
            return newNode;
        }

        public static XElement GenerateCargoComponentItem(string val, string altEncoding)
        {
            XElement newNode = new XElement("e");
            newNode.SetAttributeValue(Namespace + "type", "sFloat32");
            newNode.SetAttributeValue(Namespace + "alt_encoding", altEncoding);
            newNode.SetAttributeValue(Namespace + "precision", "string");
            newNode.SetValue(val);
            return newNode;
        }

        /// <summary>URL opened by the Help button when the bundled PDF manual isn't present.</summary>
        public const string OnlineHelpUrl = "https://github.com/flicard/LocoSwap";

        public static void OpenManual()
        {
            const string manualFileName = "LocoSwap_manual.pdf";
            string target = File.Exists(manualFileName)
                ? Path.GetFullPath(manualFileName)
                : OnlineHelpUrl;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(target) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Could not open help target {Target}", target);
            }
        }
    }
}
