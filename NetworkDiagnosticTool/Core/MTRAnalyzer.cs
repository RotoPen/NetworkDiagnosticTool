using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Linq;
using System.Net;
using System.Threading;
using NetworkDiagnosticTool.Logging;

namespace NetworkDiagnosticTool.Core
{
    public class MTRAnalyzer
    {
        private const int DefaultMaxHops = 30;
        private const int DefaultTimeout = 1000; // 毫秒
        private const int PacketsPerHop = 10;
        
        private readonly CancellationTokenSource _cts = new();
        private readonly List<MTRHopStatistics> _hopStats = new();
        
        public event EventHandler<MTRProgressEventArgs> ProgressUpdated;
        
        public async Task<List<MTRHopStatistics>> AnalyzeAsync(string target, int maxHops = DefaultMaxHops)
        {
            try
            {
                _hopStats.Clear();
                var targetIP = await ResolveTargetAsync(target);
                
                // 初始化统计数据
                for (int hop = 1; hop <= maxHops; hop++)
                {
                    _hopStats.Add(new MTRHopStatistics { HopNumber = hop });
                }
                
                // 并行执行多个探测任务
                var tasks = new List<Task>();
                for (int i = 0; i < PacketsPerHop; i++)
                {
                    tasks.Add(ProbeAllHopsAsync(targetIP, maxHops));
                    await Task.Delay(100); // 间隔发送，避免触发防火墙
                }
                
                await Task.WhenAll(tasks);
                
                // 计算最终统计结果
                foreach (var stat in _hopStats)
                {
                    if (stat.Responses.Count > 0)
                    {
                        stat.CalculateStatistics();
                    }
                }
                
                return _hopStats;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError("MTR分析失败", ex, LogCategory.NetworkAnalysis);
                throw;
            }
        }
        
        private async Task<IPAddress> ResolveTargetAsync(string target)
        {
            try
            {
                var hostEntry = await Dns.GetHostEntryAsync(target);
                return hostEntry.AddressList.FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"DNS解析失败: {target}", ex, LogCategory.NetworkAnalysis);
                throw new Exception($"无法解析目标地址: {target}", ex);
            }
        }
        
        private async Task ProbeAllHopsAsync(IPAddress target, int maxHops)
        {
            using var pinger = new Ping();
            var options = new PingOptions { DontFragment = true };
            
            for (int hop = 1; hop <= maxHops; hop++)
            {
                if (_cts.Token.IsCancellationRequested) break;
                
                options.Ttl = hop;
                var buffer = new byte[32]; // 发送32字节的数据
                
                try
                {
                    var reply = await pinger.SendPingAsync(target, DefaultTimeout, buffer, options);
                    var hopStat = _hopStats[hop - 1];
                    
                    hopStat.AddResponse(reply);
                    
                    // 触发进度更新事件
                    OnProgressUpdated(new MTRProgressEventArgs
                    {
                        CurrentHop = hop,
                        TotalHops = maxHops,
                        LastResponse = reply
                    });
                    
                    if (reply.Status == IPStatus.Success)
                    {
                        // 如果到达目标，停止继续探测
                        if (reply.Address.Equals(target)) break;
                    }
                }
                catch (PingException ex)
                {
                    Logger.Instance.LogError($"Ping失败: Hop {hop}", ex, LogCategory.NetworkAnalysis);
                    _hopStats[hop - 1].AddFailure();
                }
            }
        }
        
        public void Stop()
        {
            _cts.Cancel();
        }
        
        private void OnProgressUpdated(MTRProgressEventArgs e)
        {
            ProgressUpdated?.Invoke(this, e);
        }
    }
    
    public class MTRHopStatistics
    {
        public int HopNumber { get; set; }
        public IPAddress Address { get; private set; }
        public string HostName { get; private set; }
        public List<PingReply> Responses { get; } = new();
        public int FailedProbes { get; private set; }
        
        public double LossRate => (double)FailedProbes / (Responses.Count + FailedProbes) * 100;
        public long BestTime { get; private set; }
        public long WorstTime { get; private set; }
        public double AverageTime { get; private set; }
        public double StandardDeviation { get; private set; }
        
        public void AddResponse(PingReply reply)
        {
            if (reply.Status == IPStatus.Success || reply.Status == IPStatus.TtlExpired)
            {
                Responses.Add(reply);
                if (Address == null)
                {
                    Address = reply.Address;
                    Task.Run(async () =>
                    {
                        try
                        {
                            var hostEntry = await Dns.GetHostEntryAsync(Address);
                            HostName = hostEntry.HostName;
                        }
                        catch
                        {
                            HostName = Address.ToString();
                        }
                    });
                }
            }
            else
            {
                FailedProbes++;
            }
        }
        
        public void AddFailure()
        {
            FailedProbes++;
        }
        
        public void CalculateStatistics()
        {
            if (Responses.Count == 0) return;
            
            var roundTripTimes = Responses.Select(r => r.RoundtripTime).ToList();
            BestTime = roundTripTimes.Min();
            WorstTime = roundTripTimes.Max();
            AverageTime = roundTripTimes.Average();
            
            // 计算标准差
            var sumOfSquares = roundTripTimes.Sum(x => Math.Pow(x - AverageTime, 2));
            StandardDeviation = Math.Sqrt(sumOfSquares / roundTripTimes.Count);
        }
    }
    
    public class MTRProgressEventArgs : EventArgs
    {
        public int CurrentHop { get; set; }
        public int TotalHops { get; set; }
        public PingReply? LastResponse { get; set; }
    }
} 