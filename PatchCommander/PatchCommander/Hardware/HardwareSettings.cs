using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PatchCommander.Hardware
{
    static class HardwareSettings
    {
        public static class DAQ
        {
            public const string DeviceName = "Dev1";

            public const string Ch1Read = "ai0";

            public const string Ch1CommandRead = "ai4";

            public const string Ch1ModeRead = "ai2";

            public const string Ch2Read = "ai1";

            public const string Ch2CommandRead = "ai5";

            public const string Ch2ModeRead = "ai3";

            public const string LaserRead = "ai16";

            public const int Rate = 20000;

            public const string Ch1Mode = "port0/line0";

            public const string Ch2Mode = "port0/line1";

            /// <summary>
            /// Converts DAQ voltage to pico-amps
            /// </summary>
            /// <param name="daqVolts">The voltage read on the DAQ</param>
            /// <returns>Current in pA</returns>
            /// 
            public static double DAQV_to_pA(double daqVolts)
            {
                return daqVolts * 2000; //0.5V per nA - voltage clamp
            }

            /// <summary>
            /// Converts DAQ voltage to milli-volts
            /// </summary>
            /// <param name="daqVolts">The voltage read on the DAQ</param>
            /// <returns>Voltage in mV</returns>
            public static double DAQV_to_mV(double daqVolts)
            {
                return daqVolts * 100.0; //10mV per mv - current clamp
            }
        }
    }
}
