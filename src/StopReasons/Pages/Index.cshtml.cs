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
    private readonly AvailabilityStateManager _availabilityState;
    private const string _downtimeReasonDropDownPrefix = "drdd_";
    private const string _downtimeReasonCheckBoxPrefix = "drcb_";

    public PendingDowntimePeriodToSetReasonsForViewModel[] DowntimePeriodsPerDevice;
    public List<(string ReasonText, string ReasonCode)> ValidReasons = new();
    private readonly int _maxNumberOfReasonsToDisplay;

    public IndexModel(ILogger<IndexModel> logger, AvailabilityStateManager availabilityState, IOptions<DowntimeReasonsConfig> config)
    {
        this._availabilityState = availabilityState;
        ValidReasons = config.Value.AllowedReasons.Select(o => (ReasonText: o.Text, ReasonCode: o.Code)).ToList();
        _maxNumberOfReasonsToDisplay = config.Value.MaxNumberOfReasonsToDisplay;
    }

    public void OnGet()
    {
        DowntimePeriodsPerDevice = _availabilityState.GetPendingDowntimePeriodsToSetReasonsFor()
            .Select(p => new PendingDowntimePeriodToSetReasonsForViewModel(
                DeviceId: p.deviceId,
                InitiallyStoppedAt: $"{p.initiallyStoppedAt:MM/dd hh:mm:ss tt}",
                LastStopReportedAt: $"{p.lastStopReportedAt:MM/dd hh:mm:ss tt}",
                ReasonDropDownFieldName: $"{_downtimeReasonDropDownPrefix}{p.downtimePeriodId}",
                ReasonCheckBoxFieldName: $"{_downtimeReasonCheckBoxPrefix}{p.downtimePeriodId}",
                _sourceRecord: p ))
            .OrderByDescending(p => p._sourceRecord.lastStopReportedAt)
                .ThenByDescending(p => p._sourceRecord.initiallyStoppedAt)
                .ThenByDescending(p => p.DeviceId)
            .Take(_maxNumberOfReasonsToDisplay)
            .ToArray();
    }

    public async Task<IActionResult> OnPostSaveReasonsIndividuallyAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        foreach(var reasonDropDownFieldName in GetFieldNamesOfReasonDropdowns())
        {
            var maybeReason = TryGetReason(setInDropDownWithName: reasonDropDownFieldName);
            if(maybeReason.HasNoValue)
                continue;

            await SaveReason(forFieldName: reasonDropDownFieldName, reasonToSet: maybeReason.Value);
        }

        return RedirectToPage();
    }

    private Maybe<string> TryGetReason(string setInDropDownWithName)
    {
        var maybeValueSetInDropDown = (string) Request.Form[setInDropDownWithName];
        return string.IsNullOrEmpty(maybeValueSetInDropDown)
            ? Maybe<string>.None
            : maybeValueSetInDropDown;
    }

    private Maybe<PendingDowntimePeriodToSetReasonsFor> TryFindPeriod(long withDowntimePeriodId) =>
        _availabilityState
        .GetPendingDowntimePeriodsToSetReasonsFor()
        .FirstOrDefault(p => p.downtimePeriodId == withDowntimePeriodId)
        ?? Maybe<PendingDowntimePeriodToSetReasonsFor>.None;

    public async Task<IActionResult> OnPostSaveReasonsMassivelyAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var maybeReasonToSetMassively = TryGetReason(setInDropDownWithName: "ddlReasonToSetMassively");
        if (maybeReasonToSetMassively.HasNoValue)
            return RedirectToPage();

        foreach (var reasonCheckBoxFieldName in GetFieldNamesOfReasonCheckBoxes())
        {
            await SaveReason(forFieldName: reasonCheckBoxFieldName, reasonToSet: maybeReasonToSetMassively.Value);
        }

        return RedirectToPage();
    }

    private async Task SaveReason(string forFieldName, string reasonToSet)
    {
        var downtimePeriodId = long.Parse(forFieldName.Split('_')[1]);
        var maybePeriodFound = TryFindPeriod(withDowntimePeriodId: downtimePeriodId);
        if (maybePeriodFound.HasNoValue)
        {
            System.Console.Error.WriteLine($"Period '{downtimePeriodId}' was not found");
            return;
        }

        await _availabilityState.SetDowntimeReason(
            reason: reasonToSet,
            forDeviceId: maybePeriodFound.Value.deviceId,
            forDowntimePeriodId: maybePeriodFound.Value.downtimePeriodId);

        System.Console.WriteLine($"Period reason set successfully for '{downtimePeriodId}' downtime period id");
    }

    private IEnumerable<string> GetFieldNamesOfReasonDropdowns() =>
        Request.Form.Keys.Where(k => k.StartsWith(_downtimeReasonDropDownPrefix));

    private IEnumerable<string> GetFieldNamesOfReasonCheckBoxes() =>
        Request.Form.Keys.Where(k => k.StartsWith(_downtimeReasonCheckBoxPrefix));

}

public record PendingDowntimePeriodToSetReasonsForViewModel(
    string DeviceId, string InitiallyStoppedAt, string LastStopReportedAt,
    string ReasonDropDownFieldName, string ReasonCheckBoxFieldName,
    PendingDowntimePeriodToSetReasonsFor _sourceRecord);
