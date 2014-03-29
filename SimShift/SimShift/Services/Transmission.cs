using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SimShift.Data;
using SimShift.Data.Common;
using SimShift.Dialogs;
using SimShift.Utils;

namespace SimShift.Services
{
    public class Transmission : IControlChainObj, IConfigurable
    {
        // TODO: Move to car object
        public int RangeSize = 6;
        public int Gears = 6;

        public static bool InReverse { get; set; }

        public bool GetHomeMode { get; set; }

        public ShiftPattern ActiveShiftPattern {get { return ShiftPatterns[ActiveShiftPatternStr]; }}

        public string ActiveShiftPatternStr;
        public Dictionary<string, ShiftPattern> ShiftPatterns = new Dictionary<string, ShiftPattern>(); 

        public int ShiftFrame = 0;
        public ShifterTableConfiguration configuration;

        public int GameGear { get; private set; }

        public int ShifterGear
        {
            get { return ShifterNewGear; }
        }

        public static bool IsShifting { get; private set; }

        public int ShiftCtrlOldGear { get; private set; }
        public int ShiftCtrlNewGear { get; private set; }
        public int ShiftCtrlOldRange { get; private set; }
        public int ShiftCtrlNewRange { get; private set; }

        public int ShifterOldGear
        {
            get { return ShiftCtrlOldGear + ShiftCtrlOldRange*RangeSize; }
        }

        public int ShifterNewGear
        {
            get { return ShiftCtrlNewGear + ShiftCtrlNewRange*RangeSize; }
        }

        public DateTime TransmissionFreezeUntill { get; private set; }

        public bool TransmissionFrozen
        {
            get { return TransmissionFreezeUntill > DateTime.Now; }
        }

        public DateTime RangeButtonFreeze1Untill { get; private set; }
        public DateTime RangeButtonFreeze2Untill { get; private set; }

        public int RangeButtonSelectPhase
        {
            get
            {
                if (RangeButtonFreeze1Untill > DateTime.Now) return 1; // phase 1
                if (RangeButtonFreeze2Untill > DateTime.Now) return 2; // phase 2
                return 0; // phase 0
            }
        }

        private double transmissionThrottle;

        public Transmission()
        {
            configuration = new ShifterTableConfiguration(ShifterTableConfigurationDefault.PeakRpm, Main.Drivetrain, 20);

            LoadShiftPattern("up_1thr", "normal");

            // Initialize all shfiting stuff.
            Shift(0, 1, "up_1thr");
            IsShifting = false;
        }

        public void LoadShiftPatterns(List<ConfigurableShiftPattern> patterns)
        {
            foreach(var p in patterns)
            {
                LoadShiftPattern(p.Region, p.File);
            }
        }

        #region Transmission Shift logics

        public void Shift(int fromGear, int toGear, string style)
        {
            if (ShiftPatterns.ContainsKey(style))
                ActiveShiftPatternStr = style;
            else 
                ActiveShiftPatternStr = ShiftPatterns.Keys.FirstOrDefault();

            // Copy old control to new control values
            ShiftCtrlOldGear = fromGear;
            if (ShiftCtrlOldGear == -1) ShiftCtrlOldRange = 0;
            else if (ShiftCtrlOldGear == 0) ShiftCtrlOldRange = 0;
            else if (ShiftCtrlOldGear >= 1 && ShiftCtrlOldGear <= RangeSize) ShiftCtrlOldRange = 0;
            else if (ShiftCtrlOldGear >= RangeSize + 1 && ShiftCtrlOldGear <= 2*RangeSize) ShiftCtrlOldRange = 1;
            else if (ShiftCtrlOldGear >= 2*RangeSize + 1 && ShiftCtrlOldGear <= 3*RangeSize) ShiftCtrlOldRange = 2;
            ShiftCtrlOldGear -= ShiftCtrlOldRange*RangeSize;

            // Determine new range
            if (toGear == -1)
            {
                ShiftCtrlNewGear = -1;
                ShiftCtrlNewRange = 0;
            }
            else if (toGear == 0)
            {
                ShiftCtrlNewGear = 0;
                ShiftCtrlNewRange = 0;
            }
            else if (toGear >= 1 && toGear <= RangeSize)
            {
                ShiftCtrlNewGear = toGear;
                ShiftCtrlNewRange = 0;
            }
            else if (toGear >= RangeSize + 1 && toGear <= RangeSize*2)
            {
                ShiftCtrlNewGear = toGear - RangeSize;
                ShiftCtrlNewRange = 1;
            }
            else if (toGear >= RangeSize*2 + 1 && toGear <= RangeSize*3)
            {
                ShiftCtrlNewGear = toGear - RangeSize*2;
                ShiftCtrlNewRange = 2;
            }

            ShiftFrame = 0;
            IsShifting = true;

        }

        private void LoadShiftPattern(string pattern, string file)
        {
            // Add pattern if not existing.
            if(!ShiftPatterns.ContainsKey(pattern))
                ShiftPatterns.Add(pattern, new ShiftPattern());

            // Load configuration file
            Main.Load(ShiftPatterns[pattern], "Settings/ShiftPattern/"+file+".ini");
            return;
            /*
            switch (pattern)
            {
                    // very slow
                case "up_0thr":
                    // TODO: Patterns are not loaded from files yet.
                    ShiftPattern = new List<ShiftPatternFrame>();

                    // Phase 1: engage clutch
                    ShiftPattern.Add(new ShiftPatternFrame(0, 1, true, false));
                    ShiftPattern.Add(new ShiftPatternFrame(0, 0.7, true, false));
                    ShiftPattern.Add(new ShiftPatternFrame(0.5, 0.4, true, false));
                    ShiftPattern.Add(new ShiftPatternFrame(0.8, 0, true, false));

                    // Phase 2: disengage old gear
                    ShiftPattern.Add(new ShiftPatternFrame(1, 0, false, false));
                    ShiftPattern.Add(new ShiftPatternFrame(1, 0, false, false));
                    ShiftPattern.Add(new ShiftPatternFrame(1, 0, false, false));
                    ShiftPattern.Add(new ShiftPatternFrame(1, 0, false, false));
                    ShiftPattern.Add(new ShiftPatternFrame(1, 0, false, false));
                    ShiftPattern.Add(new ShiftPatternFrame(1, 0, false, false));
                    ShiftPattern.Add(new ShiftPatternFrame(1, 0, false, false));
                    ShiftPattern.Add(new ShiftPatternFrame(1, 0, false, false));
                    ShiftPattern.Add(new ShiftPatternFrame(1, 0, false, false));

                    // Phase 3: engage new gear
                    ShiftPattern.Add(new ShiftPatternFrame(1, 0, false, true));
                    ShiftPattern.Add(new ShiftPatternFrame(1, 0, false, true));
                    ShiftPattern.Add(new ShiftPatternFrame(1, 0, false, true));
                    ShiftPattern.Add(new ShiftPatternFrame(1, 0, false, true));
                    ShiftPattern.Add(new ShiftPatternFrame(1, 0, false, true));
                    ShiftPattern.Add(new ShiftPatternFrame(1, 0, false, true));

                    // Phase 4: disengage clutch
                    ShiftPattern.Add(new ShiftPatternFrame(0.8, 0, false, true));
                    ShiftPattern.Add(new ShiftPatternFrame(0.5, 0.4, false, true));
                    ShiftPattern.Add(new ShiftPatternFrame(0.0, 0.7, false, true));
                    ShiftPattern.Add(new ShiftPatternFrame(0.0,1, false, true));
                    break;

                case "up_1thr":
                    // TODO: Patterns are not loaded from files yet.
                    ShiftPattern = new List<ShiftPatternFrame>();

                    // Phase 1: engage clutch
                    ShiftPattern.Add(new ShiftPatternFrame(0, 0.6, true, false));
                    ShiftPattern.Add(new ShiftPatternFrame(0, 0.3, true, false));
                    ShiftPattern.Add(new ShiftPatternFrame(0.5, 0, true, false));
                    ShiftPattern.Add(new ShiftPatternFrame(0.8, 0, true, false));

                    // Phase 2: disengage old gear
                    ShiftPattern.Add(new ShiftPatternFrame(1, 0, false, false));
                    ShiftPattern.Add(new ShiftPatternFrame(1, 0, false, false));
                    ShiftPattern.Add(new ShiftPatternFrame(1, 0, false, false));

                    // Phase 3: engage new gear
                    ShiftPattern.Add(new ShiftPatternFrame(1, 0, false, true));
                    ShiftPattern.Add(new ShiftPatternFrame(1, 0, false, true));

                    // Phase 4: disengage clutch
                    ShiftPattern.Add(new ShiftPatternFrame(0.8, 0, false, true));
                    ShiftPattern.Add(new ShiftPatternFrame(0.5, 0.0, false, true));
                    ShiftPattern.Add(new ShiftPatternFrame(0.0, 0.3, false, true));
                    ShiftPattern.Add(new ShiftPatternFrame(0.0, 0.6, false, true));
                    break;

                case "down_0thr":
                    // TODO: Patterns are not loaded from files yet.
                    ShiftPattern = new List<ShiftPatternFrame>();

                    // Phase 1: engage clutch
                    ShiftPattern.Add(new ShiftPatternFrame(0, 1, true, false));
                    ShiftPattern.Add(new ShiftPatternFrame(0, 0.7, true, false));
                    ShiftPattern.Add(new ShiftPatternFrame(0.5, 0.4, true, false));
                    ShiftPattern.Add(new ShiftPatternFrame(0.8, 0, true, false));

                    // Phase 2: disengage old gear
                    ShiftPattern.Add(new ShiftPatternFrame(1, 0, false, false));
                    ShiftPattern.Add(new ShiftPatternFrame(1, 0, false, false));
                    ShiftPattern.Add(new ShiftPatternFrame(1, 1, true, false, false));

                    // Phase 3: engage new gear
                    ShiftPattern.Add(new ShiftPatternFrame(1, 1, true, false, true));
                    ShiftPattern.Add(new ShiftPatternFrame(1, 1, true, false, true));
                    ShiftPattern.Add(new ShiftPatternFrame(1, 0, false, true));

                    // Phase 4: disengage clutch
                    for (int i = 0; i < 25; i++)
                        ShiftPattern.Add(new ShiftPatternFrame((25 - i)/25.0, i/25.0, false, true));
                    break;

                case "down_1thr":
                    // TODO: Patterns are not loaded from files yet.
                    ShiftPattern = new List<ShiftPatternFrame>();

                    // Phase 1: engage clutch
                    ShiftPattern.Add(new ShiftPatternFrame(0, 1, true, false));
                    ShiftPattern.Add(new ShiftPatternFrame(0, 0.7, true, false));
                    ShiftPattern.Add(new ShiftPatternFrame(0.2, 0.4, true, false));
                    ShiftPattern.Add(new ShiftPatternFrame(0.5, 0.2, true, false));
                    ShiftPattern.Add(new ShiftPatternFrame(0.8, 0.1, true, false));

                    // Phase 2: disengage old gear
                    ShiftPattern.Add(new ShiftPatternFrame(1, 0.0, false, false));
                    ShiftPattern.Add(new ShiftPatternFrame(1, 0.0, false, false));

                    // Phase 3: engage new gear
                    ShiftPattern.Add(new ShiftPatternFrame(1, 0.0, false, true));
                    ShiftPattern.Add(new ShiftPatternFrame(1, 0.0, false, true));

                    // Phase 4: disengage clutch
                    ShiftPattern.Add(new ShiftPatternFrame(0.8, 0.1, false, true));
                    ShiftPattern.Add(new ShiftPatternFrame(0.5, 0.2, false, true));
                    ShiftPattern.Add(new ShiftPatternFrame(0.2, 0.4, false, true));
                    ShiftPattern.Add(new ShiftPatternFrame(0.0, 0.7, false, true));
                    ShiftPattern.Add(new ShiftPatternFrame(0.0, 1, false, true));
                    break;
            }*/
        }

        #endregion

        #region Transmission telemetry logic

        public void TickTelemetry(IDataMiner data)
        {
            int idealGear = data.Telemetry.Gear;


            if (data.TransmissionSupportsRanges)
                RangeSize = 6;
            else
                RangeSize = 8;

            // TODO: Add generic telemetry object
            GameGear = data.Telemetry.Gear;
            if (IsShifting) return;
            if (TransmissionFrozen) return;
            shiftRetry = 0;

            if (GetHomeMode)
            {
                if(idealGear < 1)
                {
                    idealGear = 1;
                }

                var lowRpm = Main.Drivetrain.StallRpm*1.5;
                var highRpm = Main.Drivetrain.StallRpm*3;

                if (data.Telemetry.EngineRpm < lowRpm && idealGear > 1)
                    idealGear--;
                if (data.Telemetry.EngineRpm > highRpm && idealGear < Main.Drivetrain.Gears)
                    idealGear++;

            }
            else
            {
                var lookupResult = configuration.Lookup(data.Telemetry.Speed*3.6, transmissionThrottle);
                idealGear = lookupResult.Gear;

                if(idealGear == 0)
                {
                    
                }

                if (data.Telemetry.Gear == 0 && ShiftCtrlNewGear != 0)
                {
                    Debug.WriteLine("Timeout");
                    ShiftCtrlNewGear = 0;
                    TransmissionFreezeUntill = DateTime.Now.Add(new TimeSpan(0, 0, 0, 0, 250));
                    return;
                }
            }

            if (InReverse)
            {
                if (GameGear != -1)
                {
                    Debug.WriteLine("Shift from " + data.Telemetry.Gear + " to  " + idealGear);
                    Shift(data.Telemetry.Gear, -1, "up_1thr");
                }
                return;
            }

            if (idealGear != data.Telemetry.Gear)
            {
                var upShift = idealGear > data.Telemetry.Gear;
                var fullThrottle = data.Telemetry.Throttle > 0.6;

                var shiftStyle = (upShift ? "up" : "down") + "_" + (fullThrottle ? "1" : "0") + "thr";

                Debug.WriteLine("Shift from " + data.Telemetry.Gear + " to  " + idealGear);
                Shift(data.Telemetry.Gear, idealGear, shiftStyle);
            }
        }

        #endregion

        #region Control Chain Methods

        public bool Requires(JoyControls c)
        {
            switch (c)
            {
                    // Only required when changing
                case JoyControls.Throttle:
                    return true;

                case JoyControls.Clutch:
                    return IsShifting;

                    // All gears.
                case JoyControls.GearR:
                case JoyControls.Gear1:
                case JoyControls.Gear2:
                case JoyControls.Gear3:
                case JoyControls.Gear4:
                case JoyControls.Gear5:
                case JoyControls.Gear6:
                    return true;

                case JoyControls.GearRange2:
                case JoyControls.GearRange1:
                    return Main.Data.Active.TransmissionSupportsRanges;

                case JoyControls.Gear7:
                case JoyControls.Gear8:
                    return !Main.Data.Active.TransmissionSupportsRanges;

                case JoyControls.GearUp:
                case JoyControls.GearDown:
                    return true;

                default:
                    return false;
            }
        }

        public double GetAxis(JoyControls c, double val)
        {
            switch (c)
            {
                case JoyControls.Throttle:
                    transmissionThrottle = val > 0 && val < 1 ? val : 0;

                    if (ShiftFrame >= ActiveShiftPattern.Count)
                        return val;
                    return IsShifting ? ActiveShiftPattern.Frames[ShiftFrame].Throttle * val : val;

                case JoyControls.Clutch:
                    if (ShiftFrame >= ActiveShiftPattern.Count)
                        return val;
                    return ActiveShiftPattern.Frames[ShiftFrame].Clutch;

                default:
                    return val;
            }
        }

        public bool GetButton(JoyControls c, bool val)
        {
            switch (c)
            {
                case JoyControls.Gear1:
                    return GetShiftButton(1);
                case JoyControls.Gear2:
                    return GetShiftButton(2);
                case JoyControls.Gear3:
                    return GetShiftButton(3);
                case JoyControls.Gear4:
                    return GetShiftButton(4);
                case JoyControls.Gear5:
                    return GetShiftButton(5);
                case JoyControls.Gear6:
                    return GetShiftButton(6);
                case JoyControls.Gear7:
                    return GetShiftButton(7);
                case JoyControls.Gear8:
                    return GetShiftButton(8);
                case JoyControls.GearR:
                    return GetShiftButton(-1);
                case JoyControls.GearRange1:
                    return GetRangeButton(1);
                case JoyControls.GearRange2:
                    return GetRangeButton(2);

                    // TODO: Move gear up/down to main object
                    /*
            case JoyControls.GearUp:
                if(val)
                {
                    if(!DrivingInReverse && !ChangeModeFrozen)
                    {
                        // We're already going forwards.
                        // Change shifter profile
                        switch(Active)
                        {
                            case "Opa":
                                SetActiveConfiguration("Economy");
                                break;

                            case "Economy":
                                SetActiveConfiguration("Efficiency");
                                break;

                            case "Efficiency":
                                SetActiveConfiguration("Performance");
                                break;

                            case "Performance":
                                SetActiveConfiguration("PeakRpm");
                                break;

                            case "PeakRpm":
                                SetActiveConfiguration("Opa");
                                break;

                        }
                        ChangeModeFrozenUntill = DateTime.Now.Add(new TimeSpan(0, 0, 0, 0, 1000));
                    }
                    DrivingInReverse = false;
                }
                return false;

            case JoyControls.GearDown:
                if (val)
                    DrivingInReverse = true;
                return false;
                */
                default:
                    return val;
            }
        }

        private bool GetRangeButton(int r)
        {
            if (Main.Data.Active == null || !Main.Data.Active.TransmissionSupportsRanges) return false;
            if (IsShifting && ShiftCtrlNewRange != ShiftCtrlOldRange)
            {
                // More debug values
                // Going to range 1 when old gear was outside range 1,
                // and new is in range 1.
                var engagingToRange1 = ShifterOldGear >= 7 && ShifterNewGear < 7;

                // Range 2 is engaged when the old gear was range 1 or 3, and the new one is range 2.
                var engagingToRange2 = (ShifterOldGear < 7 || ShifterOldGear > 12) &&
                                       ShifterNewGear >= 7 && ShifterNewGear <= 12;

                // Range 2 is engaged when the old gear was range 1 or 2, and the new one is range 3.
                var engagingToRange3 = ShifterOldGear < 13 &&
                                       ShifterNewGear >= 13;

                var engageR1Status = false;
                var engageR2Status = false;

                if (ShiftCtrlOldRange == 0)
                {
                    if (ShiftCtrlNewRange == 1)
                        engageR1Status = true;
                    else engageR2Status = true;
                }
                else if (ShiftCtrlOldRange == 1)
                {
                    if (ShiftCtrlNewRange == 0)
                        engageR1Status = true;
                    else
                    {
                        engageR1Status = true;
                        engageR2Status = true;
                    }
                }
                else if (ShiftCtrlOldRange == 2)
                {
                    if (ShiftCtrlNewRange == 0)
                        engageR2Status = true;
                    else
                    {
                        engageR1Status = true;
                        engageR2Status = true;
                    }

                }

                switch (RangeButtonSelectPhase)
                {
                        // On
                    case 1:
                        if (r == 1) return engageR1Status;
                        if (r == 2) return engageR2Status;

                        return false;

                        // Off
                    case 2:
                        return false;

                        // Evaluate and set phase 1(on) / phase 2 (off) timings
                    default:

                        Debug.WriteLine("Shift " + ShifterOldGear + "(" + ShiftCtrlOldRange + ") to " + ShifterNewGear +
                                        "(" + ShiftCtrlNewRange + ")");
                        Debug.WriteLine("R1: " + engageR1Status + " / R2: " + engageR2Status);
                        if (r == 1 && !engageR1Status) return false;
                        if (r == 2 && !engageR2Status) return false;

                        RangeButtonFreeze1Untill = DateTime.Now.Add(new TimeSpan(0, 0, 0, 0, 100)); //150ms ON
                        RangeButtonFreeze2Untill = DateTime.Now.Add(new TimeSpan(0, 0, 0, 0, 1500)); //150ms OFF

                        Debug.WriteLine("Yes");
                        return true;
                }
            }

            return false;
        }


        private bool GetShiftButton(int b)
        {
            if (IsShifting)
            {
                if (ShiftFrame >= ActiveShiftPattern.Count) return false;
                if (ActiveShiftPattern.Frames[ShiftFrame].UseOldGear) return (b == ShiftCtrlOldGear);
                if (ActiveShiftPattern.Frames[ShiftFrame].UseNewGear) return (b == ShiftCtrlNewGear);
                return false;
            }

            return (b == ShiftCtrlNewGear);
        }

        private int shiftRetry = 0;

        public void TickControls()
        {
            if (IsShifting)
            {

                ShiftFrame++;
                if (ShiftFrame > ActiveShiftPattern.Count)
                    ShiftFrame = 0;
                if (shiftRetry < 10 && ShiftFrame > 4 && ActiveShiftPattern.Frames[ShiftFrame - 3].UseNewGear &&
                    GameGear != ShifterNewGear)
                {
                    // So we are shifting, check lagging by 1, and new gear doesn't work
                    // We re-shfit
                    var tmp = ShiftFrame;
                    Shift(GameGear, ShifterNewGear, "up_1thr");
                    ShiftFrame = tmp - 4;
                    shiftRetry++;

                    if (ShiftCtrlNewRange != ShiftCtrlOldRange)
                    {

                        ShiftFrame = 0;

                        RangeButtonFreeze1Untill = DateTime.Now;
                        RangeButtonFreeze2Untill = DateTime.Now;

                    }
                }
                else if (ShiftFrame >= ActiveShiftPattern.Count)
                {
                    IsShifting = false;
                    TransmissionFreezeUntill = DateTime.Now.Add(new TimeSpan(0, 0, 0, 0, ShiftDeadTime));
                }
            }
        }

        #endregion

        #region Implementation of IConfigurable

        public IEnumerable<string> AcceptsConfigs { get { return new [] { "ShiftCurve" }; } }
        public void ResetParameters()
        {
            configuration = new ShifterTableConfiguration(ShifterTableConfigurationDefault.PeakRpm, Main.Drivetrain, 10);
        }

        public int speedHoldoff { get; private set; }
        public int ShiftDeadSpeed { get; private set; }
        public int ShiftDeadTime { get; private set; }
        public string GeneratedShiftTable { get; private set; }

        public void ApplyParameter(IniValueObject obj)
        {
            switch(obj.Key)
            {
                case "ShiftDeadSpeed":
                    ShiftDeadSpeed = obj.ReadAsInteger();
                    break;
                case "ShiftDeadTime":
                    ShiftDeadTime = obj.ReadAsInteger();
                    break;

                case "GenerateSpeedHoldoff":
                    speedHoldoff = obj.ReadAsInteger();
                    break;

                case "Generate":
                    var def = ShifterTableConfigurationDefault.PeakRpm;
                    GeneratedShiftTable = obj.ReadAsString();
                    switch (GeneratedShiftTable)
                    {
                        case "Economy":
                            def = ShifterTableConfigurationDefault.Economy;
                            break;
                        case "Efficiency":
                            def = ShifterTableConfigurationDefault.Efficiency;
                            break;
                        case "Opa":
                            def = ShifterTableConfigurationDefault.AlsEenOpa;
                            break;
                        case "PeakRpm":
                            def = ShifterTableConfigurationDefault.PeakRpm;
                            break;
                        case "Performance":
                            def = ShifterTableConfigurationDefault.Performance;
                            break;
                    }

                    configuration = new ShifterTableConfiguration(def, Main.Drivetrain, speedHoldoff);
                    break;
            }
        }


        public IEnumerable<IniValueObject> ExportParameters()
        {
            List<IniValueObject> obj = new List<IniValueObject>();
            obj.Add(new IniValueObject(AcceptsConfigs, "ShiftDeadSpeed", ShiftDeadSpeed.ToString()));
            obj.Add(new IniValueObject(AcceptsConfigs, "ShiftDeadTime", ShiftDeadTime.ToString()));
            obj.Add(new IniValueObject(AcceptsConfigs, "GenerateSpeedHoldoff", speedHoldoff.ToString()));
            obj.Add(new IniValueObject(AcceptsConfigs, "Generate", GeneratedShiftTable));
            //TODO: Tables not supported yet.
            return obj;
        }

        #endregion
    }
}