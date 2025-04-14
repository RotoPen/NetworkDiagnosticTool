using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetworkDiagnosticTool.Logging
{
    public enum LogCategory
    {
        General,
        System,
        NetworkDiagnosis,
        NetworkRepair,
        NetworkAnalysis,
        PingTest,
        DnsTest,
        PortScan,
        NetworkTest,
        RouteTracing
    }

    public class Logger
    {
        private static readonly Lazy<Logger> instance = new(() => new Logger());
        public static Logger Instance => instance.Value;

        private readonly string logDirectory;
        private readonly Dictionary<LogCategory, string> logFiles;
        private readonly object lockObj = new();
        private readonly int _maxFileSize = 5 * 1024 * 1024; // 5MB
        private readonly int _maxFiles = 10;

        private Logger()
        {
            logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            Directory.CreateDirectory(logDirectory);
            
            // 初始化各个分类的日志文件
            logFiles = new Dictionary<LogCategory, string>();
            foreach (LogCategory category in Enum.GetValues(typeof(LogCategory)))
            {
                var categoryDir = Path.Combine(logDirectory, category.ToString());
                Directory.CreateDirectory(categoryDir);
                logFiles[category] = Path.Combine(categoryDir, $"{category}_{DateTime.Now:yyyyMMdd}.log");
            }
        }

        public void Log(string message, LogLevel level, LogCategory category = LogCategory.General)
        {
            var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
            lock (lockObj)
            {
                WriteToFile(logMessage, category);
            }
        }

        public async Task<string> ReadLogs()
        {
            if (!File.Exists(logFiles[LogCategory.General]))
            {
                return string.Empty;
            }

            try
            {
                return await File.ReadAllTextAsync(logFiles[LogCategory.General]);
            }
            catch (Exception ex)
            {
                LogError("读取日志文件失败", ex);
                throw;
            }
        }

        public async Task ClearLogs()
        {
            try
            {
                var logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                if (Directory.Exists(logDirectory))
                {
                    var logFiles = Directory.GetFiles(logDirectory, "*.log");
                    foreach (var file in logFiles)
                    {
                        File.Delete(file);
                    }
                }
                
                // 创建新的日志文件
                var currentLogFile = Path.Combine(logDirectory, $"Log_{DateTime.Now:yyyy-MM-dd}.log");
                File.Create(currentLogFile).Dispose();
                
                Log("日志已清理", LogLevel.Info, LogCategory.System);
            }
            catch (Exception ex)
            {
                Log($"清理日志失败: {ex.Message}", LogLevel.Error, LogCategory.System);
                throw;
            }
        }

        public string LogDirectory => logDirectory;

        public void LogError(string message, Exception? ex = null, LogCategory category = LogCategory.General)
        {
            var errorMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [错误] {message}";
            if (ex != null)
            {
                errorMessage += $"\n异常: {ex.Message}\n堆栈跟踪: {ex.StackTrace}";
            }
            Log(errorMessage, LogLevel.Error, category);
        }

        private void WriteToFile(string message, LogCategory category)
        {
            try
            {
                var logFile = logFiles[category];
                lock (lockObj)
                {
                    // 检查文件大小
                    if (File.Exists(logFile) && new FileInfo(logFile).Length > _maxFileSize)
                    {
                        RotateLogFiles(category);
                    }

                    File.AppendAllText(logFile, message + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                // 如果日志写入失败，尝试写入错误日志
                try
                {
                    var errorLog = Path.Combine(logDirectory, "error.log");
                    File.AppendAllText(errorLog, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 日志写入失败: {ex.Message}{Environment.NewLine}");
                }
                catch
                {
                    // 如果错误日志也写入失败，则忽略
                }
            }
        }

        private void RotateLogFiles(LogCategory category)
        {
            var categoryDir = Path.Combine(logDirectory, category.ToString());
            var files = Directory.GetFiles(categoryDir, $"{category}_*.log")
                               .OrderByDescending(f => f)
                               .ToList();

            // 删除最旧的文件
            while (files.Count >= _maxFiles)
            {
                File.Delete(files.Last());
                files.RemoveAt(files.Count - 1);
            }

            // 重命名现有文件
            for (int i = files.Count - 1; i >= 0; i--)
            {
                var newName = Path.Combine(categoryDir, $"{category}_{i + 1}.log");
                File.Move(files[i], newName);
            }
        }

        public IEnumerable<(string FilePath, LogCategory Category)> GetLogFiles()
        {
            var allFiles = new List<(string, LogCategory)>();
            foreach (LogCategory category in Enum.GetValues(typeof(LogCategory)))
            {
                var categoryDir = Path.Combine(logDirectory, category.ToString());
                if (Directory.Exists(categoryDir))
                {
                    var files = Directory.GetFiles(categoryDir, $"{category}_*.log")
                                       .OrderByDescending(f => f)
                                       .Select(f => (f, category));
                    allFiles.AddRange(files);
                }
            }
            return allFiles;
        }

        public void CleanLogs(int daysToKeep)
        {
            lock (lockObj)
            {
                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
                foreach (LogCategory category in Enum.GetValues(typeof(LogCategory)))
                {
                    var categoryDir = Path.Combine(logDirectory, category.ToString());
                    if (Directory.Exists(categoryDir))
                    {
                        var files = Directory.GetFiles(categoryDir, $"{category}_*.log");
                        foreach (var file in files)
                        {
                            var fileInfo = new FileInfo(file);
                            if (fileInfo.LastWriteTime < cutoffDate)
                            {
                                File.Delete(file);
                            }
                        }
                    }
                }
            }
        }

        public async Task<string> ReadLogFileAsync(string filePath)
        {
            return await File.ReadAllTextAsync(filePath);
        }

        public async Task ExportLogsAsync(string targetDirectory)
        {
            foreach (var (filePath, category) in GetLogFiles())
            {
                var categoryDir = Path.Combine(targetDirectory, category.ToString());
                Directory.CreateDirectory(categoryDir);
                var fileName = Path.GetFileName(filePath);
                var targetPath = Path.Combine(categoryDir, fileName);
                File.Copy(filePath, targetPath, true);
            }
            await Task.CompletedTask;
        }
    }

    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Critical
    }
} 