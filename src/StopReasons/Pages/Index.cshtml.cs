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
using System;

namespace StopReasons.Pages;

public class IndexModel : PageModel
{
    private readonly AvailabilityStateManager _availabilityState;
    private readonly ServiceToFilterDevicesByLineOfBusiness _serviceToFilterDevicesByLine;
    private const string _downtimeReasonDropDownPrefix = "drdd_";
    private const string _downtimeReasonCheckBoxPrefix = "drcb_";
    private const int _defaultPageSize = 10;

    public PendingDowntimePeriodToSetReasonsForViewModel[] DowntimePeriodsPerDevice;
    public readonly List<(string ReasonText, string ReasonCode)> ValidReasons = new();
    public readonly List<string> LineOfBusinesses = new();

    [FromForm(Name= "cpn")]
    public int CurrentPageNumber { get; set; }

    public sealed record PageNumberInfo(string PageLabel, int PageNumber, bool IsItCurrentPageSelected)
    {
        public static PageNumberInfo From(int number, int currentPage) =>
            new(PageLabel: number.ToString(), PageNumber: number, IsItCurrentPageSelected: currentPage == number);
    }

    public List<PageNumberInfo> PageNumbersToDisplay { get; set; }

    [FromForm(Name = "cps")]
    public int CurrentPageSize { get; set; }

    [FromForm(Name = "cls")]
    public string CurrentLineSelected { get; set; } = "-";

    public IndexModel(ILogger<IndexModel> logger, AvailabilityStateManager availabilityState, IOptions<DowntimeReasonsConfig> config,
        ServiceToFilterDevicesByLineOfBusiness serviceToFilterDevicesByLine)
    {
        this._availabilityState = availabilityState;
        this._serviceToFilterDevicesByLine = serviceToFilterDevicesByLine;
        this.ValidReasons = config.Value.AllowedReasons.Select(o => (ReasonText: o.Text, ReasonCode: o.Code)).ToList();
        this.LineOfBusinesses = this._serviceToFilterDevicesByLine.ListAllLineOfBusiness();
    }

    public async Task<IActionResult> OnGet()
    {
        CurrentPageNumber = 1;
        CurrentPageSize = _defaultPageSize;

        await SetDowntimePeriodsToDisplay();

        return Page();
    }

    public async Task<IActionResult> OnPostSpecificPageNumber(int number)
    {
        CurrentPageNumber = number;

        await SetDowntimePeriodsToDisplay();

        return Page();
    }

    public async Task<IActionResult> OnPostSpecificPageSize(int size)
    {
        CurrentPageSize = size;
        CurrentPageNumber = 1;  // when changing page size, go back to first page

        await SetDowntimePeriodsToDisplay();

        return Page();
    }

    public async Task<IActionResult> OnPostSpecificDevicesInLine(string linename)
    {
        CurrentPageSize = _defaultPageSize;
        CurrentPageNumber = 1;
        CurrentLineSelected = linename;

        await SetDowntimePeriodsToDisplay();

        return Page();
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

    private async Task SetDowntimePeriodsToDisplay()
    {
        var response = await _availabilityState.GetPendingDowntimePeriodsToSetReasonsFor(withParams: new(
            PageNumber: CurrentPageNumber,
            PageSize: CurrentPageSize,
            SortingColumn: "period_start",
            SortingDirection: "desc",
            maybeFilterByTheseDeviceIds: CurrentLineSelected == "-" ? [] : _serviceToFilterDevicesByLine.ListDeviceIds(boundToLineOfBusiness: CurrentLineSelected)
        ));

        DowntimePeriodsPerDevice = response.StopingPeriods.Select(MapToViewModel).ToArray();
        PageNumbersToDisplay = GetPageNumbersToDisplay(fromNumberOfPagesRetrieved: response.NumberOfPages, withCurrentPageNumberSelected: CurrentPageNumber).ToList();
    }

    private static IEnumerable<int> GetSequenceWhenPageCountIsLessThanMaxPagesThatCanBeDisplayed(int fromNumberOfPagesRetrieved) =>
        Enumerable.Range(start: 1, count: fromNumberOfPagesRetrieved);

    private static IEnumerable<int> GetSequenceForFirstLeftSegment(int maxNumberOfPageNumbersThatCanBeDisplayed) =>
        Enumerable.Range(start: 1, count: maxNumberOfPageNumbersThatCanBeDisplayed);

    private static IEnumerable<int> GetSequenceForLastSegmentAtTheRight(int fromNumberOfPagesRetrieved, int maxNumberOfPageNumbersThatCanBeDisplayed) =>
        Enumerable.Range(start: fromNumberOfPagesRetrieved - maxNumberOfPageNumbersThatCanBeDisplayed + 1, count: maxNumberOfPageNumbersThatCanBeDisplayed);

    private static IEnumerable<int> GetSequenceForMiddleSegment(int withCurrentPageNumberSelected, int maxNumberOfPageNumbersThatCanBeDisplayed) =>
        Enumerable.Range(start: withCurrentPageNumberSelected - (maxNumberOfPageNumbersThatCanBeDisplayed / 2), count: maxNumberOfPageNumbersThatCanBeDisplayed);

    private static IEnumerable<PageNumberInfo> GetPageNumbersToDisplay(int fromNumberOfPagesRetrieved, int withCurrentPageNumberSelected)
    {
        const int maxNumberOfPageNumbersThatCanBeDisplayed = 10;
        const int minPagesLeftAtTheRightSide = 2;

        var areThereLessPagesThanMaxPageCountThatCanBeDisplayed = fromNumberOfPagesRetrieved <= maxNumberOfPageNumbersThatCanBeDisplayed;
        var isCurrentPageAtTheFirstLeftSegment = areThereLessPagesThanMaxPageCountThatCanBeDisplayed == false && withCurrentPageNumberSelected <= maxNumberOfPageNumbersThatCanBeDisplayed - minPagesLeftAtTheRightSide;
        var isCurrentPageAtMiddleSegment = isCurrentPageAtTheFirstLeftSegment == false && withCurrentPageNumberSelected <= fromNumberOfPagesRetrieved - (maxNumberOfPageNumbersThatCanBeDisplayed / 2);
        var isCurrentPageAtLastSegmentToTheRight = isCurrentPageAtMiddleSegment == false && withCurrentPageNumberSelected > fromNumberOfPagesRetrieved - (maxNumberOfPageNumbersThatCanBeDisplayed / 2);

        Func<int, PageNumberInfo> _map = pageNumber => PageNumberInfo.From(number: pageNumber, currentPage: withCurrentPageNumberSelected);

        if (areThereLessPagesThanMaxPageCountThatCanBeDisplayed)
        {
            foreach (var pageInfo in GetSequenceWhenPageCountIsLessThanMaxPagesThatCanBeDisplayed(fromNumberOfPagesRetrieved).Select(_map))
                yield return pageInfo;
        }
        else
        {
            yield return new(PageLabel: "|<<", PageNumber: 1, IsItCurrentPageSelected: false);

            if (isCurrentPageAtTheFirstLeftSegment)
                foreach (var pageInfo in GetSequenceForFirstLeftSegment(maxNumberOfPageNumbersThatCanBeDisplayed).Select(_map)) yield return pageInfo;
            
            if(isCurrentPageAtMiddleSegment)
                foreach (var pageInfo in GetSequenceForMiddleSegment(withCurrentPageNumberSelected, maxNumberOfPageNumbersThatCanBeDisplayed).Select(_map)) yield return pageInfo;

            if(isCurrentPageAtLastSegmentToTheRight)
                foreach (var pageInfo in GetSequenceForLastSegmentAtTheRight(fromNumberOfPagesRetrieved, maxNumberOfPageNumbersThatCanBeDisplayed).Select(_map)) yield return pageInfo;
            
            yield return new(PageLabel: ">>|", PageNumber: fromNumberOfPagesRetrieved, IsItCurrentPageSelected: false);
        }
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
