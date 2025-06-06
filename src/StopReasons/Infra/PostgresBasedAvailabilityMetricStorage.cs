using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using StopReasons.Services;

using Dapper;
using Npgsql;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;

namespace StopReasons.Infra;

public class PostgresBasedAvailabilityMetricStorage: IAvailabilityMetricStorage
{
    private record DbDto(long id, string device_id, DateTime initially_stopped_at, DateTime last_stopped_metric_traced_at)
    {
        public IAvailabilityMetricStorage.AvailabilityMetricInStorage Map() =>
            new IAvailabilityMetricStorage.AvailabilityMetricInStorage(Id: this.id, DeviceId: this.device_id,
                InitiallyStoppedAt: this.initially_stopped_at, LastStoppedMetricTracedAt: this.last_stopped_metric_traced_at);
    }

    private sealed record MostRecentDowntimeReasonsDbDto(string device_id, DateTime initially_stopped_at, DateTime last_stopped_metric_traced_at, string maybe_stopping_reason)
    {
        public IAvailabilityMetricStorage.StoppingPeriodWithReasonSet Map() =>
            new (DeviceId: this.device_id, InitiallyStoppedAt: this.initially_stopped_at, LastStopReportedAt: this.last_stopped_metric_traced_at, StoppingReason: this.maybe_stopping_reason);
    }

    private readonly NpgsqlConnection _connection;

    public PostgresBasedAvailabilityMetricStorage(IOptions<PostgresConfig> config)
    {
        _connection = new NpgsqlConnection(config.Value.ConnectionString);
        _connection.Open();
    }

    public async Task<long> GetCurrentIdUsedForDowntimePeriods()
    {
        var commandText = @$"SELECT Max(id)
                            FROM device_downtime_reason";

        return await _connection.QueryFirstAsync<long>(sql: commandText);
    }

    public async Task<Maybe<long>> GetDowntimePeriodIdOfMostRecentStoppedPeriod(string forDeviceId)
    {
        var commandText = @$"SELECT max(id)
                            FROM device_downtime_reason
                            WHERE device_id = @device_id
                                AND is_it_still_stopped = TRUE";

        return (await _connection.QueryFirstOrDefaultAsync<long?>(sql: commandText, param: new { device_id = forDeviceId })) ?? Maybe<long>.None;
    }

    public async Task Add(IAvailabilityMetricStorage.AvailabilityMetricInStorage metric)
    {
        var commandText = @"INSERT INTO device_downtime_reason (id, device_id, initially_stopped_at, last_stopped_metric_traced_at, is_it_still_stopped)
                            VALUES (@id, @device_id, @initially_stopped_at, @last_stopped_metric_traced_at, TRUE)";

        var queryArguments = new {
            id = metric.Id,
            device_id = metric.DeviceId,
            initially_stopped_at = metric.InitiallyStoppedAt,
            last_stopped_metric_traced_at = metric.LastStoppedMetricTracedAt
        };

        await _connection.ExecuteAsync(commandText, queryArguments);
    }
    
    public async Task StoreReason(long id, string reason)
    {
        var commandText = @"UPDATE device_downtime_reason
                            SET maybe_stopping_reason = @maybe_stopping_reason
                            where id = @id";

        var queryArguments = new {
            id = id,
            maybe_stopping_reason = reason
        };

        await _connection.ExecuteAsync(commandText, queryArguments);
    }
    
    public async Task UpdateLastStoppedMetricTracedAt(long id, DateTime at, bool isItStillStopped)
    {
        var commandText = @"UPDATE device_downtime_reason
                            SET
                                last_stopped_metric_traced_at = @last_stopped_metric_traced_at,
                                is_it_still_stopped = @isItStillStopped
                            where id = @id";

        var queryArguments = new {
            id,
            last_stopped_metric_traced_at = at,
            isItStillStopped
        };

        await _connection.ExecuteAsync(commandText, queryArguments);
    }
    
    public async Task<IAvailabilityMetricStorage.LoadingResponse> LoadPendingStopReasonsToSet(
        IAvailabilityMetricStorage.LoadingOffset offsetInfo, IAvailabilityMetricStorage.LoadingOrder sortingCriteria,
        List<string> maybeFilterByTheseDeviceIds)
    {
        var dynamicDeviceIdFilter = maybeFilterByTheseDeviceIds.Any()
            ? $" AND device_id in ({JoinValuesForInStatement(maybeFilterByTheseDeviceIds)}) "
            : "";

        var totalSqlText = @$"SELECT count(*)
                            FROM device_downtime_reason
                            WHERE maybe_stopping_reason is null {dynamicDeviceIdFilter}";

        var commandText = @$"SELECT id, device_id, initially_stopped_at, last_stopped_metric_traced_at
                            FROM device_downtime_reason
                            WHERE maybe_stopping_reason is null {dynamicDeviceIdFilter}
                            ORDER BY {Map(from: sortingCriteria)}
                            LIMIT @pCount
                            OFFSET @pOffset";

        return new IAvailabilityMetricStorage.LoadingResponse(
            RecordsLoaded: (await _connection.QueryAsync<DbDto>(commandText, param: new { pCount = offsetInfo.Count, pOffset = offsetInfo.Offset })).Select(r => r.Map()).ToList(),
            TotalRecordCount: await _connection.QueryFirstAsync<int>(sql: totalSqlText));
    }

    private static string JoinValuesForInStatement(List<string> values) =>
        string.Join(separator: ',', values: values.Select(value => $"'{value}'"));

    public async Task<List<IAvailabilityMetricStorage.StoppingPeriodWithReasonSet>> GetMostRecentDowntimeReasons(DateTime from, DateTime to)
    {
        var commandText = @$"SELECT device_id, initially_stopped_at, last_stopped_metric_traced_at, maybe_stopping_reason
                            FROM device_downtime_reason
                            WHERE maybe_stopping_reason is not null
                                AND initially_stopped_at <= @to
                                AND last_stopped_metric_traced_at >= @from
                            ORDER BY initially_stopped_at, device_id";

        return (await _connection.QueryAsync<MostRecentDowntimeReasonsDbDto>(sql: commandText, param: new { from, to }))
            .Select(r => r.Map())
            .ToList();
    }

    private static string Map(IAvailabilityMetricStorage.LoadingOrder from)
    {
        var sortingColumn = from.Column switch {
            IAvailabilityMetricStorage.AllowedSortingColumns.DEVICE =>                      "device_id",
            IAvailabilityMetricStorage.AllowedSortingColumns.PERIOD_START =>                "initially_stopped_at",
            IAvailabilityMetricStorage.AllowedSortingColumns.MOST_RECENT_TIME_REPORTED =>   "last_stopped_metric_traced_at",

            _ => "id"
        };

        var sortingDirection = from.Direction switch {
            IAvailabilityMetricStorage.SortingDirection.ASCENDENT => "ASC",
            IAvailabilityMetricStorage.SortingDirection.DESCENDENT => "DESC",

            _ => "ASC"
        };

        return $"{sortingColumn} {sortingDirection}";
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