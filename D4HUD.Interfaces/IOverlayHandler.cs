using D4HUD.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D4HUD.Interfaces
{
    public interface IOverlayHandler
    {
        int CoordsMouseX { get; }
        int CoordsMouseY { get; }

        List<ROICaptureInfo> GetROIInfo();
    }
}
