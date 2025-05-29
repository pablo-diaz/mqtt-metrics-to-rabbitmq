using System.IO;
using System.Linq;
using System.Collections.Generic;

using Microsoft.Extensions.Options;

namespace StopReasons.Services;

public sealed class ServiceToFilterDevicesByLineOfBusiness
{
    private sealed record DeviceId(string Id);
    private sealed record LineOfBusiness(string Name, List<DeviceId> Devices);

    private readonly List<LineOfBusiness> _mapOfLinesAndDevices;

    public ServiceToFilterDevicesByLineOfBusiness(IOptions<AvailabilityStateManagerConfig> config)
    {
        _mapOfLinesAndDevices = LoadLineOfBusinessNamesWithTheirRelatedDevices(fromFile: config.Value.FilePathOfDeviceInfo);
    }

    public List<string> ListAllLineOfBusiness() =>
        _mapOfLinesAndDevices
        .Select(lob => lob.Name)
        .Order()
        .ToList();

    public List<string> ListDeviceIds(string boundToLineOfBusiness) =>
        _mapOfLinesAndDevices
        .Where(lob => lob.Name == boundToLineOfBusiness)
        .SelectMany(lob => lob.Devices.Select(device => device.Id))
        .ToList();

    private List<LineOfBusiness> LoadLineOfBusinessNamesWithTheirRelatedDevices(string fromFile)
    {
        List<LineOfBusiness> result = [];
        var lineCount = 0;
        var columnIndexForLineName = -1;
        foreach (var line in File.ReadAllLines(fromFile))
        {
            lineCount++;
            if (lineCount == 1) // is CSV Header
            {
                columnIndexForLineName = GetColumnIndexForLineName(fromHeaderLine: line);
                continue;
            }

            if (line.Trim().Length == 0) continue;

            var (deviceId, lineName) = ParseCsvLine(line, columnIndexForLineName);

            if (result.Any(lob => lob.Name == lineName) == false)
                result.Add(new LineOfBusiness(Name: lineName, Devices: []));

            result.First(lob => lob.Name == lineName).Devices.Add(new DeviceId(Id: deviceId));
        }

        return result;
    }

    private static int GetColumnIndexForLineName(string fromHeaderLine) =>
        fromHeaderLine.Split(separator: ',').ToList().IndexOf(item: "linea");

    private static (string DeviceId, string LineOfBusinessName) ParseCsvLine(string contentLine, int columnIndexForLineName)
    {
        var lineContent = contentLine.Split(separator: ',');
        return (
            DeviceId: lineContent.First(),
            LineOfBusinessName: lineContent[columnIndexForLineName]
        );
    }

}
