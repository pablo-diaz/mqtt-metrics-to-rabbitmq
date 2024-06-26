using System;
using System.Threading.Tasks;
using System.Collections.Generic;

using ShouldItStopListeningForKeyPressedEvents = bool;

namespace SendMessagesViaMqtt;

public sealed class KeyboardService
{
    public delegate ShouldItStopListeningForKeyPressedEvents HandleKeyPressedEventFn(KeyPressedModifiers keyWasPressedMaybeWithSomeOtherControlKeys); 

    public record KeyPressedModifiers(bool WithCtrl, bool WithAlt, bool WithShift)
    {
        public bool NoModifiersWerePressed() => WithCtrl == false && WithAlt == false && WithShift == false;
    }

    private Dictionary<ConsoleKey, HandleKeyPressedEventFn> _keyPressedHandlers = new();
    private bool _shouldItKeepRunningKeyboardListenerTask = true;

    public async Task RunKeyboardListeners()
    {
        _shouldItKeepRunningKeyboardListenerTask = true;
        while(_shouldItKeepRunningKeyboardListenerTask)
        {
            await Task.Delay(millisecondsDelay: 50);

            if(Console.KeyAvailable)
            {
                var keyPressed = Console.ReadKey(intercept: true);
                if(_keyPressedHandlers.ContainsKey(keyPressed.Key) == false)
                    continue;

                var modifiers = new KeyPressedModifiers(
                    WithCtrl: (keyPressed.Modifiers & ConsoleModifiers.Control) != 0,
                    WithAlt: (keyPressed.Modifiers & ConsoleModifiers.Alt) != 0,
                    WithShift: (keyPressed.Modifiers & ConsoleModifiers.Shift) != 0);

                var shouldItStopListeningForKeyPressedEvents = _keyPressedHandlers[keyPressed.Key](modifiers);
                if(shouldItStopListeningForKeyPressedEvents)
                    _shouldItKeepRunningKeyboardListenerTask = false;
            }
        }
    }

    public void AddKeyboardListener(ConsoleKey forKey, HandleKeyPressedEventFn keyPressedHandler, string withMessage = null)
    {
        if(string.IsNullOrEmpty(withMessage) == false)
            Console.WriteLine(withMessage);

        _keyPressedHandlers[forKey] = keyPressedHandler;
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