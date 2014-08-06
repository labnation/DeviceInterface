using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.DeviceMemories;
using System.IO;
using ECore.DataSources;
using ECore.HardwareInterfaces;
using Common;


namespace ECore.Devices
{
    public partial class ScopeV2 : EDevice, IScope, IDisposable
    {
#if INTERNAL
    public
#else
    private
#endif
        ScopeUsbInterface hardwareInterface;
#if INTERNAL
        public
#else
        private
#endif
        Rom rom;
        private bool flashed = false;
        private bool deviceReady = false;
        private ScopeConnectHandler scopeConnectHandler;

#if INTERNAL 
        public DeviceMemories.ScopeFpgaSettingsMemory FpgaSettingsMemory { get; private set; }
        public DeviceMemories.ScopeFpgaRom FpgaRom { get; private set; }
        public DeviceMemories.ScopeStrobeMemory StrobeMemory { get; private set; }
        public DeviceMemories.MAX19506Memory AdcMemory { get; private set; }
        public DeviceMemories.ScopePicRegisterMemory PicMemory { get; private set; }
#else
        private DeviceMemories.ScopeFpgaSettingsMemory FpgaSettingsMemory;
        private DeviceMemories.ScopeFpgaRom FpgaRom;
        private DeviceMemories.ScopeStrobeMemory StrobeMemory;
        private DeviceMemories.MAX19506Memory AdcMemory;
        private DeviceMemories.ScopePicRegisterMemory PicMemory;
#endif

        private DataSources.DataSourceScope dataSourceScope;
        public DataSources.DataSourceScope DataSourceScope { get { return dataSourceScope; } }

        private bool disableVoltageConversion = false;
        private const double BASE_SAMPLE_PERIOD = 10e-9;
        private const uint NUMBER_OF_SAMPLES = 2048;
        private bool acquisitionRunning = false;
        private GainCalibration[] channelSettings;
        private float triggerLevel = 0f;

        public string Serial
        {
            get
            {
                if (hardwareInterface == null)
                    return null;
                return hardwareInterface.GetSerial();
            }
        }

#if INTERNAL
        public int ramTestPasses, ramTestFails, digitalTestPasses, digitalTestFails;
#endif
#if ANDROID
		public Android.Content.Res.AssetManager Assets;
#endif

        public ScopeV2(ScopeConnectHandler handler)
            : base()
        {
            deviceReady = false;
            channelSettings = new GainCalibration[2];
            this.scopeConnectHandler += handler;
            dataSourceScope = new DataSources.DataSourceScope(this);
            InitializeHardwareInterface();
        }

        public void Dispose()
        {
            dataSourceScope.Stop();
            HWInterfacePIC_LibUSB.RemoveConnectHandler(OnDeviceConnect);
            if (hardwareInterface != null)
                OnDeviceConnect(this.hardwareInterface, false);
        }

        #region initializers

        private void InitializeHardwareInterface()
        {
            //The memory initialisation is done here so that if no device is detected,
            //we'll still have deviceMemory objects to play with
            InitializeMemories();
#if ANDROID
		hardwareInterface = new HardwareInterfaces.HWInterfacePIC_Xamarin(this);
#else
            HWInterfacePIC_LibUSB.AddConnectHandler(OnDeviceConnect);
            HWInterfacePIC_LibUSB.Initialize();
            HWInterfacePIC_LibUSB.PollDevice();
#endif
        }

        private void OnDeviceConnect(ScopeUsbInterface hwInterface, bool connected)
        {
            if (connected)
            {
                try
                {
#if INTERNAL
                    resetTestResults("all");
#endif
                    this.hardwareInterface = hwInterface;
                    //FIXME: I have to do this synchronously here because there's no blocking on the USB traffic
                    //but there should be when flashing the FPGA.

                    hardwareInterface.SendCommand(ScopeUsbInterface.PIC_COMMANDS.PIC_VERSION);
                    byte[] response = hardwareInterface.ReadControlBytes(16);
                    if (response == null)
                        throw new Exception("Failed to read from device");
                    Logger.Debug(String.Format("PIC FW Version readout {0}", String.Join(";", response)));

                    //Init ROM
                    this.rom = new Rom(hardwareInterface);

                    //Init FPGA
                    LogWait("Starting fpga flashing...", 0);
                    if (!FlashFpga())
                        throw new Exception("failed to flash FPGA");
                    LogWait("FPGA flashed...");
                    InitializeMemories();
                    LogWait("Memories initialized...");
                    Logger.Debug("FPGA ROM MSB:LSB = " + FpgaRom[ROM.FW_MSB].Read().GetByte() + ":" + FpgaRom[ROM.FW_LSB].Read().GetByte());

                    Logger.Info(String.Format("FPGA FW version = 0x{0:x}", GetFpgaFirmwareVersion()));

                    Configure();
                    deviceReady = true;
                }
                catch (ScopeIOException e)
                {
                    Logger.Error("Failure while connecting to device: " + e.Message);
                    connected = false;
                    this.hardwareInterface = null;
                    this.flashed = false;
                    InitializeMemories();
                    throw e;
                }
            }
            else
            {
                deviceReady = false;
                if (this.hardwareInterface == hwInterface)
                {
                    this.hardwareInterface = null;
                    this.flashed = false;
                }
                InitializeMemories();
            }
            if (scopeConnectHandler != null)
                scopeConnectHandler(this, connected);
        }

#if INTERNAL
        public void resetTestResults(string test)
        {
            if (test == "ram" || test == "all")
            {
                ramTestPasses = 0;
                ramTestFails = 0;
            }

            if (test == "digi" || test == "all")
            {
                digitalTestPasses = 0;
                digitalTestFails = 0;
            }
        }
#endif

        //master method where all memories, registers etc get defined and linked together
        private void InitializeMemories()
        {
            memories.Clear();
            //Create memories
            PicMemory = new DeviceMemories.ScopePicRegisterMemory(hardwareInterface);
            FpgaSettingsMemory = new DeviceMemories.ScopeFpgaSettingsMemory(hardwareInterface);
            FpgaRom = new DeviceMemories.ScopeFpgaRom(hardwareInterface);
            StrobeMemory = new DeviceMemories.ScopeStrobeMemory(FpgaSettingsMemory, FpgaRom);
            AdcMemory = new DeviceMemories.MAX19506Memory(FpgaSettingsMemory, StrobeMemory, FpgaRom);
            //Add them in order we'd like them in the GUI
            memories.Add(PicMemory);
            memories.Add(FpgaRom);
            memories.Add(StrobeMemory);
            memories.Add(FpgaSettingsMemory);
            memories.Add(AdcMemory);
            
        }

        #endregion

        #region start_stop

		private void LogWait(string message, int sleep = 0)
        {
            Logger.Debug(message);
			System.Threading.Thread.Sleep(sleep);
        }

        private void Configure()
        {
            //Part 1: Just set all desired memory settings

            /*********
             *  ADC  *
            *********/
            AdcMemory[MAX19506.POWER_MANAGEMENT].Set(4);
            AdcMemory[MAX19506.OUTPUT_PWR_MNGMNT].Set(1);
            AdcMemory[MAX19506.FORMAT_PATTERN].Set(16);
            AdcMemory[MAX19506.CHA_TERMINATION].Set(18);
            AdcMemory[MAX19506.DATA_CLK_TIMING].Set(5);
            AdcMemory[MAX19506.POWER_MANAGEMENT].Set(3);
            AdcMemory[MAX19506.OUTPUT_FORMAT].Set(0x02); //DDR on chA

            /***************************/

            //Enable scope controller
            StrobeMemory[STR.SCOPE_ENABLE].Set(true);
            SetVerticalRange(0, -1f, 1f);
            SetVerticalRange(1, -1f, 1f);
            SetYOffset(0, 0f);
            SetYOffset(1, 0f);

            StrobeMemory[STR.ENABLE_ADC].Set(true);
            StrobeMemory[STR.ENABLE_RAM].Set(true);
            StrobeMemory[STR.ENABLE_NEG].Set(true);

            SetTriggerWidth(6);
            SetTriggerThreshold(2);            
            SetCoupling(0, Coupling.DC);
            SetCoupling(1, Coupling.DC);

            try
            {
                //Part 2: perform actual writes                
                hardwareInterface.FlushDataPipe();
                StrobeMemory[STR.GLOBAL_RESET].WriteImmediate(true);
                AdcMemory[MAX19506.SOFT_RESET].WriteImmediate(90);
                CommitSettings();
            } catch (ScopeIOException e) {
                Logger.Error("Something went wrong while configuring the scope. Try replugging it : " + e.Message);
                OnDeviceConnect(hardwareInterface, false);
            }

        }

#if INTERNAL
        public void LoadBootLoader()
        {
            this.DataSourceScope.Stop();
            this.hardwareInterface.LoadBootLoader();
        }
        public void Reset()
        {
            this.DataSourceScope.Stop();
            this.hardwareInterface.Reset();
        }
#endif
        public void SoftReset()
        {
            dataSourceScope.Reset();
            if(Connected)
                Configure();
        }

        #endregion

        #region data_handlers

        private float[] ConvertByteToVoltage(AnalogChannel ch, double divider, double multiplier, byte[] buffer, byte yOffset)
        {
            double[] coefficients = rom.getCalibration(ch, divider, multiplier).coefficients;
            float[] voltage = new float[buffer.Length];

            //this section converts twos complement to a physical voltage value
            float totalOffset = (float)(yOffset * coefficients[1] + coefficients[2]);
            voltage = buffer.Select(x => (float)(x * coefficients[0] + totalOffset)).ToArray();
            return voltage;
        }

        /// <summary>
        /// Get a package of scope data
        /// </summary>
        /// <returns>Null in case communication failed, a data package otherwise. Might result in disconnecting the device if a sync error occurs</returns>
        public DataPackageScope GetScopeData()
        {
            int samplesToFetch = 2048;
            int bytesToFetch = 64 + samplesToFetch * 2;//64 byte header + 2048 * 2 channels
            if (hardwareInterface == null)
                return null;

            
            byte[] buffer;
            ScopeV2Header header;
            byte[] chA = null, chB = null;
            int dataOffset = 0;
            while (true)
            {
                // Get data
                try { buffer = hardwareInterface.GetData(bytesToFetch); }
                catch (ScopeIOException) { return null; }
                if (buffer == null) return null;
                // Parse header

                try { header = new ScopeV2Header(buffer); }
                catch (Exception e)
                {
                    Logger.Error("Failed to parse header - disconnecting scope: " + e.Message);
                    OnDeviceConnect(this.hardwareInterface, false);
                    return null;
                }

                // Re-assemble
                int payloadOffset = header.bytesPerBurst;
                if (chA == null)
                {
                    dataOffset = 0;
                    //FIXME: REG_VIEW_DECIMATION disabled (always equals ACQUISITION_MULTIPLE_POWER)
                    int acquisitionLength = header.Samples;// *(1 << (header.GetRegister(REG.ACQUISITION_MULTIPLE_POWER) - header.GetRegister(REG.VIEW_DECIMATION)));
                    chA = new byte[acquisitionLength];
                    chB = new byte[acquisitionLength];
                }
                for (int i = 0; i < header.Samples; i++)
                {
                    chA[dataOffset + i] = buffer[payloadOffset + 2 * i];
                    chB[dataOffset + i] = buffer[payloadOffset + 2 * i + 1];
                }
                //FIXME: REG_VIEW_DECIMATION disabled (always equals ACQUISITION_MULTIPLE_POWER)
                //if (header.dumpSequence >= (1 << header.GetRegister(REG.ACQUISITION_MULTIPLE_POWER) - header.GetRegister(REG.VIEW_DECIMATION)) - 1)
                    break;
                dataOffset += header.Samples;
            }

            acquisitionRunning = header.ScopeRunning;
            //FIXME: Get these scope settings from header
            int triggerIndex = 0;

#if INTERNAL
            if (header.GetStrobe(STR.DEBUG_RAM) && header.GetRegister(REG.ACQUISITION_MULTIPLE_POWER) == 0)
            {
                UInt16[] testData = new UInt16[header.Samples];
                Buffer.BlockCopy(buffer, header.bytesPerBurst, testData, 0, sizeof(UInt16) * testData.Length);
                for (int i = 1; i < testData.Length; i++)
                {
                    UInt16 expected = Utils.nextFpgaTestVector(testData[i - 1]);
                    bool mismatch = !expected.Equals(testData[i]);
                    if (mismatch)
                    {
                        Logger.Debug("Stress test mismatch at sample " + i);
                        ramTestFails++;
                        goto ram_test_done;
                    }
                }
                ramTestPasses++;
            }
            ram_test_done:
#endif

            this.coupling[0] = header.GetStrobe(STR.CHA_DCCOUPLING) ? Coupling.DC : Coupling.AC;
            this.coupling[1] = header.GetStrobe(STR.CHB_DCCOUPLING) ? Coupling.DC : Coupling.AC;

#if INTERNAL
            if (header.GetStrobe(STR.LA_ENABLE) && header.GetStrobe(STR.DIGI_DEBUG) && !header.GetStrobe(STR.DEBUG_RAM))
            {
                //Test if data in CHB is correct
                byte[] testVector = new byte[chB.Length];
                byte nextValue = chB[0];
                for (int i = 0; i < testVector.Length; i++)
                {
                    testVector[i] = nextValue;
                    int val = (nextValue >> 4) + 1;
                    nextValue = (byte)((val << 4) + (Utils.ReverseWithLookupTable((byte)val) >> 4));
                    if (testVector[i] != chB[i])
                    {
                        Logger.Error("Digital mismatch at sample " + i + ". Aborting check");
                        digitalTestFails++;
                        goto done;
                    }
                }
                digitalTestPasses++;
            }
        done:
#endif

            //construct data package
            //FIXME: get firstsampletime and samples from FPGA
            //FIXME: parse package header and set DataPackageScope's trigger index
            DataPackageScope data = new DataPackageScope(header.SamplePeriod, triggerIndex, chA.Length, 0);
#if INTERNAL
            data.AddSetting("TriggerAddress", header.TriggerAddress);
#endif
            //Parse div_mul
            byte divMul = header.GetRegister(REG.DIVIDER_MULTIPLIER);
            double divA = validDividers[(divMul >> 0) & 0x3];
            double mulA = validMultipliers[(divMul >> 2) & 0x3];
            double divB = validDividers[(divMul >> 4) & 0x3];
            double mulB = validMultipliers[(divMul >> 6) & 0x3];
            data.AddSetting("DividerA", divA);
            data.AddSetting("DividerB", divB);
            data.AddSetting("MultiplierA", mulA);
            data.AddSetting("MultiplierB", mulB);

            if (this.disableVoltageConversion)
            {
                data.SetData(AnalogChannel.ChA, Utils.CastArray<byte, float>(chA));
                data.SetData(AnalogChannel.ChB, Utils.CastArray<byte, float>(chB));
                data.SetDataDigital(chB);
                data.AddSetting("OffsetA", (float)header.GetRegister(REG.CHA_YOFFSET_VOLTAGE));
                data.AddSetting("OffsetB", (float)header.GetRegister(REG.CHB_YOFFSET_VOLTAGE));
            }
            else
            {
                data.SetData(AnalogChannel.ChA,
                    ConvertByteToVoltage(AnalogChannel.ChA, divA, mulA, chA, header.GetRegister(REG.CHA_YOFFSET_VOLTAGE)));

                //Check if we're in LA mode and fill either analog channel B or digital channels
                if (!header.GetStrobe(STR.LA_ENABLE))
                    data.SetData(AnalogChannel.ChB,
                        ConvertByteToVoltage(AnalogChannel.ChB, divB, mulB, chB, header.GetRegister(REG.CHB_YOFFSET_VOLTAGE)));
                else
                    data.SetDataDigital(chB);
            }
            return data;
        }

        //FIXME: this needs proper handling
        public override bool Connected { get { return this.hardwareInterface != null && this.flashed && this.deviceReady; } }

        #endregion
    }
}
