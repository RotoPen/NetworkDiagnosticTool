using System;
using System.Security.Principal;
using System.Diagnostics;
using System.Windows.Forms;

namespace NetworkDiagnosticTool.Utils
{
    public static class AdminUtils
    {
        /// <summary>
        /// 检查当前程序是否以管理员权限运行
        /// </summary>
        public static bool IsRunAsAdmin()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"检查管理员权限时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 以管理员权限重启应用程序
        /// </summary>
        public static void RestartAsAdmin()
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    WorkingDirectory = Environment.CurrentDirectory,
                    FileName = Application.ExecutablePath,
                    Verb = "runas" // 请求管理员权限
                };

                Process.Start(startInfo);
                Application.Exit();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"请求管理员权限失败: {ex.Message}\n\n本程序需要管理员权限才能正常运行。", 
                    "权限错误", 
                    MessageBoxButtons.OK, 
                    MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 检查并确保程序以管理员权限运行
        /// </summary>
        /// <returns>如果已经是管理员权限返回true，否则返回false</returns>
        public static bool EnsureAdminPrivileges()
        {
            if (!IsRunAsAdmin())
            {
                var result = MessageBox.Show(
                    "本程序需要管理员权限才能执行网络诊断功能。\n是否以管理员权限重新启动程序？",
                    "需要管理员权限",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    RestartAsAdmin();
                }
                return false;
            }
            return true;
        }
    }
} 