using System;
using System.Threading.Tasks;
using System.Collections.Generic;

using CSharpFunctionalExtensions;

namespace StopReasons.Services;

public interface IAvailabilityMetricStorage: IDisposable
{
    public sealed record AvailabilityMetricInStorage(long Id, string DeviceId, DateTime InitiallyStoppedAt, DateTime LastStoppedMetricTracedAt);
    public sealed record LoadingResponse(List<AvailabilityMetricInStorage> RecordsLoaded, int TotalRecordCount);
    public sealed record LoadingOffset(int Offset, int Count);
    public enum AllowedSortingColumns { DEVICE, PERIOD_START, MOST_RECENT_TIME_REPORTED };
    public enum SortingDirection { ASCENDENT, DESCENDENT }
    public sealed record LoadingOrder(AllowedSortingColumns Column, SortingDirection Direction);
    public sealed record StoppingPeriodWithReasonSet(string DeviceId, DateTime InitiallyStoppedAt, DateTime LastStopReportedAt, string StoppingReason);

    Task<long> GetCurrentIdUsedForDowntimePeriods();
    Task<LoadingResponse> LoadPendingStopReasonsToSet(LoadingOffset offsetInfo, LoadingOrder sortingCriteria, List<string> maybeFilterByTheseDeviceIds);
    Task<Maybe<long>> GetDowntimePeriodIdOfMostRecentStoppedPeriod(string forDeviceId);
    Task Add(AvailabilityMetricInStorage metric);
    Task StoreReason(long id, string reason);
    Task UpdateLastStoppedMetricTracedAt(long id, DateTime at, bool isItStillStopped);
    Task<List<StoppingPeriodWithReasonSet>> GetMostRecentDowntimeReasons(DateTime from, DateTime to);

}
