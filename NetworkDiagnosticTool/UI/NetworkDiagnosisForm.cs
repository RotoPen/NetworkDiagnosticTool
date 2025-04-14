using System;
using System.Drawing;
using System.Windows.Forms;
using System.Net.NetworkInformation;
using System.Net;
using System.Net.Sockets;
using System.Net.Http;
using System.Threading.Tasks;
using NetworkDiagnosticTool.Logging;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Diagnostics;

namespace NetworkDiagnosticTool
{
    public class NetworkDiagnosisForm : Form
    {
        private IContainer? components;
        private readonly TableLayoutPanel mainLayout = new();
        private readonly Button fullCheckButton = new()
        {
            Text = "全面检查",
            BackColor = Color.FromArgb(0, 122, 204),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(120, 30),
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular),
            Cursor = Cursors.Hand
        };
        private readonly Button stopButton = new()
        {
            Text = "停止检查",
            BackColor = Color.FromArgb(108, 117, 125),
            ForeColor = Color.LightGray,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(120, 30),
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular),
            Cursor = Cursors.Default,
            Enabled = false
        };
        private readonly Button clearButton = new()
        {
            Text = "清理日志",
            BackColor = Color.FromArgb(64, 64, 64),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(120, 30)
        };
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
        private readonly DiagnosisItem[] diagnosisItems;
        private readonly HttpClient httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(30)  // 设置更短的超时时间
        };
        private CancellationTokenSource? cancellationTokenSource;
        private bool isDisposed;

        public NetworkDiagnosisForm()
        {
            diagnosisItems = InitializeDiagnosisItems();
            InitializeComponents();
            Logger.Instance.Log("网络诊断窗口已启动", LogLevel.Info, LogCategory.NetworkDiagnosis);
        }

        private void AppendTextSafe(string text, Color? color = null)
        {
            if (isDisposed || resultBox.IsDisposed) return;
            if (InvokeRequired)
            {
                try
                {
                    Invoke(new Action(() => AppendTextSafe(text, color)));
                }
                catch (ObjectDisposedException)
                {
                    // 忽略已释放的对象异常
                }
                return;
            }
            try
            {
                resultBox.SelectionStart = resultBox.TextLength;
                resultBox.SelectionLength = 0;
                resultBox.SelectionColor = color ?? resultBox.ForeColor;
                resultBox.AppendText(text);
                resultBox.SelectionColor = resultBox.ForeColor;
                resultBox.ScrollToCaret();
            }
            catch (ObjectDisposedException)
            {
                // 忽略已释放的对象异常
            }
        }

        private void InitializeComponents()
        {
            Text = "网络诊断";
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(30, 30, 30);
            ForeColor = Color.White;

            mainLayout.RowCount = 2;
            mainLayout.ColumnCount = 1;
            mainLayout.Dock = DockStyle.Fill;
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 70));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
            mainLayout.Padding = new Padding(10);

            var diagnosisPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(35, 35, 35),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = false,
                Padding = new Padding(5),
                Margin = new Padding(0)
            };

            diagnosisPanel.SizeChanged += (s, e) =>
            {
                foreach (Control control in diagnosisPanel.Controls)
                {
                    control.Width = diagnosisPanel.ClientSize.Width - 20;
                }
            };

            foreach (var item in diagnosisItems)
            {
                var itemPanel = CreateDiagnosisItemPanel(item);
                diagnosisPanel.Controls.Add(itemPanel);
            }

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(40, 40, 40),
                FlowDirection = FlowDirection.LeftToRight
            };

            fullCheckButton.Margin = new Padding(0, 0, 10, 0);
            stopButton.Margin = new Padding(0, 0, 10, 0);
            buttonPanel.Controls.AddRange(new Control[] { fullCheckButton, stopButton, clearButton });

            mainLayout.Controls.Add(diagnosisPanel, 0, 0);
            mainLayout.Controls.Add(resultBox, 0, 1);

            Controls.Add(buttonPanel);
            Controls.Add(mainLayout);

            fullCheckButton.Click += async (object? sender, EventArgs? e) =>
            {
                try
                {
                    resultBox.Clear();
                    cancellationTokenSource = new CancellationTokenSource();
                    UpdateFullCheckButtonState(true);
                    UpdateStopButtonState(true);
                    await RunFullCheck(cancellationTokenSource.Token);
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogError("执行完整网络诊断时发生错误", ex, LogCategory.NetworkDiagnosis);
                    resultBox.AppendText($"诊断过程中发生错误: {ex.Message}\n");
                }
                finally
                {
                    UpdateFullCheckButtonState(false);
                    UpdateStopButtonState(false);
                }
            };

            stopButton.Click += (object? sender, EventArgs? e) =>
            {
                try
                {
                    cancellationTokenSource?.Cancel();
                    Logger.Instance.Log("用户手动停止网络诊断", LogLevel.Info, LogCategory.NetworkDiagnosis);
                    fullCheckButton.Enabled = true;
                    UpdateStopButtonState(false);
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogError("停止诊断时发生错误", ex, LogCategory.NetworkDiagnosis);
                }
            };

            clearButton.Click += (object? sender, EventArgs? e) =>
            {
                try
                {
                    if (MessageBox.Show("确定要清空当前显示的诊断结果吗？", "确认", 
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        resultBox.Clear();
                        foreach (Control control in diagnosisPanel.Controls)
                        {
                            if (control is Panel itemPanel)
                            {
                                foreach (Control c in itemPanel.Controls)
                                {
                                    if (c is Label label && label.ForeColor != Color.White && label.ForeColor != Color.Gray)
                                    {
                                        label.Text = "待检查";
                                        label.ForeColor = Color.Gray;
                                    }
                                }
                            }
                        }

                        AppendTextSafe("已清空诊断结果显示。\n");
                        Logger.Instance.Log("用户清空了诊断结果显示", LogLevel.Info, LogCategory.NetworkDiagnosis);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogError("清空诊断结果显示时发生错误", ex, LogCategory.NetworkDiagnosis);
                    MessageBox.Show($"清空显示失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
        }

        private void UpdateStopButtonState(bool isChecking)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateStopButtonState(isChecking)));
                return;
            }

            stopButton.Enabled = isChecking;
            if (isChecking)
            {
                stopButton.BackColor = Color.FromArgb(220, 53, 69);
                stopButton.ForeColor = Color.White;
                stopButton.Text = "⏹ 停止检查";
                stopButton.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
                stopButton.Cursor = Cursors.Hand;
            }
            else
            {
                stopButton.BackColor = Color.FromArgb(108, 117, 125);
                stopButton.ForeColor = Color.LightGray;
                stopButton.Text = "停止检查";
                stopButton.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
                stopButton.Cursor = Cursors.Default;
            }
        }

        private void UpdateFullCheckButtonState(bool isChecking)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateFullCheckButtonState(isChecking)));
                return;
            }

            fullCheckButton.Enabled = !isChecking;
            if (isChecking)
            {
                fullCheckButton.Text = "⌛ 检查中...";
                fullCheckButton.BackColor = Color.FromArgb(108, 117, 125);
                fullCheckButton.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
                fullCheckButton.Cursor = Cursors.WaitCursor;
            }
            else
            {
                fullCheckButton.Text = "全面检查";
                fullCheckButton.BackColor = Color.FromArgb(0, 122, 204);
                fullCheckButton.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
                fullCheckButton.Cursor = Cursors.Hand;
            }
        }

        private async Task<bool> CheckNetworkConnection()
        {
            bool result = false;
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync("8.8.8.8", 3000);
                result = reply.Status == IPStatus.Success;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError("检查网络连接时发生错误", ex, LogCategory.NetworkDiagnosis);
            }
            return result;
        }

        private async Task<bool> OptimizeNetworkSettings()
        {
            try
            {
                Logger.Instance.Log("开始执行网络优化", LogLevel.Info, LogCategory.NetworkDiagnosis);
                
                // 显示网络优化说明
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
                    AppendTextSafe("用户取消了网络优化操作\n", Color.Yellow);
                    return false;
                }

                AppendTextSafe("\n▶ 正在优化网络设置...\n", Color.Yellow);
                
                // 设置TCP参数
                var commands = new[]
                {
                    "int tcp set global autotuninglevel=normal rss=enabled ecncapability=enabled timestamps=enabled initialrto=1000 rsc=enabled fastopen=enabled"
                };

                bool allSuccess = true;
                foreach (var cmd in commands)
                {
                    try
                    {
                        Logger.Instance.Log($"执行命令: netsh {cmd}", LogLevel.Info, LogCategory.NetworkDiagnosis);
                        var psi = new ProcessStartInfo
                        {
                            FileName = "netsh",
                            Arguments = cmd,
                            Verb = "runas",
                            UseShellExecute = true,
                            CreateNoWindow = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        };

                        using var process = Process.Start(psi);
                        if (process != null)
                        {
                            await process.WaitForExitAsync();
                            if (process.ExitCode != 0)
                            {
                                allSuccess = false;
                                Logger.Instance.Log($"命令执行失败，退出代码: {process.ExitCode}", LogLevel.Warning, LogCategory.NetworkDiagnosis);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        allSuccess = false;
                        Logger.Instance.LogError($"执行命令时发生错误: {cmd}", ex, LogCategory.NetworkDiagnosis);
                        AppendTextSafe($"执行命令失败: {ex.Message}\n", Color.Red);
                    }
                }

                if (allSuccess)
                {
                    AppendTextSafe("✓ 网络设置优化成功\n", Color.LightGreen);
                    Logger.Instance.Log("网络优化成功", LogLevel.Info, LogCategory.NetworkDiagnosis);
                }
                else
                {
                    Logger.Instance.Log("网络优化部分失败", LogLevel.Warning, LogCategory.NetworkDiagnosis);
                    AppendTextSafe("⚠ 部分网络设置优化失败，请以管理员身份运行程序\n", Color.Orange);
                    AppendTextSafe("如果问题持续存在，请尝试以下操作：\n", Color.Yellow);
                    AppendTextSafe("1. 确保以管理员身份运行程序\n", Color.Yellow);
                    AppendTextSafe("2. 检查网络适配器状态\n", Color.Yellow);
                    AppendTextSafe("3. 重启网络适配器\n", Color.Yellow);
                }

                return allSuccess;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError("优化网络设置时发生错误", ex, LogCategory.NetworkDiagnosis);
                AppendTextSafe($"优化网络设置失败: {ex.Message}\n", Color.Red);
                return false;
            }
        }

        private async Task<bool> CheckDnsResolution()
        {
            try
            {
                var ipAddresses = await Dns.GetHostAddressesAsync("www.baidu.com");
                var ipList = ipAddresses.Select(ip => ip.ToString()).ToList();
                AppendTextSafe($"DNS解析成功，IP地址: {string.Join(", ", ipList)}\n");
                Logger.Instance.Log($"DNS解析成功，IP地址: {string.Join(", ", ipList)}", LogLevel.Info, LogCategory.NetworkDiagnosis);
                return true;
            }
            catch (Exception ex)
            {
                AppendTextSafe($"DNS解析失败: {ex.Message}\n");
                Logger.Instance.LogError("检查DNS解析时发生错误", ex, LogCategory.NetworkDiagnosis);
                return false;
            }
        }

        private async Task<bool> CheckDefaultGateway()
        {
            bool result = false;
            try
            {
                var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up);

                foreach (var networkInterface in networkInterfaces)
                {
                    var gateway = networkInterface.GetIPProperties().GatewayAddresses
                        .FirstOrDefault()?.Address;

                    if (gateway != null)
                    {
                        using var ping = new Ping();
                        var reply = await ping.SendPingAsync(gateway, 3000);
                        
                        if (reply.Status == IPStatus.Success)
                        {
                            AppendTextSafe($"网关连接正常，延迟: {reply.RoundtripTime}ms\n");
                            Logger.Instance.Log($"默认网关可达，地址: {gateway}", LogLevel.Info, LogCategory.NetworkDiagnosis);
                            result = true;
                            break;
                        }
                        
                        AppendTextSafe($"网关连接异常: {reply.Status}\n");
                        Logger.Instance.Log($"默认网关异常: {reply.Status}", LogLevel.Warning, LogCategory.NetworkDiagnosis);
                    }
                }

                if (!result)
                {
                    AppendTextSafe("未找到可用的默认网关\n");
                    Logger.Instance.Log("未找到可用的默认网关", LogLevel.Warning, LogCategory.NetworkDiagnosis);
                }
            }
            catch (Exception ex)
            {
                AppendTextSafe($"默认网关检查失败: {ex.Message}\n");
                Logger.Instance.LogError("检查默认网关时发生错误", ex, LogCategory.NetworkDiagnosis);
            }
            return result;
        }

        private async Task CheckCommonPorts()
        {
            try
            {
                Logger.Instance.Log("开始检查常用端口...", LogLevel.Info, LogCategory.NetworkDiagnosis);
                AppendTextSafe("\n检查常用端口...\n");
                var ports = new[] { 80, 443, 53 };
                var openPorts = new List<int>();
                foreach (var port in ports)
                {
                    using var client = new TcpClient();
                    var connectTask = client.ConnectAsync("www.baidu.com", port);
                    if (await Task.WhenAny(connectTask, Task.Delay(1000)) == connectTask)
                    {
                        AppendTextSafe($"端口 {port} 开放\n");
                        openPorts.Add(port);
                        Logger.Instance.Log($"端口 {port} 开放", LogLevel.Info, LogCategory.NetworkDiagnosis);
                    }
                    else
                    {
                        AppendTextSafe($"端口 {port} 关闭或超时\n");
                        Logger.Instance.Log($"端口 {port} 关闭或超时", LogLevel.Warning, LogCategory.NetworkDiagnosis);
                    }
                }
                AppendTextSafe($"端口检查完成，可用端口: {string.Join(", ", openPorts)}\n");
                Logger.Instance.Log($"端口检查完成，可用端口: {string.Join(", ", openPorts)}", LogLevel.Info, LogCategory.NetworkDiagnosis);
            }
            catch (Exception ex)
            {
                AppendTextSafe($"端口检查失败: {ex.Message}\n");
                Logger.Instance.LogError("检查常用端口时发生错误", ex, LogCategory.NetworkDiagnosis);
            }
        }

        private async Task CheckNetworkLatency()
        {
            try
            {
                Logger.Instance.Log("开始检查网络延迟...", LogLevel.Info, LogCategory.NetworkDiagnosis);
                AppendTextSafe("\n检查网络延迟...\n");
                var targets = new[] { "8.8.8.8", "114.114.114.114", "223.5.5.5" };
                var latencies = new List<long>();
                foreach (var target in targets)
                {
                    using var ping = new Ping();
                    var reply = await ping.SendPingAsync(target, 1000);
                    if (reply.Status == IPStatus.Success)
                    {
                        AppendTextSafe($"{target} 延迟: {reply.RoundtripTime}ms\n");
                        latencies.Add(reply.RoundtripTime);
                        Logger.Instance.Log($"{target} 延迟: {reply.RoundtripTime}ms", LogLevel.Info, LogCategory.NetworkDiagnosis);
                    }
                    else
                    {
                        AppendTextSafe($"{target} 无法访问: {reply.Status}\n");
                        Logger.Instance.Log($"{target} 无法访问: {reply.Status}", LogLevel.Warning, LogCategory.NetworkDiagnosis);
                    }
                }
                var averageLatency = (long)Math.Round(latencies.Average());
                AppendTextSafe($"网络延迟检查完成，平均延迟: {averageLatency}ms\n");
                Logger.Instance.Log($"网络延迟检查完成，平均延迟: {averageLatency}ms", LogLevel.Info, LogCategory.NetworkDiagnosis);
            }
            catch (Exception ex)
            {
                AppendTextSafe($"网络延迟检查失败: {ex.Message}\n");
                Logger.Instance.LogError("检查网络延迟时发生错误", ex, LogCategory.NetworkDiagnosis);
            }
        }

        private async Task CheckBandwidth()
        {
            try
            {
                Logger.Instance.Log("开始检查带宽...", LogLevel.Info, LogCategory.NetworkDiagnosis);
                AppendTextSafe("\n检查网络带宽...\n");
                
                // 使用多个测速服务器以提高成功率
                var speedTestUrls = new[]
                {
                    "http://speedtest.tele2.net/1MB.zip",
                    "http://speedtest.ftp.otenet.gr/files/test1Mb.db",
                    "http://speedtest.ftp.otenet.gr/files/test10Mb.db"
                };

                double maxSpeed = 0;
                foreach (var url in speedTestUrls)
                {
                    try
                    {
                        var startTime = DateTime.Now;
                        var response = await httpClient.GetByteArrayAsync(url);
                        var duration = (DateTime.Now - startTime).TotalSeconds;
                        var downloadSpeed = (int)(response.Length / duration / 1024 / 1024 * 8); // Convert to Mbps
                        
                        if (downloadSpeed > maxSpeed)
                        {
                            maxSpeed = downloadSpeed;
                        }
                        
                        AppendTextSafe($"下载速度: {downloadSpeed} Mbps (来自 {new Uri(url).Host})\n");
                        Logger.Instance.Log($"带宽检查完成，下载速度: {downloadSpeed} Mbps", LogLevel.Info, LogCategory.NetworkDiagnosis);
                        
                        // 如果成功获取到速度，就退出循环
                        if (downloadSpeed > 0)
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.LogError($"从 {url} 检查带宽时发生错误", ex, LogCategory.NetworkDiagnosis);
                        // 继续尝试下一个URL
                    }
                }

                if (maxSpeed > 0)
                {
                    AppendTextSafe($"最大下载速度: {maxSpeed} Mbps\n");
                }
                else
                {
                    AppendTextSafe("无法获取带宽信息，请检查网络连接\n", Color.Orange);
                }
            }
            catch (Exception ex)
            {
                AppendTextSafe($"带宽检查失败: {ex.Message}\n", Color.Red);
                Logger.Instance.LogError("检查带宽时发生错误", ex, LogCategory.NetworkDiagnosis);
            }
        }

        private Panel CreateDiagnosisItemPanel(DiagnosisItem item)
        {
            var panel = new Panel
            {
                Height = 80,
                Margin = new Padding(0, 0, 0, 5),
                BackColor = Color.FromArgb(45, 45, 45),
                Padding = new Padding(15)
            };

            var titleLabel = new Label
            {
                Text = item.Title,
                Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(15, 15)
            };

            var descLabel = new Label
            {
                Text = item.Description,
                Font = new Font("Microsoft YaHei UI", 9),
                ForeColor = Color.Gray,
                AutoSize = true,
                Location = new Point(15, 40)
            };

            var statusLabel = new Label
            {
                Text = "待检查",
                Font = new Font("Microsoft YaHei UI", 9),
                ForeColor = Color.Gray,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleRight
            };

            var checkButton = new Button
            {
                Text = "检查",
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(80, 30)
            };

            panel.SizeChanged += (s, e) =>
            {
                checkButton.Location = new Point(panel.ClientSize.Width - checkButton.Width - 15, (panel.ClientSize.Height - checkButton.Height) / 2);
                statusLabel.Location = new Point(panel.ClientSize.Width - checkButton.Width - statusLabel.Width - 30, (panel.ClientSize.Height - statusLabel.Height) / 2);
            };

            // 更新状态标签的方法
            void UpdateStatusLabel(string text, Color color)
            {
                if (statusLabel.InvokeRequired)
                {
                    statusLabel.Invoke(new Action(() => UpdateStatusLabel(text, color)));
                    return;
                }
                statusLabel.Text = text;
                statusLabel.ForeColor = color;
            }

            // 更新按钮状态的方法
            void UpdateCheckButton(bool enabled)
            {
                if (checkButton.InvokeRequired)
                {
                    checkButton.Invoke(new Action(() => UpdateCheckButton(enabled)));
                    return;
                }
                checkButton.Enabled = enabled;
            }

            // 添加状态更新事件处理
            item.OnCheckStarted += () =>
            {
                UpdateStatusLabel("检查中...", Color.Yellow);
                UpdateCheckButton(false);
            };

            item.OnCheckCompleted += (result) =>
            {
                UpdateStatusLabel(result ? "正常" : "异常", result ? Color.Green : Color.Red);
                UpdateCheckButton(true);
            };

            checkButton.Click += async (s, e) =>
            {
                try
                {
                    await item.RunCheck();
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogError($"执行检查项 {item.Title} 时发生错误", ex, LogCategory.NetworkDiagnosis);
                }
            };

            panel.Controls.AddRange(new Control[] { titleLabel, descLabel, statusLabel, checkButton });
            return panel;
        }

        private DiagnosisItem[] InitializeDiagnosisItems()
        {
            return new[]
            {
                new DiagnosisItem("网络硬件配置", "检查网络是否连接，网卡是否启用", CheckNetworkHardware),
                new DiagnosisItem("网络连接配置", "检查网卡相关设置是否正确", CheckNetworkConnection),
                new DiagnosisItem("DHCP服务", "检查DHCP服务是否正常", CheckDHCPService),
                new DiagnosisItem("DNS服务", "检查DNS服务是否正常", CheckDNSService),
                new DiagnosisItem("HOSTS文件", "检查HOSTS文件配置是否正常", CheckHostsFile),
                new DiagnosisItem("IE代理", "若设置了IE代理服务器，检查是否能访问网络", CheckIEProxy)
            };
        }

        private async Task<bool> CheckNetworkHardware()
        {
            try
            {
                var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var ni in networkInterfaces)
                {
                    if (ni.OperationalStatus == OperationalStatus.Up)
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError("检查网络硬件时发生错误", ex, LogCategory.NetworkDiagnosis);
                return false;
            }
        }

        private async Task<bool> CheckDHCPService()
        {
            try
            {
                var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var ni in networkInterfaces)
                {
                    var ipProperties = ni.GetIPProperties();
                    if (ipProperties.GetIPv4Properties() != null && 
                        ipProperties.GetIPv4Properties().IsDhcpEnabled)
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError("检查DHCP服务时发生错误", ex, LogCategory.NetworkDiagnosis);
                return false;
            }
        }

        private async Task<bool> CheckDNSService()
        {
            try
            {
                var result = await Dns.GetHostEntryAsync("www.baidu.com");
                return result.AddressList.Length > 0;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError("检查DNS服务时发生错误", ex, LogCategory.NetworkDiagnosis);
                return false;
            }
        }

        private async Task<bool> CheckHostsFile()
        {
            try
            {
                var hostsPath = @"C:\Windows\System32\drivers\etc\hosts";
                return System.IO.File.Exists(hostsPath);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError("检查HOSTS文件时发生错误", ex, LogCategory.NetworkDiagnosis);
                return false;
            }
        }

        private async Task<bool> CheckIEProxy()
        {
            try
            {
                var proxy = WebRequest.GetSystemWebProxy();
                return proxy != null;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError("检查IE代理时发生错误", ex, LogCategory.NetworkDiagnosis);
                return false;
            }
        }

        private async Task RunFullCheck(CancellationToken cancellationToken)
        {
            bool hasError = false;
            var errorItems = new List<string>();

            try
            {
                resultBox.Clear();
                AppendTextSafe("=== 开始全面网络诊断 ===\n", Color.Cyan);
                Logger.Instance.Log("开始执行全面网络诊断", LogLevel.Info, LogCategory.NetworkDiagnosis);

                // 添加网络优化步骤
                AppendTextSafe("\n▶ 正在优化网络设置...\n", Color.Yellow);
                bool optimizationResult = await OptimizeNetworkSettings();
                if (optimizationResult)
                {
                    AppendTextSafe("✓ 网络设置优化成功\n", Color.LightGreen);
                }
                else
                {
                    AppendTextSafe("⚠ 网络设置优化失败，需要管理员权限\n", Color.Orange);
                }

                foreach (var item in diagnosisItems)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        AppendTextSafe("\n⚠ 诊断已取消\n", Color.Orange);
                        Logger.Instance.Log("用户取消了全面网络诊断", LogLevel.Info, LogCategory.NetworkDiagnosis);
                        return;
                    }

                    AppendTextSafe($"\n▶ 正在检查 {item.Title}...\n", Color.Yellow);
                    await item.RunCheck();
                    bool isError = item.Result != true;
                    if (isError)
                    {
                        hasError = true;
                        errorItems.Add(item.Title);
                        AppendTextSafe($"❌ 检查结果: 异常\n", Color.Red);
                    }
                    else
                    {
                        AppendTextSafe($"✓ 检查结果: 正常\n", Color.LightGreen);
                    }
                    await Task.Delay(500);
                }

                // 执行额外的网络测试
                if (!cancellationToken.IsCancellationRequested)
                {
                    AppendTextSafe("\n▶ 执行网络连通性测试...\n", Color.Yellow);
                    bool connectionResult = await CheckNetworkConnection();
                    if (!connectionResult)
                    {
                        hasError = true;
                        errorItems.Add("网络连通性");
                        AppendTextSafe("❌ 网络连通性测试失败\n", Color.Red);
                    }
                    else
                    {
                        AppendTextSafe("✓ 网络连通性测试通过\n", Color.LightGreen);
                    }
                }

                if (!cancellationToken.IsCancellationRequested)
                {
                    AppendTextSafe("\n▶ 执行DNS解析测试...\n", Color.Yellow);
                    bool dnsResult = await CheckDnsResolution();
                    if (!dnsResult)
                    {
                        AppendTextSafe("❌ DNS解析测试失败\n", Color.Red);
                    }
                    else
                    {
                        AppendTextSafe("✓ DNS解析测试通过\n", Color.LightGreen);
                    }
                }

                if (!cancellationToken.IsCancellationRequested)
                {
                    AppendTextSafe("\n▶ 检查默认网关...\n", Color.Yellow);
                    bool gatewayResult = await CheckDefaultGateway();
                    if (!gatewayResult)
                    {
                        AppendTextSafe("❌ 默认网关检查失败\n", Color.Red);
                    }
                    else
                    {
                        AppendTextSafe("✓ 默认网关检查通过\n", Color.LightGreen);
                    }
                }

                AppendTextSafe("\n=== 全面网络诊断完成 ===\n", Color.Cyan);
                Logger.Instance.Log("全面网络诊断完成", LogLevel.Info, LogCategory.NetworkDiagnosis);

                // 如果发现故障，显示修复建议
                if (hasError)
                {
                    AppendTextSafe("\n⚠ 发现以下问题：\n", Color.Orange);
                    foreach (var item in errorItems)
                    {
                        AppendTextSafe($"  • {item}\n", Color.Red);
                    }

                    // 生成修复建议说明
                    var repairSuggestions = new StringBuilder();
                    repairSuggestions.AppendLine("修复建议：");
                    foreach (var item in errorItems)
                    {
                        switch (item)
                        {
                            case "网络硬件配置":
                                repairSuggestions.AppendLine("• 将检查网卡驱动状态并尝试重置网卡");
                                repairSuggestions.AppendLine("• 如果网卡被禁用，将尝试启用网卡");
                                break;
                            case "网络连接配置":
                                repairSuggestions.AppendLine("• 将检查并重置TCP/IP协议栈");
                                repairSuggestions.AppendLine("• 尝试重新获取IP地址");
                                break;
                            case "DHCP服务":
                                repairSuggestions.AppendLine("• 将重启DHCP客户端服务");
                                repairSuggestions.AppendLine("• 尝试重新获取IP地址配置");
                                break;
                            case "DNS服务":
                                repairSuggestions.AppendLine("• 将检查DNS服务器设置");
                                repairSuggestions.AppendLine("• 尝试设置备用DNS服务器");
                                break;
                            case "HOSTS文件":
                                repairSuggestions.AppendLine("• 将检查HOSTS文件是否被修改");
                                repairSuggestions.AppendLine("• 如有异常将尝试恢复默认设置");
                                break;
                            case "IE代理":
                                repairSuggestions.AppendLine("• 将检查系统代理设置");
                                repairSuggestions.AppendLine("• 如有异常将重置代理设置");
                                break;
                            case "网络连通性":
                                repairSuggestions.AppendLine("• 将进行网络连接修复");
                                repairSuggestions.AppendLine("• 尝试重置网络适配器");
                                break;
                        }
                    }
                    repairSuggestions.AppendLine("\n注意：修复过程可能会临时断开网络连接，请确保保存了重要的工作。");

                    // 显示确认对话框
                    var confirmMessage = $"发现{errorItems.Count}个问题需要修复。\n\n{repairSuggestions}是否要跳转到网络修复页面进行修复？";
                    var confirmResult = MessageBox.Show(
                        confirmMessage,
                        "网络修复确认",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2  // 默认选择"否"按钮
                    );

                    if (confirmResult == DialogResult.Yes)
                    {
                        try
                        {
                            // 获取父TabControl
                            var mainForm = FindForm() as MainForm;
                            if (mainForm != null)
                            {
                                // 切换到网络修复标签页
                                var tabControl = mainForm.Controls.OfType<TabControl>().FirstOrDefault();
                                if (tabControl != null)
                                {
                                    var repairTab = tabControl.TabPages.Cast<TabPage>()
                                        .FirstOrDefault(tp => tp.Text == "网络修复");
                                    if (repairTab != null)
                                    {
                                        tabControl.SelectedTab = repairTab;
                                        Logger.Instance.Log("用户确认进行网络修复", LogLevel.Info, LogCategory.NetworkDiagnosis);
                                    }
                                    else
                                    {
                                        MessageBox.Show(
                                            "无法找到网络修复页面，请手动切换到\"网络修复\"标签。",
                                            "跳转失败",
                                            MessageBoxButtons.OK,
                                            MessageBoxIcon.Warning
                                        );
                                        Logger.Instance.Log("未找到网络修复标签页", LogLevel.Warning, LogCategory.NetworkDiagnosis);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(
                                "跳转到修复页面时发生错误，请手动切换到\"网络修复\"标签。",
                                "跳转失败",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error
                            );
                            Logger.Instance.LogError("跳转到网络修复页面时发生错误", ex, LogCategory.NetworkDiagnosis);
                        }
                    }
                    else
                    {
                        AppendTextSafe("\n用户选择暂不修复网络问题。\n", Color.Gray);
                        Logger.Instance.Log("用户取消了网络修复", LogLevel.Info, LogCategory.NetworkDiagnosis);
                    }
                }
                else
                {
                    AppendTextSafe("\n✓ 所有检查项目均正常\n", Color.LightGreen);
                }
            }
            catch (Exception ex)
            {
                AppendTextSafe($"\n❌ 诊断过程中发生错误: {ex.Message}\n", Color.Red);
                Logger.Instance.LogError("执行全面网络诊断时发生错误", ex, LogCategory.NetworkDiagnosis);
            }
            finally
            {
                UpdateFullCheckButtonState(false);
                UpdateStopButtonState(false);
            }
        }

        private void StopCheck()
        {
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource = null;
                fullCheckButton.Enabled = true;
                UpdateStopButtonState(false);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !isDisposed)
            {
                isDisposed = true;
                cancellationTokenSource?.Cancel();
                cancellationTokenSource?.Dispose();
                httpClient.Dispose();
                components?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public class DiagnosisItem
    {
        public string Title { get; }
        public string Description { get; }
        private readonly Func<Task<bool>> checkFunction;
        public bool IsChecking { get; private set; }
        public bool? Result { get; private set; }

        // 添加事件
        public event Action? OnCheckStarted;
        public event Action<bool>? OnCheckCompleted;

        public DiagnosisItem(string title, string description, Func<Task<bool>> checkFunction)
        {
            Title = title;
            Description = description;
            this.checkFunction = checkFunction;
        }

        public async Task RunCheck()
        {
            IsChecking = true;
            Result = null;
            OnCheckStarted?.Invoke();

            try
            {
                Result = await checkFunction();
                OnCheckCompleted?.Invoke(Result.Value);
                Logger.Instance.Log($"检查项 {Title} 完成，结果: {(Result.Value ? "正常" : "异常")}", 
                    Result.Value ? LogLevel.Info : LogLevel.Warning, LogCategory.NetworkDiagnosis);
            }
            catch (Exception ex)
            {
                Result = false;
                OnCheckCompleted?.Invoke(false);
                Logger.Instance.LogError($"检查项 {Title} 执行失败", ex, LogCategory.NetworkDiagnosis);
            }
            finally
            {
                IsChecking = false;
            }
        }
    }
} 