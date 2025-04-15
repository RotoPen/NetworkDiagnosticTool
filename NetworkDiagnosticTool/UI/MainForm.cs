using System;
using System.Drawing;
using System.Windows.Forms;
using NetworkDiagnosticTool.Logging;
using NetworkDiagnosticTool.UI;

namespace NetworkDiagnosticTool
{
    public partial class MainForm : Form
    {
        private readonly TabControl mainTabControl = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.White,
            Padding = new Point(12, 4),
            ItemSize = new Size(120, 30),
            SizeMode = TabSizeMode.Fixed,
            Appearance = TabAppearance.Normal
        };

        private readonly FlowLayoutPanel flowLayoutPanel = new()
        {
            Dock = DockStyle.Bottom,
            Height = 0,
            BackColor = Color.FromArgb(35, 35, 35),
            Padding = new Padding(10),
            FlowDirection = FlowDirection.LeftToRight,
            Visible = false
        };

        public MainForm()
        {
            InitializeComponent();
            SetupMainForm();
            Logger.Instance.Log("主窗口已启动", LogLevel.Info);
        }

        private void SetupMainForm()
        {
            Text = "网络故障诊断与修复工具";
            Size = new Size(1200, 800);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(30, 30, 30);
            ForeColor = Color.White;

            mainTabControl.Controls.Add(CreateTabPage<NetworkDiagnosisForm>("网络诊断"));
            mainTabControl.Controls.Add(CreateTabPage<NetworkRepairForm>("网络修复"));
            mainTabControl.Controls.Add(CreateTabPage<NetworkTestForm>("网络测试"));
            mainTabControl.Controls.Add(CreateTabPage<DnsDiagnosisForm>("DNS诊断"));
            mainTabControl.Controls.Add(CreateTabPage<PingTestForm>("Ping测试"));
            mainTabControl.Controls.Add(CreateTabPage<PortScanForm>("端口扫描"));
            mainTabControl.Controls.Add(CreateTabPage<RouteTracingForm>("路由追踪"));
            mainTabControl.Controls.Add(CreateTabPage<NetworkConfigBackupForm>("配置备份"));
            mainTabControl.Controls.Add(CreateTabPage<LogViewerForm>("日志查看"));

            Controls.Add(mainTabControl);
        }

        private TabPage CreateTabPage<T>(string title) where T : Form, new()
        {
            var tabPage = new TabPage(title)
            {
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(5),
                UseVisualStyleBackColor = false
            };
            
            var form = new T
            {
                TopLevel = false,
                Dock = DockStyle.Fill,
                FormBorderStyle = FormBorderStyle.None
            };
            form.Show();
            tabPage.Controls.Add(form);
            
            return tabPage;
        }

        private void NetworkVisualizationButton_Click(object sender, EventArgs e)
        {
            var visualizationForm = new NetworkVisualizationForm();
            visualizationForm.Show();
        }
    }
} 