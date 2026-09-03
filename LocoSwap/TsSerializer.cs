using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Serilog;

namespace LocoSwap
{
    static class TsSerializer
    {
        private static void RunSerz(string inputPath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Utilities.GetSerzPath(),
                Arguments = "\"" + inputPath + "\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            if (!File.Exists(startInfo.FileName))
            {
                throw new FileNotFoundException(
                    "serz.exe was not found. Check the Train Simulator path in settings.", startInfo.FileName);
            }

            using Process process = new Process { StartInfo = startInfo };

            // Drain both streams asynchronously so a chatty serz can never deadlock us
            var output = new StringBuilder();
            var error = new StringBuilder();
            process.OutputDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) error.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            Log.Debug("serz {Input} exited with code {Code}. Output: {Output}",
                Path.GetFileName(inputPath), process.ExitCode, output.ToString().Trim());

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"serz.exe failed (exit code {process.ExitCode}) processing '{Path.GetFileName(inputPath)}'. {error.ToString().Trim()}");
            }
        }
        public static XDocument Load(string binPath)
        {
            string xmlPath = BinToXml(binPath);
            try
            {
                return XmlDocumentLoader.Load(xmlPath);
            }
            finally
            {
                // Remove the scratch .bin/.xml copies BinToXml made. Skipped when the source
                // was already inside the temp dir (an .ap workdir the caller cleans up itself).
                if (!binPath.StartsWith(Utilities.GetTempDir(), StringComparison.OrdinalIgnoreCase))
                {
                    Utilities.RemoveFile(xmlPath);
                    Utilities.RemoveFile(Path.ChangeExtension(xmlPath, "bin"));
                }
            }
        }

        public static string BinToXml(string binPath)
        {
            FileInfo binInfo = new FileInfo(binPath);
            string baseName = Path.GetFileNameWithoutExtension(binInfo.Name);
            string tempName = string.Format("{0}-{1}.bin", baseName, Guid.NewGuid().ToString("N"));

            string tempBinPath;
            string tempXmlPath;

            if (!binPath.StartsWith(Utilities.GetTempDir()))
            {
                tempBinPath = Path.Combine(Utilities.GetTempDir(), tempName);
                tempXmlPath = Path.ChangeExtension(tempBinPath, "xml");
                Utilities.RemoveFile(tempBinPath);
                Utilities.RemoveFile(tempXmlPath);

                File.Copy(binPath, tempBinPath);
            }
            else
            {
                tempBinPath = binPath;
                tempXmlPath = Path.ChangeExtension(tempBinPath, "xml");
            }

            RunSerz(tempBinPath);

            if (!File.Exists(tempXmlPath))
            {
                throw new FileNotFoundException("serz.exe did not produce the expected XML output.", tempXmlPath);
            }

            return tempXmlPath;
        }

        public static void Save(XDocument document, string path)
        {
            string xmlPath = Path.ChangeExtension(path, "xml");
            Utilities.RemoveFile(xmlPath);
            Utilities.RemoveFile(path);

            XmlWriterSettings xmlWriterSettings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "\t",
                Encoding = new UTF8Encoding(false),
                NewLineHandling = NewLineHandling.None,
            };

            using (FileStream stream = new FileStream(xmlPath, FileMode.Create))
            using (XmlWriter writer = XmlWriter.Create(stream, xmlWriterSettings))
            {
                document.Save(writer);
            }

            RunSerz(xmlPath);

            if (!File.Exists(path) || new FileInfo(path).Length == 0)
            {
                throw new InvalidOperationException(
                    "serz.exe did not produce a valid .bin file - the scenario was not saved.");
            }
        }
    }
}
