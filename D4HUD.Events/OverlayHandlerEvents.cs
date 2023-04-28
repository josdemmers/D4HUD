using Prism.Events;

namespace D4HUD.Events
{
    public class MenuLockedEvent : PubSubEvent<MenuLockedEventParams>
    {
    }

    public class MenuLockedEventParams
    {
        public string Id { get; set; } = string.Empty;
    }

    public class MenuUnlockedEvent : PubSubEvent<MenuUnlockedEventParams>
    {
    }

    public class MenuUnlockedEventParams
    {
        public string Id { get; set; } = string.Empty;
    }

    public class ROILockedEvent : PubSubEvent<ROILockedEventParams>
    {

    }

    public class ROILockedEventParams
    {
        public string Id { get; set; } = string.Empty;
    }

    public class ConfigPanelEvent : PubSubEvent<ConfigPanelEventParams>
    {

    }

    public class ConfigPanelEventParams
    {
        public string Id { get; set; } = string.Empty;
        public string Property { get; set; } = string.Empty;
        public bool Increase { get; set; }
    }

    public class InterfaceLockedEvent : PubSubEvent<InterfaceLockedEventParams>
    {

    }

    public class InterfaceLockedEventParams
    {
        public string Id { get; set; } = string.Empty;
    }

    public class ROIUpdatedEvent : PubSubEvent<ROIUpdatedEventParams>
    {

    }

    public class ROIUpdatedEventParams
    {
        public string Id { get; set; } = string.Empty;
        public float Left { get; set; }
        public float Top { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
    }
}
