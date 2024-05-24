using System.Linq;
using System.Collections.Generic;

using Microsoft.AspNetCore.Mvc;

using StopReasons.Services;
using System;

namespace StopReasons.Controllers;

[ApiController]
[Route("[controller]")]
public class ReportController: ControllerBase
{
    private readonly AvailabilityStateManager _availabilityState;

    public ReportController(AvailabilityStateManager availabilityState)
    {
        this._availabilityState = availabilityState;
    }

    [HttpGet("downtimePeriods")]
    public IActionResult GetDowntimePeriods() =>
        Content(content: $"{GetCsvHeader()}\n{GetDummyEntry()}\n{GetCsvRowsForDowntimePeriods()}", contentType: "text/csv");

    private static string GetCsvHeader() =>
        "_time,device_id,downtime_reason";

    private static string GetDummyEntry() =>
        "2020-01-01T00:00:01.000000000Z,NoDev,NoReason";  // this is used, so that Flux queries do not break on empty CSV result sets

    private string GetCsvRowsForDowntimePeriods() =>
        string.Join(separator: "\n",
            values: this._availabilityState.GetMostRecentDowntimePeriods()
                .SelectMany(p => {
                    var csvLinesForPeriod = new List<string>();
                    for(var date = p.initiallyStoppedAt; date <= p.lastStopReportedAt; date = date.AddSeconds(1))
                    {
                        var adjustedDateForInfluxQueriesPurposes = $"{NormalizeDateToUTC(date):yyyy-MM-ddTHH:mm:ss}.000000000Z";
                        var adjustedReasonForCsvPurposes = p.reason.Replace(",", " ").Replace("\n", " ");
                        csvLinesForPeriod.Add($"{adjustedDateForInfluxQueriesPurposes},{p.deviceId},{adjustedReasonForCsvPurposes}");
                    }
                    return csvLinesForPeriod;
                })
        );

    private static DateTime NormalizeDateToUTC(DateTime date) => date.AddHours(5);
}
