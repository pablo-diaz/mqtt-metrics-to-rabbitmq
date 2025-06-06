using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using StopReasons.Services;

using Microsoft.AspNetCore.Mvc;

namespace StopReasons.Controllers;

[ApiController]
[Route("[controller]")]
public class ReportController : ControllerBase
{
    private readonly AvailabilityStateManager _availabilityState;

    public ReportController(AvailabilityStateManager availabilityState)
    {
        this._availabilityState = availabilityState;
    }

    [HttpGet("downtimeReasonsForEveryMinuteInPeriod")]
    public async Task<IActionResult> GetDowntimeReasonsForEveryMinuteInPeriod([FromQuery] string from, [FromQuery] string to, [FromQuery] string frequency) =>
        Content(content: $"{GetCsvHeader()}\n{GetDummyEntry()}\n{await GetCsvRowsForDowntimePeriods(from, to, GetTimeFrequencyOfRegistriesInPeriod(frequency))}", contentType: "text/csv");

    private static string GetCsvHeader() =>
        "_time,device_id,downtime_reason";

    private static string GetDummyEntry() =>
        "2020-01-01T00:00:01.000000000Z,NoDev,NoReason";  // this is used, so that Flux queries do not break on empty CSV result sets

    private async Task<string> GetCsvRowsForDowntimePeriods(string fromGmtDate, string toGmtDate, TimeSpan timeFrequencyOfRegistriesInPeriod) =>
        string.Join(separator: "\n",
            values: (await this._availabilityState.GetMostRecentDowntimeReasons(
                        inPeriod: new(
                            From: DateTimeOffset.Parse(fromGmtDate),
                            To: DateTimeOffset.Parse(toGmtDate))))
                    .SelectMany(p => GetAllDatesInPeriodWithGivenTimeFrequency(period: p, timeFrequencyOfRegistriesInPeriod))
                    .Distinct()
        );

    private static List<string> GetAllDatesInPeriodWithGivenTimeFrequency(
            AvailabilityStateManager.StoppingPeriodWithReasonSet period, TimeSpan timeFrequencyOfRegistriesInPeriod) =>
        GenerateSequenceForAllDatesInPeriod(
            from: period.InitiallyStoppedAt, 
            to: period.LastStopReportedAt, 
            withTimeDelta: timeFrequencyOfRegistriesInPeriod)
        .Select(date => ToCsvOutputLineFormat(deviceId: period.DeviceId, stoppingReason: period.StoppingReason, date))
        .ToList();

    private static string ToCsvOutputLineFormat(string deviceId, string stoppingReason, DateTime date)
    {
        var adjustedDateForInfluxQueriesPurposes = $"{NormalizeDateToUTC(date):yyyy-MM-ddTHH:mm}:00.000000000Z";
        var adjustedReasonForCsvPurposes = stoppingReason.Replace(",", " ").Replace("\n", " ");
        return $"{adjustedDateForInfluxQueriesPurposes},{deviceId},{adjustedReasonForCsvPurposes}";
    }

    private static IEnumerable<DateTime> GenerateSequenceForAllDatesInPeriod(DateTime from, DateTime to, TimeSpan withTimeDelta)
    {
        for (var date = from; date <= to; date = date.Add(withTimeDelta))
            yield return date;
    }

    private static DateTime NormalizeDateToUTC(DateTime date) => date.AddHours(5);

    private static TimeSpan GetTimeFrequencyOfRegistriesInPeriod(string fromFrequencyOption) => fromFrequencyOption switch {
        "1s" => TimeSpan.FromSeconds(1),
        "1m" => TimeSpan.FromMinutes(1),
        "5m" => TimeSpan.FromMinutes(5),
        "10m" => TimeSpan.FromMinutes(10),

        _ => TimeSpan.FromMinutes(1)
    };

}
