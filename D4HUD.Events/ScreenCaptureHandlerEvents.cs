using Prism.Events;

namespace D4HUD.Events
{
    public class ScreenUpdatedEvent : PubSubEvent
    {

    }

    public class MouseUpdatedEvent : PubSubEvent<MouseUpdatedEventParams>
    {

    }

    public class MouseUpdatedEventParams
    {
        public int CoordsMouseX { get; set; }
        public int CoordsMouseY { get; set; }
    }

    public class WindowHandleUpdatedEvent : PubSubEvent<WindowHandleUpdatedEventParams> 
    {

    }

    public class WindowHandleUpdatedEventParams
    {
        public IntPtr WindowHandle { get; set; }
    }
}