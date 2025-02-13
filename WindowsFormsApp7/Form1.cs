using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApp7
{
    public partial class Form1 : Form
    {
        private DataGridView dataGridView1;
        private CancellationTokenSource updateCts;
        private Dictionary<string, DataGridViewRow> currentConnections;
        private HashSet<string> alertedConnections;
        private string uniqueMachineId;

        public Form1()
        {
            InitializeComponent();
            currentConnections = new Dictionary<string, DataGridViewRow>();
            alertedConnections = new HashSet<string>();
            uniqueMachineId = Utils.GenerateMachineId();

            dataGridView1 = new DataGridView
            {
                Name = "dataGridView1",
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                Font = new Font("Consolas", 10)
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
            updateCts?.Cancel();
        }

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
                        Dictionary<string, int> pidMapping = TcpConnectionHelper.GetPidMapping();  // 获取TCP连接与PID映射
                        List<ConnectionInfo> list = new List<ConnectionInfo>();
                        foreach (var conn in tcpConnections)
                        {
                            string proto = "TCP";
                            string localEnd = conn.LocalEndPoint.ToString();
                            string remoteEnd = conn.RemoteEndPoint.ToString();
                            string state = conn.State.ToString();
                            string key = $"{proto}-{localEnd}-{remoteEnd}";

                            pidMapping.TryGetValue(key, out int pid);
                            var (processName, processPath) = Utils.GetProcessInfo(pid);  // 获取进程信息
                            string remoteIp = conn.RemoteEndPoint.Address.ToString();
                            string locationString = Utils.LookupLocation(remoteIp);  // 获取IP地理位置
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

                // 更新或新增连接信息
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

                // 替换 SendWeChatAlert 为 WeChatAlert.SendAlert
                if (info.BackColor == Color.Yellow && !alertedConnections.Contains(info.Key))
                {
                    alertedConnections.Add(info.Key);
                    _ = WeChatAlert.SendAlert(info.RemoteEnd, info.Location, info.ProcessName, info.ProcessPath, uniqueMachineId);  // 异步调用企业微信报警
                }

            }

            // 删除已消失的连接并清除报警记录
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

    }
}
