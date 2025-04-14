using System;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Collections.Generic;
using NetworkDiagnosticTool.Logging;
using System.Threading;
using System.Collections.Concurrent;
using System.Linq;
using NetworkDiagnosticTool.UI;

namespace NetworkDiagnosticTool
{
    public partial class PortScanForm : Form
    {
        private readonly TableLayoutPanel mainLayout;
        private readonly TextBox targetAddressBox;
        private readonly NumericUpDown startPortBox;
        private readonly NumericUpDown endPortBox;
        private readonly NumericUpDown timeoutBox;
        private readonly Button scanButton;
        private readonly Button stopButton;
        private readonly RichTextBox resultBox;
        private readonly RichTextBox progressTextBox;
        private readonly ProgressBar progressBar = new()
        {
            Dock = DockStyle.Fill,
            Style = ProgressBarStyle.Continuous,
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            BackColor = Color.Black
        };

        private bool isScanning = false;

        public PortScanForm()
        {
            // 基本窗体设置
            Text = "端口扫描";
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Font;
            AutoScaleDimensions = new SizeF(96F, 96F);
            FormBorderStyle = FormBorderStyle.Sizable;
            Logger.Instance.Log("端口扫描窗口已启动", LogLevel.Info, LogCategory.PortScan);

            mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 7,
                BackColor = Color.FromArgb(32, 32, 32),
                AutoSize = false,
                Padding = new Padding(10)
            };

            // 设置列宽比例
            mainLayout.ColumnStyles.Clear();
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F)); // 将第一列设为固定宽度
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));  // 第二列占据剩余空间

            // 设置行高
            mainLayout.RowStyles.Clear();
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // 创建控件
            targetAddressBox = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White
            };

            startPortBox = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = 1,
                Maximum = 65535,
                Value = 1,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White
            };

            endPortBox = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = 1,
                Maximum = 65535,
                Value = 1024,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White
            };

            timeoutBox = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = 100,
                Maximum = 10000,
                Value = 1000,
                Increment = 100,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White
            };

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.FromArgb(32, 32, 32),
                Padding = new Padding(10)
            };

            scanButton = new Button
            {
                Text = "开始扫描",
                BackColor = MessageColors.Progress,
                ForeColor = MessageColors.Normal,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(100, 30),
                Location = new Point(10, 10)
            };

            stopButton = new Button
            {
                Text = "停止",
                BackColor = MessageColors.Progress,
                ForeColor = MessageColors.Normal,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(100, 30),
                Location = new Point(120, 10),
                Enabled = false
            };

            buttonPanel.Controls.AddRange(new Control[] { scanButton, stopButton });

            resultBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                ForeColor = Color.White,
                ReadOnly = true,
                WordWrap = false
            };

            // 添加控件到布局
            var targetAddressLabel = new Label 
            { 
                Text = "目标地址:", 
                Dock = DockStyle.Fill, 
                ForeColor = Color.White, 
                TextAlign = ContentAlignment.MiddleLeft 
            };
            mainLayout.Controls.Add(targetAddressLabel, 0, 0);
            mainLayout.Controls.Add(targetAddressBox, 1, 0);

            var startPortLabel = new Label 
            { 
                Text = "起始端口:", 
                Dock = DockStyle.Fill, 
                ForeColor = Color.White, 
                TextAlign = ContentAlignment.MiddleLeft 
            };
            mainLayout.Controls.Add(startPortLabel, 0, 1);
            mainLayout.Controls.Add(startPortBox, 1, 1);

            var endPortLabel = new Label 
            { 
                Text = "结束端口:", 
                Dock = DockStyle.Fill, 
                ForeColor = Color.White, 
                TextAlign = ContentAlignment.MiddleLeft 
            };
            mainLayout.Controls.Add(endPortLabel, 0, 2);
            mainLayout.Controls.Add(endPortBox, 1, 2);

            var timeoutLabel = new Label 
            { 
                Text = "超时(毫秒):", 
                Dock = DockStyle.Fill, 
                ForeColor = Color.White, 
                TextAlign = ContentAlignment.MiddleLeft 
            };
            mainLayout.Controls.Add(timeoutLabel, 0, 3);
            mainLayout.Controls.Add(timeoutBox, 1, 3);

            mainLayout.Controls.Add(buttonPanel, 0, 4);
            mainLayout.SetColumnSpan(buttonPanel, 2);

            // 添加进度条
            var progressLabel = new Label 
            { 
                Text = "扫描进度:", 
                Dock = DockStyle.Fill, 
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft
            };
            
            // 创建一个黑色背景的文本框来显示进度
            progressTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                ForeColor = Color.White,
                ReadOnly = true,
                Text = "准备就绪",
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9F)
            };
            
            mainLayout.Controls.Add(progressLabel, 0, 5);
            mainLayout.Controls.Add(progressTextBox, 1, 5);

            // 创建包含结果框的面板
            var resultPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                Margin = new Padding(3, 10, 3, 3)
            };
            resultPanel.Controls.Add(resultBox);
            
            // 添加新的行用于显示结果
            mainLayout.RowCount = 7;
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.Controls.Add(new Label { Text = "扫描结果:", Dock = DockStyle.Fill, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft }, 0, 6);
            mainLayout.Controls.Add(resultPanel, 1, 6);

            Controls.Add(mainLayout);

            // 绑定事件
            scanButton.Click += ScanButton_Click;
            stopButton.Click += StopButton_Click;
        }

        private async void ScanButton_Click(object sender, EventArgs e)
        {
            if (isScanning) return;

            var target = targetAddressBox.Text.Trim();
            if (string.IsNullOrEmpty(target))
            {
                MessageBox.Show("请输入目标IP地址或域名", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var startPort = (int)startPortBox.Value;
            var endPort = (int)endPortBox.Value;
            var timeout = (int)timeoutBox.Value;

            if (startPort > endPort)
            {
                MessageBox.Show("起始端口不能大于结束端口", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Logger.Instance.Log($"开始端口扫描，目标: {target}，端口范围: {startPort}-{endPort}", LogLevel.Info, LogCategory.PortScan);
            isScanning = true;
            scanButton.Enabled = false;
            stopButton.Enabled = true;
            resultBox.Clear();
            progressTextBox.Clear();
            
            var startTime = DateTime.Now;
            UpdateProgressInfo($"[{startTime:yyyy-MM-dd HH:mm:ss}] 开始扫描目标: {target}    扫描端口范围: {startPort}-{endPort}    超时设置: {timeout}ms", Color.Cyan);
            UpdateProgressInfo("正在初始化扫描任务...", Color.Yellow);

            try
            {
                var tasks = new List<Task>();
                var openPorts = new ConcurrentBag<int>();
                var progress = 0;
                var total = endPort - startPort + 1;
                var lastProgressUpdate = DateTime.Now;
                var lastProgressValue = 0;

                for (int port = startPort; port <= endPort && isScanning; port++)
                {
                    if (tasks.Count >= 100)
                    {
                        await Task.WhenAny(tasks);
                        tasks.RemoveAll(t => t.IsCompleted);
                    }

                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            using var client = new TcpClient();
                            var connectTask = client.ConnectAsync(target, port);
                            if (await Task.WhenAny(connectTask, Task.Delay(timeout)) == connectTask)
                            {
                                openPorts.Add(port);
                                resultBox.Invoke(() => resultBox.AppendText($"端口 {port} 开放\n"));
                                Logger.Instance.Log($"发现开放端口: {port}", LogLevel.Info, LogCategory.PortScan);
                            }
                        }
                        catch
                        {
                            // 端口关闭或无法连接
                        }
                        finally
                        {
                            int currentProgress = Interlocked.Increment(ref progress);
                            var percentage = (int)((float)currentProgress / total * 100);
                            
                            // 每1%或每2秒更新一次界面，避免频繁更新
                            if (percentage > lastProgressValue || (DateTime.Now - lastProgressUpdate).TotalSeconds >= 2)
                            {
                                lock (this)
                                {
                                    if (percentage > lastProgressValue || (DateTime.Now - lastProgressUpdate).TotalSeconds >= 2)
                                    {
                                        UpdateProgress(percentage, currentProgress, total, startTime);
                                        lastProgressValue = percentage;
                                        lastProgressUpdate = DateTime.Now;
                                    }
                                }
                            }
                        }
                    }));
                }

                await Task.WhenAll(tasks);

                if (isScanning)
                {
                    var openPortsList = openPorts.OrderBy(p => p).ToList();
                    var endTime = DateTime.Now;
                    var duration = (endTime - startTime).TotalSeconds;
                    
                    UpdateProgressInfo($"[{endTime:yyyy-MM-dd HH:mm:ss}] 扫描完成！耗时: {duration:F2}秒    扫描速度: {total / duration:F2} 端口/秒    发现开放端口: {openPortsList.Count}个", Color.LightGreen);
                    
                    resultBox.AppendText($"\n扫描完成，共发现 {openPortsList.Count} 个开放端口\n");
                    Logger.Instance.Log($"端口扫描完成，目标: {target}，发现 {openPortsList.Count} 个开放端口", LogLevel.Info, LogCategory.PortScan);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError("端口扫描过程中发生错误", ex, LogCategory.PortScan);
                UpdateProgressInfo($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 错误: {ex.Message}", Color.Red);
                MessageBox.Show($"扫描过程中发生错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                isScanning = false;
                scanButton.Enabled = true;
                stopButton.Enabled = false;
                if (!isScanning)
                {
                    UpdateProgressInfo($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 扫描已停止", Color.Orange);
                }
            }
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            if (!isScanning) return;
            
            isScanning = false;
            UpdateProgressInfo($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 用户手动停止扫描", Color.Orange);
            Logger.Instance.Log("用户手动停止端口扫描", LogLevel.Info, LogCategory.PortScan);
        }

        private void UpdateProgress(int percentage, int scannedPorts, int totalPorts, DateTime startTime)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateProgress(percentage, scannedPorts, totalPorts, startTime)));
                return;
            }

            Text = percentage > 0 ? $"端口扫描 - {percentage}%" : "端口扫描";
            
            if (percentage > 0 && percentage < 100)
            {
                // 计算估计剩余时间
                var elapsedTime = (DateTime.Now - startTime).TotalSeconds;
                var estimatedTotalTime = (elapsedTime / percentage) * 100;
                var remainingTime = estimatedTotalTime - elapsedTime;
                
                // 计算扫描速度
                var portsPerSecond = scannedPorts / (elapsedTime > 0 ? elapsedTime : 1);
                
                // 将所有信息整合到一行中显示
                UpdateProgressInfo(
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 进度: {percentage}% ({scannedPorts}/{totalPorts})    " +
                    $"扫描速度: {portsPerSecond:F2} 端口/秒    预计剩余时间: {TimeSpan.FromSeconds(remainingTime):hh\\:mm\\:ss}",
                    Color.Yellow);
            }
        }
        
        private void UpdateProgressInfo(string text, Color color)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateProgressInfo(text, color)));
                return;
            }
            
            progressTextBox.SelectionStart = progressTextBox.TextLength;
            progressTextBox.SelectionLength = 0;
            progressTextBox.SelectionColor = color;
            progressTextBox.AppendText(text + Environment.NewLine);
            progressTextBox.ScrollToCaret();
        }
    }
} 