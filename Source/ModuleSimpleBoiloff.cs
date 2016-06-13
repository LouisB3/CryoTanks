﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;


namespace SimpleBoiloff
{
    public class ModuleCryoTank: PartModule
    {
        // Name of the fuel to boil off
        [KSPField(isPersistant = false)]
        public string FuelName;

        // Rate of boiling off in %/hr
        [KSPField(isPersistant = false)]
        public float BoiloffRate = 0.025f;

        // Cost to cool off u/s per 1000 u
        [KSPField(isPersistant = false)]
        public float CoolingCost = 0.0f;

        // Last timestamp that boiloff occurred
        [KSPField(isPersistant = true)]
        public double LastUpdateTime = 0;        

        // Whether active tank refrigeration is occurring
        [KSPField(isPersistant = true)]
        public bool CoolingEnabled = true;

        // PRIVATE
        private double fuelAmount = 0.0;
        private double coolingCost = 0.0;
        private double boiloffRateSeconds = 0.0;

        // UI FIELDS/ BUTTONS
        // Status string
        [KSPField(isPersistant = false, guiActive = true, guiName = "Boiloff")]
        public string BoiloffStatus = "N/A";

        [KSPField(isPersistant = false, guiActive = false, guiName = "Insulation")]
        public string CoolingStatus = "N/A";

        [KSPEvent(guiActive = false, guiName = "Enable Cooling", active = true)]
        public void Enable()
        {
            CoolingEnabled = true;
        }
        [KSPEvent(guiActive = false, guiName = "Disable Cooling", active = false)]
        public void Disable()
        {
            CoolingEnabled = false;
        }

        // ACTIONS
        [KSPAction("Enable Charging")]
        public void EnableAction(KSPActionParam param) { Enable(); }

        [KSPAction("Disable Charging")]
        public void DisableAction(KSPActionParam param) { Disable(); }

        [KSPAction("Toggle Charging")]
        public void ToggleAction(KSPActionParam param)
        {
            CoolingEnabled = !CoolingEnabled;
        }

        public override string GetInfo()
        {
          string msg = String.Format("Loss Rate: {0:F2}% {1}/hr", BoiloffRate, FuelName);
          if (CoolingCost > 0.0f)
              msg += String.Format("\nCooling Cost: {0:F2} EC/s per 1000 LH2", CoolingCost);
          return msg;
        }

        public override void OnStart(PartModule.StartState state)
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
              fuelAmount = GetResourceAmount(FuelName);

              boiloffRateSeconds = BoiloffRate/100.0/3600.0;
              if (CoolingCost > 0.0)
              {
                coolingCost = fuelAmount/1000.0 * CoolingCost;
                Events["Disable"].guiActive = true;
                Events["Enable"].guiActive = true;
              }
              // Catchup
              DoCatchup();
            }
        }

        public void DoCatchup()
        {
          if (part.vessel.missionTime > 0.0)
          {
              if (part.RequestResource("ElectricCharge", coolingCost * TimeWarp.fixedDeltaTime) < coolingCost * TimeWarp.fixedDeltaTime)
              {
                  double elapsedTime = part.vessel.missionTime - LastUpdateTime;

                  double toBoil = Math.Pow(1.0 - boiloffRateSeconds, elapsedTime);
                  part.RequestResource(FuelName, (1.0 - toBoil) * fuelAmount,ResourceFlowMode.NO_FLOW);
              }
          }
        }

        public void Update()
        {
          if (HighLogic.LoadedSceneIsFlight)
          {
        
            // Show the insulation status field if there is a cooling cost
            if (CoolingCost > 0f)
            {
              foreach (BaseField fld in base.Fields)
                {
                    if (fld.guiName == "Insulation")
                        fld.guiActive = true;
                }

              if (Events["Enable"].active == CoolingEnabled || Events["Disable"].active != CoolingEnabled)
                {
                    Events["Disable"].active = CoolingEnabled;
                    Events["Enable"].active = !CoolingEnabled;
               }
            }
            if (fuelAmount == 0.0)
            {
                foreach (BaseField fld in base.Fields)
                {
                    if (fld.guiName == "Boiloff")
                        fld.guiActive = false;
                }
                
            }

          }
        }
        protected void FixedUpdate()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                fuelAmount = GetResourceAmount(FuelName);
                // If we have no fuel, no need to do any calculations
                if (fuelAmount == 0.0)
                {
                  BoiloffStatus = "No Fuel";
                  CoolingStatus = "No Fuel";
                  return;
                }

                // If the cooling cost is zero, we must boil off
                if (CoolingCost == 0f)
                {
                  DoBoiloff(1d);
                  BoiloffStatus = FormatRate(boiloffRateSeconds* fuelAmount);
                }
                // else check for available power
                else
                {
                    if (CoolingEnabled)
                    {
                      double req = part.RequestResource("ElectricCharge", coolingCost * TimeWarp.fixedDeltaTime);
                      // Fully cooled
                      if (req >= coolingCost * TimeWarp.fixedDeltaTime)
                      {
                          BoiloffStatus = String.Format("Insulated");
                          CoolingStatus = String.Format("Using {0:F2} Ec/s", coolingCost);
                      
                      // partially cooled
                      } else if (req >= 0d)
                      {
                          DoBoiloff(req/(coolingCost*TimeWarp.fixedDeltaTime));
                          BoiloffStatus = FormatRate(boiloffRateSeconds * fuelAmount);
                          CoolingStatus = "Partial Cooling!";
                      }
                      // Uncooled
                      else
                      {
                          DoBoiloff(1d);
                          BoiloffStatus = FormatRate(boiloffRateSeconds * fuelAmount);
                          CoolingStatus = "No Cooling!";
                      }
                    } 
                    else
                    {
                        DoBoiloff(1d);
                        BoiloffStatus = FormatRate(boiloffRateSeconds * fuelAmount);
                        CoolingStatus = "Disabled";
                    }
                  }
                if (part.vessel.missionTime > 0.0)
                {
                    LastUpdateTime = part.vessel.missionTime;
                }
            }
        }
        protected void DoBoiloff(double scale)
        {
            // 0.025/100/3600
      		double toBoil = Math.Pow(1.0-boiloffRateSeconds, TimeWarp.fixedDeltaTime)*scale;

      		boiled = part.RequestResource(FuelName, (1.0-toBoil) * fuelAmount,ResourceFlowMode.NO_FLOW );
        }	

        private double boiled = 0d;

        protected string FormatRate(double rate)
        {
            double adjRate = rate;
            string interval = "s";
            if (adjRate < 0.01)
            {
                adjRate = adjRate*60.0;
                interval = "min";
            }
            if (adjRate < 0.01)
            {
                adjRate = adjRate * 60.0;
                interval = "hr";
            }
            return String.Format("Losing {0:F2} u/{1}", adjRate, interval);          
        }

        protected double GetResourceAmount(string nm)
       {
           PartResource res = this.part.Resources.Get(PartResourceLibrary.Instance.GetDefinition(nm).id);
           if (res)
               return res.amount;
           else
               return 0d;
       }

    }
}
