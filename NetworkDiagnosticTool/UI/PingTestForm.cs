using System;
using System.Drawing;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetworkDiagnosticTool.Logging;
using NetworkDiagnosticTool.UI;

namespace NetworkDiagnosticTool
{
    public partial class PingTestForm : Form
    {
        private readonly TableLayoutPanel mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 6,
            Padding = new Padding(10),
            BackColor = Color.FromArgb(45, 45, 48)
        };

        private readonly ComboBox targetAddressBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.White
        };

        private readonly NumericUpDown packetSizeBox = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = 32,
            Maximum = 65500,
            Value = 32,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.White
        };

        private readonly NumericUpDown timeoutBox = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = 100,
            Maximum = 10000,
            Value = 1000,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.White
        };

        private readonly NumericUpDown intervalBox = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = 100,
            Maximum = 10000,
            Value = 1000,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.White
        };

        private readonly Button startButton = new Button
        {
            Text = "开始测试",
            BackColor = MessageColors.Progress,
            ForeColor = MessageColors.Normal,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(100, 30),
            Location = new Point(10, 10)
        };

        private readonly Button stopButton = new Button
        {
            Text = "停止",
            BackColor = MessageColors.Progress,
            ForeColor = MessageColors.Normal,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(100, 30),
            Location = new Point(120, 10),
            Enabled = false
        };

        private readonly RichTextBox resultBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.Black,
            ForeColor = Color.White,
            Font = new Font("Consolas", 9F),
            Margin = new Padding(5)
        };

        private bool isPinging;

        public PingTestForm()
        {
            Text = "Ping测试";
            Size = new Size(600, 400);
            BackColor = Color.FromArgb(45, 45, 48);
            ForeColor = Color.White;

            InitializeComponents();
            Logger.Instance.Log("Ping测试窗口已启动", LogLevel.Info, LogCategory.PingTest);
        }

        private void InitializeComponents()
        {
            // 设置布局
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));

            // 添加目标地址控件
            mainLayout.Controls.Add(new Label { Text = "目标地址:", Dock = DockStyle.Fill, ForeColor = Color.White }, 0, 0);
            mainLayout.Controls.Add(targetAddressBox, 1, 0);
            targetAddressBox.Items.AddRange(new string[] { "www.baidu.com", "www.google.com", "www.microsoft.com" });
            targetAddressBox.SelectedIndex = 0;

            // 添加数据包大小控件
            mainLayout.Controls.Add(new Label { Text = "数据包大小(字节):", Dock = DockStyle.Fill, ForeColor = Color.White }, 0, 1);
            mainLayout.Controls.Add(packetSizeBox, 1, 1);

            // 添加超时时间控件
            mainLayout.Controls.Add(new Label { Text = "超时时间(毫秒):", Dock = DockStyle.Fill, ForeColor = Color.White }, 0, 2);
            mainLayout.Controls.Add(timeoutBox, 1, 2);

            // 添加间隔时间控件
            mainLayout.Controls.Add(new Label { Text = "间隔时间(毫秒):", Dock = DockStyle.Fill, ForeColor = Color.White }, 0, 3);
            mainLayout.Controls.Add(intervalBox, 1, 3);

            // 添加按钮面板
            var buttonPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            buttonPanel.Controls.Add(startButton, 0, 0);
            buttonPanel.Controls.Add(stopButton, 1, 0);
            mainLayout.Controls.Add(buttonPanel, 1, 4);

            // 添加结果显示区域
            mainLayout.SetColumnSpan(resultBox, 2);
            mainLayout.Controls.Add(resultBox, 0, 5);

            // 添加工具提示
            var toolTip = new ToolTip();
            toolTip.SetToolTip(targetAddressBox, "输入要Ping的目标地址");
            toolTip.SetToolTip(packetSizeBox, "设置Ping数据包的大小");
            toolTip.SetToolTip(timeoutBox, "设置等待响应的超时时间");
            toolTip.SetToolTip(intervalBox, "设置两次Ping之间的时间间隔");

            // 绑定事件处理程序
            startButton.Click += StartButton_Click;
            stopButton.Click += StopButton_Click;

            Controls.Add(mainLayout);
        }

        private async void StartButton_Click(object? sender, EventArgs? e)
        {
            if (isPinging) return;

            try
            {
                var target = targetAddressBox.Text.Trim();
                if (string.IsNullOrEmpty(target))
                {
                    MessageBox.Show("请输入目标地址", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                Logger.Instance.Log($"开始Ping测试，目标: {target}", LogLevel.Info, LogCategory.PingTest);
                isPinging = true;
                startButton.Enabled = false;
                stopButton.Enabled = true;

                while (isPinging)
                {
                    try
                    {
                        using var ping = new Ping();
                        var buffer = new byte[(int)packetSizeBox.Value];
                        var timeout = (int)timeoutBox.Value;
                        var reply = await ping.SendPingAsync(target, timeout, buffer);

                        var status = reply.Status == IPStatus.Success
                            ? $"成功 - 时间={reply.RoundtripTime}ms TTL={reply.Options?.Ttl ?? 0}"
                            : $"失败 - {reply.Status}";

                        resultBox.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] {status}\n");
                        Logger.Instance.Log($"Ping {target}: {status}", LogLevel.Info, LogCategory.PingTest);

                        await Task.Delay((int)intervalBox.Value);
                    }
                    catch (PingException ex)
                    {
                        resultBox.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] 错误 - {ex.Message}\n");
                        Logger.Instance.LogError($"Ping {target} 失败", ex, LogCategory.PingTest);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError("Ping测试过程中发生错误", ex, LogCategory.PingTest);
                MessageBox.Show($"发生错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                isPinging = false;
                startButton.Enabled = true;
                stopButton.Enabled = false;
                Logger.Instance.Log("Ping测试已停止", LogLevel.Info, LogCategory.PingTest);
            }
        }

        private void StopButton_Click(object? sender, EventArgs? e)
        {
            isPinging = false;
            Logger.Instance.Log("用户手动停止Ping测试", LogLevel.Info, LogCategory.PingTest);
        }
    }
} 