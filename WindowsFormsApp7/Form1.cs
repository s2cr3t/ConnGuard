using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MaxMind.GeoIP2;
using Newtonsoft.Json;

namespace WindowsFormsApp7
{
    public partial class Form1 : Form
    {
        private DataGridView dataGridView1;
        private CancellationTokenSource updateCts;

        private Dictionary<string, DataGridViewRow> currentConnections;
        private Dictionary<string, string> ipLocationCache;
        private HashSet<string> alertedConnections;
        private DatabaseReader geoDbReader;
        private string uniqueMachineId;

        public Form1()
        {
            InitializeComponent();

            currentConnections = new Dictionary<string, DataGridViewRow>();
            ipLocationCache = new Dictionary<string, string>();
            alertedConnections = new HashSet<string>();

            // 载入 MaxMind GeoLite2 数据库（请确保路径正确）
            geoDbReader = new DatabaseReader(@"C:\Users\Administrator\source\repos\WindowsFormsApp7\WindowsFormsApp7\GeoLite2-City.mmdb");

            // 生成机器码
            uniqueMachineId = GenerateMachineId();

            // 初始化 DataGridView
            dataGridView1 = new DataGridView
            {
                Name = "dataGridView1",
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                Font = new Font("Consolas", 10) // 等宽字体
            };
            this.Controls.Add(dataGridView1);

            this.Load += Form1_Load;
            this.FormClosing += Form1_FormClosing;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            MessageBox.Show("Unique Machine ID: " + uniqueMachineId);

            TextBox machineIdTextBox = new TextBox
            {
                Text = uniqueMachineId,
                ReadOnly = true,
                Dock = DockStyle.Top
            };
            this.Controls.Add(machineIdTextBox);

            dataGridView1.Columns.Add("colProto", "Proto");
            dataGridView1.Columns.Add("colLocal", "Local Address");
            dataGridView1.Columns.Add("colRemote", "Foreign Address");
            dataGridView1.Columns.Add("colState", "State");
            dataGridView1.Columns.Add("colLocation", "Location");
            dataGridView1.Columns.Add("colPID", "PID");
            dataGridView1.Columns.Add("colProcessName", "Process Name");
            dataGridView1.Columns.Add("colProcessPath", "Process Path");

            dataGridView1.Columns["colProto"].Width = 50;
            dataGridView1.Columns["colLocal"].Width = 150;
            dataGridView1.Columns["colRemote"].Width = 150;
            dataGridView1.Columns["colState"].Width = 100;
            dataGridView1.Columns["colLocation"].Width = 150;
            dataGridView1.Columns["colPID"].Width = 50;
            dataGridView1.Columns["colProcessName"].Width = 150;
            dataGridView1.Columns["colProcessPath"].Width = 300;

            updateCts = new CancellationTokenSource();
            _ = UpdateLoopAsync(updateCts.Token);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 取消更新循环
            updateCts?.Cancel();
        }

        /// <summary>
        /// 连接信息数据类，包含了所有更新 DataGridView 所需的信息。
        /// </summary>
        private class ConnectionInfo
        {
            public string Key { get; set; }
            public string Proto { get; set; }
            public string LocalEnd { get; set; }
            public string RemoteEnd { get; set; }
            public string State { get; set; }
            public int PID { get; set; }
            public string ProcessName { get; set; }
            public string ProcessPath { get; set; }
            public string Location { get; set; }
            public Color BackColor { get; set; }
        }

        /// <summary>
        /// 持续更新循环，每隔一定时间采集数据并更新 UI。
        /// </summary>
        private async Task UpdateLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                Color defaultBackColor = dataGridView1.DefaultCellStyle.BackColor;
                List<ConnectionInfo> connectionInfos = null;
                try
                {
                    connectionInfos = await Task.Run(() =>
                    {
                        var tcpConnections = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections();
                        Dictionary<string, int> pidMapping = GetPidMapping();
                        List<ConnectionInfo> list = new List<ConnectionInfo>();
                        foreach (var conn in tcpConnections)
                        {
                            string proto = "TCP";
                            string localEnd = conn.LocalEndPoint.ToString();
                            string remoteEnd = conn.RemoteEndPoint.ToString();
                            string state = conn.State.ToString();
                            string key = $"{proto}-{localEnd}-{remoteEnd}";

                            pidMapping.TryGetValue(key, out int pid);
                            var (processName, processPath) = GetProcessInfo(pid);
                            string remoteIp = conn.RemoteEndPoint.Address.ToString();
                            string locationString = LookupLocation(remoteIp);
                            Color backColor = defaultBackColor;
                            // 如果 IP 地理位置不包含 "china"，则视为海外
                            if (locationString != "N/A" && locationString != "Unknown" &&
                                !locationString.ToLower().Contains("china"))
                            {
                                backColor = Color.Yellow;
                            }

                            list.Add(new ConnectionInfo
                            {
                                Key = key,
                                Proto = proto,
                                LocalEnd = localEnd,
                                RemoteEnd = remoteEnd,
                                State = state,
                                PID = pid,
                                ProcessName = processName,
                                ProcessPath = processPath,
                                Location = locationString,
                                BackColor = backColor
                            });
                        }
                        return list;
                    }, token);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error in UpdateLoopAsync: " + ex.Message);
                }

                if (connectionInfos != null)
                {
                    // UI 更新在主线程上执行
                    UpdateUIFromConnectionInfos(connectionInfos);
                }

                // 等待 1 秒后继续下一次更新
                try
                {
                    await Task.Delay(1000, token);
                }
                catch { }
            }
        }

        /// <summary>
        /// 根据最新采集到的 ConnectionInfo 集合更新 DataGridView。
        /// 同时当发现海外 IP（行背景为黄色）且该连接未报警时，
        /// 调用企业微信机器人接口发送报警信息，内容包括报警时间、IP、进程、目录和机器码。
        /// </summary>
        private void UpdateUIFromConnectionInfos(List<ConnectionInfo> newInfos)
        {
            Dictionary<string, ConnectionInfo> newMapping = new Dictionary<string, ConnectionInfo>();
            foreach (var info in newInfos)
            {
                newMapping[info.Key] = info;
            }
            HashSet<string> keysToRemove = new HashSet<string>(currentConnections.Keys);

            foreach (var kvp in newMapping)
            {
                string key = kvp.Key;
                ConnectionInfo info = kvp.Value;

                if (currentConnections.TryGetValue(key, out DataGridViewRow row))
                {
                    row.Cells["colState"].Value = info.State;
                    row.Cells["colPID"].Value = info.PID != 0 ? info.PID.ToString() : "N/A";
                    row.Cells["colProcessName"].Value = info.ProcessName;
                    row.Cells["colProcessPath"].Value = info.ProcessPath;
                    row.Cells["colLocation"].Value = info.Location;
                    row.DefaultCellStyle.BackColor = info.BackColor;
                    keysToRemove.Remove(key);
                }
                else
                {
                    int index = dataGridView1.Rows.Add(info.Proto, info.LocalEnd, info.RemoteEnd, info.State, info.Location,
                                                         info.PID != 0 ? info.PID.ToString() : "N/A", info.ProcessName, info.ProcessPath);
                    DataGridViewRow newRow = dataGridView1.Rows[index];
                    newRow.DefaultCellStyle.BackColor = info.BackColor;
                    currentConnections.Add(key, newRow);
                }

                // 当发现海外 IP（背景为黄色）且该连接未报警时，发送报警信息
                if (info.BackColor == Color.Yellow && !alertedConnections.Contains(info.Key))
                {
                    alertedConnections.Add(info.Key);
                    _ = SendWeChatAlert(info);
                }
            }
            // 删除已消失的连接对应的行，并清除报警记录
            foreach (string vanishedKey in keysToRemove)
            {
                if (currentConnections.TryGetValue(vanishedKey, out DataGridViewRow row))
                {
                    dataGridView1.Rows.Remove(row);
                    currentConnections.Remove(vanishedKey);
                }
                if (alertedConnections.Contains(vanishedKey))
                {
                    alertedConnections.Remove(vanishedKey);
                }
            }
        }
        /// <summary>
        /// 调用企业微信机器人接口发送报警信息
        /// </summary>
        private async Task SendWeChatAlert(ConnectionInfo info)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    // 构造报警内容（包含报警时间、IP、地区、进程名、目录和机器码）
                    string contentText = $"报警时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                                         $"IP: {info.RemoteEnd}\n" +
                                         $"地区: {info.Location}\n" +
                                         $"进程: {info.ProcessName}\n" +
                                         $"目录: {info.ProcessPath}\n" +
                                         $"机器码: {uniqueMachineId}";

                    var payload = new
                    {
                        msgtype = "text",
                        text = new
                        {
                            content = contentText
                        }
                    };

                    string jsonPayload = JsonConvert.SerializeObject(payload);
                    using (var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json"))
                    {
                        // 请将下面 URL 替换为你的企业微信机器人 Webhook 地址
                        string webhookUrl = "https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key=fff9825a-9b8e-4124-9448-2f7a9061cd75";
                        HttpResponseMessage response = await client.PostAsync(webhookUrl, httpContent);
                        response.EnsureSuccessStatusCode();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SendWeChatAlert error: " + ex.Message);
            }
        }


        // 以下方法与之前基本相同……

        private Dictionary<string, int> GetPidMapping()
        {
            Dictionary<string, int> mapping = new Dictionary<string, int>();
            int buffSize = 0;
            GetExtendedTcpTable(IntPtr.Zero, ref buffSize, true, 2, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_CONNECTIONS);
            IntPtr buffTable = Marshal.AllocHGlobal(buffSize);
            try
            {
                uint ret = GetExtendedTcpTable(buffTable, ref buffSize, true, 2, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_CONNECTIONS);
                if (ret != 0)
                    return mapping;

                int rowsCount = Marshal.ReadInt32(buffTable);
                IntPtr rowPtr = (IntPtr)((long)buffTable + 4);
                for (int i = 0; i < rowsCount; i++)
                {
                    MIB_TCPROW_OWNER_PID tcpRow = (MIB_TCPROW_OWNER_PID)Marshal.PtrToStructure(rowPtr, typeof(MIB_TCPROW_OWNER_PID));
                    IPEndPoint localEP = new IPEndPoint(tcpRow.LocalAddr,
                        BitConverter.ToUInt16(new byte[2] { tcpRow.LocalPort[1], tcpRow.LocalPort[0] }, 0));
                    IPEndPoint remoteEP = new IPEndPoint(tcpRow.RemoteAddr,
                        BitConverter.ToUInt16(new byte[2] { tcpRow.RemotePort[1], tcpRow.RemotePort[0] }, 0));
                    string key = $"TCP-{localEP}-{remoteEP}";
                    if (!mapping.ContainsKey(key))
                    {
                        mapping.Add(key, (int)tcpRow.OwningPid);
                    }
                    rowPtr = (IntPtr)((long)rowPtr + Marshal.SizeOf(tcpRow));
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffTable);
            }
            return mapping;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MIB_TCPROW_OWNER_PID
        {
            public uint State;
            public uint LocalAddr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] LocalPort;
            public uint RemoteAddr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] RemotePort;
            public uint OwningPid;
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int dwOutBufLen, bool sort,
            int ipVersion, TCP_TABLE_CLASS tblClass, uint reserved = 0);

        enum TCP_TABLE_CLASS
        {
            TCP_TABLE_BASIC_LISTENER,
            TCP_TABLE_BASIC_CONNECTIONS,
            TCP_TABLE_BASIC_ALL,
            TCP_TABLE_OWNER_PID_LISTENER,
            TCP_TABLE_OWNER_PID_CONNECTIONS,
            TCP_TABLE_OWNER_PID_ALL,
            TCP_TABLE_OWNER_MODULE_LISTENER,
            TCP_TABLE_OWNER_MODULE_CONNECTIONS,
            TCP_TABLE_OWNER_MODULE_ALL
        }

        private (string processName, string processPath) GetProcessInfo(int pid)
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

        private string LookupLocation(string ipString)
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

        private string GenerateMachineId()
        {
            try
            {
                string hardDriveSerial = GetHardDriveSerial();
                string macAddress = GetMacAddress();
                return $"{hardDriveSerial}-{macAddress}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating machine ID: {ex.Message}");
                return "Unknown";
            }
        }

        private string GetHardDriveSerial()
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
                MessageBox.Show($"Error retrieving hard drive serial number: {ex.Message}");
            }
            return serialNumber;
        }

        private string GetMacAddress()
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
                MessageBox.Show($"Error retrieving MAC address: {ex.Message}");
            }
            return macAddress;
        }
    }
}
