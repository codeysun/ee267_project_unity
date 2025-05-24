// ControlMessages.cs
using System;
using UnityEngine;

public static class ControlMessages
{
    // Events for different control types
    //public static event Action<Vector2> OnThumbstickLeftMoved;
    //public static event Action<Vector2> OnThumbstickRightMoved;
    //public static event Action<bool> OnTriggerLeftPressed;
    //public static event Action<bool> OnTriggerRightPressed;
    public static event Action<bool> OnThumbstickPressed;

    // Methods to trigger the events
    public static void SendThumbstickPressed(bool isPressed)
    {
        OnThumbstickPressed?.Invoke(isPressed);
    }

    //public static void SendThumbstickRightMovement(Vector2 direction)
    //{
    //    OnThumbstickRightMoved?.Invoke(direction);
    //}

    //public static void SendTriggerLeftPressed(bool isPressed)
    //{
    //    OnTriggerLeftPressed?.Invoke(isPressed);
    //}

    //public static void SendTriggerRightPressed(bool isPressed)
    //{
    //    OnTriggerRightPressed?.Invoke(isPressed);
    //}
}