using System;
using System.Windows.Forms;
using System.Text;
using NetworkDiagnosticTool.UI;
using NetworkDiagnosticTool.Utils;

namespace NetworkDiagnosticTool
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // 注册编码提供程序
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 检查管理员权限
            if (!AdminUtils.EnsureAdminPrivileges())
            {
                return; // 如果用户选择不提升权限，直接退出
            }

            Application.Run(new MainForm());
        }
    }
}
