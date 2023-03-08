﻿using AutoRetainer.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoRetainer.Tasks
{
    internal static class TaskReassignVenture
    {
        internal static void Enqueue()
        {
            P.TaskManager.Enqueue(RetainerHandlers.SelectViewVentureReport);
            P.TaskManager.Enqueue(RetainerHandlers.ClickResultReassign);
            P.TaskManager.Enqueue(RetainerHandlers.ClickAskAssign);
        }
    }
}
