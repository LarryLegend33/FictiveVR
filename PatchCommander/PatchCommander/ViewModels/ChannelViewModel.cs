using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

using PatchCommander.Hardware;
using MHApi.GUI;
using MHApi.Threading;
using NationalInstruments.Controls;

namespace PatchCommander.ViewModels
{
    class ClampModeChangedArgs
    {
        public readonly int ChannelIndex;

        public readonly DAQ.ClampMode Mode;

        public ClampModeChangedArgs(int index, DAQ.ClampMode mode)
        {
            ChannelIndex = index;
            Mode = mode;
        }
    }

    class SealTestChangedArgs
    {
        public readonly int ChannelIndex;

        public readonly bool SealTest;

        public SealTestChangedArgs(int index, bool test)
        {
            ChannelIndex = index;
            SealTest = test;
        }
    }

    /// <summary>
    /// View model for one fully controllable
    /// acquisition channel
    /// </summary>
    class ChannelViewModel : ViewModelBase
    {
        #region Members

        /// <summary>
        /// Our 0-based channel index
        /// </summary>
        int _channelIndex;

        /// <summary>
        /// Collection of values to plot on the live plot
        /// </summary>
        ChartCollection<double> _plotData_live;

        /// <summary>
        /// Producer consumer for live plotting
        /// </summary>
        ProducerConsumer<double> _liveDump;

        /// <summary>
        /// Collection of values to plot on the seal test plot
        /// </summary>
        ChartCollection<double, double> _plotData_seal;

        /// <summary>
        /// Producer consumer for time-aligned seal test
        /// on channel 1
        /// </summary>
        ProducerConsumer<IndexedSample> _sealTestDump;

        /// <summary>
        /// UI update timer for all live plots
        /// </summary>
        DispatcherTimer _displayUpdater;

        /// <summary>
        /// Running samples for binning down
        /// from sampling rate to 2kHz display rate
        /// </summary>
        double[] _chartRunningSamples;

        /// <summary>
        /// Running chart sample index to allow binning
        /// </summary>
        long _chartRunningCount;

        /// <summary>
        /// True when performing seal test
        /// </summary>
        bool _sealTest;

        /// <summary>
        /// Seal resistance
        /// </summary>
        double _rSeal;

        /// <summary>
        /// Membrane resistance
        /// </summary>
        double _rMemb;

        /// <summary>
        /// X-Axis range on the seal test plot
        /// </summary>
        private Range<double> _sealXRange = new Range<double>(0, 100);

        /// <summary>
        /// Array to accumulate seal test samples
        /// for alignment and averaging
        /// </summary>
        double[] _sealTestAccum;

        /// <summary>
        /// Array containing the times of all seal
        /// test samples
        /// </summary>
        double[] _sealTestTime;

        #endregion

        public ChannelViewModel()
        {
            if (IsInDesignMode)
                return;
            //Subscribe to start stop events on main
            MainViewModel.Start += StartAcquisition;
            MainViewModel.Stop += StopAcquisition;
            //Set up char collections
            _plotData_live = new ChartCollection<double>(2000);
            _plotData_seal = new ChartCollection<double, double>();
            //Create buffer with 1s worth of storage
            _liveDump = new ProducerConsumer<double>(HardwareSettings.DAQ.Rate);
            _sealTestDump = new ProducerConsumer<IndexedSample>(HardwareSettings.DAQ.Rate);
            _displayUpdater = new DispatcherTimer();
            _displayUpdater.Tick += TimerEvent;
            _displayUpdater.Interval = new TimeSpan(0, 0, 0, 0, 5);
            _displayUpdater.Start();
            RaisePropertyChanged(nameof(VC));
            if (ClampModeChanged != null)
                ClampModeChanged.Invoke(new ClampModeChangedArgs(ChannelIndex, VC ? DAQ.ClampMode.VoltageClamp : DAQ.ClampMode.CurrentClamp));
        }

        /// <summary>
        /// Our display update timer event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TimerEvent(object sender, EventArgs e)
        {
            UpdateLivePlot();
            if (SealTest)
                UpdateSealTest();
        }

        #region Properties

        /// <summary>
        /// Membrane resistance
        /// </summary>
        public double RMembrane
        {
            get
            {
                return _rMemb;
            }
            set
            {
                _rMemb = value;
                RaisePropertyChanged(nameof(RMembrane));
            }
        }

        /// <summary>
        /// Seal resistance
        /// </summary>
        public double RSeal
        {
            get
            {
                return _rSeal;
            }
            set
            {
                _rSeal = value;
                RaisePropertyChanged(nameof(RSeal));
            }
        }

        /// <summary>
        /// Plot data of live plot
        /// </summary>
        public ChartCollection<double> PlotData_Live
        {
            get
            {
                return _plotData_live;
            }
            set
            {
                _plotData_live = value;
                RaisePropertyChanged(nameof(PlotData_Live));
            }
        }

        /// <summary>
        /// Plot data of triggered plot
        /// </summary>
        public ChartCollection<double, double> PlotData_Seal
        {
            get
            {
                return _plotData_seal;
            }
            set
            {
                _plotData_seal = value;
                RaisePropertyChanged(nameof(PlotData_Seal));
            }
        }

        /// <summary>
        /// Indicates and sets whether the channel is in voltage or current clamp mode
        /// </summary>
        public bool VC
        {
            get
            {
                if (IsInDesignMode)
                    return true;
                if(ChannelIndex == 0)
                    return HardwareManager.DaqBoard.Channel1Mode == DAQ.ClampMode.VoltageClamp;
                else
                    return HardwareManager.DaqBoard.Channel2Mode == DAQ.ClampMode.VoltageClamp;
            }
            set
            {
                if (IsInDesignMode)
                    return;
                DAQ.ClampMode mode;
                if (value)
                    mode = DAQ.ClampMode.VoltageClamp;
                else
                    mode = DAQ.ClampMode.CurrentClamp;
                if (ChannelIndex == 0)
                    HardwareManager.DaqBoard.Channel1Mode = mode;
                else
                    HardwareManager.DaqBoard.Channel2Mode = mode;
                if (ClampModeChanged != null)
                    ClampModeChanged.Invoke(new ClampModeChangedArgs(ChannelIndex, mode));
                RaisePropertyChanged(nameof(VC));
                RaisePropertyChanged(nameof(UnitLabel));
                RaisePropertyChanged(nameof(UnitRange));
                RaisePropertyChanged(nameof(SealTest));
            }
        }

        /// <summary>
        /// Depending on current mode sets the y-axis unit label of charts
        /// </summary>
        public string UnitLabel
        {
            get
            {
                if (VC)
                    return "Current [pA]";
                else
                    return "Voltage [mV]";
            }
        }

        /// <summary>
        /// Depending on current mode sets the y-axis range of charts
        /// </summary>
        public Range<double> UnitRange
        {
            get
            {
                if (VC)
                    return new Range<double>(-2000, 2000);
                else
                    return new Range<double>(-100, 100);
            }
        }

        /// <summary>
        /// The X-Range of our triggered plot
        /// </summary>
        public Range<double> SealXRange
        {
            get
            {
                return _sealXRange;
            }
            set
            {
                _sealXRange = value;
                RaisePropertyChanged(nameof(SealXRange));
            }
        }

        /// <summary>
        /// If true, seal-test values will be generated
        /// when in voltage clamp - ignored in current clamp
        /// </summary>
        public bool SealTest
        {
            get
            {
                return _sealTest & VC;
            }
            set
            {
                _sealTest = value;
                RaisePropertyChanged(nameof(SealTest));
                if (SealTestChanged != null)
                    SealTestChanged.Invoke(new SealTestChangedArgs(ChannelIndex, value));
            }
        }

        public int ChannelIndex
        {
            get
            {
                return _channelIndex;
            }
            set
            {
                _channelIndex = value;
                //Add ourselves to our hacky dictionary for outside manipulation...
                ChannelVMDict[value] = this;
            }
        }

        #endregion

        #region Methods

        void StartAcquisition()
        {
            //Subscribe to sample acquisition event
            HardwareManager.DaqBoard.ReadDone += SampleAcquired;
        }

        void StopAcquisition()
        {
            //Unsubscribe from sample acquisition event
            HardwareManager.DaqBoard.ReadDone -= SampleAcquired;
        }

        /// <summary>
        /// Gets called by the UI timer to update the display of
        /// the live plot
        /// </summary>
        private void UpdateLivePlot()
        {
            int bin_factor = HardwareSettings.DAQ.Rate / 2000;
            int c = _liveDump.Count;
            if (c < bin_factor)
                return;
            if (_chartRunningSamples == null || _chartRunningSamples.Length != bin_factor)
                _chartRunningSamples = new double[bin_factor];
            //we take only a number of samples that evenly fits
            //into our binning
            int to_take = c - (c % bin_factor);
            //create the appropriate chart values as the absolute maximum within the bin
            double[] values = new double[to_take / bin_factor];
            for (int i = 0; i < to_take; i++)
            {
                _chartRunningSamples[_chartRunningCount % bin_factor] = _liveDump.Consume();
                _chartRunningCount++;
                if (_chartRunningCount % bin_factor == 0)
                {
                    int maxIndex = -1;
                    double absMax = -1;
                    for (int j = 0; j < bin_factor; j++)
                    {
                        if (Math.Abs(_chartRunningSamples[j]) > absMax)
                        {
                            absMax = Math.Abs(_chartRunningSamples[j]);
                            maxIndex = j;
                        }
                    }
                    //add our binned value after conversion into mV (current clamp) or pA (voltage clamp)
                    var daqvolts = _chartRunningSamples[maxIndex];
                    if (!VC)
                        daqvolts = HardwareSettings.DAQ.DAQV_to_mV(daqvolts);
                    else
                        daqvolts = HardwareSettings.DAQ.DAQV_to_pA(daqvolts);
                    values[i / bin_factor] = daqvolts;
                }
            }
            PlotData_Live.Append(values);
        }

        /// <summary>
        /// Gets called to update our triggered plot with seal-test samples
        /// </summary>
        private void UpdateSealTest()
        {
            //Some constants that should be settable later
            int sealTestFreq = 10;
            int sealTestSamples = HardwareSettings.DAQ.Rate / sealTestFreq;
            int sealTestOnSamples = sealTestSamples / 2;
            int nAccum = sealTestFreq / 2;
            // We want to align plotting of the seal test, such that the
            // current spikes at start and end of step are in the middle of the plot
            // i.e. such that the middle of the OnSamples is in the middle of the plot
            int zeroPoint = sealTestSamples - sealTestOnSamples / 2;
            if (_sealTestAccum == null || _sealTestAccum.Length != sealTestSamples)
            {
                _sealTestAccum = new double[sealTestSamples];
                _sealTestTime = new double[sealTestSamples];
                for (int i = 0; i < sealTestSamples; i++)
                    _sealTestTime[i] = (1000.0 / HardwareSettings.DAQ.Rate) * i;
                PlotData_Seal.Capacity = sealTestSamples;
                SealXRange = new Range<double>(0, 1000.0 / sealTestFreq);
            }
            // consume samples
            int c = _sealTestDump.Count;
            if (c < sealTestSamples)
                return;
            for (int i = 0; i < c; i++)
            {
                var sample = _sealTestDump.Consume();
                //check if this sample belongs to a new accumulation window - if yes, plot and reset accumulator
                long window_index = (sample.Index + zeroPoint) % (sealTestSamples * nAccum);
                if (window_index == 0)
                {
                    //update calculated resistances
                    double currMax = (_sealTestAccum.Max() - _sealTestAccum.Min()) / 2;
                    //Seal resistance = voltage_step / max_current
                    RSeal = (10e-3 / (currMax * 1e-12)) / 1e6; // Resistance in MOhm
                    //Membrane resistance = voltage_step / steady_state_current
                    double currSS = 0;//The steady state current (seal test peak)
                    double currMean = 0;//The mean current (start of seal test window)
                    for (int j = sealTestOnSamples - 10; j <= sealTestOnSamples + 10; j++)
                    {
                        currSS += _sealTestAccum[j] / 21;
                    }
                    for (int j = 0; j <= 20; j++)
                    {
                        currMean += _sealTestAccum[j] / 21;
                    }
                    RMembrane = (10e-3 / ((currSS-currMean) * 1e-12)) / 1e6; // Resistance in MOhm
                    //plot
                    PlotData_Seal.Clear();
                    for (int j = 0; j < _sealTestAccum.Length; j++)
                        _sealTestAccum[j] -= currMean;
                    PlotData_Seal.Append(_sealTestTime, _sealTestAccum);
                    //reset accumulator
                    for (int j = 0; j < sealTestSamples; j++)
                        _sealTestAccum[j] = 0;
                }
                //add new samples to array
                int array_index = (int)((sample.Index % sealTestSamples - zeroPoint + sealTestSamples) % sealTestSamples);
                _sealTestAccum[array_index] += HardwareSettings.DAQ.DAQV_to_pA(sample.Sample / nAccum);
            }
        }

        /// <summary>
        /// Event handler whenever a bunch of new samples is acquired
        /// </summary>
        /// <param name="samples">The samples received</param>
        void SampleAcquired(ReadDoneEventArgs args)
        {
            for (int i = 0; i < args.Data.GetLength(1); i++)
            {
                _liveDump.Produce(args.Data[ChannelIndex, i]);
                if (SealTest)
                    _sealTestDump.Produce(new IndexedSample(args.Data[ChannelIndex, i], args.StartIndex + i));
            }
        }

        #endregion

        #region Events

        public static event Action<ClampModeChangedArgs> ClampModeChanged;

        public static event Action<SealTestChangedArgs> SealTestChanged;

        #endregion

        public static Dictionary<int, ChannelViewModel> ChannelVMDict = new Dictionary<int, ChannelViewModel>();

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            StopAcquisition();
        }
    }
}
