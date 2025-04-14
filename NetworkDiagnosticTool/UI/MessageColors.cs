using System.Drawing;

namespace NetworkDiagnosticTool.UI
{
    public static class MessageColors
    {
        // 操作开始和重要信息
        public static readonly Color Start = Color.FromArgb(0, 255, 255);  // 青色
        public static readonly Color StartBold = Color.FromArgb(0, 255, 255);  // 青色

        // 进行中的操作
        public static readonly Color Progress = Color.FromArgb(0, 122, 204);  // 深蓝色

        // 成功完成
        public static readonly Color Success = Color.FromArgb(0, 255, 0);  // 高饱和绿色
        public static readonly Color SuccessBold = Color.FromArgb(0, 255, 0);  // 高饱和绿色

        // 警告和取消
        public static readonly Color Warning = Color.FromArgb(255, 255, 0);  // 黄色

        // 错误信息
        public static readonly Color Error = Color.FromArgb(255, 0, 0);  // 红色

        // 普通输出
        public static readonly Color Normal = Color.FromArgb(255, 255, 255);  // 白色

        // 命令输出
        public static readonly Color Command = Color.FromArgb(200, 200, 200);  // 浅灰色
    }
} 