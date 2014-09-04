using System;
using System.Collections.Generic;
using PRoCon.Core;
using PRoCon.Core.Plugin;

namespace PRoConEvents
{
	public class TeamKillTracker : PRoConPluginAPI, IPRoConPluginInterface
	{
		private string ReplaceStaches(string s)
		{
			return s.Replace("{", "~(").Replace("}", ")~");
		}

		private void WriteConsole(string message)
		{
			ExecuteCommand("procon.protected.pluginconsole.write", ReplaceStaches(message));
		}

		public override void OnPlayerTeamChange(string strSoldierName, int iTeamID, int iSquadID)
		{
			if (strSoldierName != "stajs")
				return;

			var message = string.Format("{0} joined team {1}.", strSoldierName, iTeamID);

			ExecuteCommand("procon.protected.send", "admin.say", message, "all");
			ExecuteCommand("procon.protected.chat.write", "(AdminSay) " + message);

			ExecuteCommand("procon.protected.send", "admin.say", "Boot!", "all");
			ExecuteCommand("procon.protected.chat.write", "(AdminSay) Boot!");
			ExecuteCommand("procon.protected.send", "admin.kickPlayer", strSoldierName, "No more swapsies for you bro.");
		}

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
	}
}