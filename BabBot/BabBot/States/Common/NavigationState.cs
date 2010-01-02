﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using BabBot.Wow;
using BabBot.Common;
using BabBot.Manager;
using Pather.Graph;

namespace BabBot.States.Common
{
    /// <summary>
    /// Class for navigation to destination waypoint that
    /// usually located more than 20 yards away with expected obstacles
    /// in between.
    /// Do not use in combat (use MoveTo instead) because Caronte waypoint calculations 
    /// takes some time
    /// If path not supplied it using Caronte to calculate it
    /// </summary>
    class NavigationState : State<WowPlayer>
    {
        /// <summary>
        /// Travel destination point
        /// </summary>
        private Vector3D _dest = null;

        /// <summary>
        /// "PPather Calc"/"ClickToMove" thread
        /// </summary>
        private Thread _t;

        /// <summary>
        /// Logging facility
        /// </summary>
        private string _lfs;

        /// <summary>
        /// Maximum number of retries toon using.
        /// Defined by AppConfig.MaxTargetGetRetries parameter
        /// </summary>
        private int _max_retry;

        /// <summary>
        /// Retry counter
        /// </summary>
        private int _retry = 0;

        /// <summary>
        /// Terminated flag
        /// </summary>
        private bool _terminated = false;

        /// <summary>
        /// Pointer to player object
        /// </summary>
        private WowPlayer _player;

        /// <summary>
        /// Default distance between each step
        /// </summary>
        float _step_dist;

        /// <summary>
        /// Text shown in bot progress bar tooltip
        /// </summary>
        private string _tooltip;

        /// <summary>
        /// Waypoints supplied when state created
        /// </summary>
        private Waypoints _wp = null;

        public NavigationState(Vector3D dest, string lfs, string tooltip_text)
            : this(dest, 5F, lfs, tooltip_text) { }

        public NavigationState(Vector3D dest, float step_dist, string lfs, string tooltip_text)
        {
            _dest = dest;
            Init(lfs, step_dist, tooltip_text);
        }

        public NavigationState(Waypoints waypoints, string lfs, string tooltip_text)
            : this(waypoints, 5F, lfs, tooltip_text) { }            

        public NavigationState(Waypoints waypoints, float step_dist, string lfs, string tooltip_text)
        {
            _wp = waypoints;
            Init(lfs, step_dist, tooltip_text);
        }

        private void Init(string lfs, float step_dist, string tooltip_text)
        {
            _lfs = lfs;
            _step_dist = step_dist;
            _tooltip = tooltip_text;

            // We have a 3 tries by default to reach target NPC
            _max_retry = ProcessManager.AppConfig.MaxTargetGetRetries;
        }

        /// <summary>
        /// Enter state event handler
        /// Start PPather calculation and movement in separate thread
        /// </summary>
        /// <param name="player"></param>
        protected override void DoEnter(WowPlayer player)
        {
            _retry = 0;
            _player = player;

            _t = new Thread(MoveTo) { Name = "Navitating" };
            _t.Start();
        }

        protected override void DoExecute(WowPlayer player)
        {
            if (_t.IsAlive)
                return;
            else
                Finish(player);
        }

        protected override void DoExit(WowPlayer Entity)
        {
            if (_t == null || !_t.IsAlive)
                return;

            // Terminating movement
            _terminated = true;
            Thread.Sleep(500);
            if (_t.IsAlive)
                _t.Abort();
        }

        /// <summary>
        /// Finish event handler
        /// </summary>
        /// <param name="Entity"></param>
        protected override void DoFinish(WowPlayer Entity)
        {
            _t = null;
            ProcessManager.OnTravelCompleted();
        }

        private float MoveToDest(Path path, Vector3D dest, float step)
        {
            float distance = _player.Location.GetDistanceTo(dest);
            string tooltip = _tooltip;

            while ((distance > step) && (_retry <= _max_retry) && !_terminated)
            {
                if (_retry > 0)
                {
                    string r = "Retrying " + _retry + " of " +
                                _max_retry + " to reach the destination";

                    Output.Instance.Log(r);
                    tooltip = _tooltip + " " + r;
                }

                Output.Instance.Debug("Calculating path from player position " +
                    _player.Location + " to dest " + dest + " ...");


                Vector3D[] v_arr = new Vector3D[path.Count];
                for (int i = 0; i < path.Count; i++)
                    v_arr[i] = WaypointVector3DHelper.LocationToVector3D(path[i]);

                ProcessManager.OnPathCalculated(v_arr, _retry);

                Output.Instance.Debug("Path calculating completed. Moving to dest ... ");

                // Test
                // return 0;

                // Travel path
                int max = path.Count;
                Vector3D vprev = _player.Location;
                Vector3D vnext;

                if (_retry == 0)
                {
                    ProcessManager.OnBotProgressStart(max, tooltip);

                    // Jump b4 start to clear AFK
                    ProcessManager.CommandManager.SendKeys(CommandManager.SK_SPACE);
                    Thread.Sleep(1000);
                }

                for (int i = 0; i < max; i++)
                {
                    if (_terminated)
                        break;

                    vnext = WaypointVector3DHelper.LocationToVector3D(path.Get(i));

                    // Calculate travel time
                    distance = vprev.GetDistanceTo(vnext);
                    int t = (int)((distance / 7F) * 1000);

                    _player.ClickToMove(vnext);
                    if (_retry > 0)
                    {
                        // Bot trying unstuck
                        // Click and Send jump at the same time
                        Thread.Sleep(100);
                        ProcessManager.CommandManager.SendKeys(CommandManager.SK_SPACE);
                        t += 1000;
                    }

                    Thread.Sleep(t + 1);

                    // Check if we moved
                    float dd = _player.Location.GetDistanceTo(vnext);
                    if (dd > step)
                    {
                        // We stuck
                        if (_retry < _max_retry)
                        {
                            Output.Instance.Debug("Player stuck. Trying unstuck ...");
                            _retry++;

                            // Recalculate path to next waypoint but with smaller step
                            float new_step = (float)(step * 0.9);
                            Path new_path = ProcessManager.Caronte.CalculatePath(
                                WaypointVector3DHelper.Vector3DToLocation(_player.Location),
                                WaypointVector3DHelper.Vector3DToLocation(vnext), new_step);
                            float d = MoveToDest(new_path, vnext, new_step);
                            if (d > new_step)
                                return _player.Location.GetDistanceTo(_dest);

                            Output.Instance.Debug("Player unstuck. Continue traveling.");
                            _retry--;
                        }
                        else
                            // Just exit
                            return _player.Location.GetDistanceTo(_dest);

                    }

                    if (_retry == 0)
                        ProcessManager.OnBotProgressChange(i + 1);

                    vprev = vnext;
                }

                distance = _player.Location.GetDistanceTo(dest);
            }

            return distance;
        }

        private void MoveTo() 
        {
            Path path = null;
            Vector3D dest = null;

            if (_wp != null)
            {
                path = new Path();
                foreach (Vector3D v in _wp.List)
                    path.AddLast(WaypointVector3DHelper.Vector3DToLocation(v));
                dest = _wp.List[_wp.List.Count - 1];
            }
            else
            {
                // Calculate path
                path = ProcessManager.Caronte.CalculatePath(
                    WaypointVector3DHelper.Vector3DToLocation(_player.Location),
                    WaypointVector3DHelper.Vector3DToLocation(_dest), _step_dist);

                dest = _dest;
            }

            float distance = MoveToDest(path, dest, _step_dist);
            ProcessManager.OnBotProgressEnd();

            if (distance <= _step_dist)
                Output.Instance.Log("Destination has been reached !!!");
        }
    }
}