using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SendMessagesViaMqtt;

public sealed class AvailabilityScenario: ITestingScenario
{
    public string Name { get; init; }
    public string ClientId { get; init; }
    public int DeviceCount { get; init; }
    public int MetricCountPerDevice { get; init; }
    public DateTime StartingFromDate { get; init; }
    public Func<int> MillisecondsToWaitWhileSendingEachMessageFn { get; init; }
    public Action<(ConsoleKey forKey, Func<bool, bool> callbackFn, string withMessage)> AddKeyboardListener { get; init; }
    public Action<ConsoleKey> RemoveKeyboardListener { get; init; }

    private const string _StopReasonNotDefinedYetByDeviceUser = "-";
    private const string _TargetMqttTopicName = "disponibilidad/principal";
    private readonly string[] _DowntimeReasons = new string[] {"001", "002", "003", "004", "005", "006", "007", "008", "009"};

    private readonly Dictionary<int, (ConsoleKey Key, bool IsItAvailableNow, string MaybeStopReason)> _keyboardsKeysForDevices = new() {
        { 1, (Key: ConsoleKey.A, IsItAvailableNow: true, MaybeStopReason: null) }, { 2, (Key: ConsoleKey.S, IsItAvailableNow: true, MaybeStopReason: null) },
        { 3, (Key: ConsoleKey.D, IsItAvailableNow: true, MaybeStopReason: null) }, { 4, (Key: ConsoleKey.F, IsItAvailableNow: true, MaybeStopReason: null) },
        { 5, (Key: ConsoleKey.G, IsItAvailableNow: true, MaybeStopReason: null) }, { 6, (Key: ConsoleKey.H, IsItAvailableNow: true, MaybeStopReason: null) },
        { 7, (Key: ConsoleKey.J, IsItAvailableNow: true, MaybeStopReason: null) }, { 8, (Key: ConsoleKey.K, IsItAvailableNow: true, MaybeStopReason: null) },
        { 9, (Key: ConsoleKey.L, IsItAvailableNow: true, MaybeStopReason: null) }
    };

    public async Task RunAsync(Random usingRandomizer, CancellationToken token)
    {
        try
        {
            Console.WriteLine($"\n------ Running Availability scenario '{Name}' ---------");

            await using var broker = new Broker();
            await broker.ConnectAsync(ClientId, token);

            await Task.WhenAll(Enumerable.Range(start: 1, count: DeviceCount)
                                         .Select(deviceId => SendMetricsAsync(deviceId, broker, usingRandomizer, token))
                            );
        }
        catch(Exception ex)
        {
            Console.WriteLine($"General exception caught. Reason: {ex.Message}");
        }
    }

    private async Task SendMetricsAsync(int forDeviceId, Broker withBroker, Random usingRandomizer, CancellationToken token)
    {
        Func<bool> wasKeyListenerSetForDeviceId = () => _keyboardsKeysForDevices.ContainsKey(forDeviceId);
        if(wasKeyListenerSetForDeviceId())
            AddKeyboardListener(BindKeyToDeviceId(forDeviceId, usingRandomizer));

        var messagesToSend = GetMetrics(forDeviceId, isItAvailableFn: () => wasKeyListenerSetForDeviceId()
                ? (Available: _keyboardsKeysForDevices[forDeviceId].IsItAvailableNow, MaybeStopReason: _keyboardsKeysForDevices[forDeviceId].MaybeStopReason)
                : (Available: true, MaybeStopReason: null)
            ).Take(MetricCountPerDevice);

        foreach(var message in messagesToSend)
        {
            if(token.IsCancellationRequested)
                break;

            await withBroker.SendMessageAsync(targetBrokerTopic: _TargetMqttTopicName, message: message, token: token);
            Console.WriteLine($"MQTT message published to '{_TargetMqttTopicName}' topic with payload: '{message}'");

            await Task.Delay(millisecondsDelay: MillisecondsToWaitWhileSendingEachMessageFn());
        }

        if(wasKeyListenerSetForDeviceId())
            RemoveKeyboardListener(_keyboardsKeysForDevices[forDeviceId].Key);
    }

    private (ConsoleKey forKey, Func<bool, bool> callbackFn, string withMessage) BindKeyToDeviceId(int forDeviceId, Random usingRandomizer) =>
        (
            forKey: _keyboardsKeysForDevices[forDeviceId].Key,
            callbackFn: wasItPressedWithControlKey => {
                var newStatusOfIsItAvailableNow = !_keyboardsKeysForDevices[forDeviceId].IsItAvailableNow;
                var maybeStopReason = newStatusOfIsItAvailableNow
                                        ? null
                                        : wasItPressedWithControlKey
                                        ? _StopReasonNotDefinedYetByDeviceUser
                                        : GetNextDowntimeReason(usingRandomizer);
                _keyboardsKeysForDevices[forDeviceId] = (Key: _keyboardsKeysForDevices[forDeviceId].Key, IsItAvailableNow: newStatusOfIsItAvailableNow, MaybeStopReason: maybeStopReason);
                var stopRunningKeyPressedEvents = false;
                return stopRunningKeyPressedEvents;
            },
            withMessage: null
        );

    private IEnumerable<string> GetMetrics(int forDeviceId, Func<(bool Available, string MaybeStopReason)> isItAvailableFn)
    {
        var aDate = StartingFromDate;
        var wasItAvailableBefore = true;
        var nextDowntimeReason = "";

        while(true)
        {
            var deviceId = "Dev" + forDeviceId.ToString().PadLeft(totalWidth: 10, paddingChar: '0');
            (var isItAvailable, var maybeStopReason) = isItAvailableFn();
            var availability = isItAvailable ? "Produciendo" : "Parado";

            if(wasItAvailableBefore && isItAvailable == false)
                nextDowntimeReason = maybeStopReason;

            wasItAvailableBefore = isItAvailable;

            var downtimeReason = isItAvailable ? "-" : nextDowntimeReason;

            yield return $"{deviceId}@{availability}@{downtimeReason}@{aDate:yyyy-M-d@H_m_s}";

            aDate = aDate.AddSeconds(1);
        }
    }

    private string GetNextDowntimeReason(Random usingRandomizer)
    {
        var randomIndex = usingRandomizer.Next(minValue: 0, maxValue: _DowntimeReasons.Length - 1);
        return _DowntimeReasons[randomIndex];
    }

}
