using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace VictoriaLike.Server.Services;

public class RedisHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;

    public RedisHealthCheck(IConfiguration configuration) => _configuration = configuration;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        try
        {
            var redisConn = _configuration.GetConnectionString("Redis");
            var options = ConfigurationOptions.Parse(redisConn ?? "");
            using var connection = await ConnectionMultiplexer.ConnectAsync(options);
            if (!connection.IsConnected)
                return HealthCheckResult.Unhealthy("Redis not connected");
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis connection failed", ex);
        }
    }
}
