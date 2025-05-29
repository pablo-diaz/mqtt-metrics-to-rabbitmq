using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using CSharpFunctionalExtensions;

namespace StopReasons.Services;

public class AvailabilityStateManager
{
    private const int _hack_hoursInBogota = -5;
    private readonly IAvailabilityMetricStorage _persistence;

    public sealed record PeriodToReport(DateTimeOffset From, DateTimeOffset To);
    public sealed record StoppingPeriodWithReasonSet(string DeviceId, DateTime InitiallyStoppedAt, DateTime LastStopReportedAt, string StoppingReason);

    public AvailabilityStateManager(IAvailabilityMetricStorage persistence)
    {
        this._persistence = persistence;
    }
    
    public async Task SetDowntimeReason(string reason, long forDowntimePeriodId)
    {
        await _persistence.StoreReason(id: forDowntimePeriodId, reason: reason);
    }

    public sealed record LoadingParams(int PageNumber, int PageSize, string SortingColumn, string SortingDirection, List<string> maybeFilterByTheseDeviceIds);

    public sealed record PendingDowntimePeriodToSetReasonsFor(string DeviceId, long DowntimePeriodId, DateTime InitiallyStoppedAt, DateTime LastStopReportedAt);
    public sealed record LoadingResponse(List<PendingDowntimePeriodToSetReasonsFor> StopingPeriods, int TotalNumberOfPeriods, int NumberOfPages);

    public async Task<LoadingResponse> GetPendingDowntimePeriodsToSetReasonsFor(LoadingParams withParams)
    {
        var pendingReasonsLoadedFromPersistence = await _persistence
            .LoadPendingStopReasonsToSet(
                offsetInfo: MapLoadingOffset(from: withParams),
                sortingCriteria: MapLoadingOrder(from: withParams),
                maybeFilterByTheseDeviceIds: withParams.maybeFilterByTheseDeviceIds);

        return new(
            StopingPeriods: pendingReasonsLoadedFromPersistence
                .RecordsLoaded
                .Select(Map)
                .ToList(),
            TotalNumberOfPeriods: pendingReasonsLoadedFromPersistence.TotalRecordCount,
            NumberOfPages: (int)Math.Ceiling((decimal)pendingReasonsLoadedFromPersistence.TotalRecordCount / (decimal)withParams.PageSize)
        );
    }

    private static PendingDowntimePeriodToSetReasonsFor Map(IAvailabilityMetricStorage.AvailabilityMetricInStorage from) =>
        new(DeviceId: from.DeviceId, DowntimePeriodId: from.Id, InitiallyStoppedAt: from.InitiallyStoppedAt, LastStopReportedAt: from.LastStoppedMetricTracedAt);

    private static IAvailabilityMetricStorage.LoadingOffset MapLoadingOffset(LoadingParams from) =>
        new(Offset: (from.PageNumber - 1) * from.PageSize, Count: from.PageSize);

    private static IAvailabilityMetricStorage.LoadingOrder MapLoadingOrder(LoadingParams from) =>
        new(
            Column: from.SortingColumn switch {
                "device" => IAvailabilityMetricStorage.AllowedSortingColumns.DEVICE,
                "period_start" => IAvailabilityMetricStorage.AllowedSortingColumns.PERIOD_START,
                "most_recent_time_reported" => IAvailabilityMetricStorage.AllowedSortingColumns.MOST_RECENT_TIME_REPORTED,
                _ => IAvailabilityMetricStorage.AllowedSortingColumns.DEVICE
            },
            Direction: from.SortingDirection.ToLower().Trim() == "asc"
                ? IAvailabilityMetricStorage.SortingDirection.ASCENDENT
                : IAvailabilityMetricStorage.SortingDirection.DESCENDENT
        );

    public async Task<List<StoppingPeriodWithReasonSet>> GetMostRecentDowntimeReasons(PeriodToReport inPeriod) =>
        (await _persistence.GetMostRecentDowntimeReasons(
            from: GetLocalTime(inPeriod.From),
            to: GetLocalTime(inPeriod.To)))
        .Select(Map)
        .ToList();

    private static StoppingPeriodWithReasonSet Map(IAvailabilityMetricStorage.StoppingPeriodWithReasonSet from) =>
        new StoppingPeriodWithReasonSet(DeviceId: from.DeviceId, InitiallyStoppedAt: from.InitiallyStoppedAt,
            LastStopReportedAt: from.LastStopReportedAt, StoppingReason: from.StoppingReason);

    private static DateTime GetLocalTime(DateTimeOffset from) =>
        from.DateTime.AddHours(_hack_hoursInBogota);  // TODO: I'm being lazy and I will find a better way soon
    
}
