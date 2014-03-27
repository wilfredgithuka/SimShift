using System.Collections.Generic;
using System.Linq;
using SimShift.Data;

namespace SimShift.Services
{
    public class ControlChain
    {
        private List<IControlChainObj> chain = new List<IControlChainObj>();

        private List<JoyControls> Axis = new List<JoyControls>();
        private List<JoyControls> Buttons = new List<JoyControls>();
 
        public ControlChain()
        {
            chain.Add(Main.CruiseControl);
            chain.Add(Main.Speedlimiter);
            chain.Add(Main.Transmission);
            chain.Add(Main.Antistall);

            Axis.Add(JoyControls.Throttle);
            Axis.Add(JoyControls.Brake);
            Axis.Add(JoyControls.Clutch);

            Buttons.Add(JoyControls.Gear1);
            Buttons.Add(JoyControls.Gear2);
            Buttons.Add(JoyControls.Gear3);
            Buttons.Add(JoyControls.Gear4);
            Buttons.Add(JoyControls.Gear5);
            Buttons.Add(JoyControls.Gear6);
            Buttons.Add(JoyControls.GearR);
            Buttons.Add(JoyControls.GearRange1);
            Buttons.Add(JoyControls.GearRange2);
            Buttons.Add(JoyControls.GearUp);
            Buttons.Add(JoyControls.GearDown);
            Buttons.Add(JoyControls.CruiseControl);
        }

        public void Tick(Ets2DataMiner data)
        {
            // We take all controller input
            var buttonValues = Buttons.ToDictionary(c => c, Main.GetButtonIn);
            var axisValues = Axis.ToDictionary(c => c, Main.GetAxisIn);

            foreach (var obj in chain)
            {
                obj.TickTelemetry(data);
            }

            // Put it serially through each control block
            // Each time a block requires a control, it receives the current value of that control
            foreach(var obj in chain)
            {
                buttonValues = buttonValues.ToDictionary(c => c.Key, k => obj.Requires(k.Key) ? obj.GetButton(k.Key, k.Value) : k.Value);
                axisValues = axisValues.ToDictionary(c => c.Key, k => obj.Requires(k.Key) ? obj.GetAxis(k.Key, k.Value) : k.Value);
                obj.TickControls();
            }

            // And then put them onto our own controller.
            foreach (var b in buttonValues)
            {
                Main.SetButtonOut(b.Key, b.Value);
            }
            foreach (var b in axisValues)
            {
                var v = b.Value;
                if (v > 1) v = 1;
                if (v < 0) v = 0;
                Main.SetAxisOut(b.Key,v);
            }
        }

    }
}