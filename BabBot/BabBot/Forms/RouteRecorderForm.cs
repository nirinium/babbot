﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using BabBot.Common;
using BabBot.Wow;
using BabBot.Manager;
using BabBot.States.Common;

namespace BabBot.Forms
{
    


    public partial class RouteRecorderForm : BabBot.Forms.GenericDialog
    {
        private string[] EndpointList;
        private Route _route = new Route();

        public RouteRecorderForm()
            : base ("route_mgr")
        {
            InitializeComponent();

            EndpointList = new string[DataManager.EndpointsSet.Count];
            DataManager.EndpointsSet.Keys.CopyTo(EndpointList, 0);

            int idx = Array.IndexOf(EndpointList, "undef");
            cbTypeA.DataSource = EndpointList;
            cbTypeA.SelectedIndex = idx;
            cbTypeB.DataSource = EndpointList;
            cbTypeB.SelectedIndex = idx;

            dataGridView1.DataSource = _route.List;
#if DEBUG
            if (ProcessManager.Config.Test == 3)
                for (int i = 1; i <= 5; i++)
                    _route.List.Add(new Vector3D(i, i, i));
#endif
        }

        private void btnControl_Click(object sender, EventArgs e)
        {
            if (btnControl.Text.Equals("Start"))
            {
                if (!CheckInGame())
                    return;

                // Start recording
                _route.List.Clear();

                ProcessManager.Player.StateMachine.ChangeState(
                        new RouteRecordingState(RecordWp, numRecDistance.Value), false, true);
                ProcessManager.Player.StateMachine.IsRunning = true;

                // TODO
                btnControl.Text.Equals("Stop");
            }
            else
            {
                // Start recordint
                // TODO
                btnControl.Text.Equals("Start");
            }
        }

        public void RecordWp(Vector3D wp)
        {
            if (InvokeRequired)
            {
                RouteRecordingState.WaypointRecordingHandler del = RecordWp;
                object[] parameters = { wp };
                Invoke(del, parameters);
            }
            else
            {
                _route.List.Add(wp);
            }
        }

        public Route Record(EndpointTypes type_a, string name_a, 
                                    EndpointTypes type_b, string name_b)
        {
            return null;
        }
    }
}