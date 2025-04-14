using System;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading.Tasks;
using NetworkDiagnosticTool.Logging;
using System.IO;
using System.Text.Json;
using System.Net.NetworkInformation;
using System.Linq;
using Microsoft.Win32;
using System.Text;
using System.Security.Principal;
using NetworkDiagnosticTool.UI;

namespace NetworkDiagnosticTool
{
    public class NetworkConfigBackupForm : Form
    {
        private readonly TableLayoutPanel mainLayout = new();
        private readonly FlowLayoutPanel actionPanel = new();
        private readonly RichTextBox resultBox = new()
        {
            ReadOnly = true,
            BackColor = Color.Black,
            ForeColor = Color.White,
            Font = new Font("Consolas", 9F),
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = RichTextBoxScrollBars.Both,
            WordWrap = false
        };

        private bool isProcessing = false;
        private readonly string backupDirectory;

        public NetworkConfigBackupForm()
        {
            backupDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NetworkBackups");
            Directory.CreateDirectory(backupDirectory);
            InitializeComponents();
            Logger.Instance.Log("网络配置备份窗口已启动", LogLevel.Info, LogCategory.NetworkRepair);
        }

        private void InitializeComponents()
        {
            Text = "网络配置备份与还原";
            Size = new Size(800, 600);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(30, 30, 30);
            ForeColor = Color.White;

            mainLayout.RowCount = 2;
            mainLayout.ColumnCount = 1;
            mainLayout.Dock = DockStyle.Fill;
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 70));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
            mainLayout.Padding = new Padding(10);

            actionPanel.Dock = DockStyle.Fill;
            actionPanel.AutoScroll = true;
            actionPanel.BackColor = Color.FromArgb(35, 35, 35);
            actionPanel.FlowDirection = FlowDirection.TopDown;
            actionPanel.WrapContents = false;
            actionPanel.AutoSize = false;
            actionPanel.Padding = new Padding(5);
            actionPanel.Margin = new Padding(0);

            actionPanel.SizeChanged += (s, e) =>
            {
                foreach (Control control in actionPanel.Controls)
                {
                    control.Width = actionPanel.ClientSize.Width - 20;
                    if (control is Panel panel)
                    {
                        foreach (Control c in panel.Controls)
                        {
                            if (c is Button button)
                            {
                                button.Location = new Point(panel.Width - 130, 25);
                            }
                        }
                    }
                }
            };

            // 添加功能选项
            AddActionOption("备份网络配置", "保存当前所有网络适配器和TCP/IP设置", BackupNetworkConfig);
            AddActionOption("还原网络配置", "从备份文件还原网络配置", RestoreNetworkConfig);
            AddActionOption("导出注册表配置", "导出网络相关的注册表配置", ExportRegistryConfig);
            AddActionOption("导入注册表配置", "导入网络相关的注册表配置", ImportRegistryConfig);
            AddActionOption("优化网络设置", "自动优化TCP/IP参数", OptimizeNetworkSettings);

            mainLayout.Controls.Add(actionPanel, 0, 0);
            mainLayout.Controls.Add(resultBox, 0, 1);

            Controls.Add(mainLayout);
        }

        private void AddActionOption(string title, string description, Func<Task> action)
        {
            var panel = new Panel
            {
                Width = actionPanel.ClientSize.Width - 20,
                Height = 80,
                Margin = new Padding(0, 0, 0, 5),
                BackColor = Color.FromArgb(45, 45, 45),
                Padding = new Padding(15)
            };

            var titleLabel = new Label
            {
                Text = title,
                Font = new Font("Microsoft YaHei", 10, FontStyle.Bold),
                ForeColor = MessageColors.Normal,
                AutoSize = true,
                Location = new Point(15, 15)
            };

            var descLabel = new Label
            {
                Text = description,
                Font = new Font("Microsoft YaHei", 9),
                ForeColor = MessageColors.Command,
                AutoSize = true,
                Location = new Point(15, 40)
            };

            var actionButton = new Button
            {
                Text = "执行",
                BackColor = MessageColors.Progress,
                ForeColor = MessageColors.Normal,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(100, 30),
                Location = new Point(panel.Width - 130, 25),
                Margin = new Padding(0)
            };

            actionButton.Click += async (s, e) =>
            {
                if (isProcessing)
                {
                    MessageBox.Show("有操作正在进行中，请等待完成", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                try
                {
                    isProcessing = true;
                    actionButton.Enabled = false;
                    AppendTextWithStyle($"\n开始{title}...\n", MessageColors.Start, true);
                    Logger.Instance.Log($"开始{title}", LogLevel.Info, LogCategory.NetworkRepair);

                    await action();

                    AppendTextWithStyle($"{title}完成\n", MessageColors.Success, true);
                    Logger.Instance.Log($"{title}完成", LogLevel.Info, LogCategory.NetworkRepair);
                }
                catch (Exception ex)
                {
                    AppendTextWithStyle($"{title}失败: {ex.Message}\n", MessageColors.Error);
                    Logger.Instance.LogError($"{title}失败", ex, LogCategory.NetworkRepair);
                    MessageBox.Show($"操作失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    actionButton.Enabled = true;
                    isProcessing = false;
                }
            };

            panel.Controls.AddRange(new Control[] { titleLabel, descLabel, actionButton });
            actionPanel.Controls.Add(panel);
        }

        private void AppendTextWithStyle(string text, Color? color = null, bool isBold = false)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => AppendTextWithStyle(text, color, isBold)));
                return;
            }

            resultBox.SelectionStart = resultBox.TextLength;
            resultBox.SelectionLength = 0;
            resultBox.SelectionColor = color ?? MessageColors.Normal;
            resultBox.SelectionFont = new Font(resultBox.Font, isBold ? FontStyle.Bold : FontStyle.Regular);
            resultBox.AppendText(text);
            resultBox.SelectionColor = resultBox.ForeColor;
            resultBox.SelectionFont = resultBox.Font;
            resultBox.ScrollToCaret();
        }

        private async Task BackupNetworkConfig()
        {
            AppendTextWithStyle("\n开始备份网络配置...\n", MessageColors.Start, true);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupPath = Path.Combine(backupDirectory, $"NetworkConfig_{timestamp}");
            Directory.CreateDirectory(backupPath);

            // 备份网络适配器配置
            AppendTextWithStyle("正在备份网络适配器配置...\n", MessageColors.Progress);
            var adaptersConfig = await GetNetworkAdaptersConfig();
            await File.WriteAllTextAsync(
                Path.Combine(backupPath, "adapters.json"), 
                JsonSerializer.Serialize(adaptersConfig, new JsonSerializerOptions { WriteIndented = true })
            );

            // 备份TCP/IP配置
            AppendTextWithStyle("正在备份TCP/IP配置...\n", MessageColors.Progress);
            await RunNetworkCommand($"netsh -c interface dump > \"{Path.Combine(backupPath, "netsh_interface.txt")}\"");
            await RunNetworkCommand($"netsh -c ip dump > \"{Path.Combine(backupPath, "netsh_ip.txt")}\"");

            // 备份DNS配置
            AppendTextWithStyle("正在备份DNS配置...\n", MessageColors.Progress);
            await RunNetworkCommand($"ipconfig /displaydns > \"{Path.Combine(backupPath, "dns_cache.txt")}\"");

            // 备份hosts文件
            AppendTextWithStyle("正在备份hosts文件...\n", MessageColors.Progress);
            File.Copy(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts"),
                Path.Combine(backupPath, "hosts"),
                true
            );

            AppendTextWithStyle($"\n配置已备份到: {backupPath}\n", MessageColors.Success, true);
        }

        private async Task RestoreNetworkConfig()
        {
            AppendTextWithStyle("\n开始还原网络配置...\n", MessageColors.Start, true);
            using var dialog = new OpenFileDialog
            {
                InitialDirectory = backupDirectory,
                Filter = "网络配置备份|adapters.json",
                Title = "选择要还原的配置文件"
            };

            if (dialog.ShowDialog() != DialogResult.OK)
            {
                AppendTextWithStyle("用户取消了还原操作\n", MessageColors.Warning);
                return;
            }

            var backupPath = Path.GetDirectoryName(dialog.FileName);
            
            // 还原网络适配器配置
            AppendTextWithStyle("正在还原网络适配器配置...\n", MessageColors.Progress);
            var adaptersConfig = JsonSerializer.Deserialize<Dictionary<string, object>>(
                await File.ReadAllTextAsync(dialog.FileName)
            );

            // 还原TCP/IP配置
            AppendTextWithStyle("正在还原TCP/IP配置...\n", MessageColors.Progress);
            var interfaceScript = Path.Combine(backupPath, "netsh_interface.txt");
            if (File.Exists(interfaceScript))
                await RunNetworkCommand($"netsh -f \"{interfaceScript}\"");

            var ipScript = Path.Combine(backupPath, "netsh_ip.txt");
            if (File.Exists(ipScript))
                await RunNetworkCommand($"netsh -f \"{ipScript}\"");

            // 还原hosts文件
            AppendTextWithStyle("正在还原hosts文件...\n", MessageColors.Progress);
            var hostsBackup = Path.Combine(backupPath, "hosts");
            if (File.Exists(hostsBackup))
            {
                File.Copy(
                    hostsBackup,
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts"),
                    true
                );
            }

            AppendTextWithStyle("\n配置还原完成，部分更改可能需要重启计算机才能生效\n", MessageColors.Success, true);
        }

        private async Task ExportRegistryConfig()
        {
            AppendTextWithStyle("\n开始导出注册表配置...\n", MessageColors.Start, true);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var exportPath = Path.Combine(backupDirectory, $"NetworkRegistry_{timestamp}.reg");

            await RunNetworkCommand($"reg export \"HKLM\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\" \"{exportPath}\" /y");
            AppendTextWithStyle($"\n注册表配置已导出到: {exportPath}\n", MessageColors.Success, true);
        }

        private async Task ImportRegistryConfig()
        {
            AppendTextWithStyle("\n开始导入注册表配置...\n", MessageColors.Start, true);
            using var dialog = new OpenFileDialog
            {
                InitialDirectory = backupDirectory,
                Filter = "注册表文件|*.reg",
                Title = "选择要导入的注册表文件"
            };

            if (dialog.ShowDialog() != DialogResult.OK)
            {
                AppendTextWithStyle("用户取消了导入操作\n", MessageColors.Warning);
                return;
            }

            await RunNetworkCommand($"reg import \"{dialog.FileName}\"");
            AppendTextWithStyle("\n注册表配置已导入，部分更改可能需要重启计算机才能生效\n", MessageColors.Success, true);
        }

        private async Task OptimizeNetworkSettings()
        {
            // 检查管理员权限
            if (!IsRunAsAdministrator())
            {
                MessageBox.Show("此操作需要管理员权限，请以管理员身份运行程序", "权限不足", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 显示优化说明和确认对话框
            var optimizationInfo = new StringBuilder();
            optimizationInfo.AppendLine("网络优化说明：");
            optimizationInfo.AppendLine("1. 将设置以下TCP参数：");
            optimizationInfo.AppendLine("   • autotuninglevel=normal - 优化接收窗口大小，提高网络性能");
            optimizationInfo.AppendLine("   • rss=enabled - 启用接收方缩放，提高多核CPU的网络处理能力");
            optimizationInfo.AppendLine("   • ecncapability=enabled - 启用显式拥塞通知，提高网络稳定性");
            optimizationInfo.AppendLine("   • timestamps=enabled - 启用时间戳，提高网络连接质量");
            optimizationInfo.AppendLine("   • initialrto=1000 - 设置初始重传超时时间为1秒，加快连接建立");
            optimizationInfo.AppendLine("   • rsc=enabled - 启用接收段合并，提高网络效率");
            optimizationInfo.AppendLine("   • fastopen=enabled - 启用TCP快速打开，加快连接建立");
            optimizationInfo.AppendLine("\n优化效果：");
            optimizationInfo.AppendLine("• 提高网络连接速度");
            optimizationInfo.AppendLine("• 改善网络传输性能");
            optimizationInfo.AppendLine("• 优化网络带宽使用");
            optimizationInfo.AppendLine("• 增强网络稳定性");
            optimizationInfo.AppendLine("\n注意事项：");
            optimizationInfo.AppendLine("• 此操作需要管理员权限");
            optimizationInfo.AppendLine("• 优化后可能需要重启网络适配器");

            var result = MessageBox.Show(
                optimizationInfo.ToString(),
                "网络优化确认",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information
            );

            if (result != DialogResult.Yes)
            {
                AppendTextWithStyle("用户取消了网络优化操作\n", MessageColors.Warning);
                return;
            }

            // 备份当前设置
            await BackupNetworkConfig();
            AppendTextWithStyle("已备份当前网络配置\n", MessageColors.Success, true);

            try
            {
                AppendTextWithStyle("\n开始优化网络设置...\n", MessageColors.Start, true);

                // 优化TCP/IP参数
                var tcpCommands = new[]
                {
                    "netsh int tcp set global rss=enabled autotuninglevel=normal ecncapability=enabled timestamps=enabled initialrto=1000 rsc=enabled fastopen=enabled"
                };

                bool tcpSuccess = true;
                foreach (var cmd in tcpCommands)
                {
                    if (!await RunNetworkCommand(cmd))
                    {
                        tcpSuccess = false;
                        break;
                    }
                }

                if (!tcpSuccess)
                {
                    throw new Exception("TCP/IP参数优化失败");
                }

                // 设置DNS优化
                if (!await RunNetworkCommand("ipconfig /flushdns") || 
                    !await RunNetworkCommand("ipconfig /registerdns"))
                {
                    throw new Exception("DNS优化失败");
                }

                // 优化网络适配器设置
                var adapters = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.NetworkInterfaceType != NetworkInterfaceType.Loopback 
                        && ni.OperationalStatus == OperationalStatus.Up);

                foreach (var adapter in adapters)
                {
                    AppendTextWithStyle($"正在优化网络适配器: {adapter.Name}\n", MessageColors.Progress);
                    
                    if (!await RunNetworkCommand($"netsh interface ip set interface \"{adapter.Name}\" basereachable=30000") ||
                        !await RunNetworkCommand($"netsh interface ip set interface \"{adapter.Name}\" retransmittime=1000"))
                    {
                        AppendTextWithStyle($"警告: 网络适配器 {adapter.Name} 优化失败\n", MessageColors.Warning);
                        continue;
                    }
                }

                AppendTextWithStyle("\n网络设置优化完成\n", MessageColors.Success, true);
                Logger.Instance.Log("网络优化成功", LogLevel.Info, LogCategory.NetworkRepair);
            }
            catch (Exception ex)
            {
                AppendTextWithStyle($"\n网络优化失败: {ex.Message}\n", MessageColors.Error);
                Logger.Instance.LogError("网络优化失败", ex, LogCategory.NetworkRepair);

                // 提供回滚选项
                result = MessageBox.Show(
                    "网络优化失败，是否还原之前的设置？",
                    "优化失败",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning
                );

                if (result == DialogResult.Yes)
                {
                    await RestoreNetworkConfig();
                    AppendTextWithStyle("已还原之前的网络配置\n", MessageColors.Success, true);
                }
            }
        }

        private bool IsRunAsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private async Task<bool> RunNetworkCommand(string command)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c chcp 65001 >nul && {command}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    Verb = "runas",
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (!string.IsNullOrEmpty(output))
                    AppendTextWithStyle(output + "\n", MessageColors.Command);
                if (!string.IsNullOrEmpty(error))
                    AppendTextWithStyle("错误: " + error + "\n", MessageColors.Error);

                if (process.ExitCode != 0)
                {
                    Logger.Instance.LogError($"命令执行失败: {command}", 
                        new Exception($"退出代码: {process.ExitCode}\n错误信息: {error}"), 
                        LogCategory.NetworkRepair);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"执行命令失败: {command}", ex, LogCategory.NetworkRepair);
                return false;
            }
        }

        private async Task<Dictionary<string, object>> GetNetworkAdaptersConfig()
        {
            var config = new Dictionary<string, object>();
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                var adapterConfig = new Dictionary<string, object>
                {
                    ["Name"] = adapter.Name,
                    ["Description"] = adapter.Description,
                    ["Type"] = adapter.NetworkInterfaceType.ToString(),
                    ["Status"] = adapter.OperationalStatus.ToString(),
                    ["Speed"] = adapter.Speed,
                    ["MAC"] = adapter.GetPhysicalAddress().ToString()
                };

                var ipProps = adapter.GetIPProperties();
                adapterConfig["IPAddresses"] = ipProps.UnicastAddresses
                    .Select(ip => new { Address = ip.Address.ToString(), Mask = ip.IPv4Mask?.ToString() })
                    .ToList();

                adapterConfig["DNSServers"] = ipProps.DnsAddresses
                    .Select(dns => dns.ToString())
                    .ToList();

                adapterConfig["Gateways"] = ipProps.GatewayAddresses
                    .Select(gw => gw.Address.ToString())
                    .ToList();

                config[adapter.Name] = adapterConfig;
            }
            return config;
        }
    }
} 