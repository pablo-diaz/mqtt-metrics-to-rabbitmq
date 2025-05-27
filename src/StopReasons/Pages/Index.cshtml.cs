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
    public readonly List<(string ReasonText, string ReasonCode)> ValidReasons = new();

    public IndexModel(ILogger<IndexModel> logger, AvailabilityStateManager availabilityState, IOptions<DowntimeReasonsConfig> config)
    {
        this._availabilityState = availabilityState;
        this.ValidReasons = config.Value.AllowedReasons.Select(o => (ReasonText: o.Text, ReasonCode: o.Code)).ToList();
    }

    public async Task OnGet()
    {
        var response = await _availabilityState.GetPendingDowntimePeriodsToSetReasonsFor(withParams: new(
            PageNumber: 1,
            PageSize: 10,
            SortingColumn: "device",
            SortingDirection: "asc"
        ));

        DowntimePeriodsPerDevice = response.StopingPeriods.Select(MapToViewModel).ToArray();
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

    private Maybe<string> TryGetReason(string setInDropDownWithName)
    {
        var maybeValueSetInDropDown = (string) Request.Form[setInDropDownWithName];
        return string.IsNullOrEmpty(maybeValueSetInDropDown)
            ? Maybe<string>.None
            : maybeValueSetInDropDown;
    }

    private static PendingDowntimePeriodToSetReasonsForViewModel MapToViewModel(AvailabilityStateManager.PendingDowntimePeriodToSetReasonsFor from) =>
        new PendingDowntimePeriodToSetReasonsForViewModel(
            DeviceId: from.DeviceId,
            InitiallyStoppedAt: $"{from.InitiallyStoppedAt:MM/dd hh:mm:ss tt}",
            LastStopReportedAt: $"{from.LastStopReportedAt:MM/dd hh:mm:ss tt}",
            ReasonDropDownFieldName: $"{_downtimeReasonDropDownPrefix}{from.DowntimePeriodId}",
            ReasonCheckBoxFieldName: $"{_downtimeReasonCheckBoxPrefix}{from.DowntimePeriodId}",
            _sourceRecord: from);

    private async Task SaveReason(string forFieldName, string reasonToSet)
    {
        var downtimePeriodId = long.Parse(forFieldName.Split('_')[1]);

        await _availabilityState.SetDowntimeReason(
            reason: reasonToSet,
            forDowntimePeriodId: downtimePeriodId);

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
    AvailabilityStateManager.PendingDowntimePeriodToSetReasonsFor _sourceRecord);
