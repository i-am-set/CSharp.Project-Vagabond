using System;
using System.Collections.Generic;

namespace ProjectVagabond
{
    /// <summary>
    /// A simple static event bus for decoupled communication between different parts of the application.
    /// Systems can publish events without needing to know who is listening.
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> _events = new Dictionary<Type, Delegate>();

        /// <summary>
        /// Subscribes a listener to a specific type of event.
        /// </summary>
        /// <typeparam name="T">The type of the event to listen for.</typeparam>
        /// <param name="listener">The action to execute when the event is published.</param>
        public static void Subscribe<T>(Action<T> listener)
        {
            var eventType = typeof(T);
            if (_events.TryGetValue(eventType, out var existingDelegate))
            {
                _events[eventType] = Delegate.Combine(existingDelegate, listener);
            }
            else
            {
                _events[eventType] = listener;
            }
        }

        /// <summary>
        /// Unsubscribes a listener from a specific type of event.
        /// </summary>
        /// <typeparam name="T">The type of the event to unsubscribe from.</typeparam>
        /// <param name="listener">The action to remove.</param>
        public static void Unsubscribe<T>(Action<T> listener)
        {
            var eventType = typeof(T);
            if (_events.TryGetValue(eventType, out var existingDelegate))
            {
                var newDelegate = Delegate.Remove(existingDelegate, listener);
                if (newDelegate == null)
                {
                    _events.Remove(eventType);
                }
                else
                {
                    _events[eventType] = newDelegate;
                }
            }
        }

        /// <summary>
        /// Publishes an event to all subscribed listeners.
        /// </summary>
        /// <typeparam name="T">The type of the event being published.</typeparam>
        /// <param name="eventArgs">The event data to pass to listeners.</param>
        public static void Publish<T>(T eventArgs)
        {
            var eventType = typeof(T);
            if (_events.TryGetValue(eventType, out var existingDelegate))
            {
                (existingDelegate as Action<T>)?.Invoke(eventArgs);
            }
        }
    }
}