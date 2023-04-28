using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D4HUD.Entities
{
    public class PersistOverlayObject
    {
        public string Id { get; set; } = string.Empty;
        public float Left { get; set; }
        public float Top { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
    }
}
