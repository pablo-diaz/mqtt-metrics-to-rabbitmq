using System.Linq;
using System.Collections.Generic;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using Microsoft.Extensions.Logging;

using CSharpFunctionalExtensions;

using StopReasons.Services;

namespace StopReasons.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly AvailabilityStateManager _availabilityState;

    public PendingDowntimePeriodToSetReasonsForViewModel[] DowntimePeriodsPerDevice;
    
    public List<(string ReasonText, string ReasonCode)> ValidReasons = new() {
        (ReasonText: "Producción 01", ReasonCode: "001"),
        (ReasonText: "Producción 02", ReasonCode: "002"),
        (ReasonText: "Producción 03", ReasonCode: "003"),
        (ReasonText: "Producción 04", ReasonCode: "004"),
        (ReasonText: "Producción 05", ReasonCode: "005"),
        (ReasonText: "Producción 06", ReasonCode: "006"),
        (ReasonText: "Producción 07", ReasonCode: "007"),
        (ReasonText: "Producción 08", ReasonCode: "008"),
        (ReasonText: "Producción 09", ReasonCode: "009"),
        (ReasonText: "Producción 10", ReasonCode: "010"),
        (ReasonText: "Producción 11", ReasonCode: "011"),
        (ReasonText: "Producción 12", ReasonCode: "012")
    };

    public IndexModel(ILogger<IndexModel> logger, AvailabilityStateManager availabilityState)
    {
        _logger = logger;
        this._availabilityState = availabilityState;
    }

    public void OnGet()
    {
        DowntimePeriodsPerDevice = _availabilityState.GetPendingDowntimePeriodsToSetReasonsFor()
            .Select(p => new PendingDowntimePeriodToSetReasonsForViewModel(DeviceId: p.deviceId, ReasonFieldName: $"downtimeReason_{p.downtimePeriodId}",
                InitiallyStoppedAt: $"{p.initiallyStoppedAt:dd/MMM/yyyy hh:mm:ss tt}", LastStopReportedAt: $"{p.lastStopReportedAt:dd/MMM/yyyy hh:mm:ss tt}"))
            .ToArray();
    }

    public IActionResult OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        foreach(var k in Request.Form.Keys.Where(k => k.StartsWith("downtimeReason_")))
        {
            var maybeReason = string.IsNullOrEmpty(Request.Form[k]) ? Maybe<string>.None : Maybe<string>.From(Request.Form[k]);
            if(maybeReason.HasNoValue)
                continue;

            var downtimePeriodId = long.Parse(k.Split('_')[1]);
            var maybePeriodFound = _availabilityState.GetPendingDowntimePeriodsToSetReasonsFor()
                .FirstOrDefault(p => p.downtimePeriodId == downtimePeriodId) ?? Maybe<PendingDowntimePeriodToSetReasonsFor>.None;

            if(maybePeriodFound.HasNoValue)
            {
                System.Console.WriteLine($"Period '{downtimePeriodId}' was not found");
                continue;
            }

            _availabilityState.SetDowntimeReason(reason: maybeReason.Value, forDeviceId: maybePeriodFound.Value.deviceId, forDowntimePeriodId: maybePeriodFound.Value.downtimePeriodId);
            System.Console.WriteLine($"Period reason set successfully for '{downtimePeriodId}' downtime period id");
        }

        return RedirectToPage();
    }
}

public record PendingDowntimePeriodToSetReasonsForViewModel(string DeviceId, string InitiallyStoppedAt, string LastStopReportedAt, string ReasonFieldName);
