﻿using AutoRetainer.Internal;
using AutoRetainer.Modules.Voyage.Tasks;
using AutoRetainerAPI.Configuration;
using Dalamud.Game.ClientState.Conditions;
using ECommons.Automation;
using ECommons.ExcelServices.TerritoryEnumeration;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game.Housing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AutoRetainer.Modules.Voyage
{
    internal static unsafe class VoyageMain
    {
        static bool IsInVoyagePanel = false;

        internal static void Init()
        {
            Svc.Framework.Update += Tick;
            Svc.Toasts.ErrorToast += Toasts_ErrorToast;
            VoyageMemory.Init();
        }

        private static void Toasts_ErrorToast(ref Dalamud.Game.Text.SeStringHandling.SeString message, ref bool isHandled)
        {
            if (MultiMode.Active || P.TaskManager.IsBusy)
            {
                var txt = message.ExtractText();
                if (txt == Lang.VoyageInventoryError)
                {
                    DuoLog.Warning($"[Voyage] Your inventory is full!");
                    Data.WorkshopEnabled = false;
                    VoyageScheduler.Enabled = false;
                    P.TaskManager.Abort();
                    P.TaskManager.Enqueue(VoyageScheduler.SelectQuitVesselSelectorMenu);
                    P.TaskManager.Enqueue(VoyageScheduler.SelectExitMainPanel);
                }
                if (txt.Contains("Unable to repair vessel."))
                {
                    TaskRepairAll.Abort = true;
                    DuoLog.Warning($"[Voyage] You are out of repair components!");
                    Data.GetEnabledVesselsData(TaskRepairAll.Type).Remove(TaskRepairAll.Name);
                }
            }
        }

        internal static void Shutdown()
        {
            Svc.Framework.Update -= Tick;
            Svc.Toasts.ErrorToast -= Toasts_ErrorToast;
            VoyageMemory.Dispose();
        }

        internal static void Tick(object _)
        {
            if (VoyageUtils.IsVoyageCondition())
            {
                if (Svc.Targets.Target.IsVoyagePanel())
                {
                    if (!IsInVoyagePanel)
                    {
                        IsInVoyagePanel = true;
                        Notify.Info($"Entered voyage panel");
                        if (IsKeyPressed(C.Suppress))
                        {
                            Notify.Warning("No operation was requested by user");
                        }
                        else
                        {
                            if (C.SubsAutoResend)
                            {
                                if (Data.AnyEnabledVesselsAvailable())
                                {
                                    VoyageScheduler.Enabled = true;
                                }
                                else
                                {
                                    Notify.Warning($"Warning!\nDeployables were not enabled as there are nothing to process yet");
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                if (IsInVoyagePanel)
                {
                    IsInVoyagePanel = false;
                    Notify.Info("Closed voyage panel");
                    VoyageScheduler.Enabled = false;
                }
            }

            if (VoyageUtils.IsInVoyagePanel())
            {
                if (EzThrottler.Throttle("Voyage.WriteOfflineData", 100))
                {
                    VoyageUtils.WriteOfflineData();
                }
            }

            if (VoyageScheduler.Enabled)
            {
                DoWorkshopPanelTick();
            }
        }

        static void DoWorkshopPanelTick()
        {
            if (!P.TaskManager.IsBusy && FrameThrottler.Check("SchedulerRestartCooldown"))
            {
                var data = Utils.GetCurrentCharacterData();
                var panel = VoyageUtils.GetCurrentWorkshopPanelType();
                if (panel == PanelType.TypeSelector)
                {
                    if (data.AnyEnabledVesselsAvailable(VoyageType.Airship))
                    {
                        if(EzThrottler.Throttle("DoWorkshopPanelTick.EnqueuePanelSelector", 1000))
                        {
                            P.TaskManager.Enqueue(VoyageScheduler.SelectAirshipManagement);
                        }
                    }
                    else if (data.AnyEnabledVesselsAvailable(VoyageType.Submersible))
                    {
                        if (EzThrottler.Throttle("DoWorkshopPanelTick.EnqueuePanelSelector", 1000))
                        {
                            P.TaskManager.Enqueue(VoyageScheduler.SelectSubManagement);
                        }
                    }
                    else if (!data.AreAnyVesselsReturnInNext(5))
                    {
                        if (EzThrottler.Throttle("DoWorkshopPanelTick.EnqueuePanelSelector", 1000))
                        {
                            P.TaskManager.Enqueue(VoyageScheduler.SelectExitMainPanel);
                        }
                    }
                }
                else if (panel == PanelType.Submersible)
                {
                    ScheduleResend(VoyageType.Submersible);
                }
                else if (panel == PanelType.Airship)
                {
                    ScheduleResend(VoyageType.Airship);
                }
            }
            else
            {
                FrameThrottler.Throttle("SchedulerRestartCooldown", 10, true);
            }
        }

        static void ScheduleResend(VoyageType type)
        {
            var next = VoyageUtils.GetNextCompletedVessel(type);
            if (next != null)
            {
                if (C.SubsOnlyFinalize || C.DontReassign || Data.GetFinalizeVesselsData(type).Contains(next))
                {
                    if (EzThrottler.Throttle("DoWorkshopPanelTick.ScheduleResend", 1000))
                    {
                        TaskFinalizeVessel.Enqueue(next, type);
                    }
                }
                else
                {
                    if (EzThrottler.Throttle("DoWorkshopPanelTick.ScheduleResend", 1000))
                    {
                        TaskRedeployVessel.Enqueue(next, type);
                    }
                }
            }
            else
            {
                if (!Data.AreAnyVesselsReturnInNext(type, 1))
                {
                    if (EzThrottler.Throttle("DoWorkshopPanelTick.ScheduleResendQuitPanel", 1000))
                    {
                        TaskQuitMenu.Enqueue();
                    }
                }
            }
        }
    }
}
