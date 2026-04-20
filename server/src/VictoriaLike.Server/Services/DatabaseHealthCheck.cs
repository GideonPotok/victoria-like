using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace VictoriaLike.Server.Services;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;

    public DatabaseHealthCheck(IConfiguration configuration) => _configuration = configuration;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        try
        {
            var connString = _configuration.GetConnectionString("DefaultConnection");
            using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync(cancellationToken);
            await conn.CloseAsync();
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL connection failed", ex);
        }
    }
}
