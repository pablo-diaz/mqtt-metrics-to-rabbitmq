using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using StopReasons.Services;

using Dapper;
using Npgsql;
using CSharpFunctionalExtensions;

namespace StopReasons.Infra;

public class PostgresBasedAvailabilityMetricStorage: IAvailabilityMetricStorage
{
    private record DbDto(long id, string device_id, DateTime initially_stopped_at, DateTime last_stopped_metric_traced_at, string maybe_stopping_reason)
    {
        public IAvailabilityMetricStorage.AvailabilityMetricInStorage Map() =>
            new IAvailabilityMetricStorage.AvailabilityMetricInStorage(Id: this.id, DeviceId: this.device_id,
                InitiallyStoppedAt: this.initially_stopped_at, LastStoppedMetricTracedAt: this.last_stopped_metric_traced_at,
                MaybeReason: string.IsNullOrEmpty(this.maybe_stopping_reason) ? Maybe<string>.None : Maybe<string>.From(this.maybe_stopping_reason));
    }

    private readonly NpgsqlConnection _connection;

    public PostgresBasedAvailabilityMetricStorage(PostgresConfig config)
    {
        _connection = new NpgsqlConnection(config.ConnectionString);
        _connection.Open();
    }

    public Task Add(IAvailabilityMetricStorage.AvailabilityMetricInStorage metric)
    {
        var commandText = @"INSERT INTO device_downtime_reason (id, device_id, initially_stopped_at, last_stopped_metric_traced_at, maybe_stopping_reason)
                            VALUES (@id, @device_id, @initially_stopped_at, @last_stopped_metric_traced_at, @maybe_stopping_reason)";

        var queryArguments = new {
            id = metric.Id,
            device_id = metric.DeviceId,
            initially_stopped_at = metric.InitiallyStoppedAt,
            last_stopped_metric_traced_at = metric.LastStoppedMetricTracedAt,
            maybe_stopping_reason = metric.MaybeReason.HasValue ? metric.MaybeReason.Value : null
        };

        return _connection.ExecuteAsync(commandText, queryArguments);
    }
    
    public Task StoreReason(long id, string reason)
    {
        var commandText = @"UPDATE device_downtime_reason
                            SET maybe_stopping_reason = @maybe_stopping_reason
                            where id = @id";

        var queryArguments = new {
            id = id,
            maybe_stopping_reason = reason
        };

        return _connection.ExecuteAsync(commandText, queryArguments);
    }
    
    public Task UpdateLastStoppedMetricTracedAt(long id, DateTime at)
    {
        var commandText = @"UPDATE device_downtime_reason
                            SET last_stopped_metric_traced_at = @last_stopped_metric_traced_at
                            where id = @id";

        var queryArguments = new {
            id = id,
            last_stopped_metric_traced_at = at
        };

        return _connection.ExecuteAsync(commandText, queryArguments);
    }
    
    public async Task<List<IAvailabilityMetricStorage.AvailabilityMetricInStorage>> Load()
    {
        var commandText = @"SELECT id, device_id, initially_stopped_at, last_stopped_metric_traced_at, maybe_stopping_reason
                            FROM device_downtime_reason
                            ORDER BY id";

        return (await _connection.QueryAsync<DbDto>(commandText))
               .Select(r => r.Map())
               .ToList();
    }

    public void Dispose()
    {
        if(_connection != null)
        {
            _connection.Close();
            _connection.Dispose();
        }
    }
}