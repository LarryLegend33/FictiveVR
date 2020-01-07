using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace PatchCommander.Hardware
{
    /// <summary>
    /// Provides unified access to hardware interacting objects
    /// </summary>
    static class HardwareManager
    {
        public static DAQ DaqBoard = new DAQ();
    }
}
