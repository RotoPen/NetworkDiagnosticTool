using System;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using NetworkDiagnosticTool.Logging;
using NetworkDiagnosticTool.UI;
using System.Collections.Generic;
using System.Linq;

namespace NetworkDiagnosticTool
{
    public class NetworkTestForm : Form
    {
        private Panel dragPanel = null;
        private Point dragStartPoint;
        private int dragStartIndex = -1;
        private bool isDragging = false;
        private System.Windows.Forms.Timer reorderTimer = null;  // 明确指定Timer类型
        private const int REORDER_DELAY = 50;  // 50ms的重排延迟

        private readonly TableLayoutPanel mainLayout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.FromArgb(32, 32, 32),
            Padding = new Padding(10),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None
        };

        private readonly Panel testPanel = new()
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.FromArgb(32, 32, 32),
            Padding = new Padding(10),
            AutoSize = false
        };

        private readonly RichTextBox descriptionBox = new()
        {
            ReadOnly = true,
            BackColor = Color.Black,
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9F)
        };

        private readonly Panel customUrlPanel = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(45, 45, 48),
            Padding = new Padding(10),
            Margin = new Padding(0, 10, 0, 10)
        };

        private readonly TextBox nameTextBox = new()
        {
            PlaceholderText = "网站名称",
            Width = 200,
            Margin = new Padding(0, 0, 10, 0)
        };

        private readonly TextBox urlTextBox = new()
        {
            PlaceholderText = "网站地址",
            Width = 300,
            Margin = new Padding(0, 0, 10, 0)
        };

        private readonly TextBox descTextBox = new()
        {
            PlaceholderText = "网站描述",
            Width = 400,
            Margin = new Padding(0, 0, 10, 0)
        };

        private readonly Button addButton = new()
        {
            Text = "添加",
            BackColor = MessageColors.Progress,
            ForeColor = MessageColors.Normal,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(80, 30)
        };

        public NetworkTestForm()
        {
            InitializeComponents();
            Logger.Instance.Log("网络测试窗口已启动", LogLevel.Info, LogCategory.NetworkTest);
        }

        private void InitializeComponents()
        {
            Text = "网络测试";
            Size = new Size(800, 800);  // 增加窗体高度
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(32, 32, 32);
            ForeColor = Color.White;

            // 设置布局
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));  // 增加描述文本区域高度
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));   // 自定义网址区域
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // 测试项目列表

            // 添加描述文本
            descriptionBox.Text = "点击以下测试项目，将在浏览器中打开对应的网络测试网站。\n" +
                                "这些网站提供专业的网络测试服务，包括：\n" +
                                "• 网络速度测试（上传/下载）\n" +
                                "• 网络延迟测试\n" +
                                "• 网络质量评估\n" +
                                "• 路由追踪\n" +
                                "• 网络诊断\n\n" +
                                "请确保在测试时关闭其他占用带宽的应用程序，以获得准确结果。";

            // 设置自定义网址面板
            var customUrlLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                Padding = new Padding(0),
                BackColor = Color.FromArgb(45, 45, 48),
                Margin = new Padding(0)
            };

            // 设置输入框样式
            nameTextBox.BackColor = Color.FromArgb(45, 45, 48);
            nameTextBox.ForeColor = MessageColors.Normal;
            nameTextBox.BorderStyle = BorderStyle.FixedSingle;

            urlTextBox.BackColor = Color.FromArgb(45, 45, 48);
            urlTextBox.ForeColor = MessageColors.Normal;
            urlTextBox.BorderStyle = BorderStyle.FixedSingle;

            descTextBox.BackColor = Color.FromArgb(45, 45, 48);
            descTextBox.ForeColor = MessageColors.Normal;
            descTextBox.BorderStyle = BorderStyle.FixedSingle;

            customUrlLayout.Controls.AddRange(new Control[] { nameTextBox, urlTextBox, descTextBox, addButton });
            customUrlPanel.Controls.Add(customUrlLayout);

            // 修改添加按钮点击事件
            addButton.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(nameTextBox.Text) || 
                    string.IsNullOrWhiteSpace(urlTextBox.Text) || 
                    string.IsNullOrWhiteSpace(descTextBox.Text))
                {
                    MessageBox.Show("请填写完整的网站信息", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string url = urlTextBox.Text;
                if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                {
                    url = "https://" + url;
                }

                AddTestItem(nameTextBox.Text, descTextBox.Text, url, true);
                
                // 清空输入框
                nameTextBox.Clear();
                urlTextBox.Clear();
                descTextBox.Clear();
            };

            testPanel.SizeChanged += (s, e) =>
            {
                int yOffset = 0;
                foreach (Control control in testPanel.Controls)
                {
                    if (control is Panel panel)
                    {
                        panel.Width = testPanel.ClientSize.Width - 20;
                        panel.Location = new Point(0, yOffset);
                        yOffset += panel.Height + 10;
                        
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

            // 添加一个空白面板作为偏移
            var spacerPanel = new Panel
            {
                Height = 10,
                Margin = new Padding(0)
            };
            testPanel.Controls.Add(spacerPanel);

            // 添加默认测试项目（保持原有顺序）
            AddTestItem("Speedtest by Ookla", "全球最受欢迎的网络速度测试网站", "https://www.speedtest.net/");
            AddTestItem("Fast.com", "Netflix提供的简单快速的速度测试", "https://fast.com/");
            AddTestItem("Cloudflare Speed Test", "Cloudflare提供的全球网络性能测试", "https://speed.cloudflare.com/");
            AddTestItem("Ping Test", "全球多个节点的延迟测试", "https://www.pingtest.net/");
            AddTestItem("IPIP.net", "网络路由追踪和诊断", "https://tools.ipip.net/traceroute.php");
            AddTestItem("DNS Leak Test", "DNS泄露测试", "https://www.dnsleaktest.com/");
            AddTestItem("WebRTC Leak Test", "WebRTC泄露测试", "https://browserleaks.com/webrtc");
            AddTestItem("MTR Online", "网络路由和延迟分析", "https://www.mtr-online.com/");

            // 初始化重排计时器
            reorderTimer = new System.Windows.Forms.Timer
            {
                Interval = REORDER_DELAY,
                Enabled = false
            };
            reorderTimer.Tick += (s, e) =>
            {
                reorderTimer.Stop();
                ReorderTestItems();
            };

            // 添加控件
            mainLayout.Controls.Add(descriptionBox, 0, 0);
            mainLayout.Controls.Add(customUrlPanel, 0, 1);
            mainLayout.Controls.Add(testPanel, 0, 2);
            Controls.Add(mainLayout);
        }

        private void AddTestItem(string title, string description, string url, bool isCustom = false)
        {
            var panel = new Panel
            {
                Width = testPanel.ClientSize.Width - 20,
                Height = 90,
                Margin = new Padding(0, 0, 0, 10),
                BackColor = Color.FromArgb(45, 45, 48),
                Padding = new Padding(15),
                AutoSize = false,
                Tag = isCustom
            };

            // 添加鼠标事件处理
            panel.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    dragPanel = panel;
                    dragStartPoint = e.Location;
                    dragStartIndex = testPanel.Controls.GetChildIndex(panel);
                    isDragging = false;  // 初始状态设为false
                    panel.Cursor = Cursors.SizeAll;
                    
                    // 使用半透明颜色
                    panel.BackColor = Color.FromArgb(180, 60, 60, 60);
                    // 将当前面板置于最顶层
                    testPanel.Controls.SetChildIndex(panel, testPanel.Controls.Count - 1);
                }
            };

            panel.MouseMove += (s, e) =>
            {
                if (dragPanel != null)
                {
                    // 计算移动距离
                    int deltaX = Math.Abs(e.X - dragStartPoint.X);
                    int deltaY = Math.Abs(e.Y - dragStartPoint.Y);
                    
                    // 增加拖动阈值到20像素
                    if (!isDragging && (deltaX > 20 || deltaY > 20))
                    {
                        isDragging = true;
                    }

                    if (isDragging)
                    {
                        // 计算新的Y坐标
                        int currentY = panel.Top + (e.Y - dragStartPoint.Y);
                        currentY = Math.Max(0, currentY);
                        currentY = Math.Min(testPanel.ClientSize.Height - panel.Height, currentY);

                        // 更新面板位置
                        panel.Top = currentY;
                    }
                }
            };

            panel.MouseUp += (s, e) =>
            {
                if (dragPanel != null)
                {
                    isDragging = false;
                    dragPanel.Cursor = Cursors.Default;
                    panel.BackColor = Color.FromArgb(45, 45, 48);
                    dragPanel = null;
                    
                    // 确保最后一次重排
                    ReorderTestItems();
                    Logger.Instance.Log($"重新排序测试网站: {title}", LogLevel.Info, LogCategory.NetworkTest);
                }
            };

            // 添加鼠标离开事件处理
            panel.MouseLeave += (s, e) =>
            {
                if (isDragging)
                {
                    isDragging = false;
                    panel.Cursor = Cursors.Default;
                    panel.BackColor = Color.FromArgb(45, 45, 48);
                    dragPanel = null;
                    ReorderTestItems();
                }
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
                Location = new Point(15, 45)
            };

            var testButton = new Button
            {
                Text = "开始测试",
                BackColor = MessageColors.Progress,
                ForeColor = MessageColors.Normal,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(100, 30),
                Location = new Point(panel.Width - (isCustom ? 330 : 130), 30),
                Margin = new Padding(0),
                Tag = url
            };

            if (isCustom)
            {
                // 添加上移按钮
                var upButton = new Button
                {
                    Text = "↑",
                    BackColor = Color.FromArgb(60, 60, 60),
                    ForeColor = MessageColors.Normal,
                    FlatStyle = FlatStyle.Flat,
                    Size = new Size(40, 30),
                    Location = new Point(panel.Width - 230, 30),
                    Margin = new Padding(0)
                };

                // 添加下移按钮
                var downButton = new Button
                {
                    Text = "↓",
                    BackColor = Color.FromArgb(60, 60, 60),
                    ForeColor = MessageColors.Normal,
                    FlatStyle = FlatStyle.Flat,
                    Size = new Size(40, 30),
                    Location = new Point(panel.Width - 180, 30),
                    Margin = new Padding(0)
                };

                // 删除按钮
                var deleteButton = new Button
                {
                    Text = "删除",
                    BackColor = Color.FromArgb(200, 50, 50),
                    ForeColor = MessageColors.Normal,
                    FlatStyle = FlatStyle.Flat,
                    Size = new Size(80, 30),
                    Location = new Point(panel.Width - 130, 30),
                    Margin = new Padding(0)
                };

                upButton.Click += (s, e) =>
                {
                    int index = testPanel.Controls.GetChildIndex(panel);
                    if (index > 1) // 考虑spacerPanel的存在，所以是1而不是0
                    {
                        testPanel.Controls.SetChildIndex(panel, index - 1);
                        ReorderTestItems();
                        Logger.Instance.Log($"上移测试网站: {title}", LogLevel.Info, LogCategory.NetworkTest);
                    }
                };

                downButton.Click += (s, e) =>
                {
                    int index = testPanel.Controls.GetChildIndex(panel);
                    if (index < testPanel.Controls.Count - 1)
                    {
                        testPanel.Controls.SetChildIndex(panel, index + 1);
                        ReorderTestItems();
                        Logger.Instance.Log($"下移测试网站: {title}", LogLevel.Info, LogCategory.NetworkTest);
                    }
                };

                deleteButton.Click += (s, e) =>
                {
                    testPanel.Controls.Remove(panel);
                    ReorderTestItems();
                    Logger.Instance.Log($"删除测试网站: {title}", LogLevel.Info, LogCategory.NetworkTest);
                };

                panel.Controls.AddRange(new Control[] { upButton, downButton, deleteButton });
            }

            testButton.Click += (s, e) =>
            {
                if (testButton.Tag is string testUrl)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new ProcessStartInfo
                        {
                            FileName = testUrl,
                            UseShellExecute = true
                        });
                        Logger.Instance.Log($"打开测试网站: {title}", LogLevel.Info, LogCategory.NetworkTest);
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.LogError($"打开测试网站失败: {title}", ex, LogCategory.NetworkTest);
                        MessageBox.Show($"无法打开测试网站: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            };

            panel.Controls.AddRange(new Control[] { titleLabel, descLabel, testButton });
            
            // 将自定义网址添加到列表底部
            if (isCustom)
            {
                // 找到最后一个非自定义网址的位置
                int lastDefaultIndex = -1;
                for (int i = testPanel.Controls.Count - 1; i >= 0; i--)
                {
                    if (testPanel.Controls[i] is Panel p && p.Tag is bool isCustomPanel && !isCustomPanel)
                    {
                        lastDefaultIndex = i;
                        break;
                    }
                }

                testPanel.Controls.Add(panel);
                if (lastDefaultIndex != -1)
                {
                    // 将面板移动到最后一个默认网址的后面
                    testPanel.Controls.SetChildIndex(panel, lastDefaultIndex + 1);
                }
            }
            else
            {
                testPanel.Controls.Add(panel);
            }
            
            // 重新排序所有项目
            ReorderTestItems();
        }

        private void ReorderTestItems()
        {
            // 获取所有面板并按Y坐标排序
            var panels = testPanel.Controls.OfType<Panel>().OrderBy(p => p.Top).ToList();
            
            // 清空并重新添加面板
            testPanel.Controls.Clear();
            foreach (var panel in panels)
            {
                testPanel.Controls.Add(panel);
            }
            
            // 更新所有面板的位置
            int y = 0;
            foreach (Panel panel in testPanel.Controls)
            {
                panel.Top = y;
                y += panel.Height + 10;
            }
        }

        private void InitializeDragAndDrop(Panel panel)
        {
            bool isDragging = false;
            Point dragStartPoint = Point.Empty;
            Panel dragPanel = null;

            panel.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    isDragging = false;
                    dragStartPoint = e.Location;
                    dragPanel = panel;
                    panel.BackColor = Color.FromArgb(180, 60, 60, 60);
                    // 将当前面板置于最顶层
                    testPanel.Controls.SetChildIndex(panel, testPanel.Controls.Count - 1);
                }
            };

            panel.MouseMove += (s, e) =>
            {
                if (dragPanel != null)
                {
                    int deltaX = Math.Abs(e.X - dragStartPoint.X);
                    int deltaY = Math.Abs(e.Y - dragStartPoint.Y);
                    
                    if (!isDragging && (deltaX > 20 || deltaY > 20))
                    {
                        isDragging = true;
                    }

                    if (isDragging)
                    {
                        int currentY = panel.Top + (e.Y - dragStartPoint.Y);
                        currentY = Math.Max(0, currentY);
                        currentY = Math.Min(testPanel.ClientSize.Height - panel.Height, currentY);
                        panel.Top = currentY;
                    }
                }
            };

            panel.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    dragPanel = null;
                    isDragging = false;
                    panel.BackColor = Color.FromArgb(45, 45, 48);
                    ReorderTestItems();
                }
            };

            panel.MouseLeave += (s, e) =>
            {
                if (isDragging)
                {
                    dragPanel = null;
                    isDragging = false;
                    panel.BackColor = Color.FromArgb(45, 45, 48);
                    ReorderTestItems();
                }
            };
        }
    }
} 