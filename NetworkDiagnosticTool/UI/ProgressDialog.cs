using System;
using System.Drawing;
using System.Windows.Forms;

namespace NetworkDiagnosticTool.UI
{
    public class ProgressDialog : Form
    {
        private readonly Label titleLabel;
        private readonly Label messageLabel;
        private readonly ProgressBar progressBar;

        public ProgressDialog(string title, string message)
        {
            // 设置窗体属性
            Text = title;
            Size = new Size(400, 150);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(30, 30, 30);
            ForeColor = Color.White;

            // 创建标题标签
            titleLabel = new Label
            {
                Text = title,
                AutoSize = true,
                Location = new Point(20, 20),
                Font = new Font(Font.FontFamily, 12, FontStyle.Bold),
                ForeColor = Color.White
            };

            // 创建消息标签
            messageLabel = new Label
            {
                Text = message,
                AutoSize = true,
                Location = new Point(20, 50),
                ForeColor = Color.White
            };

            // 创建进度条
            progressBar = new ProgressBar
            {
                Location = new Point(20, 80),
                Size = new Size(350, 23),
                Style = ProgressBarStyle.Continuous,
                Value = 0,
                Maximum = 100
            };

            // 添加控件到窗体
            Controls.Add(titleLabel);
            Controls.Add(messageLabel);
            Controls.Add(progressBar);
        }

        public void SetProgress(int current, int total)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => SetProgress(current, total)));
                return;
            }

            int percentage = (int)((float)current / total * 100);
            progressBar.Value = Math.Min(Math.Max(percentage, 0), 100);
            messageLabel.Text = $"进度: {current}/{total} ({percentage}%)";
        }

        public void SetMessage(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => SetMessage(message)));
                return;
            }

            messageLabel.Text = message;
        }
    }
} 