﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeslaSCAN;

namespace CANBUS {
  class ModelSPackets : Parser {

    double nominalFullPackEnergy;
    double amp;
    double volt;
    double power;
    double mechPower;
    double fMechPower;
    double speed;
    double drivePowerMax;
    double torque;
    double chargeTotal;
    double dischargeTotal;
    double odometer;
    double tripDistance;
    double charge;
    double discharge;
    bool metric = true;
    private double frTorque;
    private double dcChargeTotal;
    private double acChargeTotal;
    private double regenTotal;
    private double energy;
    private double regen;
    private double acCharge;
    private double dcCharge;
    private double nominalRemaining;
    private double buffer;
    private double soc;
    private double fl;
    private double fr;
    private double rl;
    private double rr;
    private int frpm;
    private int rrpm;
    private bool feet;
    private bool seat;
    private bool win;
    private double dcOut;
    private double dcIn;
    private double rInput;
    private double fInput;
    private double fDissipation;
    private double combinedMechPower;
    private double rDissipation;
    private double hvacPower;
    private bool dissipationUpdated;
    private int dissipationTimeStamp;
    private int statorTemp;
    private int inverterTemp;
    int numCells;

    protected override PacketDefinitions GetPacketDefinitions()
    {
        return PacketDefinitions.GetSMTModelS();
    }

    public ModelSPackets(MainWindow mainWindow) : base(mainWindow) {

      /* tags:
      p: performance
      t: trip
      b: battery
      c: temperature
      f: front drive unit
      s: startup (app will wait until these packets are found before starting 'normal' mode)
      i: imperial
      m: metric
      i: ignore
      */

      Packet p;

      /*packets.Add(0x256, p = new Packet(0x256, this));
      p.AddValue("Metric", "bool", "s", (bytes) => {
        metric = Convert.ToBoolean(bytes[3] & 0x80);
        if (metric) {
          foreach (var packet in packets)
            foreach (var v in packet.Value.values)
              if (v.tag.Contains("i"))
                packet.Value.values.Remove(v);
        } else {
          foreach (var packet in packets)
            foreach (var v in packet.Value.values)
              if (v.tag.Contains("m"))
                packet.Value.values.Remove(v);
        }
        return metric ? 1 : 0;
      });*/

      packets.Add(0x102, p = new Packet(0x102, this));
      p.AddValue("Battery voltage", " V", "bpr", (bytes) => volt =
          (bytes[0] + (bytes[1] << 8)) / 100.0);
      p.AddValue("Battery current", " A", "b", (bytes) => amp =
          1000 - ((Int16)((((bytes[3] & 0x7F) << 8) + bytes[2]) << 1)) / 20.0);
      p.AddValue("Battery power", " kW", "bpe", (bytes) => power = amp * volt / 1000.0);
      //p.AddValue("cell average", "Vc", "bp", (bytes) => numCells > 70 ? volt / numCells : (double?)null);
      //p.AddValue("negative terminal", "C", (bytes) => ((bytes[6] + ((bytes[7] & 0x07) << 8))) * 0.1 - 10);


      packets.Add(0x210, p = new Packet(0x210, this));
      p.AddValue("DC-DC current", "A12", "b", (bytes) => bytes[4]);
      p.AddValue("DC-DC voltage", "V12", "b", (bytes) => bytes[5] / 10.0);
      p.AddValue("DC-DC coolant inlet", "C", "c", (bytes) => ((bytes[2] - (2 * (bytes[2] & 0x80))) * 0.5) + 40);
      p.AddValue("DC-DC input power", "W", "be", (bytes) => dcIn = (bytes[3] * 16));
      p.AddValue("DC-DC output power", "W", "b", (bytes) => dcOut = (bytes[4] * bytes[5] / 10.0));
      p.AddValue("DC-DC efficiency", "%", "e", (bytes) => dcOut / dcIn * 100.0);
      p.AddValue("HV power", " kW", "e", (bytes) => power - dcIn / 1000.0);
      p.AddValue("Heating/cooling", "kW", "eh", (bytes) => {
        if (dissipationUpdated ||
          DateTime.Now.Millisecond > dissipationTimeStamp + 2000) {
          hvacPower = hvacPower * 0.99 + (power - (rInput + fInput) - (dcIn / 1000.0)) * 0.01;
          dissipationUpdated = false;
          return hvacPower;
        } else return (double?)null;
      }, new int[] { 0x102, 0x266, 0x2E5 });


      packets.Add(0x306, p = new Packet(0x306, this));
      p.AddValue("Rr coolant inlet", "C", "c", (bytes) => bytes[5] == 0 ? (double?)null : bytes[5] - 40);
      p.AddValue("Rr inverter PCB", "C", "", (bytes) => bytes[0] - 40);
      p.AddValue("Rr stator", "C", "cp", (bytes) => statorTemp = bytes[2] - 40);
      p.AddValue("Rr DC capacitor", "C", "", (bytes) => bytes[3] - 40);
      p.AddValue("Rr heat sink", "C", "c", (bytes) => bytes[4] - 40);
      p.AddValue("Rr inverter", "C", "c", (bytes) => inverterTemp = bytes[1] - 40);
      p.AddValue("Rr stator %", "%", "c", (bytes) => (bytes[7] * .4));
      p.AddValue("Rr inverter %", "%", "c", (bytes) => (bytes[6] * .4));
      /*p.AddValue("Rr stator max", "%", "c", (bytes) => (bytes[7] *.4 / 100.0 * statorTemp ));
      p.AddValue("Rr inverter max", "%", "c", (bytes) => (bytes[6] * .4 / 100.0 * inverterTemp));*/


      packets.Add(0x1D4, p = new Packet(0x1D4, this));
      p.AddValue("Fr torque measured", "Nm", "pf", (bytes) => frTorque =
         (bytes[5] + ((bytes[6] & 0x1F) << 8) - (512 * (bytes[6] & 0x10))) * 0.25);
      p.AddValue("Rr/Fr torque bias", "%", "pf",
        (bytes) => Math.Abs(frTorque) + Math.Abs(torque) == 0 ? 50 : Math.Abs(torque) / (Math.Abs(frTorque) + Math.Abs(torque)) * 100);

      packets.Add(0x154, p = new Packet(0x154, this));
      p.AddValue("Rr torque measured", "Nm", "p", (bytes) => torque =
         (bytes[5] + ((bytes[6] & 0x1F) << 8) - (512 * (bytes[6] & 0x10))) * 0.25);
      //p.AddValue("Pedal position A", "%", "",  (bytes) => bytes[2] * 0.4);
      p.AddValue("Watt pedal", "%", "i", (bytes) => bytes[3] * 0.4);
      /*p.AddValue("HP 'measured'", "HP", "p",
          (bytes) => (torque * rpm / 9549 * kw_to_hp));*/

      packets.Add(0x2E5, p = new Packet(0x2E5, this));
      p.AddValue("Fr mech power", " kW", "fe", (bytes) => fMechPower =
          ((bytes[2] + ((bytes[3] & 0x7) << 8)) - (512 * (bytes[3] & 0x4))) / 2.0);
      p.AddValue("Fr dissipation", " kW", "f", (bytes) => fDissipation = bytes[1] * 125.0 / 1000.0);
      p.AddValue("Fr input power", " kW", "e", (bytes) => fInput = fMechPower + fDissipation);
      p.AddValue("Fr mech power HP", "HP", "pf", (bytes) => fMechPower * kw_to_hp);
      p.AddValue("Fr stator current", "A", "f", (bytes) => bytes[4] + ((bytes[5] & 0x7) << 8));
      p.AddValue("Fr drive power max", " kW", "bc", (bytes) => drivePowerMax =
          (((bytes[6] & 0x3F) << 5) + ((bytes[5] & 0xF0) >> 3)) + 1);
      p.AddValue("Mech power combined", " kW", "f", (bytes) => combinedMechPower = mechPower + fMechPower);
      p.AddValue("HP combined", "HP", "pf", (bytes) => (mechPower + fMechPower) * kw_to_hp);
      p.AddValue("Fr efficiency", "%", "e", (bytes) => Math.Abs(fMechPower) > Math.Abs(fInput) ? 100 : fMechPower / fInput * 100.0);
      p.AddValue("Fr+Rr efficiency", "%", "e", (bytes) => Math.Abs(mechPower + fMechPower) > Math.Abs(rInput + fInput) ? 100 : mechPower / rInput * 100.0);

      packets.Add(0x266, p = new Packet(0x266, this));
      p.AddValue("Rr inverter 12V", "V12", "", (bytes) => bytes[0] / 10.0);
      p.AddValue("Rr mech power", " kW", "e", (bytes) => mechPower =
          ((bytes[2] + ((bytes[3] & 0x7) << 8)) - (512 * (bytes[3] & 0x4))) / 2.0);
      p.AddValue("Rr dissipation", " kW", "", (bytes) => {
        rDissipation = bytes[1] * 125.0 / 1000.0;
        dissipationUpdated = true;
        dissipationTimeStamp = DateTime.Now.Millisecond;
        return rDissipation;
      });
      p.AddValue("Rr input power", " kW", "e", (bytes) => rInput = mechPower + rDissipation);
      p.AddValue("Rr mech power HP", "HP", "p", (bytes) => mechPower * kw_to_hp);
      p.AddValue("Rr stator current", "A", "", (bytes) => bytes[4] + ((bytes[5] & 0x7) << 8));
      p.AddValue("Rr regen power max", "KW", "b", (bytes) => (bytes[7] * 4) - 200);
      p.AddValue("Rr drive power max", "KW", "b", (bytes) => drivePowerMax =
          (((bytes[6] & 0x3F) << 5) + ((bytes[5] & 0xF0) >> 3)) + 1);
      p.AddValue("Rr efficiency", "%", "e", (bytes) => Math.Abs(mechPower) > Math.Abs(rInput) ? 100 : mechPower / rInput * 100.0);
      p.AddValue("Non-propulsive power", "kW", "e", (bytes) => power - (rInput + fInput));
      p.AddValue("Car efficiency", "%", "e", (bytes) => Math.Abs(mechPower + fMechPower) > Math.Abs(power) ? 100 : (mechPower + fMechPower) / power * 100.0);


      packets.Add(0x145, p = new Packet(0x145, this));
      p.AddValue("Fr torque estimate", "Nm", "f",
          (bytes) => ((bytes[0] + ((bytes[1] & 0xF) << 8)) - (512 * (bytes[1] & 0x8))) / 2);

      packets.Add(0x116, p = new Packet(0x116, this));
      p.AddValue("Rr torque estimate", "Nm", "",
          (bytes) => ((bytes[0] + ((bytes[1] & 0xF) << 8)) - (512 * (bytes[1] & 0x8))) / 2);
      p.AddValue("Speed", "km|h", "",
          (bytes) => speed = ((bytes[2] + ((bytes[3] & 0xF) << 8)) - 500) / 20.0 * miles_to_km);
      /*p.AddValue("Consumption", "wh|km", "p",
          (bytes) => power / speed * 1000,
          new int[] { 0x102 });*/

      packets.Add(0x382, p = new Packet(0x382, this));
      p.AddValue("Nominal full pack", "kWh", "br", (bytes) => nominalFullPackEnergy = (bytes[0] + ((bytes[1] & 0x03) << 8)) * 0.1);
      p.AddValue("Nominal remaining", "kWh", "br", (bytes) => nominalRemaining = ((bytes[1] >> 2) + ((bytes[2] & 0x0F) * 64)) * 0.1);
      p.AddValue("Expected remaining", "kWh", "r", (bytes) => ((bytes[2] >> 4) + ((bytes[3] & 0x3F) * 16)) * 0.1);
      p.AddValue("Ideal remaining", "kWh", "r", (bytes) => ((bytes[3] >> 6) + ((bytes[4] & 0xFF) * 4)) * 0.1);
      p.AddValue("To charge complete", "kWh", "", (bytes) => (bytes[5] + ((bytes[6] & 0x03) << 8)) * 0.1);
      p.AddValue("Energy buffer", "kWh", "br", (bytes) => buffer = ((bytes[6] >> 2) + ((bytes[7] & 0x03) * 64)) * 0.1);
      p.AddValue("SOC", "%", "br", (bytes) => soc = (nominalRemaining - buffer) / (nominalFullPackEnergy - buffer) * 100.0);
      p.AddValue("Usable full pack", "kWh", "br", (bytes) => (nominalFullPackEnergy - buffer));
      p.AddValue("Usable remaining", "kWh", "br", (bytes) => (nominalRemaining - buffer));


      packets.Add(0x302, p = new Packet(0x302, this));
      p.AddValue("SOC Min", "%", "br", (bytes) => (bytes[0] + ((bytes[1] & 0x3) << 8)) / 10.0);
      p.AddValue("SOC UI", "%", "br", (bytes) => ((bytes[1] >> 2) + ((bytes[2] & 0xF) << 6)) / 10.0);

      p.AddValue("DC Charge total", "kWH", "bs",
            (bytes) => {
              if (bytes[2] >> 4 == 0) {
                dcChargeTotal =
                  (bytes[4] +
                  (bytes[5] << 8) +
                  (bytes[6] << 16) +
                  (bytes[7] << 24)) / 1000.0;
                /*if (mainActivity.currentTab.trip.dcChargeStart == 0)
                  mainActivity.currentTab.trip.dcChargeStart = dcChargeTotal;
                dcCharge = dcChargeTotal - mainActivity.currentTab.trip.dcChargeStart;*/
                return dcChargeTotal;
              } else return (double?)null;
            });

      p.AddValue("AC Charge total", "kWH", "bs",
        (bytes) => {
          if (bytes[2] >> 4 == 1) {
            acChargeTotal =
              (bytes[4] +
              (bytes[5] << 8) +
              (bytes[6] << 16) +
              (bytes[7] << 24)) / 1000.0;
            /*if (mainActivity.currentTab.trip.acChargeStart == 0)
              mainActivity.currentTab.trip.acChargeStart = acChargeTotal;
            acCharge = acChargeTotal - mainActivity.currentTab.trip.acChargeStart;*/
            return acChargeTotal;
          } else return (double?)null;
        });
      /*p.AddValue("DC Charge", "kWh", "ti",
        (bytes) => dcChargeTotal - mainActivity.currentTab.trip.dcChargeStart);
      p.AddValue("AC Charge", "kWh", "ti",
        (bytes) => acChargeTotal - mainActivity.currentTab.trip.acChargeStart);*/

      packets.Add(0x3D2, p = new Packet(0x3D2, this));
      p.AddValue("Charge total", "kWH", "bs",
                (bytes) => {
                  chargeTotal =
                    (bytes[0] +
                    (bytes[1] << 8) +
                    (bytes[2] << 16) +
                    (bytes[3] << 24)) / 1000.0;
                  /*if (mainActivity.currentTab.trip.chargeStart == 0)
                    mainActivity.currentTab.trip.chargeStart = chargeTotal;
                  charge = chargeTotal - mainActivity.currentTab.trip.chargeStart;*/
                  return chargeTotal;
                });

      p.AddValue("Discharge total", "kWH", "b",
          (bytes) => {
            dischargeTotal =
              (bytes[4] +
              (bytes[5] << 8) +
              (bytes[6] << 16) +
              (bytes[7] << 24)) / 1000.0;
            /*if (mainActivity.currentTab.trip.dischargeStart == 0)
              mainActivity.currentTab.trip.dischargeStart = dischargeTotal;
            discharge = dischargeTotal - mainActivity.currentTab.trip.dischargeStart;*/
            return dischargeTotal;
          });
      p.AddValue("Regenerated", "kWh", "tr",
          (bytes) => regen = charge - acCharge - dcCharge);
      p.AddValue("Energy", "kWh", "tr",
          (bytes) => energy = discharge - regen);
      p.AddValue("Discharge", "kWh", "r",
          (bytes) => discharge);
      p.AddValue("Charge", "kWh", "r",
          (bytes) => charge);
      p.AddValue("Regen total", "kWH", "b",
        (bytes) => regenTotal = chargeTotal - acChargeTotal - dcChargeTotal,
        new int[] { 0x302 });
      p.AddValue("Regen %", "% ", "tr",
          (bytes) => energy > 0 ? regen / discharge * 100 : (double?)null);//,
                                                                           //new int[] { 0x302 });

      p.AddValue("Discharge cycles", "x", "b",
          (bytes) => nominalFullPackEnergy > 0 ? dischargeTotal / nominalFullPackEnergy : (double?)null,
          new int[] { 0x382 });
      p.AddValue("Charge cycles", "x", "b",
          (bytes) => nominalFullPackEnergy > 0 ? chargeTotal / nominalFullPackEnergy : (double?)null,
          new int[] { 0x382 });

      packets.Add(0x562, p = new Packet(0x562, this));
      p.AddValue("Battery odometer", "Km", "b",
          (bytes) => odometer = (bytes[0] + (bytes[1] << 8) + (bytes[2] << 16) + (bytes[3] << 24)) / 1000.0 * miles_to_km);
      /*p.AddValue("Trip distance", "km", "tsr",
          (bytes) => {
            if (mainActivity.currentTab.trip.odometerStart == 0)
              mainActivity.currentTab.trip.odometerStart = odometer;
            return tripDistance = odometer - mainActivity.currentTab.trip.odometerStart;*
          });*/
      p.AddValue("Trip consumption", "wh|km", "tr",
          (bytes) => tripDistance > 0 ? energy / tripDistance * 1000 : (double?)null,
          new int[] { 0x3D2 });
      /*p.AddValue("Lifetime consumption", "wh/km", "bt",
          (bytes) => odometer > 0 ? dischargeTotal / odometer * 1000 : (double?)null);*/

      packets.Add(0x115, p = new Packet(0x115, this));
      p.AddValue("Fr motor RPM", "RPM", "",
          (bytes) => frpm = (bytes[4] + (bytes[5] << 8)) - (512 * (bytes[5] & 0x80)));
      // 0x115 --- DIS_motorRPM = (data[4] + (data[5]<<8)) - (512 * (data[5]&0x80));

      packets.Add(0x106, p = new Packet(0x106, this));
      p.AddValue("Rr motor RPM", "RPM", "",
          (bytes) => rrpm = (bytes[4] + (bytes[5] << 8)) - (512 * (bytes[5] & 0x80)));

      packets.Add(0x232, p = new Packet(0x232, this));
      p.AddValue("Max discharge power", "kW", "b", (bytes) => (bytes[2] + (bytes[3] << 8)) / 100.0);
      p.AddValue("Max regen power", "kW", "b", (bytes) => (bytes[0] + (bytes[1] << 8)) / 100.0);

      packets.Add(0x168, p = new Packet(0x168, this));
      p.AddValue("Brake pedal", "%", "i",
          (bytes) => (bytes[0] + (bytes[1] << 8)) - 3239);

      packets.Add(0x00E, p = new Packet(0x00E, this));
      p.AddValue("Steering angle", "deg", "i",
        (bytes) => (((bytes[0] << 8) + bytes[1] - 8200.0) / 10.0));

      packets.Add(0x338, p = new Packet(0x338, this));
      p.AddValue("Rated range", "km", "br",
        (bytes) => (bytes[0] + (bytes[1] << 8)) * miles_to_km);
      p.AddValue("Typical range", "km", "br",
        (bytes) => (bytes[2] + (bytes[3] << 8)) * miles_to_km);
      p.AddValue("Full rated range", "km", "br",
        (bytes) => (bytes[0] + (bytes[1] << 8)) * miles_to_km / (soc == 0.0 ? 100.0 : soc) * 100.0);
      p.AddValue("Full typical range", "km", "br",
        (bytes) => (bytes[2] + (bytes[3] << 8)) * miles_to_km / (soc == 0.0 ? 100.0 : soc) * 100.0);


      packets.Add(0x2A8, p = new Packet(0x2A8, this));
      p.AddValue("Front left", "WRPM", "e",
        (bytes) => fl = (bytes[4] + (bytes[3] << 8)) * 0.7371875 / 9.73);
      p.AddValue("Front right", "WRPM", "e",
        (bytes) => fr = (bytes[6] + (bytes[5] << 8)) * 0.7371875 / 9.73);
      p.AddValue("Front drive ratio", ":1", "e",
        (bytes) => fl + fr > 20 ? frpm / ((fl + fr) / 2) : (double?)null,
        new int[] { 0x115 });


      packets.Add(0x288, p = new Packet(0x288, this));
      p.AddValue("Rear left", "WRPM", "e",
        (bytes) => rl = (bytes[4] + (bytes[3] << 8)) * 0.7371875 / 9.73);
      p.AddValue("Rear right", "WRPM", "e",
        (bytes) => rr = (bytes[7] + (bytes[6] << 8)) * 0.7371875 / 9.73);
      p.AddValue("Rear drive ratio", ":1", "e",
        (bytes) => rl + rr > 20 ? rrpm / ((rl + rr) / 2) : (double?)null,
        new int[] { 0x106 });

      packets.Add(0x6F2, p = new Packet(0x6F2, this));
      p.AddValue("Last cell block updated", "xb", "", (bytes) => {
        Int64 data = BitConverter.ToInt64(bytes, 0);
        if (bytes[0] < 24) {
          int cell = 0;
          for (int i = 0; i < 4; i++)
            UpdateItem("Cell " + (cell = ((bytes[0]) * 4 + i + 1)).ToString().PadLeft(2) + " voltage"
              , "zVC"
              , "z"
              , bytes[0]
              , ((data >> ((14 * i) + 8)) & 0x3FFF) * 0.000305
              , 0x6F2);
          if (cell > numCells)
            numCells = cell;
          var values = items.Where(x => x.Value.unit == "zVC");
          double min = values.Min(x => (double) x.Value.GetValue(false));
          double max = values.Max(x => (double) x.Value.GetValue(false));
          double avg = values.Average(x => (double) x.Value.GetValue(false));
          UpdateItem("Cell min", "Vc", "bz", 0, min, 0x6F2);
          UpdateItem("Cell avg", "Vc", "bpz", 1, avg, 0x6F2);
          UpdateItem("Cell max", "Vc", "bz", 2, max, 0x6F2);
          UpdateItem("Cell diff", "Vcd", "bz", 3, max - min, 0x6F2);
        } else {
          for (int i = 0; i < 4; i++)
            UpdateItem("Cell " + ((bytes[0] - 24) * 4 + i + 1).ToString().PadLeft(2) + " temp"
              , "zCC"
              , "c"
              , bytes[0]
              , ((Int16)(((data >> ((14 * i) + 6)) & 0xFFFC)) * 0.0122 / 4.0)
              , 0x6F2);
          var values = items.Where(x => x.Value.unit == "zCC");
          double min = values.Min(x => (double) x.Value.GetValue(false));
          double max = values.Max(x => (double) x.Value.GetValue(false));
          double avg = values.Average(x => (double) x.Value.GetValue(false));
          UpdateItem("Cell temp min", "c", "bcz", 0, min, 0x6F2);
          UpdateItem("Cell temp avg", "c", "bcpz", 1, avg, 0x6F2);
          UpdateItem("Cell temp max", "c", "bcz", 2, max, 0x6F2);
          UpdateItem("Cell temp diff", "Cd", "bcz", 3, max - min, 0x6F2);
        }
          return bytes[0];
      });

      // these are a bit stupid, but they are placeholders for the filters to be generated correctly.
      p.AddValue("Cell temp min", "C", "b", null);
      p.AddValue("Cell temp avg", "C", "bcp", null);
      p.AddValue("Cell temp max", "C", "b", null);
      p.AddValue("Cell temp diff", "Cd", "bc", null);
      p.AddValue("Cell min", "Vc", "b", null);
      p.AddValue("Cell avg", "Vc", "bpzr", null);
      p.AddValue("Cell max", "Vc", "b", null);
      p.AddValue("Cell diff", "Vcd", "bz", null);
      for (int i = 1; i <= 96; i++)
        p.AddValue("Cell " + i.ToString().PadLeft(2) + " voltage"
          , "zVC"
          , "z", null);
      for (int i = 1; i <= 32; i++)
        p.AddValue("Cell " + i.ToString().PadLeft(2) + " temp"
          , "zCC"
          , "c"
          , null);


      packets.Add(0x754, p = new Packet(0x754, this));
      p.AddValue("Last 51E block updated", "xb", "", (bytes) => {
        Int64 data = BitConverter.ToInt64(bytes, 0);
        int cell = 0;
        /*for (int i = 0; i < 4; i++)
          UpdateItem("Cell " + (cell = ((bytes[0]) * 4 + i + 1)).ToString().PadLeft(2) + " voltage"
            , "zVC"
            , "z"
            , bytes[0]
            , ((data >> ((14 * i) + 8)) & 0x3FFF) * 0.000305
            , p.id);
        if (cell > numCells)
          numCells = cell;
      } else*/
        //if ((bytes[0]) == 0)
        for (int i = 0; i < 8; i += 2)
          UpdateItem("754 Block " + (bytes[0]) + ":" + i.ToString().PadLeft(2)
            , "zzz"
            , "c"
            , bytes[0]
            , bytes[i] + (bytes[i + 1] << 8)
            , 0x754);
        return bytes[0];
      });


      packets.Add(0x125, p = new Packet(0x125, this));
      p.AddValue("125 0", "km", "br",
        (bytes) => (/*bytes[2]/256.0 + */ (bytes[0] << 8) - (512 * (bytes[0] & 0x80))));
      p.AddValue("125 1", "km", "br",
        (bytes) => (/*bytes[2]/256.0 + */ (bytes[1] << 8) - (512 * (bytes[1] & 0x80))));

      p.AddValue("125 2", "km", "br",
       (bytes) => (/*bytes[2]/256.0 + */ ((bytes[2] & 0xF0) << 4) /*- (512 * (bytes[2] & 0x80)))*/));

      //p.AddValue("125 2", "km", "br",
      //(bytes) => (bytes[1] + (bytes[0] << 8))-((bytes[0]<<8)& 0x80));
      //(bytes) => (bytes[2]));
      //p.AddValue("125 3", "km", "br",
      //(bytes) => (bytes[1] + (bytes[0] << 8))-((bytes[0]<<8)& 0x80));
      //(bytes) => (bytes[3]));
      //p.AddValue("125 1", "km", "br",
      //(bytes) => (bytes[2] + (bytes[3] << 8)));

      packets.Add(0x126, p = new Packet(0x126, this));
      p.AddValue("126 Stator current", " A", "b", (bytes) => amp =
         ((Int16)((((bytes[3] & 0x7F) << 8) + bytes[2]) << 1)) / 2.0);
      p.AddValue("126 Battery Voltage", "V", "br",
          (bytes) => (bytes[0] + (bytes[1] << 8) - (512 * (bytes[1] & 0x80))) / 2.0);
      p.AddValue("126 4", "km", "br",
        (bytes) => (bytes[4]));
      p.AddValue("126 5", "km", "br",
        (bytes) => (bytes[5]));
      p.AddValue("126 6", "km", "br",
        (bytes) => (bytes[6]));
      p.AddValue("126 7", "km", "br",
        (bytes) => (bytes[7]));

      packets.Add(0x1F8, p = new Packet(0x1F8, this));
      p.AddValue("1F8 0-1", "km", "br", (bytes) =>
         (((bytes[0] << 8) + bytes[1] - 500)));
      //  (bytes) => (bytes[1] + (bytes[0] << 8))/* - (512 * (bytes[1] & 0x80))*/);
      /*p.AddValue("1F8 2", "km", "br",
          (bytes) => (bytes[2]));*/
      /* p.AddValue("1F8 3", "km", "br",
           (bytes) => (bytes[3]));*/
      p.AddValue("1F8 4", "km", "br",
        (bytes) => ((bytes[4] + ((bytes[5] & 0xF) << 8)) - 2000));
      /*p.AddValue("1F8 5", "km", "br",
        (bytes) => (bytes[5]));*/
      /*p.AddValue("1F8 6", "km", "br",
        (bytes) => (bytes[6]));
      p.AddValue("1F8 7", "km", "br",
        (bytes) => (bytes[7]));*/


      packets.Add(0x2AA, p = new Packet(0x2AA, this));
      p.AddValue("HVAC feet", "km", "br",
          (bytes) => {
            var set1 = bytes[2] & 0x07;
            feet = false;
            seat = false;
            win = false;
            switch (set1) {
              case 1:
                seat = true;
                break;
              case 2:
                feet = true;
                seat = true;
                break;
              case 3:
                feet = true;
                break;
              case 4:
                feet = true;
                win = true;
                break;
              case 5:
                win = true;
                break;
              case 6:
                feet = true;
                seat = true;
                win = true;
                break;
              case 7:
                seat = true;
                win = true;
                break;
            }
            return feet ? 1 : 0;
          });
      p.AddValue("HVAC seat", "km", "br",
          (bytes) => seat ? 1 : 0);
      p.AddValue("HVAC window", "km", "br",
          (bytes) => win ? 1 : 0);

      p.AddValue("HVAC recycle", "km", "br",
          (bytes) => {
            return (bytes[3] & 0x10) >> 4;
          });

      // 0xDEADBEEF

      p.AddValue("HVAC recycle2", "0", "eh",
     (bytes) => {
       return (bytes[3] & 0x8) >> 3;
     });

      p.AddValue("HVAC A/C", "km", "br",
          (bytes) => {
            var set3 = bytes[4] & 0x01;
            return set3;
          });
      p.AddValue("HVAC on/off", "km", "br",
          (bytes) =>
             (bytes[3] & 0x10) >> 4 == 0 ? 1 : 0);

      p.AddValue("HVAC fan speed", "km", "br",
          (bytes) => (bytes[2] & 0xf0) >> 4);

      p.AddValue("HVAC Temp1", "km", "br",
          (bytes) => bytes[0] / 2);
      p.AddValue("HVAC Temp2", "km", "br",
          (bytes) => bytes[1] / 2);



      /*p.AddValue("Last 754 block updated", "xb", "", (bytes) => {
        Int64 data = BitConverter.ToInt64(bytes, 0);
        int cell = 0;
        /*for (int i = 0; i < 4; i++)
          UpdateItem("Cell " + (cell = ((bytes[0]) * 4 + i + 1)).ToString().PadLeft(2) + " voltage"
            , "zVC"
            , "z"
            , bytes[0]
            , ((data >> ((14 * i) + 8)) & 0x3FFF) * 0.000305
            , p.id);
        if (cell > numCells)
          numCells = cell;
      } else*/
      /*  for (int i = 0; i < 7; i+=2)
          UpdateItem("754 Block " + (bytes[0]) + ":" + i.ToString().PadLeft(2)
            , "zzZ"
            , "c"
            , bytes[0]
            , bytes[i] + (bytes[i + 1] << 8)
            , 0x754);
        return bytes[0];
      });*/

      packets.Add(0x51E, p = new Packet(0x51E, this));
      p.AddValue("Last 51E block updated", "xb", "", (bytes) => {
        Int64 data = BitConverter.ToInt64(bytes, 0);
        int cell = 0;
        /*for (int i = 0; i < 4; i++)
          UpdateItem("Cell " + (cell = ((bytes[0]) * 4 + i + 1)).ToString().PadLeft(2) + " voltage"
            , "zVC"
            , "z"
            , bytes[0]
            , ((data >> ((14 * i) + 8)) & 0x3FFF) * 0.000305
            , p.id);
        if (cell > numCells)
          numCells = cell;
      } else*/
        if ((bytes[0] & 0xF0 >> 4) == 1)
          for (int i = 0; i < 8; i += 2)
            UpdateItem("51E Block " + (bytes[0] & 0xF0 >> 4) + ":" + i.ToString().PadLeft(2)
              , "zzz"
              , "c"
              , bytes[0]
              , bytes[i] + (bytes[i + 1] << 8)
              , 0x51E);
        return bytes[0] & 0xF0 >> 4;
      });



      packets.Add(0x222, p = new Packet(0x222, this));
      p.AddValue("Charge rate", "??", "br",
        (bytes) => (bytes[0] + (bytes[1] << 8)) / 100.0);
      p.AddValue("Charger volt", "V", "br",
        (bytes) => (bytes[2] + (bytes[3] << 8)) / 100.0);


      packets.Add(0x262, p = new Packet(0x262, this));

      p.AddValue("DC Charge amps1", "??", "br",
      (bytes) => ((Int16)((((bytes[3] & 0x7F) << 8) + bytes[2]) << 1)) / 20.0);
      //(bytes) =>  ((Int16)((((bytes[3]) << 8) + bytes[2]))) / 20.0);
      p.AddValue("DC Charge amps2", "??", "br",
      (bytes) => ((Int16)((((bytes[5] & 0x7F) << 8) + bytes[4]) << 1)) / 20.0);
      //(bytes) => ((Int16)((((bytes[5]) << 8) + bytes[4]))) / 20.0);
      p.AddValue("DC Charge amps3", "??", "br",
      (bytes) => ((Int16)((((bytes[7] & 0x7F) << 8) + bytes[6]) << 1)) / 20.0);
      //(bytes) => ((Int16)((((bytes[7]) << 8) + bytes[6]))) / 20.0);


      //(bytes) => ((Int16)((((bytes[7] & 0x7F) << 8) + bytes[6]) << 1)) / 50.0);
      //(bytes) => (bytes[6] + (bytes[7] << 8)) / 100.0);
      p.AddValue("Charge port volt", "V", "br",
        (bytes) => (bytes[0] + (bytes[1] << 8)) / 13.65);

      packets.Add(0x258, p = new Packet(0x258, this));
      p.AddValue("258 byte 7", "C", "c", (bytes) => bytes[7]);


      packets.Add(0x31A, p = new Packet(0x31A, this));
      p.AddValue("Battery inlet", "C", "e",
        (bytes) => (bytes[0] + ((bytes[1] & 0x03) << 8) - 320) / 8.0);
      //(bytes) => (bytes[0] *0.4 ));
      //(bytes) => (((bytes[1] & 0xF0) >> 4) + ((bytes[2]) << 8)));

      p.AddValue("PT inlet", "C", "e",
        (bytes) => (bytes[4] + ((bytes[5] & 0x03) << 8) - 320) / 8.0);
      //(bytes) => (bytes[4] *0.4 ));
      //31A - temperaturer. 0, 4:  F / 10->C

      /*p.AddValue("Battery bytes 2+3", "C", "e",
        (bytes) => (bytes[2] + ((bytes[3] & 0x03) << 8) - 320) / 8.0);
      p.AddValue("Battery bytes 6+7", "C", "e",
        (bytes) => (bytes[6] + ((bytes[7] & 0x03) << 8) - 320) / 8.0);*/

      /*p.AddValue("Battery 2+3", "C", "e",
        (bytes) => (bytes[2] + ((bytes[3] & 0x03) << 8)) / 8.0 - 40);
      //(bytes) => (bytes[0] *0.4 ));
      //(bytes) => (((bytes[1] & 0xF0) >> 4) + ((bytes[2]) << 8)));

      p.AddValue("DU inlet 6+7", "C", "e",
        (bytes) => (bytes[6] + ((bytes[7] & 0x03) << 8)) / 8.0 - 40);*/

      packets.Add(0x26A, p = new Packet(0x26A, this));
      p.AddValue("THC_batteryHeaterTemp", "C", "e",
        (bytes) => ((bytes[0] + ((bytes[1] & 0x7) << 8)) * 0.125) - 40);
      p.AddValue("Coolant heater exit", "C", "e",
        (bytes) => (bytes[0] + ((bytes[1] & 0x03) << 8) - 320) / 8.0);
      p.AddValue("THC_batteryHeaterReq", "b", "e",
        (bytes) => (bytes[1] & 0x8) >> 3);
      p.AddValue("THC_batteryHeaterState", "b", "e",
        (bytes) => (bytes[2] & 0x70) >> 4);
      //(bytes) => (bytes[0] *0.4 ));
      //(bytes) => (((bytes[1] & 0xF0) >> 4) + ((bytes[2]) << 8)));
      /*
       * #THC human decode
        THC_batteryHeaterState = "Undefined"
        heater_state = []
        heater_state.append("Off")
        heater_state.append("Startup")
        heater_state.append("BAT_IN_HEAT_CK")
        heater_state.append("Run")
        heater_state.append("Overtemp")
        heater_state.append("Suspended")
        heater_state.append("Undefined")
        heater_state.append("Undefined")
       
      THC (thermal controller) found at 
      https://github.com/apach3guy/CAN3/blob/master/thc.py
       
       */

      packets.Add(0x318, p = new Packet(0x318, this));
      p.AddValue("Outside temp", " C", "e",
        (bytes) => (bytes[0] / 2.0 - 40));
      p.AddValue("Outside temp filtered", " C", "e",
        (bytes) => (bytes[1] / 2.0 - 40));
      p.AddValue("Inside temp", " C", "e",
        (bytes) => (bytes[2] / 2.0 - 40));
      p.AddValue("A/C air temp", " C", "e",
        (bytes) => (bytes[4] / 2.0 - 40));
      //318 - temperaturer. 0, 1, 2, 4:  / 2 - 40 = C

      packets.Add(0x3F8, p = new Packet(0x3F8, this));
      p.AddValue("Floor vent L", " C", "e",
        (bytes) => ((bytes[4] + (bytes[5] << 8)) / 10.0) - 40);
      p.AddValue("Floor vent R", " C", "e",
        (bytes) => ((bytes[6] + (bytes[7] << 8)) / 10.0) - 40);
      p.AddValue("Mid vent L", " C", "e",
        (bytes) => ((bytes[0] + (bytes[1] << 8)) / 10.0) - 40);
      p.AddValue("Mid vent R", " C", "e",
        (bytes) => ((bytes[2] + (bytes[3] << 8)) / 10.0) - 40);
      //3F8 - as int. tror dette er 4 tempavlesninger evt innblåstemperatur, F / 10->C

      packets.Add(0x388, p = new Packet(0x388, this));
      p.AddValue("Heater L", " C", "h",
        (bytes) => (bytes[1] - 40));
      p.AddValue("Heater R", " C", "h",
        (bytes) => (bytes[0] - 40));
      p.AddValue("Temp 1", " C", "h",
        (bytes) => (bytes[2] - 40));
      p.AddValue("Temp 2", " C", "h",
        (bytes) => (bytes[3] - 40));
      p.AddValue("Temp 3", " C", "h",
        (bytes) => (bytes[4] - 40));
      p.AddValue("Temp 4", " C", "h",
        (bytes) => (bytes[5] - 40));


      //packets.Add(0x388, p = new Packet(0x388, this));
      /*p.AddValue("Floor L-40", " C", "h",
        (bytes) => (bytes[1]  -40));
      p.AddValue("Floor R-40", " C", "h",
        (bytes) => (bytes[0]  -40));*/
      /*p.AddValue("Temp 1-40", " C", "h",
        (bytes) => (bytes[2] -40));
      p.AddValue("Temp 2-40", " C", "h",
        (bytes) => (bytes[3] -40));
      p.AddValue("Temp 3-40", " C", "h",
        (bytes) => (bytes[4]  -40));
      p.AddValue("Temp 4-40", " C", "h",
        (bytes) => (bytes[5]  -40));*/
      //388 - temperaturer!0 - 1: / 4 = C, 2,3,4,5: / 2 - 40 = C
      /*p.AddValue("Floor L/4", " C", "h",
        (bytes) => (bytes[1] / 4));
      p.AddValue("Floor R/4", " C", "h",
        (bytes) => (bytes[0] / 4));
      p.AddValue("Temp 1/4", " C", "h",
        (bytes) => (bytes[2] / 4));
      p.AddValue("Temp 2/4", " C", "h",
        (bytes) => (bytes[3] / 4));
      p.AddValue("Temp 3/4", " C", "h",
        (bytes) => (bytes[4] / 4));
      p.AddValue("Temp 4/4", " C", "h",
        (bytes) => (bytes[5] / 4));*/


      packets.Add(0x308, p = new Packet(0x308, this));
      p.AddValue("Louver 1", "b", "e",
        (bytes) => bytes[0] > 0 ? ((bytes[0] - 15.0) / 219.0) * 100.0 : (double?)null);
      p.AddValue("Louver 2", "b", "e",
        (bytes) => bytes[1] > 0 ? ((bytes[1] - 15.0) / 219.0) * 100.0 : (double?)null);
      p.AddValue("Louver 3", "b", "e",
        (bytes) => bytes[2] > 0 ? ((bytes[2] - 15.0) / 219.0) * 100.0 : (double?)null);
      p.AddValue("Louver 4", "b", "e",
        (bytes) => bytes[3] > 0 ? ((bytes[3] - 15.0) / 219.0) * 100.0 : (double?)null);
      p.AddValue("Louver 5", "b", "e",
        (bytes) => bytes[4] > 0 ? ((bytes[4] - 15.0) / 219.0) * 100.0 : (double?)null);
      p.AddValue("Louver 6", "b", "e",
        (bytes) => bytes[5] > 0 ? ((bytes[5] - 15.0) / 219.0) * 100.0 : (double?)null);
      p.AddValue("Louver 7", "b", "e",
        (bytes) => bytes[6] > 0 ? ((bytes[6] - 15.0) / 219.0) * 100.0 : (double?)null);
      p.AddValue("Louver 8", "b", "e",
        (bytes) => bytes[7] > 0 ? ((bytes[7] - 15.0) / 219.0) * 100.0 : (double?)null);
      //388 - temperaturer!0 - 1: / 4 = C, 2,3,4,5: / 2 - 40 = C

      packets.Add(0x32A, p = new Packet(0x32A, this));
      p.AddValue("Battery pump 1", "%", "bc",
          (bytes) => (bytes[0]) & 0x7F);
      p.AddValue("Battery pump 2", "%", "bc",
          (bytes) => (bytes[1]));
      p.AddValue("Powertrain pump", "%", "bc",
          (bytes) => (bytes[2]));
      p.AddValue("Powertrain pump 2", "%", "bc",
          (bytes) => (bytes[7] > 0 ? bytes[7] : (double?)null));
      p.AddValue("Radiator bypass", "%", "bc",
          (bytes) => (bytes[3]));
      p.AddValue("Chiller bypass", "%", "bc",
          (bytes) => (bytes[4]));
      p.AddValue("Coolant heater", "%", "bch",
          (bytes) => (bytes[5]));
      p.AddValue("PTC air heater", "%", "bch",
          (bytes) => (bytes[6]));




      packets.Add(0x33A, p = new Packet(0x33A, this));
      p.AddValue("Refrigerant temp", "C", "e",
        (bytes) => (bytes[0] + ((bytes[1] & 0x07) << 8) - 320) / 8.0);

      packets.Add(0x3AA, p = new Packet(0x3AA, this));
      p.AddValue("Series/Parallel", "%", "e",
        (bytes) => (bytes[0] & 0x80) == 0x80 ? 0 : 100);

      /*p.AddValue("33A 12 bit 0", "b", "br",
      (bytes) => (bytes[0] + ((bytes[1] & 0x0F) << 8)));
      p.AddValue("33A 12 bit 1", "b", "br",
      (bytes) => (((bytes[1] & 0xF0) >> 4) + ((bytes[2]) << 4)));
      p.AddValue("33A 12 bit 3", "b", "br",
      (bytes) => (bytes[3] + ((bytes[4] & 0x0F) << 8)));
      p.AddValue("33A 12 bit 4", "b", "br",
      (bytes) => (((bytes[4] & 0xF0) >> 4) + ((bytes[5]) << 4)));
      p.AddValue("33A 12 bit 5", "b", "br",
      (bytes) => (bytes[6] + ((bytes[7] & 0x0F) << 8)));*/


      packets.Add(0x35A, p = new Packet(0x35A, this));
      /*p.AddValue("35A 12 bit 0", "b", "br",
      (bytes) => (bytes[0] + ((bytes[1] & 0x0F) << 8)));
      p.AddValue("35A 12 bit 1", "b", "br",
      (bytes) => (((bytes[1] & 0xF0) >> 4) + ((bytes[2]) << 4)));
      p.AddValue("35A 12 bit 3", "b", "br",
      (bytes) => (bytes[3] + ((bytes[4] & 0x0F) << 8)));
      p.AddValue("35A 12 bit 4", "b", "br",
      (bytes) => (((bytes[4] & 0xF0) >> 4) + ((bytes[5]) << 4)));
      p.AddValue("35A 12 bit 5", "b", "br",
      (bytes) => (bytes[6] + ((bytes[7] & 0x0F) << 8)));*/

      /*p.AddValue("THC_totalPowerConsumedHV", "b", "br",
        (bytes) => (bytes[2] + (bytes[3] << 8)));
      p.AddValue("THC_totalPowerConsumed12V", "b", "br",
        (bytes) => (bytes[4] + ((bytes[5] & 0xF) << 8)));
      p.AddValue("THC_HVPowerLimit", "b", "br",
        (bytes) => (bytes[6] + (bytes[7] << 8)) / 100);
      p.AddValue("THC_limitedBatteryHeater", "b", "br",
        (bytes) => (bytes[5] & 0x10) >> 4);
      p.AddValue("THC_limitedCompressor", "b", "br",
        (bytes) => (bytes[5] & 0x20) >> 5);
      p.AddValue("THC_limitedPtcHeater", "b", "br",
        (bytes) => (bytes[5] & 0x40) >> 6);*/

      p.AddValue("Thermal 400V", "kW", "eth",
        (bytes) => (bytes[2] + (bytes[3] << 8)) / 1000.0);
      p.AddValue("Thermal 12V", "kW", "eth",
        (bytes) => (bytes[4] + ((bytes[5] & 0xF) << 8)) / 1000.0);
      p.AddValue("Thermal PowerLimit 400V", "kW", "th",
        (bytes) => (bytes[6] + (bytes[7] << 8)) / 10000.0);
      p.AddValue("THC_limitedBatteryHeater", "b", "th",
        (bytes) => (bytes[5] & 0x10) >> 4);
      p.AddValue("THC_limitedCompressor", "b", "th",
        (bytes) => (bytes[5] & 0x20) >> 5);
      p.AddValue("THC_limitedPtcHeater", "b", "th",
        (bytes) => (bytes[5] & 0x40) >> 6);


      packets.Add(0x4, p = new Packet(0x4, this));
      p.AddValue("Nibble 00", "b", "br",
        (bytes) => (bytes[0] & 0x0F));
      p.AddValue("Nibble 01", "b", "br",
        (bytes) => (bytes[0] & 0xF0) >> 4);
      p.AddValue("Nibble 10", "b", "br",
        (bytes) => (bytes[1] & 0x0F));
      p.AddValue("Nibble 11", "b", "br",
        (bytes) => (bytes[1] & 0xF0) >> 4);
      p.AddValue("Nibble 20", "b", "br",
        (bytes) => (bytes[2] & 0x0F));
      p.AddValue("Nibble 21", "b", "br",
        (bytes) => (bytes[2] & 0xF0) >> 4);
      p.AddValue("Nibble 30", "b", "br",
        (bytes) => (bytes[3] & 0x0F));
      p.AddValue("Nibble 31", "b", "br",
        (bytes) => (bytes[3] & 0xF0) >> 4);
      p.AddValue("Nibble 40", "b", "br",
        (bytes) => (bytes[4] & 0x0F));
      p.AddValue("Nibble 41", "b", "br",
        (bytes) => (bytes[4] & 0xF0) >> 4);
      p.AddValue("Nibble 50", "b", "br",
        (bytes) => (bytes[5] & 0x0F));
      p.AddValue("Nibble 51", "b", "br",
        (bytes) => (bytes[5] & 0xF0) >> 4);
      p.AddValue("Nibble 60", "b", "br",
        (bytes) => (bytes[6] & 0x0F));
      p.AddValue("Nibble 61", "b", "br",
        (bytes) => (bytes[6] & 0xF0) >> 4);
      p.AddValue("Nibble 70", "b", "br",
        (bytes) => (bytes[7] & 0x0F));
      p.AddValue("Nibble 71", "b", "br",
        (bytes) => (bytes[7] & 0xF0) >> 4);


      //p.AddValue("268 Int 2-3", "C", "c",
      //  (bytes) => ((bytes[2]) + ((bytes[3] & 0xF0) << 8)) /*- (512 * (bytes[3] & 0x80))*/);





    }
  }
}
