using System;
using System.Collections.Generic;

namespace SendMessagesViaMqtt;

public sealed class Device
{
    private const string _StopReasonNotDefinedYetByDeviceUser = "-";
    private const string _StopReasonWhenAvailable = "-";
    private const int _ApprovedCountWhenStopped = 0;
    private const int _RejectedCountWhenStopped = 0;
    private readonly string[] _DowntimeReasons = new string[] {"001", "002", "003", "004", "005", "006", "007", "008", "009"};

    public ConsoleKey KeyToBind { get; init; }
    public int Velocity { get; init; }
    public string WorkingForProductId { get; init; }
    public Func<int> GetApprovedCount { get; init; }
    
    private bool _isItAvailableNow = true;
    private string _maybeStopReason = null;
    private int _currentRejectedCount = 0;

    public void ToggleAvailability(bool shouldSetKnownReasonWhenStopped, Random usingRandomizer)
    {
        _isItAvailableNow = !_isItAvailableNow;
        _maybeStopReason = _isItAvailableNow
                           ? null
                           : shouldSetKnownReasonWhenStopped
                             ? GetNextDowntimeReason(usingRandomizer)
                             : _StopReasonNotDefinedYetByDeviceUser;
    }

    public void IncrementRejectedCount()
    {
        _currentRejectedCount++;
    }

    private string GetNextDowntimeReason(Random usingRandomizer)
    {
        var randomIndex = usingRandomizer.Next(minValue: 0, maxValue: _DowntimeReasons.Length - 1);
        return _DowntimeReasons[randomIndex];
    }

    private void ResetRejectedCount()
    {
        _currentRejectedCount = 0;
    }

    public IEnumerable<(string AvailabilityMetric, string QualityMetric)> GetMetrics(int forDeviceId, DateTime startingFromDate)
    {
        var aDate = startingFromDate;

        while(true)
        {
            var deviceId = "Dev" + forDeviceId.ToString().PadLeft(totalWidth: 3, paddingChar: '0');

            var availability = _isItAvailableNow ? "Produciendo" : "Parado";
            var downtimeReason = _isItAvailableNow ? _StopReasonWhenAvailable : _maybeStopReason;

            var approvedCount = GetApprovedCount() - _currentRejectedCount;
            if(approvedCount < 0)
                approvedCount = 0;

            var approved = _isItAvailableNow ? approvedCount : _ApprovedCountWhenStopped;
            var rejected = _isItAvailableNow ? _currentRejectedCount : _RejectedCountWhenStopped;
            
            aDate = aDate.AddSeconds(1);
            ResetRejectedCount();

            yield return (AvailabilityMetric: $"{deviceId}@{availability}@{downtimeReason}@{aDate:yyyy-M-d@H_m_s}",
                          QualityMetric:      $"{deviceId}@{Velocity}@{WorkingForProductId}@Aprobados@{approved}@Rechazados@{rejected}@{aDate:yyyy-M-d@H_m_s}");
        }
    }
}