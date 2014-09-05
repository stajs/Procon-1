using System;
using System.Collections.Generic;
using System.Linq;
using PRoCon.Core;
using PRoCon.Core.Plugin;

namespace PRoConEvents
{
    public class TeamKillTracker : PRoConPluginAPI, IPRoConPluginInterface
    {
        private static object _lock = new object();
        private List<Killer> _killers = new List<Killer>();

        public class Killer
        {
            public string Name { get; set; }
            public List<Victim> Victims { get; set; }
        }

        public class Victim
        {
            public string Name { get; set; }
            public int Count { get; set; }
        }

        #region IPRoConPluginInterface

        public void OnPluginLoaded(string strHostName, string strPort, string strPRoConVersion)
        {
            this.RegisterEvents(this.GetType().Name, "OnPlayerJoin", "OnPlayerKilled");
        }

        public void OnPluginEnable()
        {
            WriteConsole("^bTeam Kill Tracker:^n ^2Enabled.^0");

            _killers = new List<Killer>();
        }

        public void OnPluginDisable()
        {
            WriteConsole("^bTeam Kill Tracker:^n ^8Disabled.^0");
        }

        public string GetPluginName()
        {
            return "Team Kill Tracker";
        }

        public string GetPluginVersion()
        {
            return "0.1.0";
        }

        public string GetPluginAuthor()
        {
            return "stajs";
        }

        public string GetPluginWebsite()
        {
            return "http://battlelog.battlefield.com/bf4/soldier/stajs/stats/904562646/pc/";
        }

        public string GetPluginDescription()
        {
            return "Pure awesomeness.";
        }

        public List<CPluginVariable> GetDisplayPluginVariables()
        {
            return GetPluginVariables();
        }

        public List<CPluginVariable> GetPluginVariables()
        {
            return new List<CPluginVariable>();
        }

        public void SetPluginVariable(string strVariable, string strValue)
        {

        }

        #endregion

        #region PRoConPluginAPI

        public override void OnPlayerJoin(string player)
        {
            // TODO: Remove. For testing purposes, kill counts are reset on joining. Should be on round start.
            lock (_lock)
            {
                _killers.RemoveAll(k => k.Name == player);
            }

            WriteConsole(player + " joined.");
        }

        public override void OnPlayerKilled(Kill kill)
        {
            var message = "Team Kill Tracker: OnPlayerKilled | ";

            if (kill == null || kill.Victim == null || kill.Killer == null)
            {
                WriteConsole(message + "Can not determine kill.");
                return;
            }

            var victimName = kill.Victim.SoldierName;
            var killerName = kill.Killer.SoldierName;

            // TODO: Remove safety check.
            if (string.IsNullOrEmpty(victimName) || string.IsNullOrEmpty(killerName) || victimName != "stajs")
            {
                WriteConsole(message + "Can not determine name.");
                return;
            }

            // TODO: Uncomment. This is commented out to test fake TKs with suicides.
            //if (kill.IsSuicide)
            //{
            //    WriteConsole(message + "Was suicide.");
            //    return;
            //}

            var isTeamKill = kill.Killer.TeamID == kill.Victim.TeamID;

            if (!isTeamKill)
            {
                WriteConsole(message + "Not a TK.");
                return;
            }

            var killer = _killers.FirstOrDefault(k => k.Name == killerName);

            if (killer == null)
            {
                killer = new Killer
                {
                    Name = killerName,
                    Victims = new List<Victim>()
                };

                _killers.Add(killer);
            }

            var victim = killer.Victims.FirstOrDefault(v => v.Name == victimName);

            if (victim == null)
            {
                victim = new Victim
                {
                    Name = victimName,
                    Count = 0
                };

                killer.Victims.Add(victim);
            }

            victim.Count++;

            WriteConsole(string.Format("{0}{1} has TK'd {2} {3} times.", message, killerName, victimName, victim.Count));
        }

        //public override void OnPlayerTeamChange(string strSoldierName, int iTeamID, int iSquadID)
        //{
        //    if (strSoldierName != "stajs")
        //        return;

        //    const int maxSwaps = 1;

        //    var count = 0;

        //    lock (_lock)
        //    {
        //        if (_teamSwaps.TryGetValue(strSoldierName, out count))
        //            count++;

        //        _teamSwaps[strSoldierName] = count;
        //    }

        //    var swapsLeft = maxSwaps - count;
        //    var message = string.Format("{0} joined team {1}, swaps remaining before kick: {2}", strSoldierName, iTeamID, swapsLeft);

        //    ExecuteCommand("procon.protected.send", "admin.say", message, "all");
        //    ExecuteCommand("procon.protected.chat.write", "(AdminSay) " + message);

        //    if (swapsLeft > 0)
        //        return;

        //    message = "Say bye-bye to stajs. Kicked in 5 seconds.";

        //    ExecuteCommand("procon.protected.send", "admin.say", message, "all");
        //    ExecuteCommand("procon.protected.chat.write", "(AdminSay) " + message);
        //    ExecuteCommand("procon.protected.tasks.add", "TeamKillTracker", "5", "1", "1", "procon.protected.send", "admin.kickPlayer", strSoldierName, "Boot!");
        //}

        #endregion

        private string ReplaceStaches(string s)
        {
            return s.Replace("{", "~(").Replace("}", ")~");
        }

        private void WriteConsole(string message)
        {
            ExecuteCommand("procon.protected.pluginconsole.write", ReplaceStaches(message));
        }
    }
}