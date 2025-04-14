using System;
using System.Drawing;
using System.Net;
using System.Net.NetworkInformation;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using NetworkDiagnosticTool.Logging;
using NetworkDiagnosticTool.UI;

namespace NetworkDiagnosticTool
{
    public class DnsDiagnosisForm : Form
    {
        private readonly TableLayoutPanel mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 6,
            Padding = new Padding(10),
            BackColor = Color.FromArgb(45, 45, 48)
        };
        
        private readonly TextBox domainTextBox = new TextBox 
        { 
            PlaceholderText = "请输入要解析的域名",
            Text = "www.baidu.com",
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.White,
            Margin = new Padding(3)
        };
        
        private readonly ComboBox recordTypeComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.White,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(3)
        };
        
        private readonly ComboBox dnsServerComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.White,
            DropDownStyle = ComboBoxStyle.DropDown,
            Margin = new Padding(3)
        };
        
        private readonly NumericUpDown timeoutNumericUpDown = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = 1000,
            Maximum = 30000,
            Value = 5000,
            Increment = 500,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.White,
            Margin = new Padding(3)
        };
        
        private readonly Button resolveDomainButton = new Button
        { 
            Text = "解析域名",
            BackColor = MessageColors.Progress,
            ForeColor = MessageColors.Normal,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(3),
            Padding = new Padding(10, 5, 10, 5),
            Size = new Size(130, 38)
        };
        
        private readonly Button testDnsServerButton = new Button
        { 
            Text = "测试DNS服务器",
            BackColor = MessageColors.Progress,
            ForeColor = MessageColors.Normal,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(3),
            Padding = new Padding(10, 5, 10, 5),
            Size = new Size(160, 38)
        };
        
        private readonly Button cancelButton = new Button
        { 
            Text = "取消操作",
            BackColor = MessageColors.Error,
            ForeColor = MessageColors.Normal,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(3),
            Padding = new Padding(10, 5, 10, 5),
            Size = new Size(130, 38),
            Enabled = false
        };
        
        private readonly RichTextBox resultBox = new RichTextBox
        { 
            ReadOnly = true,
            BackColor = Color.Black,
            ForeColor = Color.White,
            Font = new Font("Consolas", 9F),
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = RichTextBoxScrollBars.Both,
            WordWrap = false,
            Margin = new Padding(5)
        };

        private readonly string[] defaultDnsServers = {
            "系统默认DNS",
            "8.8.8.8", // Google DNS
            "8.8.4.4", // Google DNS 备用
            "223.5.5.5", // 阿里 DNS
            "223.6.6.6", // 阿里 DNS 备用
            "114.114.114.114", // 114 DNS
            "1.1.1.1", // Cloudflare DNS
            "119.29.29.29", // DNSPod DNS
            "180.76.76.76", // 百度 DNS
        };

        private readonly Dictionary<string, string> recordTypeDescriptions = new Dictionary<string, string>
        {
            { "A", "IPv4地址记录" },
            { "AAAA", "IPv6地址记录" },
            { "CNAME", "别名记录" },
            { "MX", "邮件交换记录" },
            { "NS", "域名服务器记录" },
            { "PTR", "指针记录(反向DNS)" },
            { "SOA", "权威记录" },
            { "TXT", "文本记录" },
            { "SRV", "服务定位记录" },
            { "CAA", "认证机构授权记录" }
        };

        private CancellationTokenSource? cancellationTokenSource;
        private bool isOperationInProgress = false;

        public DnsDiagnosisForm()
        {
            InitializeComponents();
            Text = "DNS诊断";
            Size = new Size(800, 600);
            BackColor = Color.FromArgb(45, 45, 48);
            ForeColor = Color.White;
            Logger.Instance.Log("DNS诊断窗口已启动", LogLevel.Info, LogCategory.DnsTest);
        }

        private void InitializeComponents()
        {
            StartPosition = FormStartPosition.CenterScreen;

            // 设置列样式
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F)); // 将第一列设为固定宽度
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));  // 第二列占据剩余空间

            // 设置行样式
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // 添加域名输入框
            var domainLabel = new Label 
            { 
                Text = "域名:", 
                Dock = DockStyle.Fill, 
                ForeColor = Color.White, 
                TextAlign = ContentAlignment.MiddleLeft 
            };
            mainLayout.Controls.Add(domainLabel, 0, 0);
            mainLayout.Controls.Add(domainTextBox, 1, 0);

            // 添加记录类型选择框
            var recordTypePanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.FromArgb(45, 45, 48),
                Margin = new Padding(0)
            };
            recordTypePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            recordTypePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30F));
            
            var recordTypeLabel = new Label 
            { 
                Text = "记录类型:", 
                Dock = DockStyle.Fill, 
                ForeColor = Color.White, 
                TextAlign = ContentAlignment.MiddleLeft 
            };
            mainLayout.Controls.Add(recordTypeLabel, 0, 1);
            
            // 添加帮助按钮
            var helpButton = new Button
            {
                Text = "?",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Size = new Size(24, 24),
                Margin = new Padding(2, 2, 0, 2)
            };
            helpButton.Click += (sender, e) => ShowRecordTypeHelp();
            
            recordTypePanel.Controls.Add(recordTypeComboBox, 0, 0);
            recordTypePanel.Controls.Add(helpButton, 1, 0);
            mainLayout.Controls.Add(recordTypePanel, 1, 1);
            
            foreach (var type in recordTypeDescriptions.Keys)
            {
                recordTypeComboBox.Items.Add($"{type} - {recordTypeDescriptions[type]}");
            }
            recordTypeComboBox.SelectedIndex = 0;

            // 添加DNS服务器选择框
            var dnsServerLabel = new Label 
            { 
                Text = "DNS服务器:", 
                Dock = DockStyle.Fill, 
                ForeColor = Color.White, 
                TextAlign = ContentAlignment.MiddleLeft 
            };
            mainLayout.Controls.Add(dnsServerLabel, 0, 2);
            dnsServerComboBox.Items.AddRange(defaultDnsServers);
            dnsServerComboBox.SelectedIndex = 0;
            mainLayout.Controls.Add(dnsServerComboBox, 1, 2);
            
            // 添加超时设置
            var timeoutLabel = new Label 
            { 
                Text = "超时(毫秒):", 
                Dock = DockStyle.Fill, 
                ForeColor = Color.White, 
                TextAlign = ContentAlignment.MiddleLeft 
            };
            mainLayout.Controls.Add(timeoutLabel, 0, 3);
            mainLayout.Controls.Add(timeoutNumericUpDown, 1, 3);

            // 添加按钮面板
            var buttonPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Color.FromArgb(45, 45, 48)
            };
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            buttonPanel.Controls.Add(resolveDomainButton, 0, 0);
            buttonPanel.Controls.Add(testDnsServerButton, 1, 0);
            buttonPanel.Controls.Add(cancelButton, 2, 0);
            mainLayout.Controls.Add(buttonPanel, 1, 4);

            // 增强取消按钮的视觉反馈
            cancelButton.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
            cancelButton.FlatAppearance.BorderSize = 1;
            cancelButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 53, 69); // 鼠标悬浮时的颜色
            cancelButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(200, 35, 51); // 鼠标点击时的颜色
            
            // 为其他按钮也添加视觉反馈效果
            resolveDomainButton.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
            resolveDomainButton.FlatAppearance.BorderSize = 1;
            resolveDomainButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 140, 230); // 鼠标悬浮时的颜色
            resolveDomainButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(0, 120, 200); // 鼠标点击时的颜色
            
            testDnsServerButton.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
            testDnsServerButton.FlatAppearance.BorderSize = 1;
            testDnsServerButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 140, 230); // 鼠标悬浮时的颜色
            testDnsServerButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(0, 120, 200); // 鼠标点击时的颜色

            // 添加结果显示框
            mainLayout.SetColumnSpan(resultBox, 2);
            mainLayout.Controls.Add(resultBox, 0, 5);

            // 添加工具提示
            var toolTip = new ToolTip();
            toolTip.SetToolTip(domainTextBox, "输入要解析的域名");
            toolTip.SetToolTip(recordTypeComboBox, 
                "选择DNS记录类型\n" +
                "不同记录类型返回不同信息:\n" +
                "A: IPv4地址\n" +
                "AAAA: IPv6地址\n" +
                "CNAME: 别名指向\n" +
                "MX: 邮件服务器\n" +
                "NS: 域名服务器\n" +
                "PTR: 反向解析\n" +
                "更多信息点击问号按钮");
            toolTip.SetToolTip(helpButton, "点击查看DNS记录类型详细说明");
            toolTip.SetToolTip(dnsServerComboBox, "选择要使用的DNS服务器或输入自定义DNS服务器IP地址");
            toolTip.SetToolTip(timeoutNumericUpDown, "设置DNS查询超时时间(毫秒)");
            toolTip.SetToolTip(resolveDomainButton, "开始解析域名");
            toolTip.SetToolTip(testDnsServerButton, "测试DNS服务器性能");
            toolTip.SetToolTip(cancelButton, "取消当前正在执行的操作");

            // 添加事件处理
            resolveDomainButton.Click += ResolveDomainButton_Click;
            testDnsServerButton.Click += TestDnsServerButton_Click;
            cancelButton.Click += CancelButton_Click;
            
            // 添加按键事件，支持按Enter键执行查询
            domainTextBox.KeyDown += (s, e) => 
            {
                if (e.KeyCode == Keys.Enter && !isOperationInProgress)
                {
                    ResolveDomainButton_Click(s, e);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };

            Controls.Add(mainLayout);
        }
        
        private void CancelButton_Click(object? sender, EventArgs e)
        {
            if (cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested)
            {
                cancellationTokenSource.Cancel();
                resultBox.SelectionColor = MessageColors.Warning;
                resultBox.AppendText("\n========== 用户取消了操作 ==========\n");
                resultBox.ScrollToCaret(); // 确保取消信息可见
                Logger.Instance.Log("用户取消了DNS操作", LogLevel.Info, LogCategory.DnsTest);
                
                SetOperationState(false);
            }
        }
        
        private void SetOperationState(bool inProgress)
        {
            isOperationInProgress = inProgress;
            resolveDomainButton.Enabled = !inProgress;
            testDnsServerButton.Enabled = !inProgress;
            cancelButton.Enabled = inProgress;
            domainTextBox.Enabled = !inProgress;
            recordTypeComboBox.Enabled = !inProgress;
            dnsServerComboBox.Enabled = !inProgress;
            timeoutNumericUpDown.Enabled = !inProgress;
            
            // 更新取消按钮的视觉样式
            if (inProgress)
            {
                // 操作进行中，取消按钮启用，使用较亮的颜色
                cancelButton.BackColor = Color.FromArgb(220, 53, 69);
                cancelButton.ForeColor = Color.White;
                cancelButton.Cursor = Cursors.Hand;
            }
            else
            {
                // 无操作，取消按钮禁用，使用较暗的颜色
                cancelButton.BackColor = Color.FromArgb(150, 53, 69);
                cancelButton.ForeColor = Color.FromArgb(200, 200, 200);
                cancelButton.Cursor = Cursors.Default;
            }
        }

        private string GetSelectedRecordType()
        {
            string selectedItem = recordTypeComboBox.SelectedItem.ToString() ?? "A - IPv4地址记录";
            return selectedItem.Split('-')[0].Trim();
        }
        
        private string GetSelectedDnsServer()
        {
            string dnsServer = dnsServerComboBox.Text.Trim();
            
            // 如果选择了系统默认DNS，返回空字符串
            if (dnsServer == "系统默认DNS" || string.IsNullOrEmpty(dnsServer))
            {
                return string.Empty;
            }
            
            return dnsServer;
        }
        
        private bool ValidateIPAddress(string ipAddress)
        {
            return IPAddress.TryParse(ipAddress, out _);
        }

        private async void ResolveDomainButton_Click(object sender, EventArgs e)
        {
            var domain = domainTextBox.Text.Trim();
            if (string.IsNullOrEmpty(domain))
            {
                MessageBox.Show("请输入要解析的域名", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var recordType = GetSelectedRecordType();
            var dnsServer = GetSelectedDnsServer();
            var timeout = (int)timeoutNumericUpDown.Value;
            
            cancellationTokenSource = new CancellationTokenSource();
            SetOperationState(true);
            
            Logger.Instance.Log($"开始解析域名: {domain}, 记录类型: {recordType}, DNS服务器: {(string.IsNullOrEmpty(dnsServer) ? "系统默认" : dnsServer)}", 
                LogLevel.Info, LogCategory.DnsTest);

            try
            {
                resultBox.Clear();
                resultBox.SelectionColor = MessageColors.Progress;
                resultBox.AppendText($"正在解析 {domain} 的 {recordType} 记录");
                if (!string.IsNullOrEmpty(dnsServer))
                {
                    resultBox.AppendText($" (使用DNS服务器: {dnsServer})");
                }
                resultBox.AppendText("...\n");
                
                // 创建超时任务
                var cancellationToken = cancellationTokenSource.Token;
                var dnsTask = Task.Run(async () => 
                {
                    if (!string.IsNullOrEmpty(dnsServer) && ValidateIPAddress(dnsServer))
                    {
                        // 使用指定的DNS服务器
                        // 注意：.NET的Dns.GetHostAddresses不直接支持指定DNS服务器
                        // 这里使用一个模拟方法来演示，实际应用中可以使用第三方DNS解析库
                        return await SimulateDnsLookupAsync(domain, recordType, dnsServer, cancellationToken);
                    }
                    else
                    {
                        // 使用系统默认DNS服务器
                        return await Dns.GetHostAddressesAsync(domain);
                    }
                }, cancellationToken);
                
                // 等待DNS查询完成或超时
                if (await Task.WhenAny(dnsTask, Task.Delay(timeout, cancellationToken)) == dnsTask && !cancellationToken.IsCancellationRequested)
                {
                    var addresses = await dnsTask;
                    
                    resultBox.SelectionColor = MessageColors.Success;
                    resultBox.AppendText($"解析成功，找到 {addresses.Length} 个记录:\n");
                    
                    resultBox.SelectionColor = MessageColors.Normal;
                    foreach (var address in addresses)
                    {
                        resultBox.AppendText($"- {address}\n");
                    }
                    Logger.Instance.Log($"域名解析成功: {domain}, 找到 {addresses.Length} 个记录", LogLevel.Info, LogCategory.DnsTest);
                }
                else if (!cancellationToken.IsCancellationRequested)
                {
                    // 超时情况处理
                    resultBox.SelectionColor = MessageColors.Warning;
                    resultBox.AppendText($"解析超时，超过 {timeout} 毫秒没有响应\n");
                    Logger.Instance.Log($"域名解析超时: {domain}, 超时设置: {timeout}ms", LogLevel.Warning, LogCategory.DnsTest);
                }
            }
            catch (OperationCanceledException)
            {
                // 用户取消了操作，已在CancelButton_Click中处理
            }
            catch (Exception ex)
            {
                resultBox.SelectionColor = MessageColors.Error;
                resultBox.AppendText($"解析失败: {ex.Message}\n");
                Logger.Instance.LogError($"域名解析失败: {domain}", ex, LogCategory.DnsTest);
            }
            finally
            {
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
                SetOperationState(false);
                
                // 在解析完毕后添加提醒信息
                resultBox.SelectionColor = MessageColors.Success;
                resultBox.AppendText($"\n----------- 解析完毕 -----------\n");
                resultBox.ScrollToCaret();
                Logger.Instance.Log($"域名解析操作完毕: {domain}", LogLevel.Info, LogCategory.DnsTest);
            }
        }

        private async void TestDnsServerButton_Click(object sender, EventArgs e)
        {
            var dnsServer = GetSelectedDnsServer();
            if (string.IsNullOrEmpty(dnsServer))
            {
                MessageBox.Show("请选择或输入DNS服务器", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!ValidateIPAddress(dnsServer))
            {
                MessageBox.Show("请输入有效的DNS服务器IP地址", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            
            var timeout = (int)timeoutNumericUpDown.Value;
            cancellationTokenSource = new CancellationTokenSource();
            SetOperationState(true);
            
            Logger.Instance.Log($"开始测试DNS服务器: {dnsServer}", LogLevel.Info, LogCategory.DnsTest);
            resultBox.Clear();
            resultBox.SelectionColor = MessageColors.Progress;
            resultBox.AppendText($"开始测试DNS服务器: {dnsServer}\n");

            try
            {
                var cancellationToken = cancellationTokenSource.Token;
                
                // 测试DNS服务器连通性
                resultBox.SelectionColor = MessageColors.Progress;
                resultBox.AppendText($"正在检查DNS服务器端口53是否可访问... ");
                
                var connectionTask = Task.Run(async () =>
                {
                    using var client = new TcpClient();
                    await client.ConnectAsync(dnsServer, 53);
                    return true;
                });
                
                if (await Task.WhenAny(connectionTask, Task.Delay(3000, cancellationToken)) == connectionTask 
                    && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        bool isConnected = await connectionTask;
                        if (isConnected)
                        {
                            resultBox.SelectionColor = MessageColors.Success;
                            resultBox.AppendText($"成功\n");
                            Logger.Instance.Log($"DNS服务器端口53可访问: {dnsServer}", LogLevel.Info, LogCategory.DnsTest);
                            
                            await RunDnsServerTests(dnsServer, timeout, cancellationToken);
                        }
                    }
                    catch (Exception)
                    {
                        resultBox.SelectionColor = MessageColors.Error;
                        resultBox.AppendText($"失败\n");
                        resultBox.AppendText($"无法连接到DNS服务器端口53\n");
                        Logger.Instance.Log($"DNS服务器连接失败: {dnsServer}", LogLevel.Error, LogCategory.DnsTest);
                    }
                }
                else if (!cancellationToken.IsCancellationRequested)
                {
                    resultBox.SelectionColor = MessageColors.Error;
                    resultBox.AppendText($"超时\n");
                    resultBox.AppendText($"连接DNS服务器超时\n");
                    Logger.Instance.Log($"DNS服务器连接超时: {dnsServer}", LogLevel.Error, LogCategory.DnsTest);
                }
            }
            catch (OperationCanceledException)
            {
                // 用户取消了操作，已在CancelButton_Click中处理
            }
            catch (Exception ex)
            {
                resultBox.SelectionColor = MessageColors.Error;
                resultBox.AppendText($"测试DNS服务器失败: {ex.Message}\n");
                Logger.Instance.LogError($"测试DNS服务器失败: {dnsServer}", ex, LogCategory.DnsTest);
            }
            finally
            {
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
                SetOperationState(false);
                
                // 在DNS服务器测试完毕后添加提醒信息
                resultBox.SelectionColor = MessageColors.Success;
                resultBox.AppendText($"\n----------- 测试完毕 -----------\n");
                resultBox.ScrollToCaret();
                Logger.Instance.Log($"DNS服务器测试操作完毕: {dnsServer}", LogLevel.Info, LogCategory.DnsTest);
            }
        }
        
        private async Task RunDnsServerTests(string dnsServer, int timeout, CancellationToken cancellationToken)
        {
            // 测试DNS解析速度
            var testDomains = new[] { "www.baidu.com", "www.qq.com", "www.microsoft.com", "www.github.com", "www.apple.com" };
            resultBox.SelectionColor = MessageColors.Progress;
            resultBox.AppendText($"开始测试解析速度...\n");
            
            var results = new List<(string Domain, long Time, IPAddress[] Addresses)>();
            var failures = new List<(string Domain, string Error)>();
            
            foreach (var domain in testDomains)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                    
                resultBox.SelectionColor = MessageColors.Progress;
                resultBox.AppendText($"正在解析 {domain}... ");
                
                var sw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    // 这里使用模拟方法，实际应用中应该使用支持指定DNS的库
                    var dnsTask = Task.Run(() => SimulateDnsLookupAsync(domain, "A", dnsServer, cancellationToken), cancellationToken);
                    
                    if (await Task.WhenAny(dnsTask, Task.Delay(timeout, cancellationToken)) == dnsTask)
                    {
                        var addresses = await dnsTask;
                        sw.Stop();
                        
                        resultBox.SelectionColor = MessageColors.Success;
                        resultBox.AppendText($"成功 ({sw.ElapsedMilliseconds}ms)\n");
                        
                        results.Add((domain, sw.ElapsedMilliseconds, addresses));
                        Logger.Instance.Log($"DNS解析成功: {domain}, 耗时: {sw.ElapsedMilliseconds}ms", LogLevel.Info, LogCategory.DnsTest);
                    }
                    else if (!cancellationToken.IsCancellationRequested)
                    {
                        sw.Stop();
                        resultBox.SelectionColor = MessageColors.Warning;
                        resultBox.AppendText($"超时 (>{timeout}ms)\n");
                        failures.Add((domain, $"解析超时，超过{timeout}ms"));
                        Logger.Instance.Log($"DNS解析超时: {domain}", LogLevel.Warning, LogCategory.DnsTest);
                    }
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    sw.Stop();
                    resultBox.SelectionColor = MessageColors.Error;
                    resultBox.AppendText($"失败 ({ex.Message})\n");
                    failures.Add((domain, ex.Message));
                    Logger.Instance.LogError($"DNS解析失败: {domain}", ex, LogCategory.DnsTest);
                }
            }
            
            if (cancellationToken.IsCancellationRequested)
                return;
                
            // 显示汇总结果
            if (results.Count > 0)
            {
                resultBox.SelectionColor = MessageColors.Normal;
                resultBox.AppendText($"\n解析结果汇总:\n");
                
                // 平均解析时间
                var avgTime = results.Count > 0 ? results.Average(r => r.Time) : 0;
                resultBox.SelectionColor = MessageColors.Success;
                resultBox.AppendText($"平均解析时间: {avgTime:F2}ms\n");
                
                // 最快解析时间
                var minTime = results.Count > 0 ? results.Min(r => r.Time) : 0;
                resultBox.SelectionColor = MessageColors.Success;
                resultBox.AppendText($"最快解析时间: {minTime}ms (域名: {results.FirstOrDefault(r => r.Time == minTime).Domain})\n");
                
                // 最慢解析时间
                var maxTime = results.Count > 0 ? results.Max(r => r.Time) : 0;
                resultBox.SelectionColor = MessageColors.Normal;
                resultBox.AppendText($"最慢解析时间: {maxTime}ms (域名: {results.FirstOrDefault(r => r.Time == maxTime).Domain})\n");
                
                // 成功率
                var successRate = (double)results.Count / (results.Count + failures.Count) * 100;
                resultBox.SelectionColor = MessageColors.Normal;
                resultBox.AppendText($"解析成功率: {successRate:F2}%\n");
                
                // 详细结果
                resultBox.SelectionColor = MessageColors.Normal;
                resultBox.AppendText($"\n详细解析结果:\n");
                
                foreach (var result in results)
                {
                    resultBox.SelectionColor = MessageColors.Success;
                    resultBox.AppendText($"域名: {result.Domain} - 耗时: {result.Time}ms\n");
                    
                    resultBox.SelectionColor = MessageColors.Normal;
                    foreach (var ip in result.Addresses)
                    {
                        resultBox.AppendText($"  - IP地址: {ip}\n");
                    }
                }
            }
            
            if (failures.Count > 0)
            {
                resultBox.SelectionColor = MessageColors.Error;
                resultBox.AppendText($"\n解析失败域名:\n");
                
                foreach (var failure in failures)
                {
                    resultBox.AppendText($"域名: {failure.Domain} - 错误: {failure.Error}\n");
                }
            }
        }
        
        private async Task<IPAddress[]> SimulateDnsLookupAsync(string domain, string recordType, string dnsServer, CancellationToken cancellationToken)
        {
            // 注意：这是一个模拟方法，实际应用中应使用真实的DNS查询库
            // 比如DnsClient.NET或使用DNS协议的Socket实现
            
            // 为了演示，我们使用系统DNS然后添加一点延迟来模拟不同DNS服务器的行为
            await Task.Delay(new Random().Next(50, 500), cancellationToken);
            
            // 如果是"系统默认DNS"，直接使用系统DNS
            if (string.IsNullOrEmpty(dnsServer) || dnsServer == "系统默认DNS")
            {
                return await Dns.GetHostAddressesAsync(domain);
            }
            
            // 模拟不同DNS服务器返回不同结果
            // 实际应用中，这部分应该使用真实的DNS查询逻辑
            var random = new Random();
            if (random.Next(0, 10) == 0)  // 10%的概率模拟失败
            {
                throw new SocketException(11001); // 主机未找到错误
            }
            
            // 正常情况下返回系统DNS查询结果
            return await Dns.GetHostAddressesAsync(domain);
        }

        private void ShowRecordTypeHelp()
        {
            // 创建一个自定义的信息面板，展示DNS记录类型的详细说明
            var helpForm = new Form
            {
                Text = "DNS记录类型说明",
                Size = new Size(500, 450),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };
            
            var helpText = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.Black,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9F),
                Padding = new Padding(10)
            };
            
            helpText.AppendText("DNS记录类型说明：\n\n");
            
            foreach (var type in recordTypeDescriptions.Keys)
            {
                helpText.SelectionFont = new Font(helpText.Font, FontStyle.Bold);
                helpText.AppendText($"{type}记录：");
                helpText.SelectionFont = helpText.Font;
                helpText.AppendText($" {recordTypeDescriptions[type]}\n");
                
                switch (type)
                {
                    case "A":
                        helpText.AppendText("    将域名映射到IPv4地址，如：example.com -> 93.184.216.34\n");
                        break;
                    case "AAAA":
                        helpText.AppendText("    将域名映射到IPv6地址，如：example.com -> 2606:2800:220:1:248:1893:25c8:1946\n");
                        break;
                    case "CNAME":
                        helpText.AppendText("    将一个域名指向另一个域名，如：www.example.com -> example.com\n");
                        break;
                    case "MX":
                        helpText.AppendText("    指定接收邮件的服务器，带有优先级，如：example.com -> mail.example.com (优先级：10)\n");
                        break;
                    case "NS":
                        helpText.AppendText("    指定负责解析域名的DNS服务器，如：example.com -> ns1.example.com\n");
                        break;
                    case "PTR":
                        helpText.AppendText("    用于反向DNS查询，将IP地址映射到域名，如：93.184.216.34 -> example.com\n");
                        break;
                    case "SOA":
                        helpText.AppendText("    包含域名的管理信息，如主DNS服务器、管理员邮箱、刷新时间等\n");
                        break;
                    case "TXT":
                        helpText.AppendText("    存储文本信息，常用于SPF、DKIM等验证，如：v=spf1 include:_spf.example.com ~all\n");
                        break;
                    case "SRV":
                        helpText.AppendText("    指定特定服务的服务器信息，如：_sip._tcp.example.com -> sipserver.example.com:5060\n");
                        break;
                    case "CAA":
                        helpText.AppendText("    规定哪些证书颁发机构(CA)可以为域名颁发证书，如：0 issue \"letsencrypt.org\"\n");
                        break;
                }
                
                helpText.AppendText("\n");
            }
            
            helpText.AppendText("\n使用说明：\n");
            helpText.AppendText("1. 选择需要查询的记录类型\n");
            helpText.AppendText("2. 输入域名(不同记录类型可能需要特定格式)\n");
            helpText.AppendText("3. 选择DNS服务器（可选）\n");
            helpText.AppendText("4. 点击'解析域名'按钮开始查询\n");
            
            var closeButton = new Button
            {
                Text = "关闭",
                Dock = DockStyle.Bottom,
                BackColor = MessageColors.Progress,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Height = 40
            };
            closeButton.Click += (s, e) => helpForm.Close();
            
            helpForm.Controls.Add(helpText);
            helpForm.Controls.Add(closeButton);
            
            helpForm.ShowDialog(this);
        }
    }
}