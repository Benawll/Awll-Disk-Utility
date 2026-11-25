using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace Awll
{
    internal static class ExecutionHistoryService
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint QueryDosDevice(string lpDeviceName, StringBuilder lpTargetPath, int ucchMax);

        private static readonly object _lock = new();
        private static Dictionary<string, DateTime> _exeLastRun = new(StringComparer.OrdinalIgnoreCase);
        private static DateTime _lastLoadedUtc = DateTime.MinValue;

        public static void RefreshIfNeeded(bool force = false)
        {
            lock (_lock)
            {
                if (!force && (DateTime.UtcNow - _lastLoadedUtc).TotalSeconds < 30 && _exeLastRun.Count > 0)
                    return;

                var map = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
                var deviceMap = BuildDeviceToDriveMap();
                var nowLimit = DateTime.Now.AddDays(1);

                // 1. Windows BAM (Background Activity Moderator) - timestamps exacts d'exécution par chemin .exe
                try
                {
                    var sid = WindowsIdentity.GetCurrent()?.User?.Value;
                    if (!string.IsNullOrEmpty(sid))
                    {
                        string[] bamSubKeys =
                        {
                            $@"SYSTEM\CurrentControlSet\Services\bam\State\UserSettings\{sid}",
                            $@"SYSTEM\CurrentControlSet\Services\bam\UserSettings\{sid}"
                        };
                        foreach (var subKey in bamSubKeys)
                        {
                            try
                            {
                                using var key = Registry.LocalMachine.OpenSubKey(subKey);
                                if (key == null) continue;
                                foreach (var valName in key.GetValueNames())
                                {
                                    if (!valName.StartsWith(@"\Device\", StringComparison.OrdinalIgnoreCase)) continue;
                                    if (key.GetValue(valName) is byte[] bytes && bytes.Length >= 8)
                                    {
                                        long ft = BitConverter.ToInt64(bytes, 0);
                                        if (ft <= 0) continue;
                                        var dt = DateTime.FromFileTimeUtc(ft).ToLocalTime();
                                        if (dt.Year < 2015 || dt > nowLimit) continue;

                                        var dosPath = ConvertDevicePathToDosPath(valName, deviceMap);
                                        if (!string.IsNullOrEmpty(dosPath))
                                        {
                                            if (!map.TryGetValue(dosPath, out var existing) || dt > existing)
                                                map[dosPath] = dt;
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }

                // 2. Windows Explorer UserAssist (ROT13)
                try
                {
                    using var uaRoot = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist");
                    if (uaRoot != null)
                    {
                        foreach (var guidName in uaRoot.GetSubKeyNames())
                        {
                            try
                            {
                                using var countKey = uaRoot.OpenSubKey($@"{guidName}\Count");
                                if (countKey == null) continue;
                                foreach (var encodedName in countKey.GetValueNames())
                                {
                                    var decoded = Rot13(encodedName);
                                    if (decoded.Length < 4 || decoded[1] != ':') continue;
                                    if (countKey.GetValue(encodedName) is byte[] bytes && bytes.Length >= 68)
                                    {
                                        long ft = BitConverter.ToInt64(bytes, 60);
                                        if (ft <= 0) continue;
                                        var dt = DateTime.FromFileTimeUtc(ft).ToLocalTime();
                                        if (dt.Year < 2015 || dt > nowLimit) continue;

                                        if (!map.TryGetValue(decoded, out var existing) || dt > existing)
                                            map[decoded] = dt;
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }

                _exeLastRun = map;
                _lastLoadedUtc = DateTime.UtcNow;
            }
        }

        public static bool TryGetFileLastExecution(string filePath, out DateTime lastExec)
        {
            lastExec = DateTime.MinValue;
            if (string.IsNullOrWhiteSpace(filePath)) return false;
            RefreshIfNeeded();
            lock (_lock)
            {
                return _exeLastRun.TryGetValue(filePath.Trim(), out lastExec);
            }
        }

        public static bool TryGetFolderLastExecution(string folderPath, out DateTime lastExec, Dictionary<string, DateTime>? overrideMap = null)
        {
            lastExec = DateTime.MinValue;
            if (string.IsNullOrWhiteSpace(folderPath)) return false;

            Dictionary<string, DateTime> source;
            if (overrideMap != null)
            {
                source = overrideMap;
            }
            else
            {
                RefreshIfNeeded();
                lock (_lock) { source = _exeLastRun; }
            }

            string prefix = folderPath.TrimEnd('\\') + @"\";
            bool found = false;
            foreach (var kv in source)
            {
                if (kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    var exeName = Path.GetFileName(kv.Key);
                    if (exeName.StartsWith("unins", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!found || kv.Value > lastExec)
                    {
                        lastExec = kv.Value;
                        found = true;
                    }
                }
            }
            return found;
        }

        internal static string ConvertDevicePathToDosPath(string devicePath, Dictionary<string, string> deviceToDriveMap)
        {
            foreach (var kv in deviceToDriveMap)
            {
                if (devicePath.StartsWith(kv.Key + @"\", StringComparison.OrdinalIgnoreCase))
                {
                    return kv.Value + devicePath.Substring(kv.Key.Length);
                }
            }
            return "";
        }

        private static Dictionary<string, string> BuildDeviceToDriveMap()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var d in DriveInfo.GetDrives())
                {
                    if (!d.IsReady) continue;
                    string letter = d.Name.TrimEnd('\\');
                    var sb = new StringBuilder(512);
                    if (QueryDosDevice(letter, sb, sb.Capacity) > 0)
                    {
                        var dev = sb.ToString();
                        if (!string.IsNullOrEmpty(dev))
                            map[dev] = letter;
                    }
                }
            }
            catch { }
            return map;
        }

        private static string Rot13(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            var chars = input.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (c >= 'a' && c <= 'z') chars[i] = (char)('a' + (c - 'a' + 13) % 26);
                else if (c >= 'A' && c <= 'Z') chars[i] = (char)('A' + (c - 'A' + 13) % 26);
            }
            return new string(chars);
        }
    }
}
