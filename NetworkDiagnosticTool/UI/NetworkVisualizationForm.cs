using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Threading.Tasks;
using NetworkDiagnosticTool.Logging;
using System.Net.NetworkInformation;
using System.Linq;
using System.Text;
using NetworkDiagnosticTool.Core;

namespace NetworkDiagnosticTool.UI
{
    public class NetworkVisualizationForm : Form
    {
        private readonly TableLayoutPanel mainLayout = new();
        private readonly Panel visualizationPanel = new();
        private readonly Panel controlPanel = new();
        private readonly DataGridView mtrResultGrid = new();
        private readonly Button startAnalysisButton = new();
        private readonly ComboBox targetComboBox = new();
        private readonly NumericUpDown intervalUpDown = new();
        private readonly Label statusLabel = new();
        
        private bool isAnalyzing = false;
        private List<MTRHopStatistics> _currentResults = new();
        private MTRAnalyzer _mtrAnalyzer = new();
        
        public NetworkVisualizationForm()
        {
            InitializeComponents();
            InitializeMTRGrid();
            _mtrAnalyzer.ProgressUpdated += MtrAnalyzer_ProgressUpdated;
            Logger.Instance.Log("网络可视化窗口已启动", LogLevel.Info, LogCategory.NetworkAnalysis);
        }

        private void InitializeComponents()
        {
            Text = "网络路径可视化与MTR分析";
            Size = new Size(1000, 800);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(30, 30, 30);
            ForeColor = Color.White;

            mainLayout.RowCount = 2;
            mainLayout.ColumnCount = 2;
            mainLayout.Dock = DockStyle.Fill;
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 70));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            mainLayout.Padding = new Padding(10);

            // 可视化面板
            visualizationPanel.Dock = DockStyle.Fill;
            visualizationPanel.BackColor = Color.FromArgb(35, 35, 35);
            visualizationPanel.Paint += VisualizationPanel_Paint;

            // 控制面板
            controlPanel.Dock = DockStyle.Fill;
            controlPanel.BackColor = Color.FromArgb(40, 40, 40);
            controlPanel.Padding = new Padding(10);

            // 目标输入
            targetComboBox.Dock = DockStyle.Top;
            targetComboBox.Font = new Font("Microsoft YaHei", 9F);
            targetComboBox.Items.AddRange(new object[] { "www.baidu.com", "www.google.com", "www.microsoft.com" });
            targetComboBox.AutoCompleteMode = AutoCompleteMode.Suggest;
            targetComboBox.AutoCompleteSource = AutoCompleteSource.ListItems;

            // 间隔设置
            var intervalLabel = new Label
            {
                Text = "检测间隔(秒):",
                Dock = DockStyle.Top,
                Margin = new Padding(0, 10, 0, 0)
            };

            intervalUpDown.Minimum = 1;
            intervalUpDown.Maximum = 60;
            intervalUpDown.Value = 5;
            intervalUpDown.Dock = DockStyle.Top;

            // 开始按钮
            startAnalysisButton.Text = "开始分析";
            startAnalysisButton.Dock = DockStyle.Top;
            startAnalysisButton.Margin = new Padding(0, 10, 0, 0);
            startAnalysisButton.Click += StartAnalysisButton_Click;

            // 状态标签
            statusLabel.Dock = DockStyle.Top;
            statusLabel.Margin = new Padding(0, 10, 0, 0);
            statusLabel.ForeColor = Color.LightGreen;

            // MTR结果表格
            mtrResultGrid.Dock = DockStyle.Fill;
            mtrResultGrid.BackgroundColor = Color.FromArgb(35, 35, 35);
            mtrResultGrid.ForeColor = Color.Black;
            mtrResultGrid.GridColor = Color.FromArgb(60, 60, 60);
            mtrResultGrid.BorderStyle = BorderStyle.None;

            // 添加控件
            controlPanel.Controls.AddRange(new Control[] {
                startAnalysisButton,
                intervalUpDown,
                intervalLabel,
                targetComboBox,
                statusLabel
            });

            mainLayout.Controls.Add(visualizationPanel, 0, 0);
            mainLayout.Controls.Add(controlPanel, 1, 0);
            mainLayout.Controls.Add(mtrResultGrid, 0, 1);
            mainLayout.SetColumnSpan(mtrResultGrid, 2);

            Controls.Add(mainLayout);
        }

        private void InitializeMTRGrid()
        {
            mtrResultGrid.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { Name = "Hop", HeaderText = "跳数", Width = 60 },
                new DataGridViewTextBoxColumn { Name = "Host", HeaderText = "主机", Width = 200 },
                new DataGridViewTextBoxColumn { Name = "Loss", HeaderText = "丢包率(%)", Width = 80 },
                new DataGridViewTextBoxColumn { Name = "Sent", HeaderText = "已发送", Width = 80 },
                new DataGridViewTextBoxColumn { Name = "Recv", HeaderText = "已接收", Width = 80 },
                new DataGridViewTextBoxColumn { Name = "Best", HeaderText = "最佳(ms)", Width = 80 },
                new DataGridViewTextBoxColumn { Name = "Avg", HeaderText = "平均(ms)", Width = 80 },
                new DataGridViewTextBoxColumn { Name = "Worst", HeaderText = "最差(ms)", Width = 80 },
                new DataGridViewTextBoxColumn { Name = "StDev", HeaderText = "标准差", Width = 80 }
            });

            mtrResultGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            mtrResultGrid.RowHeadersVisible = false;
            mtrResultGrid.AllowUserToAddRows = false;
            mtrResultGrid.ReadOnly = true;
        }

        private async void StartAnalysisButton_Click(object sender, EventArgs e)
        {
            if (isAnalyzing)
            {
                isAnalyzing = false;
                startAnalysisButton.Text = "开始分析";
                return;
            }

            if (string.IsNullOrWhiteSpace(targetComboBox.Text))
            {
                MessageBox.Show("请输入目标地址", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            isAnalyzing = true;
            startAnalysisButton.Text = "停止分析";
            _currentResults.Clear();
            mtrResultGrid.Rows.Clear();

            try
            {
                while (isAnalyzing)
                {
                    await PerformMTRAnalysis(targetComboBox.Text);
                    await Task.Delay(TimeSpan.FromSeconds((double)intervalUpDown.Value));
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError("MTR分析失败", ex, LogCategory.NetworkAnalysis);
                MessageBox.Show($"分析过程中出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                isAnalyzing = false;
                startAnalysisButton.Text = "开始分析";
            }
        }

        private async Task PerformMTRAnalysis(string target)
        {
            try
            {
                statusLabel.Text = $"正在分析 {target}...";
                _currentResults = await _mtrAnalyzer.AnalyzeAsync(target);
                UpdateMTRGrid(_currentResults);
                visualizationPanel.Invalidate(); // 触发重绘
                statusLabel.Text = $"分析完成: {target}";
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"分析失败: {ex.Message}";
                throw;
            }
        }

        private void MtrAnalyzer_ProgressUpdated(object sender, MTRProgressEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => MtrAnalyzer_ProgressUpdated(sender, e)));
                return;
            }

            var hopStat = _currentResults[e.CurrentHop - 1];
            UpdateMTRGridRow(hopStat);
        }

        private void UpdateMTRGrid(List<MTRHopStatistics> results)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateMTRGrid(results)));
                return;
            }

            mtrResultGrid.Rows.Clear();
            foreach (var result in results)
            {
                if (result.Responses.Count == 0 && result.FailedProbes == 0) continue;
                UpdateMTRGridRow(result);
            }
        }

        private void UpdateMTRGridRow(MTRHopStatistics stat)
        {
            var rowIndex = mtrResultGrid.Rows.Cast<DataGridViewRow>()
                .FirstOrDefault(r => r.Cells["Hop"].Value?.ToString() == stat.HopNumber.ToString())?.Index ?? -1;

            if (rowIndex == -1)
            {
                rowIndex = mtrResultGrid.Rows.Add();
            }

            var row = mtrResultGrid.Rows[rowIndex];
            row.Cells["Hop"].Value = stat.HopNumber;
            row.Cells["Host"].Value = stat.HostName ?? stat.Address?.ToString() ?? "*";
            row.Cells["Loss"].Value = stat.LossRate.ToString("F1");
            row.Cells["Sent"].Value = stat.Responses.Count + stat.FailedProbes;
            row.Cells["Recv"].Value = stat.Responses.Count;
            row.Cells["Best"].Value = stat.Responses.Count > 0 ? stat.BestTime.ToString() : "*";
            row.Cells["Avg"].Value = stat.Responses.Count > 0 ? stat.AverageTime.ToString("F1") : "*";
            row.Cells["Worst"].Value = stat.Responses.Count > 0 ? stat.WorstTime.ToString() : "*";
            row.Cells["StDev"].Value = stat.Responses.Count > 0 ? stat.StandardDeviation.ToString("F2") : "*";

            // 设置单元格颜色
            var lossRate = stat.LossRate;
            if (lossRate > 20)
            {
                row.Cells["Loss"].Style.BackColor = Color.Red;
                row.Cells["Loss"].Style.ForeColor = Color.White;
            }
            else if (lossRate > 5)
            {
                row.Cells["Loss"].Style.BackColor = Color.Yellow;
                row.Cells["Loss"].Style.ForeColor = Color.Black;
            }
            else
            {
                row.Cells["Loss"].Style.BackColor = Color.LightGreen;
                row.Cells["Loss"].Style.ForeColor = Color.Black;
            }
        }

        private void VisualizationPanel_Paint(object sender, PaintEventArgs e)
        {
            if (_currentResults.Count == 0) return;

            var graphics = e.Graphics;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var validHops = _currentResults.Where(r => r.Responses.Count > 0).ToList();
            if (validHops.Count == 0) return;

            var panelWidth = visualizationPanel.Width;
            var panelHeight = visualizationPanel.Height;
            var margin = 50;
            var nodeRadius = 20;
            var maxLatency = validHops.Max(h => h.WorstTime);

            // 计算节点位置
            var nodeSpacing = (panelWidth - 2 * margin) / (validHops.Count - 1);
            var points = new List<Point>();
            var nodes = new List<Rectangle>();

            for (int i = 0; i < validHops.Count; i++)
            {
                var hop = validHops[i];
                var x = margin + i * nodeSpacing;
                // 根据延迟计算y坐标
                var y = margin + (panelHeight - 2 * margin) * (hop.AverageTime / maxLatency);
                points.Add(new Point(x, (int)y));
                nodes.Add(new Rectangle(x - nodeRadius, (int)y - nodeRadius, 2 * nodeRadius, 2 * nodeRadius));
            }

            // 绘制连线
            using (var pen = new Pen(Color.LightGray, 2))
            {
                for (int i = 0; i < points.Count - 1; i++)
                {
                    graphics.DrawLine(pen, points[i], points[i + 1]);
                }
            }

            // 绘制节点
            for (int i = 0; i < nodes.Count; i++)
            {
                var hop = validHops[i];
                var nodeBrush = new SolidBrush(GetNodeColor(hop.LossRate));
                graphics.FillEllipse(nodeBrush, nodes[i]);
                graphics.DrawEllipse(Pens.White, nodes[i]);

                // 绘制标签
                var labelFont = new Font("Arial", 8);
                var label = $"{hop.HopNumber}\n{hop.AverageTime:F1}ms";
                var labelRect = new RectangleF(
                    nodes[i].X - nodeRadius,
                    nodes[i].Y + nodeRadius + 5,
                    2 * nodeRadius,
                    40
                );
                using (var labelBrush = new SolidBrush(Color.White))
                {
                    graphics.DrawString(label, labelFont, labelBrush, labelRect,
                        new StringFormat { Alignment = StringAlignment.Center });
                }
            }
        }

        private Color GetNodeColor(double lossRate)
        {
            if (lossRate > 20) return Color.Red;
            if (lossRate > 5) return Color.Orange;
            return Color.Green;
        }

        public void StartAnalysis(string target)
        {
            if (isAnalyzing)
            {
                MessageBox.Show("分析已在进行中", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            targetComboBox.Text = target;
            StartAnalysisButton_Click(this, EventArgs.Empty);
        }
    }
} 