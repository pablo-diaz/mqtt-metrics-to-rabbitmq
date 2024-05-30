using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SendMessagesViaMqtt;

public sealed class KeyboardService
{
    private Dictionary<ConsoleKey, Func<bool, bool>> _keyPressedHandlers = new();
    private bool _shouldItKeepRunningKeyboardListenerTask = true;

    public async Task RunKeyboardListeners()
    {
        _shouldItKeepRunningKeyboardListenerTask = true;
        while(_shouldItKeepRunningKeyboardListenerTask)
        {
            await Task.Delay(millisecondsDelay: 500);

            if(Console.KeyAvailable)
            {
                var keyPressed = Console.ReadKey(intercept: true);
                if(_keyPressedHandlers.ContainsKey(keyPressed.Key) == false)
                    continue;

                var wasItPressedWithControlKey = (keyPressed.Modifiers & ConsoleModifiers.Control) != 0;
                var shouldItStopListeningForKeyPressedEvents = _keyPressedHandlers[keyPressed.Key](wasItPressedWithControlKey);
                if(shouldItStopListeningForKeyPressedEvents)
                    _shouldItKeepRunningKeyboardListenerTask = false;
            }
        }
    }

    public void AddKeyboardListener(ConsoleKey forKey, Func<bool, bool> callbackFn, string withMessage = null)
    {
        if(string.IsNullOrEmpty(withMessage) == false)
            Console.WriteLine(withMessage);

        _keyPressedHandlers[forKey] = callbackFn;
    }

    public void RemoveKeyboardListener(ConsoleKey forKey)
    {
        if(_keyPressedHandlers.ContainsKey(forKey))
            _keyPressedHandlers.Remove(forKey);
    }

    public void StopKeyboardListener()
    {
        _shouldItKeepRunningKeyboardListenerTask = false;
    }
}