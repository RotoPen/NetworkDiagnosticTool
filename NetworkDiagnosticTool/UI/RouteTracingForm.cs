using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Threading.Tasks;
using NetworkDiagnosticTool.Network;
using NetworkDiagnosticTool.Models;
using System.Text;
using System.IO;
using System.Linq;
using System.Diagnostics;
using NetworkDiagnosticTool.Logging;
using System.Threading;

namespace NetworkDiagnosticTool.UI
{
    public partial class RouteTracingForm : Form
    {
        private bool isTracing = false;
        private const int MaxHistoryCount = 10;
        private readonly List<string> historyHosts = new();
        private RouteTracer? currentTracer = null;
        private CancellationTokenSource? _cancellationTokenSource;
        public RichTextBox? statusTextBox;
        
        // 添加简单的保护锁，防止重入
        private bool _isProcessingAction = false;

        public RouteTracingForm()
        {
            try
        {
            InitializeComponent();
                SetupCustomizations();
            LoadHistory();
                UpdateButtonStates();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化窗体时出错: {ex.Message}\n{ex.StackTrace}", "严重错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetupCustomizations()
        {
            try
            {
                // 调整现有控件样式而不是创建新控件
                this.BackColor = Color.FromArgb(30, 30, 30);
                this.ForeColor = Color.LightGray;
                this.MinimumSize = new Size(800, 600);

                // 使状态文本框占据表单的底部四分之一空间，而不是三分之一
                int statusHeight = this.ClientSize.Height / 4;
                
                // 确保resultListView已正确设置，并让它占据更多空间
                if (resultListView != null)
                {
                    resultListView.BackColor = Color.FromArgb(35, 35, 35);
                    resultListView.ForeColor = Color.LightGray;
                    resultListView.GridLines = true;
                    resultListView.FullRowSelect = true;
                    resultListView.View = View.Details;
                    resultListView.Height = this.ClientSize.Height - statusHeight - 40; // 减去状态框高度和工具栏高度
                    resultListView.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom; // 添加底部锚点，让它可以随窗口调整
                    resultListView.BorderStyle = BorderStyle.FixedSingle; // 设置固定单线边框
                    resultListView.GridLines = false; // 关闭网格线
                    
                    // 清除所有列然后添加需要的列
                    resultListView.Columns.Clear();
                    resultListView.Columns.Add("跳数", 50);
                    resultListView.Columns.Add("IP地址", 150);
                    resultListView.Columns.Add("延迟1", 80);
                    resultListView.Columns.Add("延迟2", 80);
                    resultListView.Columns.Add("延迟3", 80);
                    resultListView.Columns.Add("平均延迟", 80);
                    resultListView.Columns.Add("主机名", 200);
                    resultListView.Columns.Add("位置", 150);
                }

                // 设置按钮样式
                if (startButton != null)
                {
                    startButton.BackColor = Color.FromArgb(40, 40, 40);
                    startButton.ForeColor = Color.LightGray;
                    startButton.FlatStyle = FlatStyle.Flat;
                    startButton.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 60);
                }

                if (exportButton != null)
                {
                    exportButton.BackColor = Color.FromArgb(40, 40, 40);
                    exportButton.ForeColor = Color.LightGray;
                    exportButton.FlatStyle = FlatStyle.Flat;
                    exportButton.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 60);
                    exportButton.Enabled = false;
                }

                if (visualizeButton != null)
                {
                    visualizeButton.BackColor = Color.FromArgb(40, 40, 40);
                    visualizeButton.ForeColor = Color.LightGray;
                    visualizeButton.FlatStyle = FlatStyle.Flat;
                    visualizeButton.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 60);
                    visualizeButton.Enabled = false;
                }

                if (hostComboBox != null)
                {
                    hostComboBox.BackColor = Color.FromArgb(40, 40, 40);
                    hostComboBox.ForeColor = Color.LightGray;
                    hostComboBox.FlatStyle = FlatStyle.Flat;
                    hostComboBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                    hostComboBox.AutoCompleteSource = AutoCompleteSource.ListItems;
                }

                if (statusLabel != null)
                {
                    statusLabel.AutoSize = true;
                    statusLabel.ForeColor = Color.LightGray;
                    statusLabel.Text = "准备就绪";
                }

                // 创建一个新的Panel来容纳状态文本框
                Panel statusPanel = new Panel
                {
                    Dock = DockStyle.Bottom,
                    Height = statusHeight,
                    BackColor = Color.FromArgb(20, 20, 20),
                    BorderStyle = BorderStyle.None
                };
                
                // 使用Dock布局，确保状态文本框填充整个Panel
                statusTextBox = new RichTextBox
                {
                    BorderStyle = BorderStyle.None,
                    BackColor = Color.Black,
                    ForeColor = Color.White,
                    ReadOnly = true,
                    Dock = DockStyle.Fill,
                    Font = new Font("Consolas", 9F),
                    Padding = new Padding(10),
                    ScrollBars = RichTextBoxScrollBars.Vertical,
                    WordWrap = true
                };
                
                // 先将状态文本框添加到Panel
                statusPanel.Controls.Add(statusTextBox);
                
                // 再将Panel添加到窗体
                this.Controls.Add(statusPanel);
                
                // 确保这个Panel显示在最上层（Z顺序）
                statusPanel.BringToFront();
                
                // 初始状态提示
                AppendToStatusText("准备就绪", true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"自定义控件时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadHistory()
        {
            // 添加一些常用的DNS服务器作为示例
            AddToHistory("8.8.8.8");
            AddToHistory("8.8.4.4");
            AddToHistory("1.1.1.1");
        }

        private void AddToHistory(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                return;
            }

            if (historyHosts.Contains(host))
            {
            historyHosts.Remove(host);
            }
            
            historyHosts.Insert(0, host);
            
            if (historyHosts.Count > MaxHistoryCount)
            {
                historyHosts.RemoveAt(historyHosts.Count - 1);
            }

            UpdateComboBox();
        }

        private void UpdateComboBox()
        {
            hostComboBox.Items.Clear();
            hostComboBox.Items.AddRange(historyHosts.ToArray());
        }

        private void VisualizeButton_Click(object sender, EventArgs e)
        {
            if (resultListView.Items.Count == 0)
            {
                MessageBox.Show("请先执行路由追踪", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                var visualizationForm = new NetworkVisualizationForm();
                visualizationForm.Show();
                
                // 自动开始分析当前追踪的目标
                if (!string.IsNullOrEmpty(hostComboBox.Text))
                {
                    visualizationForm.StartAnalysis(hostComboBox.Text);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError("打开路径可视化窗口失败", ex, LogCategory.RouteTracing);
                MessageBox.Show($"打开路径可视化窗口失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateButtonStates()
        {
            var hasResults = resultListView.Items.Count > 0;
            exportButton.Enabled = hasResults;
            visualizeButton.Enabled = hasResults && !isTracing;
        }

        private async void StartButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (startButton.Text == "开始追踪")
                {
                    if (string.IsNullOrWhiteSpace(hostComboBox.Text))
                    {
                        UpdateStatus("请输入目标主机地址");
                        return;
                    }

                    // 初始化UI状态
                    startButton.Text = "停止追踪";
                    exportButton.Enabled = false;
                    visualizeButton.Enabled = false;
                    resultListView.Items.Clear();
                    UpdateStatus($"开始追踪路由: {hostComboBox.Text}");

                    // 使用InitializeRouteTracer方法初始化路由追踪器
                    InitializeRouteTracer();

                    // 保存到历史记录
                    AddToHistory(hostComboBox.Text);

                    // 开始追踪
                    await StartTrace();
                }
                else
                {
                    // 停止追踪
                    if (currentTracer != null)
                    {
                        currentTracer.Stop();
                        currentTracer = null;
                    }
                    if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
                    {
                        _cancellationTokenSource.Cancel();
                    }
                    startButton.Text = "开始追踪";
                    UpdateStatus("追踪已停止");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError("追踪操作异常", ex, LogCategory.RouteTracing);
                UpdateStatus($"错误: {ex.Message}");
                startButton.Text = "开始追踪";
            }
        }

        private async Task StartTrace()
        {
            if (string.IsNullOrWhiteSpace(hostComboBox.Text))
            {
                ShowError("请输入目标主机");
                return;
            }

            try
            {
                // 设置追踪状态为true
                isTracing = true;
                
                // 清理旧资源
                AppendToStatusText("开始清理旧资源...", true);
                CleanupResources();
                
                // 确保之前的令牌源已被释放
                AppendToStatusText("创建新的取消令牌...", true);
                _cancellationTokenSource = new CancellationTokenSource();
                
                // 更新UI状态 - 这里使用变量而不是直接调用，确保能够被覆盖
                UpdateUIState(true);
                
                // 清空之前的结果
                AppendToStatusText("清空先前的结果列表...", true);
                resultListView.Items.Clear();
                
                UpdateStatus($"开始追踪主机: {hostComboBox.Text}", true);
                UpdateStatus("初始化追踪...", true);
                
                // 创建和设置追踪器
                AppendToStatusText($"创建追踪器实例，目标: {hostComboBox.Text}", true);
                currentTracer = new RouteTracer(hostComboBox.Text);
                
                // 使用SynchronizationContext确保UI更新在UI线程上
                var uiContext = SynchronizationContext.Current;
                
                // 添加定期检查追踪状态的计时器，确保能够响应停止命令
                using (var checkCancellationTimer = new System.Windows.Forms.Timer())
                {
                    checkCancellationTimer.Interval = 100; // 每100毫秒检查一次
                    checkCancellationTimer.Tick += (s, e) => {
                        // 如果追踪状态已经为false，但追踪器还在运行，强制取消
                        if (!isTracing && currentTracer != null && _cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
                        {
                            AppendToStatusText("检测到追踪状态改变，强制取消操作", true);
                            _cancellationTokenSource.Cancel();
                        }
                    };
                    checkCancellationTimer.Start();
                
                    // 添加状态更新事件处理
                    AppendToStatusText("注册追踪器事件处理器...", true);
                    currentTracer.StatusUpdated += (s, status) => 
                    {
                        // 检查是否已经停止追踪，避免继续处理
                        if (!isTracing)
                        {
                            return;
                        }
                        
                        uiContext?.Post(_ => 
                        {
                            UpdateStatus(status, true);
                            Application.DoEvents(); // 确保UI响应
                        }, null);
                    };
                    
                    currentTracer.HopDiscovered += (s, hop) => 
                    {
                        // 检查是否已经停止追踪，避免继续处理
                        if (!isTracing)
                        {
                            return;
                        }
                        
                        // 使用UI同步上下文确保UI更新在主线程上
                        uiContext?.Post(_ => 
                        {
                            AppendHopResult(hop);
                            // 定期强制处理UI消息队列，确保停止按钮可以响应
                            Application.DoEvents();
                        }, null);
                    };
                    
                    // 在后台线程中运行追踪，避免阻塞UI
                    bool reached = false;
                    try
                    {
                        // 使用简单的Task.Run执行异步操作
                        AppendToStatusText("开始执行追踪操作...", true);
                        reached = await Task.Run(() => currentTracer.StartTrace(_cancellationTokenSource.Token));
                        AppendToStatusText($"追踪操作完成，结果: {(reached ? "成功" : "未到达目标")}", true);
                    }
                    catch (OperationCanceledException)
                    {
                        AppendToStatusText("追踪操作被取消", false);
                        UpdateStatus("追踪已取消", false);
                        return;
                    }
                    
                    string resultMessage = reached ? "追踪完成，已到达目标主机" : "追踪完成，但未能到达目标主机";
                    UpdateStatus(resultMessage, reached);
                    
                    // 停止计时器
                    checkCancellationTimer.Stop();
                }
            }
            catch (Exception ex)
            {
                AppendToStatusText($"追踪过程发生错误: {ex.Message}\n{ex.StackTrace}", false);
                UpdateStatus($"错误: {ex.Message}", false);
                HandleError("追踪过程中发生错误", ex);
            }
            finally
            {
                UpdateUIState(false);
                
                // 清理资源
                AppendToStatusText("追踪完成，开始清理资源...", true);
                CleanupResources();
                
                // 重置追踪状态
                isTracing = false;
                startButton.Text = "开始追踪";
                startButton.Enabled = true;
                AppendToStatusText("追踪状态已重置，UI已更新", true);
            }
        }

        private void UpdateUIState(bool tracing)
        {
            // 更新isTracing变量
            this.isTracing = tracing;
            
            // 修改此处，允许在追踪时点击停止按钮
            startButton.Enabled = true; // 始终启用开始/停止按钮
            hostComboBox.Enabled = !tracing;
            exportButton.Enabled = !tracing && resultListView.Items.Count > 0;
            visualizeButton.Enabled = !tracing && resultListView.Items.Count > 0;
            startButton.Text = tracing ? "停止追踪" : "开始追踪";
        }

        private void UpdateStatus(string message, bool success = true)
        {
            try {
                // 更新状态标签
                if (statusLabel != null && !statusLabel.IsDisposed)
                {
                    if (statusLabel.InvokeRequired)
                    {
                        statusLabel.BeginInvoke(new Action(() => {
                            statusLabel.Text = message;
                            statusLabel.ForeColor = success ? Color.Green : Color.Red;
                        }));
                    }
                    else
                    {
                        statusLabel.Text = message;
                        statusLabel.ForeColor = success ? Color.Green : Color.Red;
                    }
                }
                
                // 更新状态文本框
                AppendToStatusText(message, success);
            }
            catch (Exception) {
                // 不再输出调试信息
            }
        }

        private void HandleError(string message, Exception ex)
        {
            Logger.Instance.LogError(message, ex);
            UpdateStatus($"{message}: {ex.Message}", false);
            MessageBox.Show(
                $"{message}\n\n{ex.Message}",
                "错误",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }

        private void ShowError(string message)
        {
            MessageBox.Show(
                message,
                "提示",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
        }

        private void AppendHopResult(RouteHop hop)
        {
            if (hop == null) return;

            try
            {
                if (hop.HopNumber == 0)
                {
                    if (hop.IpAddress == "ERROR")
                    {
                        // statusLabel.Text = hop.HostName;
                        AppendToStatusText($"错误: {hop.HostName}", false);
                    }
                    return;
                }

                var item = new ListViewItem(hop.HopNumber.ToString());
                item.SubItems.Add(hop.IpAddress);
                
                // 添加延迟时间
                foreach (var delay in hop.DelayTimes)
                {
                    string delayText = delay switch
                    {
                        < 0 => "*",
                        0 => "<1 ms",
                        _ => $"{delay} ms"
                    };
                    item.SubItems.Add(delayText);
                }

                // 计算并添加平均延迟
                var validDelays = hop.DelayTimes.Where(d => d >= 0).ToList();
                string avgDelay = validDelays.Any() 
                    ? $"{validDelays.Average():F1} ms" 
                    : "*";
                item.SubItems.Add(avgDelay);

                // 添加主机名和位置信息
                item.SubItems.Add(hop.HostName ?? string.Empty);
                item.SubItems.Add(hop.Location ?? string.Empty);

                // 设置项目颜色
                if (validDelays.Any())
                {
                    var avg = validDelays.Average();
                    item.ForeColor = avg switch
                    {
                        <= 50 => Color.LightGreen,
                        <= 100 => Color.FromArgb(255, 200, 100), // 橙色
                        <= 200 => Color.FromArgb(255, 127, 80),  // 珊瑚色
                        _ => Color.FromArgb(255, 100, 100)       // 较亮的红色
                    };
                }
                else
                {
                    item.ForeColor = Color.FromArgb(150, 150, 150); // 较亮的灰色
                }

                if (resultListView == null || resultListView.IsDisposed)
                {
                    Debug.WriteLine("ListView is null or disposed");
                    return;
                }

                if (resultListView.InvokeRequired)
                {
                    try
                    {
                        resultListView.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                resultListView.Items.Add(item);
                                if (resultListView.Items.Count > 0)
                                {
                                    resultListView.Items[resultListView.Items.Count - 1].EnsureVisible();
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error adding item to ListView: {ex.Message}");
                            }
                        }));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error invoking ListView update: {ex.Message}");
                    }
                }
                else
                {
                    try
                    {
                        resultListView.Items.Add(item);
                        if (resultListView.Items.Count > 0)
                        {
                            resultListView.Items[resultListView.Items.Count - 1].EnsureVisible();
                        }
                }
                catch (Exception ex)
                {
                        Debug.WriteLine($"Error adding item to ListView: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AppendHopResult Error: {ex.Message}\nHop: {hop?.HopNumber}\n{ex.StackTrace}");
            }
        }

        private async void ExportButton_Click(object sender, EventArgs e)
        {
            if (resultListView.Items.Count == 0)
            {
                ShowError("没有可导出的追踪结果");
                return;
            }

            try
            {
                using var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV文件|*.csv|文本文件|*.txt|所有文件|*.*",
                    Title = "导出追踪结果",
                    DefaultExt = "csv",
                    FileName = $"路由追踪结果_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveDialog.ShowDialog() != DialogResult.OK) return;

                UpdateUIState(true);
                UpdateStatus("正在导出...", true);
                
                // 不再使用进度对话框
                // using var progress = new ProgressDialog("导出结果", "正在导出...");
                // progress.Show(this);

                await Task.Run(() =>
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("跳数,IP地址,延迟1,延迟2,延迟3,平均延迟,主机名,位置");

                    var totalItems = resultListView.Items.Count;
                    for (int i = 0; i < totalItems; i++)
                    {
                        var item = resultListView.Items[i];
                        string line = string.Join(",", item.SubItems.Cast<ListViewItem.ListViewSubItem>()
                            .Select(subItem => $"\"{subItem.Text}\""));
                        sb.AppendLine(line);
                        
                        // 不再更新进度对话框
                        // progress.SetProgress(i + 1, totalItems);
                        
                        // 改为更新状态
                        int percentage = (int)((float)(i + 1) / totalItems * 100);
                        if (statusTextBox != null && statusTextBox.InvokeRequired)
                        {
                            statusTextBox.BeginInvoke(new Action(() => 
                                UpdateStatus($"正在导出... {i + 1}/{totalItems} ({percentage}%)", true)));
                        }
                        else if (statusTextBox != null)
                        {
                            UpdateStatus($"正在导出... {i + 1}/{totalItems} ({percentage}%)", true);
                        }
                    }

                    File.WriteAllText(saveDialog.FileName, sb.ToString(), Encoding.UTF8);
                });

                UpdateStatus("导出完成");
                MessageBox.Show("结果已成功导出", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                HandleError("导出失败", ex);
            }
            finally
            {
                UpdateUIState(false);
            }
        }

        private void AppendToLog(string message)
        {
            // 方法保留但不执行任何操作，直接转发到状态更新方法
            UpdateStatus(message, !message.Contains("错误") && !message.Contains("失败"));
        }

        // 添加一个新方法用于追加状态信息到状态文本框
        private void AppendToStatusText(string message, bool success = true)
        {
            try
            {
                if (statusTextBox == null || statusTextBox.IsDisposed) return;

                if (statusTextBox.InvokeRequired)
                {
                    try
                    {
                        statusTextBox.BeginInvoke(new Action(() => AppendToStatusTextInternal(message, success)));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error invoking AppendToStatusTextInternal: {ex.Message}");
                    }
                }
                else
                {
                    AppendToStatusTextInternal(message, success);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in AppendToStatusText: {ex.Message}");
            }
        }
        
        private void AppendToStatusTextInternal(string message, bool success)
        {
            try
            {
                // 确保statusTextBox不为null
                if (statusTextBox == null) return;
                
                // 添加时间戳
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                string formattedMessage = $"[{timestamp}] {message}\n";
                
                // 设置文本颜色
                Color textColor = success ? Color.LightGreen : Color.FromArgb(255, 100, 100);
                
                // 记录当前位置
                int start = statusTextBox.TextLength;
                statusTextBox.AppendText(formattedMessage);
                int end = statusTextBox.TextLength;
                
                // 设置新添加文本的颜色
                statusTextBox.Select(start, end - start);
                statusTextBox.SelectionColor = textColor;
                statusTextBox.SelectionLength = 0; // 清除选择
                
                // 限制最大行数
                const int maxLines = 200;
                if (statusTextBox.Lines.Length > maxLines)
                {
                    var lines = statusTextBox.Lines.Skip(statusTextBox.Lines.Length - maxLines).ToArray();
                    statusTextBox.Clear();
                    foreach (var line in lines)
                    {
                        statusTextBox.AppendText(line + Environment.NewLine);
                    }
                }
                
                // 滚动到底部
                statusTextBox.SelectionStart = statusTextBox.Text.Length;
                statusTextBox.ScrollToCaret();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in AppendToStatusTextInternal: {ex.Message}");
            }
        }

        // 添加资源清理方法
        private void CleanupResources()
        {
            try
            {
                // 首先停止追踪器
                if (currentTracer != null)
                {
                    AppendToStatusText("清理资源: 停止追踪器", true);
                    currentTracer.Stop();
                    currentTracer = null;
                }
                else
                {
                    AppendToStatusText("清理资源: 追踪器已为空", true);
                }
                
                // 然后处理令牌源
                if (_cancellationTokenSource != null)
                {
                    if (!_cancellationTokenSource.IsCancellationRequested)
                    {
                        try 
                        { 
                            AppendToStatusText("清理资源: 取消未完成的操作", true);
                            _cancellationTokenSource.Cancel(); 
                        } 
                        catch (Exception ex) 
                        { 
                            AppendToStatusText($"清理资源: 取消操作时出错: {ex.Message}", false);
                        }
                    }
                    
                    AppendToStatusText("清理资源: 释放取消令牌", true);
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
                }
                else
                {
                    AppendToStatusText("清理资源: 取消令牌已为空", true);
                }
                
                // 强制垃圾回收
                AppendToStatusText("清理资源: 执行垃圾回收", true);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                AppendToStatusText("清理资源: 完成", true);
            }
            catch (Exception ex)
            {
                AppendToStatusText($"清理资源时出错: {ex.Message}\n{ex.StackTrace}", false);
                Logger.Instance.LogError("清理资源时出错", ex);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (isTracing)
            {
                // 确保在关闭表单前停止任何正在进行的追踪
                try
                {
                    // 停止追踪器
                    if (currentTracer != null)
                    {
                        currentTracer.Stop();
                        currentTracer = null;
                    }
                    
                    // 取消操作
                    if (_cancellationTokenSource != null)
                    {
                        if (!_cancellationTokenSource.IsCancellationRequested)
                            _cancellationTokenSource.Cancel();
                        
                        _cancellationTokenSource.Dispose();
                        _cancellationTokenSource = null;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogError("关闭表单时停止追踪出错", ex);
                }
            }
            
            CleanupResources();
            base.OnFormClosing(e);
        }

        private void InitializeRouteTracer()
        {
            currentTracer = new RouteTracer(hostComboBox.Text);
            currentTracer.StatusUpdated += (sender, status) => UpdateStatus(status);
            currentTracer.HopDiscovered += RouteTracer_HopDiscovered;
            // 订阅新增的位置信息更新事件
            currentTracer.HopLocationUpdated += RouteTracer_HopLocationUpdated;
        }

        // 处理发现新的跳点
        private void RouteTracer_HopDiscovered(object? sender, RouteHop hop)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => RouteTracer_HopDiscovered(sender, hop)));
                return;
            }

            AppendHopResult(hop);
        }

        // 处理位置信息更新
        private void RouteTracer_HopLocationUpdated(object? sender, RouteHopUpdateEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => RouteTracer_HopLocationUpdated(sender, e)));
                return;
            }

            try
            {
                // 查找对应的列表项并更新位置信息
                int hopNumber = e.Hop.HopNumber;
                foreach (ListViewItem item in resultListView.Items)
                {
                    if (int.TryParse(item.SubItems[0].Text, out int itemHopNumber) && itemHopNumber == hopNumber)
                    {
                        // 第8列(索引7)是位置信息
                        if (item.SubItems.Count > 7)
                        {
                            item.SubItems[7].Text = e.Hop.Location ?? string.Empty;
                            UpdateStatus($"更新跳点 #{hopNumber} 的位置信息: {e.Hop.Location}");
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError("更新跳点位置信息失败", ex, LogCategory.RouteTracing);
            }
        }
    }
} 