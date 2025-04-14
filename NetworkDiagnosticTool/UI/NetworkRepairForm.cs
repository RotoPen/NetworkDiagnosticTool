using System;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading.Tasks;
using NetworkDiagnosticTool.Logging;
using System.Net.NetworkInformation;
using System.Linq;

namespace NetworkDiagnosticTool
{
    public class NetworkRepairForm : Form
    {
        private readonly TableLayoutPanel mainLayout = new();
        private readonly FlowLayoutPanel repairPanel = new();
        private readonly RichTextBox resultBox = new()
        {
            ReadOnly = true,
            BackColor = Color.Black,
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            Margin = new Padding(10),
            ScrollBars = RichTextBoxScrollBars.Both,
            Font = new Font("Consolas", 9)
        };

        private bool isRepairing = false;

        public NetworkRepairForm()
        {
            InitializeComponents();
            Logger.Instance.Log("网络修复窗口已启动", LogLevel.Info, LogCategory.NetworkRepair);
        }

        private void InitializeComponents()
        {
            Text = "网络修复";
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

            repairPanel.Dock = DockStyle.Fill;
            repairPanel.AutoScroll = true;
            repairPanel.BackColor = Color.FromArgb(35, 35, 35);
            repairPanel.FlowDirection = FlowDirection.TopDown;
            repairPanel.WrapContents = false;
            repairPanel.AutoSize = false;
            repairPanel.Padding = new Padding(5);
            repairPanel.Margin = new Padding(0);

            repairPanel.SizeChanged += (s, e) =>
            {
                foreach (Control control in repairPanel.Controls)
                {
                    control.Width = repairPanel.ClientSize.Width - 20;
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

            // 添加修复选项
            AddRepairOption("重置网络适配器", "重置所有网络适配器的状态", ResetNetworkAdapters);
            AddRepairOption("刷新DNS缓存", "清除DNS解析缓存", FlushDnsCache);
            AddRepairOption("重置TCP/IP协议栈", "重置TCP/IP协议相关配置", ResetTcpIp);
            AddRepairOption("修复Winsock", "修复Windows Socket配置", RepairWinsock);
            AddRepairOption("自动修复网络", "尝试自动检测并修复网络问题", AutoRepairNetwork);

            mainLayout.Controls.Add(repairPanel, 0, 0);
            mainLayout.Controls.Add(resultBox, 0, 1);

            Controls.Add(mainLayout);
        }

        private void AddRepairOption(string title, string description, Func<Task> repairAction)
        {
            var panel = new Panel
            {
                Width = repairPanel.ClientSize.Width - 20,
                Height = 80,
                Margin = new Padding(0, 0, 0, 5),
                BackColor = Color.FromArgb(45, 45, 45),
                Padding = new Padding(15)
            };

            var titleLabel = new Label
            {
                Text = title,
                Font = new Font("Microsoft YaHei", 10, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(15, 15)
            };

            var descLabel = new Label
            {
                Text = description,
                Font = new Font("Microsoft YaHei", 9),
                ForeColor = Color.Gray,
                AutoSize = true,
                Location = new Point(15, 40)
            };

            var repairButton = new Button
            {
                Text = "执行修复",
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(100, 30),
                Location = new Point(panel.Width - 130, 25),
                Margin = new Padding(0)
            };

            repairButton.Click += async (s, e) =>
            {
                if (isRepairing)
                {
                    MessageBox.Show("有修复操作正在进行中，请等待完成", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                try
                {
                    isRepairing = true;
                    repairButton.Enabled = false;
                    resultBox.AppendText($"\n开始{title}...\n");
                    Logger.Instance.Log($"开始{title}", LogLevel.Info, LogCategory.NetworkRepair);

                    await repairAction();

                    resultBox.AppendText($"{title}完成\n");
                    Logger.Instance.Log($"{title}完成", LogLevel.Info, LogCategory.NetworkRepair);
                }
                catch (Exception ex)
                {
                    resultBox.AppendText($"{title}失败: {ex.Message}\n");
                    Logger.Instance.LogError($"{title}失败", ex, LogCategory.NetworkRepair);
                    MessageBox.Show($"修复失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    repairButton.Enabled = true;
                    isRepairing = false;
                }
            };

            panel.Controls.AddRange(new Control[] { titleLabel, descLabel, repairButton });
            repairPanel.Controls.Add(panel);
        }

        private async Task ResetNetworkAdapters()
        {
            var adapters = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            foreach (var adapter in adapters)
            {
                resultBox.AppendText($"正在重置网络适配器: {adapter.Name}\n");
                
                // 禁用网络适配器
                await RunNetworkCommand($"netsh interface set interface \"{adapter.Name}\" disable");
                await Task.Delay(2000); // 等待2秒

                // 启用网络适配器
                await RunNetworkCommand($"netsh interface set interface \"{adapter.Name}\" enable");
                await Task.Delay(1000); // 等待1秒
            }
        }

        private async Task FlushDnsCache()
        {
            await RunNetworkCommand("ipconfig /flushdns");
            await RunNetworkCommand("ipconfig /registerdns");
        }

        private async Task ResetTcpIp()
        {
            await RunNetworkCommand("netsh int ip reset");
            await RunNetworkCommand("netsh int ipv4 reset");
            await RunNetworkCommand("netsh int ipv6 reset");
            resultBox.AppendText("需要重启计算机才能完成TCP/IP重置\n");
        }

        private async Task RepairWinsock()
        {
            await RunNetworkCommand("netsh winsock reset");
            resultBox.AppendText("需要重启计算机才能完成Winsock修复\n");
        }

        private async Task AutoRepairNetwork()
        {
            resultBox.AppendText("开始自动修复网络...\n");

            // 检查并修复网络连接
            await RunNetworkCommand("ipconfig /release");
            await Task.Delay(2000);
            await RunNetworkCommand("ipconfig /renew");

            // 重置网络配置
            await FlushDnsCache();
            await ResetTcpIp();
            await RepairWinsock();

            resultBox.AppendText("自动修复完成，建议重启计算机使所有修复生效\n");
        }

        private async Task RunNetworkCommand(string command)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (!string.IsNullOrEmpty(output))
                    resultBox.AppendText(output + "\n");
                if (!string.IsNullOrEmpty(error))
                    resultBox.AppendText("错误: " + error + "\n");

                if (process.ExitCode != 0)
                    throw new Exception($"命令执行失败，退出代码: {process.ExitCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"执行命令 '{command}' 失败: {ex.Message}");
            }
        }
    }
} 