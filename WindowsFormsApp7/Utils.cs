using System;
using System.Net.NetworkInformation;
using System.Management;
using MaxMind.GeoIP2;
using System.Net;
using System.Collections.Generic;
using System.Diagnostics;

namespace WindowsFormsApp7
{
    public static class Utils
    {
        private static DatabaseReader geoDbReader = new DatabaseReader(@"C:\Users\Administrator\source\repos\WindowsFormsApp7\WindowsFormsApp7\GeoLite2-City.mmdb");
        private static Dictionary<string, string> ipLocationCache = new Dictionary<string, string>();

        // 生成机器码的逻辑
        public static string GenerateMachineId()
        {
            try
            {
                string hardDriveSerial = GetHardDriveSerial();
                string macAddress = GetMacAddress();
                return $"{hardDriveSerial}-{macAddress}";
            }
            catch (Exception ex)
            {
                return $"Error generating machine ID: {ex.Message}";
            }
        }

        // IP 地理位置查询
        public static string LookupLocation(string ipString)
        {
            if (ipLocationCache.ContainsKey(ipString))
            {
                return ipLocationCache[ipString];
            }

            string result = "Unknown";
            if (IPAddress.TryParse(ipString, out IPAddress address))
            {
                try
                {
                    var cityResponse = geoDbReader.City(address);
                    string country = cityResponse.Country?.Name ?? "N/A";
                    string city = cityResponse.City?.Name ?? "";
                    result = $"{country} {city}".Trim();
                    if (string.IsNullOrWhiteSpace(result))
                        result = country;
                }
                catch
                {
                    result = "N/A";
                }
            }

            ipLocationCache[ipString] = result;
            return result;
        }

        // 获取进程信息
        public static (string processName, string processPath) GetProcessInfo(int pid)
        {
            string processName = "N/A";
            string processPath = "N/A";

            if (pid == 0)
                return (processName, processPath);

            try
            {
                using (var process = Process.GetProcessById(pid))
                {
                    processName = process.ProcessName;
                    processPath = process.MainModule?.FileName ?? "N/A";
                }
            }
            catch
            {
                try
                {
                    using (var searcher = new ManagementObjectSearcher(
                        $"SELECT Name, ExecutablePath FROM Win32_Process WHERE ProcessId = {pid}"))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            processName = obj["Name"]?.ToString() ?? "N/A";
                            processPath = obj["ExecutablePath"]?.ToString() ?? "N/A";
                            break;
                        }
                    }
                }
                catch { }
            }

            return (processName, processPath);
        }

        // 获取 MAC 地址
        public static string GetMacAddress()
        {
            string macAddress = string.Empty;
            try
            {
                NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
                if (nics.Length > 0)
                    macAddress = nics[0].GetPhysicalAddress().ToString();
            }
            catch (Exception ex)
            {
                // 错误处理
            }

            return macAddress;
        }

        // 获取硬盘序列号
        public static string GetHardDriveSerial()
        {
            string serialNumber = string.Empty;
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(@"SELECT * FROM Win32_PhysicalMedia");
                foreach (ManagementObject disk in searcher.Get())
                {
                    if (disk["SerialNumber"] != null)
                    {
                        serialNumber = disk["SerialNumber"].ToString().Trim();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                // 错误处理
            }
            return serialNumber;
        }
    }
}
