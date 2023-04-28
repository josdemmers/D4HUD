using System.Drawing;

namespace D4HUD.Entities
{
    public class ROICaptureInfo
    {
        public string Id { get; set; } = string.Empty;
        public float Left { get; set; }
        public float Top { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public Bitmap Bitmap { get; set; }
    }
}
