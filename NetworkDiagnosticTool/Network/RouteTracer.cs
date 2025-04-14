using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using NetworkDiagnosticTool.Models;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net;
using System.Net.Sockets;
using NetworkDiagnosticTool.Utils;
using NetworkDiagnosticTool.Logging;
using System.Net.Http;
using System.Text.Json;
using System.IO;
using System.Collections.Concurrent;

namespace NetworkDiagnosticTool.Network
{
    public class RouteTracer : IDisposable
    {
        private readonly string targetHost;
        private bool isStopped = false;
        private readonly List<string> dnsServers = new();
        public event EventHandler<RouteHop>? HopDiscovered;
        public event EventHandler<string>? StatusUpdated;
        private const int TimeoutMilliseconds = 1000; // 降低超时时间，从3000改为1000ms
        private const int MaxHops = 30;
        private const int PingsPerHop = 3;
        private static readonly HttpClient httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
        private bool useFallbackMethod = false;
        private CancellationToken _cancellationToken;

        // 添加IP位置信息缓存
        private static ConcurrentDictionary<string, string> _locationCache = new ConcurrentDictionary<string, string>();
        
        // 添加位置信息获取队列
        private ConcurrentQueue<RouteHop> _pendingLocationHops = new ConcurrentQueue<RouteHop>();
        private bool _isProcessingLocationQueue = false;

        public RouteTracer(string host)
        {
            targetHost = host;
            LoadDnsServers();
        }

        public async Task<bool> StartTrace(CancellationToken cancellationToken = default)
        {
            try
            {
                _cancellationToken = cancellationToken;
                isStopped = false;
                useFallbackMethod = false; // 重置标志，确保每次开始追踪时都使用主方法
                Logger.Instance.Log($"开始追踪: {targetHost}", LogLevel.Info, LogCategory.NetworkDiagnosis);
                StatusUpdated?.Invoke(this, $"正在解析主机: {targetHost}...");
                
                // 确保在后台线程执行以避免阻塞UI
                await Task.Yield();
                
                // 注册取消令牌
                if (cancellationToken.CanBeCanceled)
                {
                    cancellationToken.Register(() => 
                    {
                        isStopped = true;
                        Logger.Instance.Log("收到取消令牌请求，立即停止追踪", LogLevel.Info, LogCategory.NetworkDiagnosis);
                        StatusUpdated?.Invoke(this, "收到取消请求，正在停止追踪...");
                    });
                }
                
                // 检查是否已取消
                if (cancellationToken.IsCancellationRequested || isStopped)
                {
                    Logger.Instance.Log("追踪已取消", LogLevel.Info, LogCategory.NetworkDiagnosis);
                    return false;
                }
                
                // 首先尝试使用 Ping 方法追踪
                bool result = await TraceRouteWithPing();
                
                // 再次检查是否已取消
                if (cancellationToken.IsCancellationRequested || isStopped)
                {
                    Logger.Instance.Log("追踪已取消", LogLevel.Info, LogCategory.NetworkDiagnosis);
                    return false;
                }
                
                // 如果被标记为需要使用系统命令，且第一次追踪未成功
                if (useFallbackMethod && !result && !isStopped && !_cancellationToken.IsCancellationRequested)
                {
                    StatusUpdated?.Invoke(this, $"使用系统命令追踪...");
                    Logger.Instance.Log("使用系统命令追踪", LogLevel.Info, LogCategory.NetworkDiagnosis);
                    result = await TraceRouteWithSystemCommand();
                }
                
                return result;
            }
            catch (OperationCanceledException)
            {
                Logger.Instance.Log("追踪被取消", LogLevel.Info, LogCategory.NetworkDiagnosis);
                StatusUpdated?.Invoke(this, "追踪已取消");
                return false;
            }
            catch (Exception ex)
            {
                OnHopDiscovered(new RouteHop { HopNumber = 0, IpAddress = "ERROR", HostName = ex.Message });
                StatusUpdated?.Invoke(this, $"错误: {ex.Message}");
                Logger.Instance.LogError("路由追踪失败", ex, LogCategory.NetworkDiagnosis);
                return false;
            }
        }
        
        private async Task<bool> TraceRouteWithPing()
        {
            bool reachedTarget = false;
            int hopCount = 0;
            List<RouteHop> allHops = new List<RouteHop>();
            
            try
            {
                // 检查是否已经停止
                if (isStopped || _cancellationToken.IsCancellationRequested)
                {
                    Logger.Instance.Log("追踪已取消 (Ping)", LogLevel.Info, LogCategory.NetworkDiagnosis);
                    return false;
                }
                
                IPAddress targetIP;
                try
                {
                    // 在后台线程中解析主机名，避免阻塞 UI
                    await Task.Yield(); // 确保切换到后台线程
                    
                    // 检查是否已取消
                    if (_cancellationToken.IsCancellationRequested || isStopped)
                    {
                        Logger.Instance.Log("追踪已取消", LogLevel.Info, LogCategory.NetworkDiagnosis);
                        return false;
                    }
                    
                    StatusUpdated?.Invoke(this, $"解析主机名: {targetHost}...");
                    
                    // 尝试解析目标IP地址
                    if (!IPAddress.TryParse(targetHost, out targetIP))
                    {
                        Logger.Instance.Log($"解析域名: {targetHost}", LogLevel.Info, LogCategory.NetworkDiagnosis);
                        var hostEntry = await Dns.GetHostEntryAsync(targetHost);
                        if (hostEntry.AddressList.Length == 0)
                        {
                            StatusUpdated?.Invoke(this, $"无法解析主机: {targetHost}");
                            HopDiscovered?.Invoke(this, new RouteHop 
                            { 
                                HopNumber = 0, 
                                IpAddress = "ERROR", 
                                HostName = $"无法解析主机: {targetHost}" 
                            });
                            return false;
                        }
                        targetIP = hostEntry.AddressList[0];
                        StatusUpdated?.Invoke(this, $"域名解析成功: {targetIP}");
                        Logger.Instance.Log($"域名解析为IP: {targetIP}", LogLevel.Info, LogCategory.NetworkDiagnosis);
                    }
                }
                catch (Exception ex)
                {
                    StatusUpdated?.Invoke(this, $"DNS解析错误: {ex.Message}");
                    Logger.Instance.LogError($"DNS解析错误", ex, LogCategory.NetworkDiagnosis);
                    HopDiscovered?.Invoke(this, new RouteHop 
                    { 
                        HopNumber = 0, 
                        IpAddress = "ERROR", 
                        HostName = $"DNS解析错误: {ex.Message}" 
                    });
                    return false;
                }

                // 记录DNS解析时间
                Stopwatch dnsStopwatch = new Stopwatch();
                dnsStopwatch.Start();
                // 测试与目标的连通性
                using (var pingTest = new Ping())
                {
                    var pingReply = await pingTest.SendPingAsync(targetIP, TimeoutMilliseconds);
                    dnsStopwatch.Stop();
                    long dnsTime = dnsStopwatch.ElapsedMilliseconds;
                    Logger.Instance.Log($"DNS解析完成，耗时: {dnsTime}ms", LogLevel.Info, LogCategory.NetworkDiagnosis);
                }

                // 准备ping数据
                byte[] buffer = Encoding.ASCII.GetBytes("NetworkDiagnosticTracerouteData");
                string targetIPString = targetIP.ToString();

                // 执行路由追踪
                for (int ttl = 1; ttl <= MaxHops; ttl++)
                {
                    // 在开始每一跳前检查是否请求停止
                    if (isStopped || _cancellationToken.IsCancellationRequested)
                    {
                        StatusUpdated?.Invoke(this, "追踪已停止");
                        Logger.Instance.Log("追踪中断 (用户取消)", LogLevel.Info, LogCategory.NetworkDiagnosis);
                        return reachedTarget;
                    }

                    var hop = new RouteHop { HopNumber = ttl };
                    IPAddress? hopAddress = null;
                    int successfulPings = 0;
                    
                    StatusUpdated?.Invoke(this, $"追踪跳点 #{ttl}...");

                    for (int i = 0; i < PingsPerHop; i++)
                    {
                        // 每次ping前检查取消状态
                        if (isStopped || _cancellationToken.IsCancellationRequested) 
                        {
                            Logger.Instance.Log($"追踪在TTL={ttl}处被取消", LogLevel.Info, LogCategory.NetworkDiagnosis);
                            return reachedTarget;
                        }

                        try
                        {
                            // 确保每次都创建新的Ping实例
                            using (var ping = new Ping())
                            {
                                var options = new PingOptions(ttl, true);
                                
                                StatusUpdated?.Invoke(this, $"向 {targetIP} 发送 Ping #{i+1} (TTL={ttl})...");
                                
                                // 记录开始时间以精确计算延迟
                                var stopwatch = Stopwatch.StartNew();
                                
                                // 添加取消令牌支持
                                var pingTask = ping.SendPingAsync(targetIP, TimeoutMilliseconds, buffer, options);
                                
                                // 创建一个更短的超时任务，使得停止按钮更快响应
                                var timeoutTask = Task.Delay(TimeoutMilliseconds / 2, _cancellationToken);
                                
                                // 等待Ping结果、超时或取消 - 任何一个先完成就继续
                                var completedTask = await Task.WhenAny(pingTask, timeoutTask);
                                
                                // 再次立即检查是否已取消或停止
                                if (isStopped || _cancellationToken.IsCancellationRequested)
                                {
                                    Logger.Instance.Log("Ping操作期间收到取消请求", LogLevel.Info, LogCategory.NetworkDiagnosis);
                                    return reachedTarget;
                                }
                                
                                // 只在任务完成的情况下处理结果
                                if (pingTask.IsCompleted)
                                {
                                    var reply = await pingTask;
                                    stopwatch.Stop();
                                    
                                    // 获取往返时间
                                    long elapsed = stopwatch.ElapsedMilliseconds;
                                    Logger.Instance.Log($"Hop {ttl}: Ping returned with RTT: {elapsed}ms", LogLevel.Debug, LogCategory.RouteTracing);

                                    if (reply.Status == IPStatus.Success || reply.Status == IPStatus.TtlExpired)
                                    {
                                        if (elapsed == 0) elapsed = 1; // 避免显示0ms
                                        successfulPings++;
                                        hopAddress = reply.Address;
                                        
                                        if (reply.Status == IPStatus.Success)
                                        {
                                            reachedTarget = true;
                                            StatusUpdated?.Invoke(this, $"到达目标: {reply.Address}, 延迟={elapsed}ms");
                                        }
                                        else
                                        {
                                            StatusUpdated?.Invoke(this, $"跳点 #{ttl}: {reply.Address}, 延迟={elapsed}ms");
                                        }
                                    }
                                    else
                                    {
                                        elapsed = -1;
                                        StatusUpdated?.Invoke(this, $"Ping失败: {reply.Status}");
                                        Logger.Instance.Log($"Ping失败: {reply.Status}", LogLevel.Debug, LogCategory.NetworkDiagnosis);
                                    }

                                    hop.DelayTimes.Add((int)elapsed);
                                }
                                else
                                {
                                    // 如果任务未完成，可能是超时或被取消
                                    hop.DelayTimes.Add(-1);
                                    Logger.Instance.Log($"Ping请求超时或被取消 (TTL={ttl})", LogLevel.Debug, LogCategory.NetworkDiagnosis);
                                }
                            }
                        }
                        catch (PingException ex)
                        {
                            StatusUpdated?.Invoke(this, $"Ping异常: {ex.Message}");
                            Logger.Instance.Log($"Ping异常: {ex.Message}", LogLevel.Debug, LogCategory.NetworkDiagnosis);
                            hop.DelayTimes.Add(-1);
                        }

                        // 在ping之间添加短暂延迟，同时提供UI响应
                        if (i < PingsPerHop - 1 && !isStopped && !_cancellationToken.IsCancellationRequested)
                        {
                            try
                            {
                                // 减少延迟以提高响应性
                                await Task.Delay(15, _cancellationToken); 
                            }
                            catch (OperationCanceledException)
                            {
                                Logger.Instance.Log("Ping间隔期间收到取消请求", LogLevel.Info, LogCategory.NetworkDiagnosis);
                                return reachedTarget;
                            }
                        }
                    }

                    // 确保有3个延迟值
                    while (hop.DelayTimes.Count < PingsPerHop)
                    {
                        hop.DelayTimes.Add(-1);
                    }

                    // 处理IP地址和主机名
                    if (hopAddress != null)
                    {
                        hop.IpAddress = hopAddress.ToString();
                        hopCount++;

                        // 只执行主机名解析，不在这里获取位置信息
                        try
                        {
                            // 获取主机名
                            var hostEntryTask = Dns.GetHostEntryAsync(hopAddress);
                            
                            // 添加超时控制
                            var timeoutTask = Task.Delay(1000);
                            var completedTask = await Task.WhenAny(hostEntryTask, timeoutTask);

                            if (completedTask != timeoutTask && hostEntryTask.IsCompletedSuccessfully)
                            {
                                hop.HostName = hostEntryTask.Result.HostName;
                            }
                            else
                            {
                                hop.HostName = hopAddress.ToString();
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Instance.Log($"解析主机名出错: {ex.Message}", LogLevel.Debug, LogCategory.NetworkDiagnosis);
                            hop.HostName = hopAddress.ToString();
                        }

                        // 如果是公网IP，将跳点加入位置信息获取队列
                        if (successfulPings > 0 && hopAddress != null)
                        {
                            string ipString = hopAddress.ToString();
                            
                            // 如果是内网IP，直接标记
                            if (!IsPublicIP(ipString))
                            {
                                hop.Location = "内网";
                                Logger.Instance.Log($"识别为内网IP: {ipString}", LogLevel.Debug, LogCategory.NetworkDiagnosis);
                            }
                            // 如果是公网IP，先检查缓存
                            else if (_locationCache.TryGetValue(ipString, out string cachedLocation))
                            {
                                hop.Location = "[缓存] " + cachedLocation;
                                Logger.Instance.Log($"从缓存获取位置信息: {ipString} -> {cachedLocation}", LogLevel.Debug, LogCategory.NetworkDiagnosis);
                            }
                            else
                            {
                                // 加入位置获取队列
                                _pendingLocationHops.Enqueue(hop);
                                
                                // 如果队列处理尚未开始，启动它
                                if (!_isProcessingLocationQueue)
                                {
                                    _ = ProcessLocationQueue();
                                }
                            }
                        }
                        
                        // 将跳点添加到列表中，以便后续并行处理
                        allHops.Add(hop);
                    }
                    else
                    {
                        hop.IpAddress = "请求超时";
                        hop.HostName = "请求超时";
                        allHops.Add(hop);
                    }

                    // 通知UI更新
                    HopDiscovered?.Invoke(this, hop);

                    // 如果到达目标，结束追踪
                    if (reachedTarget)
                    {
                        break;
                    }

                    // 在跳数之间添加短暂延迟
                    if (ttl < MaxHops && !isStopped && !_cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            // 减少延迟以提高响应性
                            await Task.Delay(15, _cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            Logger.Instance.Log("跳数间隔期间收到取消请求", LogLevel.Info, LogCategory.NetworkDiagnosis);
                            return reachedTarget;
                        }
                    }
                }
                
                // 如果只有一跳或没有跳，且目标IP是公网，则使用备选方法
                if ((hopCount <= 1) && targetIP != null && IsPublicIP(targetIP.ToString()))
                {
                    Logger.Instance.Log("只探测到一跳或没有跳且目标是公网IP，开始使用备选方法", LogLevel.Info, LogCategory.NetworkDiagnosis);
                    useFallbackMethod = true;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError("使用Ping进行路由追踪失败", ex, LogCategory.NetworkDiagnosis);
                throw;
            }

            return reachedTarget;
        }

        private async Task<bool> TraceRouteWithSystemCommand()
        {
            bool reachedTarget = false;
            
            try
            {
                using (var process = new Process())
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = "tracert",
                        Arguments = $"-d -w {TimeoutMilliseconds} {targetHost}", // -d 参数不解析主机名，加快速度
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.Default
                    };

                    process.StartInfo = startInfo;
                    Logger.Instance.Log($"执行命令: tracert -d -w {TimeoutMilliseconds} {targetHost}", 
                        LogLevel.Debug, LogCategory.NetworkDiagnosis);
                    
                    // 创建一个集合来存储所有输出行
                    var outputLines = new List<string>();
                    
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            Logger.Instance.Log($"TracertOutput: {e.Data}", LogLevel.Debug, LogCategory.NetworkDiagnosis);
                            outputLines.Add(e.Data);
                        }
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            Logger.Instance.Log($"TracertError: {e.Data}", LogLevel.Error, LogCategory.NetworkDiagnosis);
                        }
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    // 等待进程完成或超时
                    bool exited = process.WaitForExit(TimeoutMilliseconds * MaxHops);
                    if (!exited)
                    {
                        Logger.Instance.Log("Tracert命令超时", LogLevel.Warning, LogCategory.NetworkDiagnosis);
                        process.Kill();
                    }
                    
                    // 处理收集到的输出行
                    int hopNumber = 0;
                    foreach (var line in outputLines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        
                        // 忽略标题行
                        if (line.Contains("跟踪") || line.Contains("Tracing") || 
                            line.Contains("超过") || line.Contains("over") ||
                            line.Contains("结果") || line.Contains("complete"))
                            continue;
                        
                        // 尝试解析跳数行
                        var hopMatch = Regex.Match(line, @"^\s*(\d+)");
                        if (hopMatch.Success)
                        {
                            hopNumber = int.Parse(hopMatch.Groups[1].Value);
                            var hop = new RouteHop { HopNumber = hopNumber };
                            
                            // 解析延迟时间
                            var delays = new List<int>();
                            var delayMatches = Regex.Matches(line, @"(<?\d+)\s*ms|(\*)");
                            foreach (Match m in delayMatches)
                            {
                                if (m.Groups[1].Success) // 数字 ms
                                {
                                    string value = m.Groups[1].Value;
                                    if (value.StartsWith("<"))
                                        delays.Add(1);
                                    else
                                        delays.Add(int.Parse(value));
                                }
                                else if (m.Groups[2].Success) // *
                                {
                                    delays.Add(-1);
                                }
                            }
                            
                            // 确保有3个延迟值
                            while (delays.Count < PingsPerHop)
                            {
                                delays.Add(-1);
                            }
                            hop.DelayTimes.AddRange(delays);
                            
                            // 解析IP地址
                            var ipMatch = Regex.Match(line, @"(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})");
                            if (ipMatch.Success)
                            {
                                string ip = ipMatch.Groups[1].Value;
                                hop.IpAddress = ip;
                                
                                // 尝试解析主机名和位置信息
                                try
                                {
                                    var ipAddress = IPAddress.Parse(ip);
                                    
                                    // 判断是否为内网IP
                                    if (!IsPublicIP(ip))
                                    {
                                        hop.Location = "内网";
                                    }
                                    else
                                    {
                                        hop.Location = await GetLocationInfo(ipAddress);
                                    }
                                    
                                    try
                                    {
                                        var hostEntry = await Dns.GetHostEntryAsync(ipAddress);
                                        hop.HostName = hostEntry.HostName;
                                    }
                                    catch
                                    {
                                        hop.HostName = ip;
                                    }
                                }
                                catch
                                {
                                    hop.HostName = ip;
                                }
                                
                                // 如果达到目标IP，标记为成功
                                if (IPAddress.TryParse(targetHost, out IPAddress targetIPObj) && IPAddress.Parse(ip).Equals(targetIPObj))
                                {
                                    reachedTarget = true;
                                }
                            }
                            else
                            {
                                hop.IpAddress = "请求超时";
                                hop.HostName = "请求超时";
                            }
                            
                            // 通知UI更新
                            HopDiscovered?.Invoke(this, hop);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError("使用系统命令进行路由追踪失败", ex, LogCategory.NetworkDiagnosis);
                throw;
            }
            
            return reachedTarget;
        }
        
        private bool IsPublicIP(string ipString)
        {
            if (IPAddress.TryParse(ipString, out IPAddress ip))
            {
                // 检查是否是公网IP
                byte[] bytes = ip.GetAddressBytes();
                
                // 检查是否是私有IP范围
                // 10.0.0.0/8
                if (bytes[0] == 10)
                    return false;
                    
                // 172.16.0.0/12
                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                    return false;
                    
                // 192.168.0.0/16
                if (bytes[0] == 192 && bytes[1] == 168)
                    return false;
                    
                // 127.0.0.0/8 (回环地址)
                if (bytes[0] == 127)
                    return false;
                    
                return true;
            }
            return false;
        }

        private async Task<string> GetLocationInfo(IPAddress ipAddress)
        {
            // 先检查缓存
            string ipString = ipAddress.ToString();
            if (_locationCache.TryGetValue(ipString, out string cachedLocation))
            {
                return "[缓存] " + cachedLocation;
            }

            try
            {
                // 使用 ip-api.com 的免费API获取位置信息
                var url = $"http://ip-api.com/json/{ipAddress}?lang=zh-CN&fields=country,regionName,city";
                
                // 添加取消令牌支持
                var response = await httpClient.GetStringAsync(url, _cancellationToken);
                
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                var location = new List<string>();
                if (root.TryGetProperty("country", out var country) && !string.IsNullOrEmpty(country.GetString()))
                {
                    location.Add(country.GetString());
                }
                if (root.TryGetProperty("regionName", out var region) && !string.IsNullOrEmpty(region.GetString()))
                {
                    location.Add(region.GetString());
                }
                if (root.TryGetProperty("city", out var city) && !string.IsNullOrEmpty(city.GetString()))
                {
                    location.Add(city.GetString());
                }

                string result = string.Join(" ", location);
                
                // 添加到缓存
                if (!string.IsNullOrEmpty(result))
                {
                    _locationCache.TryAdd(ipString, result);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                // 只记录警告级别，减少错误日志
                Logger.Instance.Log($"获取位置信息时出错: {ex.Message}", LogLevel.Warning, LogCategory.NetworkDiagnosis);
                return string.Empty;
            }
        }

        public void Stop()
        {
            try
            {
                if (isStopped) return; // 防止重复调用
                
                isStopped = true;
                Logger.Instance.Log("停止路由追踪 (用户手动停止)", LogLevel.Info, LogCategory.NetworkDiagnosis);
                
                // 通知UI状态更新
                StatusUpdated?.Invoke(this, "用户已停止追踪操作");
                
                // 清理所有潜在的挂起操作
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Stop Trace Exception: {ex.Message}");
                Logger.Instance.LogError("停止路由追踪出错", ex, LogCategory.NetworkDiagnosis);
            }
        }

        private void OnHopDiscovered(RouteHop hop)
        {
            HopDiscovered?.Invoke(this, hop);
        }

        private async Task<IPAddress?> ResolveHostname(string hostname)
        {
            try
            {
                var hostEntry = await Dns.GetHostEntryAsync(hostname);
                if (hostEntry.AddressList.Length > 0)
                {
                    return hostEntry.AddressList[0];
                }
                return null;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"解析主机名时出错: {ex.Message}", ex, LogCategory.NetworkDiagnosis);
                return null;
            }
        }

        private async Task<string> GetHostName(IPAddress ipAddress)
        {
            try
            {
                var hostEntry = await Dns.GetHostEntryAsync(ipAddress);
                return hostEntry.HostName;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"获取主机名时出错: {ex.Message}", ex, LogCategory.NetworkDiagnosis);
                return ipAddress.ToString();
            }
        }

        private async Task<string> GetIpLocation(string ipAddress)
        {
            try
            {
                var response = await httpClient.GetStringAsync($"http://ip-api.com/json/{ipAddress}?lang=zh-CN&fields=country,regionName,city");
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                var location = new List<string>();
                if (root.TryGetProperty("country", out var country) && !string.IsNullOrEmpty(country.GetString()))
                {
                    location.Add(country.GetString());
                }
                if (root.TryGetProperty("regionName", out var region) && !string.IsNullOrEmpty(region.GetString()))
                {
                    location.Add(region.GetString());
                }
                if (root.TryGetProperty("city", out var city) && !string.IsNullOrEmpty(city.GetString()))
                {
                    location.Add(city.GetString());
                }

                return string.Join(" ", location);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"获取IP位置信息时出错: {ex.Message}", ex, LogCategory.NetworkDiagnosis);
                return "位置信息获取失败";
            }
        }

        private void LoadDnsServers()
        {
            // 实现加载DNS服务器列表的逻辑
        }

        // 添加位置信息队列处理方法
        private async Task ProcessLocationQueue()
        {
            try
            {
                _isProcessingLocationQueue = true;
                Logger.Instance.Log("开始处理位置信息队列", LogLevel.Debug, LogCategory.NetworkDiagnosis);
                
                // 持续处理队列中的项目
                while (!_cancellationToken.IsCancellationRequested && !isStopped && _pendingLocationHops.TryDequeue(out RouteHop hop))
                {
                    try
                    {
                        string ipString = hop.IpAddress;
                        if (string.IsNullOrEmpty(ipString) || ipString == "请求超时") continue;
                        
                        // 再次检查缓存，因为在队列等待期间可能已被其他请求缓存
                        if (!IsPublicIP(ipString))
                        {
                            hop.Location = "内网";
                            Logger.Instance.Log($"识别为内网IP: {ipString}", LogLevel.Debug, LogCategory.NetworkDiagnosis);
                        }
                        else if (_locationCache.TryGetValue(ipString, out string cachedLocation))
                        {
                            hop.Location = "[缓存] " + cachedLocation;
                            Logger.Instance.Log($"从缓存获取位置信息: {ipString} -> {cachedLocation}", LogLevel.Debug, LogCategory.NetworkDiagnosis);
                        }
                        else
                        {
                            // 获取位置信息
                            IPAddress ipAddress = IPAddress.Parse(ipString);
                            string location = await GetLocationInfo(ipAddress);
                            
                            // 更新跳点位置并缓存结果
                            hop.Location = location;
                            _locationCache.TryAdd(ipString, location);
                            
                            // 通知UI更新
                            HopLocationUpdated?.Invoke(this, new RouteHopUpdateEventArgs(hop));
                            
                            // 小延迟避免API请求过于频繁
                            await Task.Delay(100, _cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Log($"处理位置信息出错: {ex.Message}", LogLevel.Error, LogCategory.NetworkDiagnosis);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Instance.Log("位置信息处理已取消", LogLevel.Info, LogCategory.NetworkDiagnosis);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"位置信息队列处理出错: {ex.Message}", LogLevel.Error, LogCategory.NetworkDiagnosis);
            }
            finally
            {
                _isProcessingLocationQueue = false;
            }
        }
        
        // 添加位置信息更新事件
        public event EventHandler<RouteHopUpdateEventArgs>? HopLocationUpdated;

        public void Dispose()
        {
            // 清空队列
            while (_pendingLocationHops.TryDequeue(out _)) { }
        }
    }
    
    // 添加位置信息更新事件参数类
    public class RouteHopUpdateEventArgs : EventArgs
    {
        public RouteHop Hop { get; }
        
        public RouteHopUpdateEventArgs(RouteHop hop)
        {
            Hop = hop;
        }
    }
} 