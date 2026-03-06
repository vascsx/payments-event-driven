using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;

namespace Payments.EventDriven.Infrastructure.HealthChecks;

/// <summary>
/// Health check para verificar sincronização de clock (NTP).
/// GUID v7 depende de DateTime.UtcNow para ordenação correta.
/// Clock skew entre servidores pode causar IDs fora de ordem.
/// 
/// Thresholds:
/// - Healthy: < 500ms de skew
/// - Degraded: 500ms - 2s de skew  
/// - Unhealthy: > 2s de skew (pode causar problemas de ordenação)
/// </summary>
public class ClockSkewHealthCheck : IHealthCheck
{
    private readonly ILogger<ClockSkewHealthCheck> _logger;
    private const string NtpServer = "pool.ntp.org";
    private static readonly TimeSpan HealthyThreshold = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan DegradedThreshold = TimeSpan.FromSeconds(2);

    public ClockSkewHealthCheck(ILogger<ClockSkewHealthCheck> logger)
    {
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var ntpTime = await GetNtpTimeAsync(cancellationToken);
            var localTime = DateTime.UtcNow;
            var skew = (ntpTime - localTime).Duration();

            var data = new Dictionary<string, object>
            {
                ["clock_skew_ms"] = skew.TotalMilliseconds,
                ["ntp_server"] = NtpServer,
                ["local_time"] = localTime.ToString("O"),
                ["ntp_time"] = ntpTime.ToString("O")
            };

            if (skew < HealthyThreshold)
            {
                return HealthCheckResult.Healthy(
                    $"Clock synchronized. Skew: {skew.TotalMilliseconds:F0}ms",
                    data);
            }

            if (skew < DegradedThreshold)
            {
                _logger.LogWarning(
                    "Clock skew detected: {SkewMs}ms. This may affect GUID v7 ordering.",
                    skew.TotalMilliseconds);

                return HealthCheckResult.Degraded(
                    $"Clock skew detected: {skew.TotalMilliseconds:F0}ms. Consider NTP sync.",
                    data: data);
            }

            _logger.LogError(
                "CRITICAL: Clock skew too high: {SkewMs}ms. GUID v7 ordering may be incorrect!",
                skew.TotalMilliseconds);

            return HealthCheckResult.Unhealthy(
                $"Clock skew too high: {skew.TotalMilliseconds:F0}ms. " +
                "This will cause incorrect GUID v7 ordering. Fix NTP sync immediately!",
                data: data);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check clock skew via NTP");
            
            return HealthCheckResult.Degraded(
                "Unable to verify clock sync. NTP server unreachable.",
                ex);
        }
    }

    /// <summary>
    /// Obtém o tempo atual do servidor NTP usando protocolo SNTP simplificado.
    /// </summary>
    private static async Task<DateTime> GetNtpTimeAsync(CancellationToken cancellationToken)
    {
        const int NtpPort = 123;
        var ntpData = new byte[48];
        ntpData[0] = 0x1B; // NTP version 3, client mode

        var addresses = await Dns.GetHostAddressesAsync(NtpServer, cancellationToken);
        var endPoint = new IPEndPoint(addresses[0], NtpPort);

        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.ReceiveTimeout = 3000;
        socket.SendTimeout = 3000;

        await socket.ConnectAsync(endPoint, cancellationToken);
        await socket.SendAsync(ntpData, SocketFlags.None, cancellationToken);
        await socket.ReceiveAsync(ntpData, SocketFlags.None, cancellationToken);

        // NTP timestamp starts at byte 40 (transmit timestamp)
        ulong intPart = (ulong)ntpData[40] << 24 | (ulong)ntpData[41] << 16 | 
                        (ulong)ntpData[42] << 8 | ntpData[43];
        ulong fractPart = (ulong)ntpData[44] << 24 | (ulong)ntpData[45] << 16 | 
                          (ulong)ntpData[46] << 8 | ntpData[47];

        var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);
        
        // NTP epoch is January 1, 1900
        var ntpEpoch = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var ntpTime = ntpEpoch.AddMilliseconds(milliseconds);

        return ntpTime;
    }
}
