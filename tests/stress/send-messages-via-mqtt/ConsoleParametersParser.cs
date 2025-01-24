using System;
using System.Linq;
using System.Collections.Generic;

namespace SendMessagesViaMqtt;

internal static class ConsoleParametersParser
{
    public record struct ParametersForSpecificScenario(int DeviceCount, int NumberOfMetricsPerDevice, bool ShouldBeVerbose, int VelocityPerMinuteForEachDevice,
        string WorkingForProductId, string TargetMqttServer, bool ShouldItRandomlySendFailingQualityMessages);
    private record Parameter(string Name, string Value);

    private class ParameterNames
    {
        public const string ForDeviceCount = "--dev-count";
        public const string ForNumberOfMetricsPerDevice = "--met-per-dev";
        public const string ForShouldBeVerbose = "--verbose";
        public const string ForVelocityPerMinuteForEachDevice = "--velocity-per-min";
        public const string ForWorkingForProductId = "--prod-id";
        public const string ForTargetMqttServer = "--mqtt-server";
        public const string ForShouldItRandomlySendFailingQualityMessages = "--with-random-failing-quality-messages";
    }

    private class DefaultValues
    {
        public const int ForDeviceCount = 1;
        public const int ForNumberOfMetricsPerDevice = 60;
        public const bool ForShouldBeVerbose = false;
        public const int ForVelocityPerMinuteForEachDevice = 600;
        public const string ForWorkingForProductId = "--002";
        public const string ForTargetMqttServer = "localhost";
        public const bool ForShouldItRandomlySendFailingQualityMessages = false;
    }

    public static ParametersForSpecificScenario? GetParametersForSpecificScenario(string[] fromConsoleArguments)
    {
        var expectedParamsParsed = ParseExpectedParameters(fromConsoleArguments);
        return expectedParamsParsed.Any()
            ? Map(expectedParamsParsed)
            : null;
    }

    private static IEnumerable<Parameter> ParseExpectedParameters(string[] fromConsoleArguments)
    {
        var areTherePairsOfValues = fromConsoleArguments.Length % 2 == 0;
        if (areTherePairsOfValues == false) yield break;

        for(var i = 0; i < fromConsoleArguments.Length; i += 2)
        {
            (var paramName, var paramValue) = (fromConsoleArguments[i], fromConsoleArguments[i + 1]);
            if (IsParamIsExpected(paramName) == false) continue;

            yield return new Parameter(Name: paramName, Value: paramValue);
        }
    }

    private static bool IsParamIsExpected(string paramName)
    {
        if (paramName == ParameterNames.ForDeviceCount) return true;
        if (paramName == ParameterNames.ForNumberOfMetricsPerDevice) return true;
        if (paramName == ParameterNames.ForShouldBeVerbose) return true;
        if (paramName == ParameterNames.ForVelocityPerMinuteForEachDevice) return true;
        if (paramName == ParameterNames.ForWorkingForProductId) return true;
        if (paramName == ParameterNames.ForTargetMqttServer) return true;
        if (paramName == ParameterNames.ForShouldItRandomlySendFailingQualityMessages) return true;

        return false;
    }

    private static ParametersForSpecificScenario Map(IEnumerable<Parameter> expectedParamsParsed) =>
        new ParametersForSpecificScenario(
            DeviceCount: expectedParamsParsed.FirstOrDefault(p => p.Name == ParameterNames.ForDeviceCount).TryParseInt() ?? DefaultValues.ForDeviceCount,
            NumberOfMetricsPerDevice: expectedParamsParsed.FirstOrDefault(p => p.Name == ParameterNames.ForNumberOfMetricsPerDevice).TryParseInt() ?? DefaultValues.ForNumberOfMetricsPerDevice,
            ShouldBeVerbose: expectedParamsParsed.FirstOrDefault(p => p.Name == ParameterNames.ForShouldBeVerbose).TryParseBoolean() ?? DefaultValues.ForShouldBeVerbose,
            VelocityPerMinuteForEachDevice: expectedParamsParsed.FirstOrDefault(p => p.Name == ParameterNames.ForVelocityPerMinuteForEachDevice).TryParseInt() ?? DefaultValues.ForVelocityPerMinuteForEachDevice,
            WorkingForProductId: expectedParamsParsed.FirstOrDefault(p => p.Name == ParameterNames.ForWorkingForProductId).TryGetString() ?? DefaultValues.ForWorkingForProductId,
            TargetMqttServer: expectedParamsParsed.FirstOrDefault(p => p.Name == ParameterNames.ForTargetMqttServer).TryGetString() ?? DefaultValues.ForTargetMqttServer,
            ShouldItRandomlySendFailingQualityMessages: expectedParamsParsed.FirstOrDefault(p => p.Name == ParameterNames.ForShouldItRandomlySendFailingQualityMessages).TryParseBoolean() ?? DefaultValues.ForShouldItRandomlySendFailingQualityMessages
        );

    private static int? TryParseInt(this Parameter fromParameter)
    {
        if (fromParameter == null) return null;
        
        if (string.IsNullOrEmpty(fromParameter.Value)) return null;

        try
        {
            return Convert.ToInt32(fromParameter.Value);
        }
        catch
        {
            return null;
        }
    }

    private static string TryGetString(this Parameter fromParameter)
    {
        if (fromParameter == null) return null;

        if (string.IsNullOrEmpty(fromParameter.Value)) return null;

        return fromParameter.Value;
    }

    private static bool? TryParseBoolean(this Parameter fromParameter)
    {
        if (fromParameter == null) return null;

        if (string.IsNullOrEmpty(fromParameter.Value)) return null;

        try
        {
            return Convert.ToBoolean(fromParameter.Value);
        }
        catch
        {
            return null;
        }
    }
}
