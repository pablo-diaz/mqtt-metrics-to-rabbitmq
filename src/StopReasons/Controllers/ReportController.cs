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

    [HttpGet("downtimeReasonsForEveryMinuteInPeriod")]
    public IActionResult GetDowntimeReasonsForEveryMinuteInPeriod([FromQuery] string from, [FromQuery] string to, [FromQuery] string frequency) =>
        Content(content: $"{GetCsvHeader()}\n{GetDummyEntry()}\n{GetCsvRowsForDowntimePeriods(from, to, GetTimeFrequencyOfRegistriesInPeriod(frequency))}", contentType: "text/csv");

    private static string GetCsvHeader() =>
        "_time,device_id,downtime_reason";

    private static string GetDummyEntry() =>
        "2020-01-01T00:00:01.000000000Z,NoDev,NoReason";  // this is used, so that Flux queries do not break on empty CSV result sets

    private string GetCsvRowsForDowntimePeriods(string fromGmtDate, string toGmtDate, TimeSpan timeFrequencyOfRegistriesInPeriod) =>
        string.Join(separator: "\n",
            values: this._availabilityState.GetMostRecentDowntimeReasons(inPeriod: new (From: DateTimeOffset.Parse(fromGmtDate), To: DateTimeOffset.Parse(toGmtDate)))
                .SelectMany(p => {
                    var csvLinesForPeriod = new List<string>();
                    for(var date = p.initiallyStoppedAt; date <= p.lastStopReportedAt; date = date.Add(timeFrequencyOfRegistriesInPeriod))
                    {
                        var adjustedDateForInfluxQueriesPurposes = $"{NormalizeDateToUTC(date):yyyy-MM-ddTHH:mm}:00.000000000Z";
                        var adjustedReasonForCsvPurposes = p.reason.Replace(",", " ").Replace("\n", " ");
                        csvLinesForPeriod.Add($"{adjustedDateForInfluxQueriesPurposes},{p.deviceId},{adjustedReasonForCsvPurposes}");
                    }
                    return csvLinesForPeriod;
                })
        );

    private static DateTime NormalizeDateToUTC(DateTime date) => date.AddHours(5);

    private static TimeSpan GetTimeFrequencyOfRegistriesInPeriod(string fromFrequencyOption) => fromFrequencyOption switch {
        "1s" => TimeSpan.FromSeconds(1),
        "1m" => TimeSpan.FromMinutes(1),
        "5m" => TimeSpan.FromMinutes(5),
        "10m" => TimeSpan.FromMinutes(10),

        _ => TimeSpan.FromMinutes(1)
    };

}
