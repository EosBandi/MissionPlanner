using System;

namespace MPBackend.Core
{
    /// <summary>
    /// Well-known event topics published by the backend.
    /// </summary>
    public static class Topics
    {
        public const string Links = "links";
        public const string Vehicles = "vehicles";
        public const string Status = "status";
        public const string Jobs = "jobs";
        public const string Params = "params";
        public const string Missions = "missions";
        public const string Calibration = "calibration";
        public const string Rtk = "rtk";
        public const string Adsb = "adsb";
    }

    /// <summary>
    /// In-process pub/sub used to fan backend events out to API clients (WebSocket etc).
    /// Handlers are invoked synchronously on the publishing thread and must not block.
    /// </summary>
    public sealed class EventHub
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(EventHub));

        public event Action<string, object> Published;

        public void Publish(string topic, object data)
        {
            var handlers = Published;
            if (handlers == null)
                return;

            foreach (Action<string, object> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(topic, data);
                }
                catch (Exception ex)
                {
                    log.Error($"event handler failed for {topic}", ex);
                }
            }
        }
    }
}
