using System;
using System.Threading.Tasks;
using System.Collections.Generic;

using CSharpFunctionalExtensions;

namespace StopReasons.Services;

public interface IAvailabilityMetricStorage: IDisposable
{
    public record AvailabilityMetricInStorage(long Id, string DeviceId, DateTime InitiallyStoppedAt, DateTime LastStoppedMetricTracedAt, Maybe<string> MaybeReason);

    Task<List<AvailabilityMetricInStorage>> Load(DateTime from);
    Task Add(AvailabilityMetricInStorage metric);
    Task StoreReason(long id, string reason);
    Task UpdateLastStoppedMetricTracedAt(long id, DateTime at);
}
