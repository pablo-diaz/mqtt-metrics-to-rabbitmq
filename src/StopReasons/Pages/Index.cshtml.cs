using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

using CSharpFunctionalExtensions;

using StopReasons.Config;
using StopReasons.Services;

namespace StopReasons.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly AvailabilityStateManager _availabilityState;

    public PendingDowntimePeriodToSetReasonsForViewModel[] DowntimePeriodsPerDevice;
    public List<(string ReasonText, string ReasonCode)> ValidReasons = new();

    public IndexModel(ILogger<IndexModel> logger, AvailabilityStateManager availabilityState, IOptions<DowntimeReasonsConfig> config)
    {
        _logger = logger;
        this._availabilityState = availabilityState;
        ValidReasons = config.Value.AllowedReasons.Select(o => (ReasonText: o.Text, ReasonCode: o.Code)).ToList();
    }

    public void OnGet()
    {
        DowntimePeriodsPerDevice = _availabilityState.GetPendingDowntimePeriodsToSetReasonsFor()
            .Select(p => new PendingDowntimePeriodToSetReasonsForViewModel(DeviceId: p.deviceId, ReasonFieldName: $"downtimeReason_{p.downtimePeriodId}",
                InitiallyStoppedAt: $"{p.initiallyStoppedAt:dd/MMM/yyyy hh:mm:ss tt}", LastStopReportedAt: $"{p.lastStopReportedAt:dd/MMM/yyyy hh:mm:ss tt}"))
            .ToArray();
    }

    public async Task<IActionResult> OnPostAsync()
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

            await _availabilityState.SetDowntimeReason(reason: maybeReason.Value, forDeviceId: maybePeriodFound.Value.deviceId, forDowntimePeriodId: maybePeriodFound.Value.downtimePeriodId);
            System.Console.WriteLine($"Period reason set successfully for '{downtimePeriodId}' downtime period id");
        }

        return RedirectToPage();
    }
}

public record PendingDowntimePeriodToSetReasonsForViewModel(string DeviceId, string InitiallyStoppedAt, string LastStopReportedAt, string ReasonFieldName);
