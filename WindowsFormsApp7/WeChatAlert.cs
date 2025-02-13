using Newtonsoft.Json;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System;

public static class WeChatAlert
{
    public static async Task SendAlert(string remoteIp, string location, string processName, string processPath, string uniqueMachineId)
    {
        try
        {
            using (HttpClient client = new HttpClient())
            {
                string contentText = $"报警时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                                     $"IP: {remoteIp}\n" +
                                     $"地区: {location}\n" +
                                     $"进程: {processName}\n" +
                                     $"目录: {processPath}\n" +
                                     $"机器码: {uniqueMachineId}";

                var payload = new
                {
                    msgtype = "text",
                    text = new { content = contentText }
                };

                string jsonPayload = JsonConvert.SerializeObject(payload);
                using (var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json"))
                {
                    string webhookUrl = "";  // 请确保替换为正确的 Webhook 地址
                    HttpResponseMessage response = await client.PostAsync(webhookUrl, httpContent);
                    response.EnsureSuccessStatusCode();
                }
            }
        }
        catch (Exception ex)
        {
            // 错误处理
            Debug.WriteLine($"Error sending WeChat alert: {ex.Message}");
        }
    }
}
