# ConnGuard

`ConnGuard` 是一个用于监控网络连接、识别海外 IP、报警并记录机器唯一标识的工具。它提供了实时的 TCP 连接监控，帮助用户及时了解网络状态，并对不安全的海外 IP 地址发出警报。
![image](https://github.com/user-attachments/assets/d5f9d840-238c-4e8e-bb68-a2c323a4a902)

## 功能

- 实时显示 TCP 连接信息，包括协议、IP 地址、状态、进程名称等。
- 检测连接是否来自海外 IP（不包含中国），并为此类连接提供视觉标识（黄色背景）。
- 对海外 IP 连接发出警报，报警内容包括时间、IP 地址、进程信息、机器唯一标识等。
- 显示当前机器的唯一标识，用于设备识别。
- 支持 GeoLite2 数据库，提供 IP 地理位置信息。

## 安装

1. **系统要求**  
   - Windows 操作系统（建议 Windows 10 或更高版本）
   - .NET Framework 4.8 或更高版本

2. **下载与安装**  
   下载源代码或发布版本后，解压并运行 `ConnGuard.exe`。

## 使用方法

1. 启动 `ConnGuard`，界面将显示所有当前活动的 TCP 连接信息，包括本地和远程地址、状态等。
2. 所有来自海外 IP（不含中国）的连接行背景会被标记为黄色。
3. 当发现海外 IP 时，系统会自动触发报警并通过企业微信发送警报信息。
4. 您还可以查看当前设备的唯一机器 ID，它由硬盘序列号和 MAC 地址生成。

## 配置

- 在工具启动时，您可以查看并复制机器唯一 ID，该 ID 用于区分不同设备。
- `ConnGuard` 使用 MaxMind GeoLite2-City 数据库进行 IP 地址地理位置查询。请确保数据库文件路径设置正确。

## 项目结构

ConnGuard/ │ ├── ConnGuard.exe # 可执行文件 ├── GeoLite2-City.mmdb # MaxMind GeoLite2 数据库 └── README.md # 本文档

## 贡献

欢迎对 `ConnGuard` 提出改进建议或提交问题报告！如果您希望贡献代码，请按以下步骤：

1. Fork 本仓库
2. 创建您的特性分支 (`git checkout -b feature-branch`)
3. 提交您的修改 (`git commit -am 'Add new feature'`)
4. 推送到您的分支 (`git push origin feature-branch`)
5. 创建 Pull Request

## 许可证

此工具采用 MIT 许可证，详情请参见 `LICENSE` 文件。
