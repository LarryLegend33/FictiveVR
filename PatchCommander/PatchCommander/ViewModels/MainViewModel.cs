using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using MHApi.GUI;
using MHApi.Threading;
using System.Threading;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using MHApi.Utilities;
using NationalInstruments.Controls;
using PatchCommander.Hardware;

namespace PatchCommander.ViewModels
{
    /// <summary>
    /// Represents an indexed sample
    /// </summary>
    struct IndexedSample
    {
        public double Sample;

        public long Index;

        public IndexedSample(double sample, long index)
        {
            Sample = sample;
            Index = index;
        }
    }

    /// <summary>
    /// Represents a data chunk for one channel
    /// </summary>
    struct ChannelReadDataChunk
    {
        public long StartIndex;

        public double[,] Data;
    }

    class MainViewModel : ViewModelBase
    {
        #region Members

        /// <summary>
        /// Cache of seal test samples
        /// </summary>
        double[] _stSamples;

        /// <summary>
        /// True if channel 1 is in voltage clamp
        /// </summary>
        bool _vc_ch1;

        /// <summary>
        /// True if channel 2 is in voltage clamp
        /// </summary>
        bool _vc_ch2;

        /// <summary>
        /// Indicates whether seal test should
        /// be run on channel 1
        /// </summary>
        bool _sealTest_ch1;

        /// <summary>
        /// Indicates whether seal test
        /// should be run on channel 2
        /// </summary>
        bool _sealTest_ch2;

        /// <summary>
        /// Indicates whether data is currently acquired from the DAQ board
        /// </summary>
        bool _isAcquiring;

        /// <summary>
        /// Indicates wheter data is currently written to file for Ch1
        /// </summary>
        bool _isRecordingCh1;

        /// <summary>
        /// The base filename for channel 1
        /// </summary>
        string _baseFNameCh1;

        /// <summary>
        /// Indicates whether holding voltage is requested for channel 1
        /// </summary>
        bool _holdingCh1;

        /// <summary>
        /// Indicates the desired holding voltage in mV for channel 1
        /// </summary>
        double _holdingVoltageCh1;

        /// <summary>
        /// Indicates whether current should be injected on channel 1
        /// </summary>
        bool _injectCh1;

        /// <summary>
        /// Indicates the desired current injection in pA for channel 1
        /// </summary>
        double _injectionCurrentCh1;

        /// <summary>
        /// Task to write data to file
        /// </summary>
        Task _recordTask;

        /// <summary>
        /// Indicates that a stimulation experiment is currently running
        /// </summary>
        bool _stimExpRunning;

        /// <summary>
        /// When recording, receives copy of the read data for asynchronous disk writing
        /// </summary>
        ProducerConsumer<ChannelReadDataChunk> _record_dataQueue;

        /// <summary>
        /// The number of milliseconds in the current step pre-phase
        /// </summary>
        uint _currStep_prePostMs;

        /// <summary>
        /// The number of milliseconds in each current step
        /// </summary>
        uint _currStep_stimMs;

        /// <summary>
        /// The number of current steps to perform
        /// </summary>
        uint _n_currSteps;

        /// <summary>
        /// The pico-amps of the first current step
        /// </summary>
        double _currStep_fistPico;

        /// <summary>
        /// The pico-amps of the last current step
        /// </summary>
        double _currStep_lastPico;

        /// <summary>
        /// The number of seconds in the laser stimulus pre and post phases
        /// </summary>
        uint _laserStim_prePostS;

        /// <summary>
        /// The number of seconds the laser stimulus should last
        /// </summary>
        uint _laserStim_stimS;

        /// <summary>
        /// The amplitude of the laser stimulus in mA
        /// </summary>
        double _laserStim_mA;

        /// <summary>
        /// The number of laser stimulus presentations to run
        /// </summary>
        uint _n_laserSteps;

        /// <summary>
        /// If true, laser steps will be presented in
        /// voltage clamp mode
        /// </summary>
        bool _laser_holdV;

        /// <summary>
        /// When holding the holding voltage to use during
        /// laser stimulation
        /// </summary>
        double _laser_holding_mV;

        const double mV_per_V = 20;

        const double pA_per_V = 400;

        #endregion

        public MainViewModel()
        {
            BaseFNameCh1 = "Fish_01";
            if (IsInDesignMode)
                return;
            _record_dataQueue = new ProducerConsumer<ChannelReadDataChunk>(HardwareSettings.DAQ.Rate);
            //Subscribe to channel view events
            ChannelViewModel.ClampModeChanged += ClampModeChanged;
            ChannelViewModel.SealTestChanged += SealTestChanged;
            //Set defaults for current stimulation
            NCurrSteps = 5;
            CurrStep_PrePostMs = 1000;
            CurrStep_StimMs = 10000;
            CurrStep_FirstPico = 0;
            CurrStep_LastPico = 100;
            NLaserSteps = 5;
            LaserHoldV = false;
            LaserHoldingmV = -40;
            LaserStim_mA = 2000;
            LaserStim_StimS = 20;
            LaserStim_PrePostS = 10;
        }

        #region Properties

        /// <summary>
        /// When holding the holding voltage to use during
        /// laser stimulation
        /// </summary>
        public double LaserHoldingmV
        {
            get
            {
                return _laser_holding_mV;
            }
            set
            {
                _laser_holding_mV = value;
                RaisePropertyChanged(nameof(LaserHoldingmV));
            }
        }

        /// <summary>
        /// If true, laser steps will be presented in
        /// voltage clamp mode
        /// </summary>
        public bool LaserHoldV
        {
            get
            {
                return _laser_holdV;
            }
            set
            {
                _laser_holdV = value;
                RaisePropertyChanged(nameof(LaserHoldV));
            }
        }

        /// <summary>
        /// The number of laser stimulus presentations to run
        /// </summary>
        public uint NLaserSteps
        {
            get
            {
                return _n_laserSteps;
            }
            set
            {
                _n_laserSteps = value;
                RaisePropertyChanged(nameof(NLaserSteps));
            }
        }

        /// <summary>
        /// The amplitude of the laser stimulus in mA
        /// </summary>
        public double LaserStim_mA
        {
            get
            {
                return _laserStim_mA;
            }
            set
            {
                _laserStim_mA = value;
                RaisePropertyChanged(nameof(LaserStim_mA));
            }
        }

        /// <summary>
        /// The number of seconds the laser stimulus should last
        /// </summary>
        public uint LaserStim_StimS
        {
            get
            {
                return _laserStim_stimS;
            }
            set
            {
                _laserStim_stimS = value;
                RaisePropertyChanged(nameof(LaserStim_StimS));
            }
        }

        /// <summary>
        /// The number of seconds in the laser stimulus pre and post phases
        /// </summary>
        public uint LaserStim_PrePostS
        {
            get
            {
                return _laserStim_prePostS;
            }
            set
            {
                _laserStim_prePostS = value;
                RaisePropertyChanged(nameof(LaserStim_PrePostS));
            }
        }

        /// <summary>
        /// The pico-amps of the first current step
        /// </summary>
        public double CurrStep_FirstPico
        {
            get
            {
                return _currStep_fistPico;
            }
            set
            {
                _currStep_fistPico = value;
                RaisePropertyChanged(nameof(CurrStep_FirstPico));
            }
        }

        /// <summary>
        /// The pico-amps of the last current step
        /// </summary>
        public double CurrStep_LastPico
        {
            get
            {
                return _currStep_lastPico;
            }
            set
            {
                _currStep_lastPico = value;
                RaisePropertyChanged(nameof(CurrStep_LastPico));
            }
        }

        /// <summary>
        /// The number of milliseconds in the current step pre-phase
        /// </summary>
        public uint CurrStep_PrePostMs
        {
            get
            {
                return _currStep_prePostMs;
            }
            set
            {
                _currStep_prePostMs = value;
                RaisePropertyChanged(nameof(CurrStep_PrePostMs));
            }
        }

        /// <summary>
        /// The number of milliseconds in each current step
        /// </summary>
        public uint CurrStep_StimMs
        {
            get
            {
                return _currStep_stimMs;
            }
            set
            {
                _currStep_stimMs = value;
                RaisePropertyChanged(nameof(CurrStep_StimMs));
            }
        }

        /// <summary>
        /// The number of current steps to perform
        /// </summary>
        public uint NCurrSteps
        {
            get
            {
                return _n_currSteps;
            }
            set
            {
                _n_currSteps = value;
                RaisePropertyChanged(nameof(NCurrSteps));
            }
        }

        public bool StimExpRunning
        {
            get
            {
                return _stimExpRunning;
            }
            set
            {
                _stimExpRunning = value;
                RaisePropertyChanged(nameof(StimExpRunning));
            }
        }

        /// <summary>
        /// Indicates if channel1 is in voltage clamp
        /// </summary>
        public bool VC_Channel1
        {
            get
            {
                return _vc_ch1;
            }
            private set
            {
                _vc_ch1 = value;
                RaisePropertyChanged(nameof(VC_Channel1));
            }
        }

        /// <summary>
        /// Indicates if channel2 is in voltage clamp
        /// </summary>
        public bool VC_Channel2
        {
            get
            {
                return _vc_ch2;
            }
            private set
            {
                _vc_ch2 = value;
                RaisePropertyChanged(nameof(VC_Channel2));
            }
        }

        /// <summary>
        /// Indicates if channel1 should produce seal test
        /// </summary>
        public bool SealTest_Channel1
        {
            get
            {
                return _sealTest_ch1;
            }
            private set
            {
                _sealTest_ch1 = value;
                RaisePropertyChanged(nameof(SealTest_Channel1));
            }
        }

        /// <summary>
        /// Indicates if channel2 should produce seal test
        /// </summary>
        public bool SealTest_Channel2
        {
            get
            {
                return _sealTest_ch2;
            }
            private set
            {
                _sealTest_ch2 = value;
                RaisePropertyChanged(nameof(SealTest_Channel2));
            }
        }

        /// <summary>
        /// Indicates whether data is currently being acquired from the daq board
        /// </summary>
        public bool IsAcquiring
        {
            get
            {
                return _isAcquiring;
            }
            set
            {
                _isAcquiring = value;
                RaisePropertyChanged(nameof(IsAcquiring));
            }
        }

        /// <summary>
        /// Indicates whether data is currently written to file for Ch1
        /// </summary>
        public bool IsRecordingCh1
        {
            get
            {
                return _isRecordingCh1;
            }
            set
            {
                _isRecordingCh1 = value;
                RaisePropertyChanged(nameof(IsRecordingCh1));
            }
        }

        /// <summary>
        /// The base filename for channel 1
        /// </summary>
        public string BaseFNameCh1
        {
            get
            {
                return _baseFNameCh1;
            }
            set
            {
                _baseFNameCh1 = value;
                RaisePropertyChanged(nameof(BaseFNameCh1));
            }
        }

        /// <summary>
        /// Indicates whether holding voltage should be applied to channel 1
        /// </summary>
        public bool HoldingCh1
        {
            get
            {
                return _holdingCh1;
            }
            set
            {
                _holdingCh1 = value;
                RaisePropertyChanged(nameof(HoldingCh1));
            }
        }

        /// <summary>
        /// Indicates the holding voltage of channel 1 in mV
        /// </summary>
        public double HoldingVoltageCh1
        {
            get
            {
                return _holdingVoltageCh1;
            }
            set
            {
                _holdingVoltageCh1 = value;
                RaisePropertyChanged(nameof(HoldingVoltageCh1));
            }
        }

        /// <summary>
        /// Indicates whether current should be injected on channel 1
        /// </summary>
        public bool InjectCh1
        {
            get
            {
                return _injectCh1;
            }
            set
            {
                _injectCh1 = value;
                RaisePropertyChanged(nameof(InjectCh1));
            }
        }

        /// <summary>
        /// The desired injection current in pA for channel 1
        /// </summary>
        public double InjectionCurrentCh1
        {
            get
            {
                return _injectionCurrentCh1;
            }
            set
            {
                _injectionCurrentCh1 = value;
                RaisePropertyChanged(nameof(InjectionCurrentCh1));
            }
        }

        #endregion

        #region Methods

        #region Button Handlers
        public void StartStop()
        {
            if (HardwareManager.DaqBoard.IsRunning)
                StopAcquisition();
            else
                StartAcquisition();
        }

        public void StartStopRecCh1()
        {
            if (IsRecordingCh1)
                StopRecording(1);
            else
                StartRecording(1);
        }

        public void RunCurrentStepsCh1()
        {
            StimExpRunning = true;
            //Stop any currently ongoing acquisition to launch fixed program
            if (HardwareManager.DaqBoard.IsRunning)
                StopAcquisition();
            //Change mode to current clamp
            VC_Channel1 = false;
            ChannelViewModel.ChannelVMDict[0].VC = false;
            //Subscribe to read finished signal
            HardwareManager.DaqBoard.ReadThreadFinished += DaqBoard_ReadThreadFinished;
            //Get everything ready to record - save experiment info as well
            Dictionary<string, string> exp_info = new Dictionary<string, string>();
            exp_info["Experiment type"] = "Current steps";
            exp_info["n_steps"] = NCurrSteps.ToString();
            exp_info["pre_post_ms"] = CurrStep_PrePostMs.ToString();
            exp_info["stim_ms"] = CurrStep_StimMs.ToString();
            exp_info["first_pA"] = CurrStep_FirstPico.ToString();
            exp_info["last_pA"] = CurrStep_LastPico.ToString();
            StartRecording(1, exp_info);
            //Notify and launch the board
            if (Start != null)
                Start.Invoke();
            long totalSamples = (NCurrSteps) * (CurrStep_StimMs + 2 * CurrStep_PrePostMs) * HardwareSettings.DAQ.Rate / 1000;
            HardwareManager.DaqBoard.Start((s, i) => { return genCurrentSteps(s, i, 1); }, totalSamples);
        }

        public void RunLaserStepsCh1()
        {
            StimExpRunning = true;
            //Stop any currently ongoing acquisition to launch fixed program
            if (HardwareManager.DaqBoard.IsRunning)
                StopAcquisition();
            //Adjust board mode depending on whether we should hold voltage or not
            VC_Channel1 = LaserHoldV;
            ChannelViewModel.ChannelVMDict[0].VC = LaserHoldV;
            //Subscribe to read finished signal
            HardwareManager.DaqBoard.ReadThreadFinished += DaqBoard_ReadThreadFinished;
            //Get everything ready to record - save experiment info as well
            Dictionary<string, string> exp_info = new Dictionary<string, string>();
            exp_info["Experiment type"] = "Laser steps";
            exp_info["n_steps"] = NLaserSteps.ToString();
            exp_info["pre_post_s"] = LaserStim_PrePostS.ToString();
            exp_info["stim_s"] = LaserStim_StimS.ToString();
            exp_info["stim_mA"] = LaserStim_mA.ToString();
            exp_info["hold_V"] = LaserHoldV ? "True" : "False";
            if (LaserHoldV)
                exp_info["holding_mV"] = LaserHoldingmV.ToString();
            StartRecording(1, exp_info);
            //Notify and launch the board
            if (Start != null)
                Start.Invoke();
            long totalSamples = (NLaserSteps) * (LaserStim_StimS + 2 * LaserStim_PrePostS) * HardwareSettings.DAQ.Rate;
            HardwareManager.DaqBoard.Start((s, i) => { return genLaserSteps(s, i, 1); }, totalSamples);
        }

        #endregion Button Handlers

        /// <summary>
        /// Write experiment information to an info file in form of a Pytyon dictionary initializer
        /// </summary>
        /// <param name="name">Experiment name</param>
        /// <param name="infoData">Dictionary with the experiment information</param>
        /// <param name="infoFile">The file-stream to write to</param>
        private void WriteExperimentInfo(string name, Dictionary<string, string> infoData, TextWriter infoFile)
        {
            //Preamble
            infoFile.WriteLine(name + "_info_d = {");
            foreach (string k in infoData.Keys)
            {
                infoFile.WriteLine("'{0}': '{1}',", k, infoData[k]);
            }
            //Finish
            infoFile.WriteLine("}");
        }

        /// <summary>
        /// Generates necessary analog out samples
        /// </summary>
        /// <param name="start_sample">The index of the first sample</param>
        /// <param name="nSamples">The number of samples to generate</param>
        /// <returns>For each analog out the appropriate voltage samples</returns>
        private double[,] genSamples(long start_sample, int nSamples)
        {
            double[,] samples = new double[2, nSamples];
            // Channel 1
            double offset = 0;//= (VC_Channel1 && HoldingCh1) ? milliVoltsToAOVolts(HoldingVoltageCh1) : 0;
            if (VC_Channel1)
            {
                //Set offset to holding voltage if requested
                if (HoldingCh1)
                    offset = milliVoltsToAOVolts(HoldingVoltageCh1);
            }
            else
            {
                //Set offset to injection current if requested
                if (InjectCh1)
                    offset = picoAmpsToAOVolts(InjectionCurrentCh1);
            }
            if (SealTest_Channel1 && VC_Channel1)
            {
                var sts = sealTestSamples(start_sample, nSamples);
                for (int i = 0; i < nSamples; i++)
                    samples[0, i] = sts[i] + offset;
            }
            else
            {
                for (int i = 0; i < nSamples; i++)
                {
                    samples[0, i] = offset;
                }
            }
            // Channel 2
            if (SealTest_Channel2 && VC_Channel2)
            {
                var sts = sealTestSamples(start_sample, nSamples);
                for (int i = 0; i < nSamples; i++)
                    samples[1, i] = sts[i];
            }
            else
            {
                for (int i = 0; i < nSamples; i++)
                {
                    samples[1, i] = 0;
                }
            }
            return samples;
        }

        /// <summary>
        /// Generates  analog out samples for current step presentation
        /// </summary>
        /// <param name="start_sample">The index of the first sample</param>
        /// <param name="nSamples">The number of samples to generate</param>
        /// <param name="channelIndex">The channel on which to generate samples</param>
        /// <returns>For each analog out the appropriate voltage samples</returns>
        private double[,] genCurrentSteps(long start_sample, int nSamples, int channelIndex)
        {
            long pre_post_samples = CurrStep_PrePostMs * HardwareSettings.DAQ.Rate / 1000;
            long stim_samples = CurrStep_StimMs * HardwareSettings.DAQ.Rate / 1000;
            long step_samples = stim_samples + 2 * pre_post_samples;
            double[,] samples = new double[2, nSamples];
            for(int i = 0; i<nSamples; i++)
            {
                long curr_sample = start_sample + i;
                long curr_step = curr_sample / step_samples;
                //If we are beyond the last step, or in the pre-phase, or in the post-phase set current to 0
                if (curr_step >= NCurrSteps || curr_sample % step_samples < pre_post_samples || curr_sample % step_samples > pre_post_samples+stim_samples)
                    samples[channelIndex - 1, i] = 0;
                else
                {
                    //Determine step-current: Note, we wan the first current to be exactly the user set value and the last current as well
                    double current = (CurrStep_LastPico - CurrStep_FirstPico) / (NCurrSteps-1) * curr_step + CurrStep_FirstPico;
                    samples[channelIndex - 1, i] = picoAmpsToAOVolts(current);
                }
            }
            return samples;
        }

        /// <summary>
        /// Generates  analog out samples for laser step presentation
        /// </summary>
        /// <param name="start_sample">The index of the first sample</param>
        /// <param name="nSamples">The number of samples to generate</param>
        /// <param name="channelIndex">The channel on which to generate samples</param>
        /// <returns>For each analog out the appropriate voltage samples</returns>
        private double[,] genLaserSteps(long start_sample, int nSamples, int channelIndex)
        {
            long pre_post_samples = LaserStim_PrePostS * HardwareSettings.DAQ.Rate;
            long stim_samples = LaserStim_StimS * HardwareSettings.DAQ.Rate;
            long step_samples = stim_samples + 2 * pre_post_samples;
            double[,] samples = new double[3, nSamples];
            for (int i = 0; i < nSamples; i++)
            {
                long curr_sample = start_sample + i;
                long curr_step = curr_sample / step_samples;
                //If we are supposed to hold, keep holding voltage constant throughout
                if (LaserHoldV)
                    samples[channelIndex - 1, i] = milliVoltsToAOVolts(LaserHoldingmV);
                //If we are beyond the last step, or in the pre-phase, or in the post-phase set laser current to 0
                if (curr_step >= NCurrSteps || curr_sample % step_samples < pre_post_samples || curr_sample % step_samples > pre_post_samples + stim_samples)
                    samples[2, i] = 0;
                else
                {
                    //Stim phase: Turn laser on
                    samples[2, i] = laser_mA_toAOVolts(LaserStim_mA);
                }
            }
            return samples;
        }

        /// <summary>
        /// Function to convert desired millivoltages in voltage clamp
        /// to corresponding analog out values to the amplifier
        /// </summary>
        /// <param name="mv">The desired milli-volts</param>
        /// <returns>The analog out voltage to apply</returns>
        private double milliVoltsToAOVolts(double mv)
        {
            return mv / mV_per_V;
        }

        /// <summary>
        /// Function to convert desired pico-amps injection in current clamp
        /// to corresponding analog out values to the amplifier
        /// </summary>
        /// <param name="pa">The desired pico-amps</param>
        /// <returns>The analog out voltage to apply</returns>
        private double picoAmpsToAOVolts(double pa)
        {
            return pa / pA_per_V;
        }

        /// <summary>
        /// Function to conver a desired laser current in mA to
        /// the appropriate analog out voltage
        /// </summary>
        /// <param name="ma"></param>
        /// <returns></returns>
        private double laser_mA_toAOVolts(double ma)
        {
            var v = ma / 4000 * 10;
            if (v < 0)
                v = 0;
            else if (v > 10)
                v = 10;
            return v;
        }

        /// <summary>
        /// For one electrode generates our seal test samples for one whole second
        /// </summary>
        /// <param name="start_sampe">The index of the first sample</param>
        /// <param name="nSamples">The number of samples to generate</param>
        /// <param name="freqHz">The frequency in Hz at which to generate sealTestSamples</param>
        /// <param name="ampMV">The amplitude in mV</param>
        /// <returns></returns>
        private double[] sealTestSamples(long start_sample, int nSamples, int freqHz=10, double ampMV=10)
        {
            if (_stSamples == null || _stSamples.Length != HardwareSettings.DAQ.Rate)
            {
                //Generate our sample buffer
                _stSamples = new double[HardwareSettings.DAQ.Rate];
                if(HardwareSettings.DAQ.Rate % (freqHz*2) != 0)
                {
                    System.Diagnostics.Debug.WriteLine("Warning seal test frequency does not result in even samples.");
                }
                int sam_per_seal = HardwareSettings.DAQ.Rate / freqHz;
                int sam_on = sam_per_seal / 2;
                for(int i = 0; i<_stSamples.Length; i++)
                {
                    if (i % sam_per_seal < sam_on)
                        _stSamples[i] = milliVoltsToAOVolts(ampMV);
                    else
                        _stSamples[i] = 0;
                }
            }
            double[] samOut = new double[nSamples];
            for(int i = 0;i < nSamples; i++)
            {
                Array.Copy(_stSamples, start_sample % HardwareSettings.DAQ.Rate, samOut, 0, samOut.Length); 
            }
            return samOut;
        }

        void StartAcquisition()
        {
            //Notify all dependents that acquistion starts
            if (Start != null)
                Start.Invoke();
            //Start the DAQ board
            HardwareManager.DaqBoard.Start((s, i) => { return genSamples(s, i); });
            IsAcquiring = true;
        }

        void StopAcquisition()
        {
            //Notify all dependents that acquisition stops
            if (Stop != null)
                Stop.Invoke();
            if (IsRecordingCh1)
                StopRecording(1);
            //Stop the DAQ board
            HardwareManager.DaqBoard.Stop();
            IsAcquiring = false;
        }

        void StartRecording(int channelIndex, Dictionary<string, string> exp_info = null)
        {
            //Attach ourselves to the sample read event queue
            if(!IsRecordingCh1)
                HardwareManager.DaqBoard.ReadDone += RecordSamples;
            if (channelIndex == 1)
            {
                _recordTask = new Task(() =>
               {
                   string data_file_name = CreateFilename(1) + ".data";
                   //Write info file
                   if (exp_info == null)
                   {
                       exp_info = new Dictionary<string, string>();
                       exp_info["Experiment type"] = "Free run";
                   }
                   exp_info["datafile"] = Path.GetFileName(data_file_name);
                   exp_info["daq_rate"] = HardwareSettings.DAQ.Rate.ToString();
                   exp_info["channel"] = channelIndex.ToString();
                   exp_info["mV_per_V"] = mV_per_V.ToString();
                   exp_info["pA_per_V"] = pA_per_V.ToString();
                   TextWriter infoWriter = new StreamWriter(CreateFilename(1) + ".info");
                   WriteExperimentInfo(BaseFNameCh1, exp_info, infoWriter);
                   infoWriter.Dispose();
                   BinaryWriter ch1File = new BinaryWriter(File.OpenWrite(data_file_name));
                   while (true)
                   {
                       ChannelReadDataChunk chnk = _record_dataQueue.Consume();
                       if (chnk.StartIndex == 0 && chnk.Data == null)
                           break;
                       for (int i = 0; i < chnk.Data.GetLength(1); i++)
                           WriteChannel1Sample(ch1File, chnk.StartIndex + i, chnk.Data[2, i] > 2, (float)chnk.Data[4, i], (float)chnk.Data[0, i],
                               (float)chnk.Data[6, i]);
                   }
                   ch1File.Dispose();
               });
                _recordTask.Start();
                IsRecordingCh1 = true;
            }
            else
                throw new NotImplementedException();
        }

        void StopRecording(int channelIndex)
        {
            //Detach ourselves from the sample read event queue
            if (IsRecordingCh1)
            {
                HardwareManager.DaqBoard.ReadDone -= RecordSamples;
                //add end of recording to signal to our queue
                ChannelReadDataChunk end_chunk = new ChannelReadDataChunk();
                end_chunk.StartIndex = 0;
                end_chunk.Data = null;
                _record_dataQueue.Produce(end_chunk);
                //wait for our file writing to finish
                if (_recordTask != null && !_recordTask.IsCompleted)
                    if (!_recordTask.Wait(1000))
                        System.Diagnostics.Debug.WriteLine("Timed out waiting on record task to end");
                _recordTask = null;
            }
            if (channelIndex == 1)
            {
                IsRecordingCh1 = false;
            }
        }

        /// <summary>
        /// Creates a unique recording filename
        /// </summary>
        /// <param name="channelIndex">The index of the channel for which the filename should be created</param>
        /// <returns>The filename string without extension</returns>
        string CreateFilename(int channelIndex)
        {
            if (channelIndex == 1)
            {
                DateTime now = DateTime.Now;
                string folder = string.Format("F:\\PatchCommander_Data\\{0}_{1}_{2}", now.Year, now.Month, now.Day);
                Directory.CreateDirectory(folder);
                return string.Format("{0}\\Ch1_{1}_{2}_{3}_{4}_{5}", folder, BaseFNameCh1, now.Year, now.Month, now.Day, now.Ticks);
            }
            else
                throw new NotImplementedException("Channel 2 not currently implemented");
        }

        void WriteChannel1Sample(BinaryWriter file, long index, bool mode, float command, float read, float laser)
        {
            //To read in python, use numpy.fromfile with the following data-type definition
            //dt = numpy.dtype([('index',np.int64),('mode',np.bool),('command',np.float32),('read',np.float32),('laser',np.float32)])
            if (file==null)
                return;
            try
            {
                file.Write(index);
                file.Write(mode);
                file.Write(command);
                file.Write(read);
                file.Write(laser);
            }
            catch (IOException)
            {
                System.Diagnostics.Debug.WriteLine("Error writing to data file. File may be corrupted.");
            }
        }

        #endregion

        #region EventHandlers

        /// <summary>
        /// Event to receive and distribute analog in samples received
        /// </summary>
        /// <param name="args">Sample payload</param>
        void RecordSamples(ReadDoneEventArgs args)
        {
            ChannelReadDataChunk chunk = new ChannelReadDataChunk();
            chunk.StartIndex = args.StartIndex;
            chunk.Data = args.Data.Clone() as double[,];
            _record_dataQueue.Produce(chunk);
        }

        /// <summary>
        /// During fixed length experiments allows us to "clean up" after reading is finished
        /// </summary>
        private void DaqBoard_ReadThreadFinished()
        {
            //Unsubscribe ourselves
            HardwareManager.DaqBoard.ReadThreadFinished -= DaqBoard_ReadThreadFinished;
            //Stop recording and acquisition
            StopAcquisition();
            StimExpRunning = false;
        }

        /// <summary>
        /// Gets called whenver the clamp mode changes on a channel view
        /// </summary>
        /// <param name="args"></param>
        void ClampModeChanged(ClampModeChangedArgs args)
        {
            if (args.ChannelIndex == 0)
                VC_Channel1 = (args.Mode == DAQ.ClampMode.VoltageClamp);
            else if (args.ChannelIndex == 1)
                VC_Channel2 = (args.Mode == DAQ.ClampMode.VoltageClamp);
        }

        /// <summary>
        /// Gets called whenver the seal test mode changed on a channel view
        /// </summary>
        /// <param name="args"></param>
        void SealTestChanged(SealTestChangedArgs args)
        {
            if (args.ChannelIndex == 0)
                SealTest_Channel1 = args.SealTest;
            else if (args.ChannelIndex == 1)
                SealTest_Channel2 = args.SealTest;
        }

        #endregion

        #region Events

        /// <summary>
        /// Event to subscribe to to get notified
        /// about acquisition starts
        /// </summary>
        public static event Action Start;

        /// <summary>
        /// Event to subscribe to get notified
        /// about acquisition stops
        /// </summary>
        public static event Action Stop;

        #endregion

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (IsRecordingCh1)
                StopRecording(1);
            if (IsAcquiring)
                StopAcquisition();
            if (HardwareManager.DaqBoard.IsRunning)
                StopAcquisition();
        }
    }
}
