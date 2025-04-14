using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetworkDiagnosticTool.Logging;
using System.Linq;
using System.Collections.Generic;

namespace NetworkDiagnosticTool
{
    public partial class LogViewerForm : Form
    {
        private readonly TableLayoutPanel mainLayout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            BackColor = Color.FromArgb(32, 32, 32)
        };

        private readonly TableLayoutPanel filterPanel = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.FromArgb(32, 32, 32)
        };

        private readonly FlowLayoutPanel buttonPanel = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.FromArgb(32, 32, 32)
        };

        private readonly ComboBox categoryComboBox = new()
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(45, 45, 48),
            ForeColor = Color.White
        };

        private readonly ComboBox logLevelComboBox = new()
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(45, 45, 48),
            ForeColor = Color.White
        };

        private readonly ListBox logFileList = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(45, 45, 48),
            ForeColor = Color.White,
            Margin = new Padding(5),
            MinimumSize = new Size(0, 100)
        };

        private readonly TextBox searchBox = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(45, 45, 48),
            ForeColor = Color.White,
            PlaceholderText = "搜索..."
        };

        private readonly RichTextBox logContentBox = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            ForeColor = Color.White,
            Font = new Font("Consolas", 9F),
            ReadOnly = true,
            WordWrap = false,
            Margin = new Padding(5),
            MinimumSize = new Size(0, 100)
        };

        private readonly Button refreshButton = new()
        {
            Text = "刷新",
            BackColor = Color.FromArgb(0, 122, 204),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(120, 35),
            Margin = new Padding(5, 0, 5, 0)
        };

        private readonly Button clearButton = new()
        {
            Text = "清空",
            BackColor = Color.FromArgb(0, 122, 204),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(120, 35),
            Margin = new Padding(5, 0, 5, 0)
        };

        private readonly Button exportButton = new()
        {
            Text = "导出",
            BackColor = Color.FromArgb(0, 122, 204),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(120, 35),
            Margin = new Padding(5, 0, 5, 0)
        };

        public LogViewerForm()
        {
            Text = "日志查看器";
            Size = new Size(800, 600);
            BackColor = Color.FromArgb(32, 32, 32);

            InitializeLayout();
            InitializeFilters();
            InitializeButtons();
            InitializeLogViewer();
            
            // 绑定事件
            refreshButton.Click += RefreshButton_Click;
            clearButton.Click += ClearButton_Click;
            exportButton.Click += ExportButton_Click;
            logFileList.SelectedIndexChanged += LogFileList_SelectedIndexChanged;
            categoryComboBox.SelectedIndexChanged += (s, e) => RefreshLogFiles();
            logLevelComboBox.SelectedIndexChanged += (s, e) => FilterLogContent();
            searchBox.TextChanged += (s, e) => FilterLogContent();

            // 初始加载日志文件
            RefreshLogFiles();
        }

        private void InitializeLayout()
        {
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));

            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            Controls.Add(mainLayout);
        }

        private void InitializeFilters()
        {
            filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));

            categoryComboBox.Items.AddRange(
                Enum.GetValues(typeof(LogCategory))
                    .Cast<LogCategory>()
                    .Select(c => GetCategoryDisplayName(c))
                    .ToArray());
            categoryComboBox.SelectedIndex = 0;

            logLevelComboBox.Items.AddRange(new[] { "全部", "调试", "信息", "警告", "错误", "严重" });
            logLevelComboBox.SelectedIndex = 0;

            filterPanel.Controls.Add(categoryComboBox, 0, 0);
            filterPanel.Controls.Add(logLevelComboBox, 1, 0);
            filterPanel.Controls.Add(searchBox, 2, 0);

            mainLayout.Controls.Add(filterPanel, 0, 0);
            mainLayout.SetColumnSpan(filterPanel, 2);
        }

        private void InitializeButtons()
        {
            buttonPanel.Controls.AddRange(new Control[] { refreshButton, clearButton, exportButton });
            mainLayout.Controls.Add(buttonPanel, 0, 1);
            mainLayout.SetColumnSpan(buttonPanel, 2);
        }

        private void InitializeLogViewer()
        {
            mainLayout.Controls.Add(logFileList, 0, 2);
            mainLayout.Controls.Add(logContentBox, 1, 2);
        }

        private string GetCategoryDisplayName(LogCategory category)
        {
            return category switch
            {
                LogCategory.General => "常规",
                LogCategory.NetworkDiagnosis => "网络诊断",
                LogCategory.PingTest => "Ping测试",
                LogCategory.DnsTest => "DNS测试",
                LogCategory.PortScan => "端口扫描",
                _ => category.ToString()
            };
        }

        private void RefreshLogFiles()
        {
            logFileList.Items.Clear();
            var selectedCategoryName = categoryComboBox.Text;
            var selectedCategory = Enum.GetValues(typeof(LogCategory))
                .Cast<LogCategory>()
                .First(c => GetCategoryDisplayName(c) == selectedCategoryName);
            
            var logFiles = Logger.Instance.GetLogFiles()
                .Where(x => x.Category == selectedCategory)
                .OrderByDescending(x => x.FilePath);

            foreach (var (filePath, _) in logFiles)
            {
                logFileList.Items.Add(Path.GetFileName(filePath));
            }
        }

        private async void LogFileList_SelectedIndexChanged(object? sender, EventArgs? e)
        {
            if (logFileList.SelectedItem == null) return;

            try
            {
                var selectedCategory = (LogCategory)Enum.Parse(typeof(LogCategory), categoryComboBox.Text);
                var fileName = logFileList.SelectedItem.ToString();
                var filePath = Path.Combine(
                    Logger.Instance.LogDirectory,
                    selectedCategory.ToString(),
                    fileName);

                var content = await Logger.Instance.ReadLogFileAsync(filePath);
                logContentBox.Text = content;
                FilterLogContent();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"读取日志文件失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string GetLogLevelTag(string level)
        {
            return level switch
            {
                "调试" => "[Debug]",
                "信息" => "[Info]",
                "警告" => "[Warning]",
                "错误" => "[Error]",
                "严重" => "[Critical]",
                _ => level
            };
        }

        private void FilterLogContent()
        {
            if (string.IsNullOrEmpty(logContentBox.Text)) return;

            var selectedLevel = logLevelComboBox.SelectedItem.ToString();
            var searchText = searchBox.Text.ToLower();
            var lines = logContentBox.Text.Split(Environment.NewLine);
            var filteredLines = new List<string>();

            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line)) continue;

                bool levelMatch = selectedLevel == "全部" || line.Contains(GetLogLevelTag(selectedLevel));
                bool searchMatch = string.IsNullOrEmpty(searchText) || line.ToLower().Contains(searchText);

                if (levelMatch && searchMatch)
                {
                    filteredLines.Add(line);
                }
            }

            logContentBox.Text = string.Join(Environment.NewLine, filteredLines);
        }

        private void RefreshButton_Click(object? sender, EventArgs? e)
        {
            RefreshLogFiles();
        }

        private async void ClearButton_Click(object? sender, EventArgs? e)
        {
            if (MessageBox.Show("确定要清空所有日志吗？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                try
                {
                    await Logger.Instance.ClearLogs();
                    logContentBox.Clear();
                    RefreshLogFiles();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"清空日志失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async void ExportButton_Click(object? sender, EventArgs? e)
        {
            using var dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    await Logger.Instance.ExportLogsAsync(dialog.SelectedPath);
                    MessageBox.Show("日志导出成功！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出日志失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
} 