using System;
using System.Collections.Generic;
using PRoCon.Core;
using PRoCon.Core.Plugin;

namespace PRoConEvents
{
	public class TeamKillTracker : PRoConPluginAPI, IPRoConPluginInterface
	{
		private static object _lock = new object();
		private Dictionary<string, int> _teamSwaps = new Dictionary<string, int>();

		#region IPRoConPluginInterface

		public void OnPluginLoaded(string strHostName, string strPort, string strPRoConVersion)
		{
			this.RegisterEvents(this.GetType().Name, "OnPlayerTeamChange");
		}

		public void OnPluginEnable()
		{
			WriteConsole("^bTeam Kill Tracker:^n ^2Enabled.^0");
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
		public override void OnPlayerTeamChange(string strSoldierName, int iTeamID, int iSquadID)
		{
			if (strSoldierName != "stajs")
				return;

			const int maxSwaps = 2;

			var count = 0;

			lock (_lock)
			{
				if (_teamSwaps.TryGetValue(strSoldierName, out count))
					count++;

				_teamSwaps[strSoldierName] = count;
			}

			var swapsLeft = maxSwaps - count;
			var message = string.Format("{0} joined team {1}, swaps remaining before kick: {2}", strSoldierName, iTeamID, swapsLeft);

			ExecuteCommand("procon.protected.send", "admin.say", message, "all");
			ExecuteCommand("procon.protected.chat.write", "(AdminSay) " + message);

			if (swapsLeft > 0)
				return;

			message = "Say bye-bye to stajs. Kicked in 5 seconds.";

			ExecuteCommand("procon.protected.send", "admin.say", message, "all");
			ExecuteCommand("procon.protected.chat.write", "(AdminSay) " + message);
			ExecuteCommand("procon.protected.tasks.add", "TeamKillTracker", "5", "1", "1", "procon.protected.send", "admin.kickPlayer", strSoldierName, "Boot!");
		}

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