using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace ImasKoreanPatcher
{
    internal static class XexToolValidator
    {
        private const int ValidationTimeoutMilliseconds = 5000;
        private static readonly Regex VersionPattern = new Regex(
            @"\bXexTool\s+v?(\d+\.\d+)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static bool IsSupported(string path, out string detectedVersion)
        {
            detectedVersion = String.Empty;
            if (String.IsNullOrEmpty(path)
                || !File.Exists(path)
                || !String.Equals(Path.GetFileName(path), "xextool.exe", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = path;
                string workingDirectory = Path.GetDirectoryName(path);
                startInfo.WorkingDirectory = String.IsNullOrEmpty(workingDirectory)
                    ? Environment.CurrentDirectory
                    : workingDirectory;
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;

                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.Start();
                    if (!process.WaitForExit(ValidationTimeoutMilliseconds))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                        }
                        return false;
                    }

                    string output = process.StandardOutput.ReadToEnd() + Environment.NewLine + process.StandardError.ReadToEnd();
                    Match match = VersionPattern.Match(output);
                    if (!match.Success)
                    {
                        return false;
                    }

                    detectedVersion = match.Groups[1].Value;
                    return String.Equals(detectedVersion, "6.3", StringComparison.Ordinal);
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
