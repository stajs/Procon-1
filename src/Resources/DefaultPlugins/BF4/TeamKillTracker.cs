using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PRoCon.Core;
using PRoCon.Core.Plugin;

namespace PRoConEvents
{
	public class TeamKillTracker : PRoConPluginAPI, IPRoConPluginInterface
	{
		private static object _lock = new object();
		private List<TeamKill> _teamKills = new List<TeamKill>();

		private enum TeamKillStatus
		{
			Pending,
			Punished,
			Forgiven,
			AutoForgiven
		}

		private class TeamKill
		{
			public string KillerName { get; set; }
			public string VictimName { get; set; }
			public DateTime At { get; set; }
			public TeamKillStatus Status { get; set; }

		}

		#region IPRoConPluginInterface

		public void OnPluginLoaded(string strHostName, string strPort, string strPRoConVersion)
		{
			this.RegisterEvents(this.GetType().Name, "OnLevelLoaded", "OnLoadingLevel", "OnLevelStarted", "OnGlobalChat", "OnPlayerJoin", "OnPlayerKilled");
		}

		public void OnPluginEnable()
		{
			WriteConsole("^2Enabled.^0");

			_teamKills = new List<TeamKill>();
		}

		public void OnPluginDisable()
		{
			WriteConsole("^8Disabled.^0");
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

		// TODO: Remove. Only for testing.
		public override void OnGlobalChat(string speaker, string message)
		{
			// TODO: remove safety check.    
			if (speaker != "stajs")
				return;

			if (message == "!shame")
				Shame();

			if (message.StartsWith("!add"))
				Add(message);
		}

		// TODO: Remove. Only for testing.
		private void Add(string message)
		{
			var parts = message.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);

			if (parts.Length != 3)
				return;

			var name = parts[1];
			TeamKillStatus status;

			switch (parts[2])
			{
				case "a":
					status = TeamKillStatus.AutoForgiven;
					break;

				case "f":
					status = TeamKillStatus.Forgiven;
					break;

				case "p":
					status = TeamKillStatus.Punished;
					break;

				default:
					status = TeamKillStatus.Pending;
					break;
			}

			_teamKills.Add(new TeamKill
			{
				KillerName = name,
				VictimName = "stajs",
				At = DateTime.UtcNow,
				Status = status
			});
		}

		// TODO: Auto-shame on round end.
		private void Shame()
		{
			var worstTeamKillers = _teamKills
				 .GroupBy(tk => tk.KillerName)
				 .Select(g => new
				 {
					 KillerName = g.Key,
					 Count = g.Count(),
					 TeamKills = g
				 })
				 .OrderByDescending(a => a.Count)
				 .Take(3)
				 .ToList();

			if (!worstTeamKillers.Any())
			{
				AdminSayAll("Wow! We got through a round without a single teamkill!");
				return;
			}

			var sb = new StringBuilder();

			for (int i = 0; i < worstTeamKillers.Count; i++)
			{
				var killer = worstTeamKillers[i];

				sb.AppendFormat("{0} ({1}){2}",
					  killer.KillerName,
					  killer.Count,
					  i + 1 < worstTeamKillers.Count ? ", " : ".");
			}

			AdminSayAll("Worst team killers: " + sb);
		}

		public override void OnLoadingLevel(string mapFileName, int roundsPlayed, int roundsTotal)
		{
			WriteConsole("OnLoadingLevel " + mapFileName);
		}

		public override void OnLevelLoaded(string mapFileName, string gamemode, int roundsPlayed, int roundsTotal)
		{
			WriteConsole("OnLevelLoaded " + mapFileName + ", " + gamemode);
		}

		// TODO: Confirm that this is round start.
		public override void OnLevelStarted()
		{
			WriteConsole("OnLevelStarted()");

			lock (_lock)
			{
				_teamKills = new List<TeamKill>();
			}
		}

		private void AutoForgive(string killer, string victim)
		{
			_teamKills
				.Where(tk => tk.KillerName == killer && tk.VictimName == victim && tk.Status == TeamKillStatus.Pending)
				.ToList()
				.ForEach(tk => tk.Status = TeamKillStatus.AutoForgiven);
		}

		public override void OnPlayerKilled(Kill kill)
		{
			const string prefix = "OnPlayerKilled | ";

			if (kill == null || kill.Victim == null || kill.Killer == null)
			{
				WriteConsole(prefix + "Can not determine kill.");
				return;
			}

			var victimName = kill.Victim.SoldierName;
			var killerName = kill.Killer.SoldierName;

			// TODO: Remove safety check.
			if (string.IsNullOrEmpty(victimName) || string.IsNullOrEmpty(killerName) || victimName != "stajs")
			{
				WriteConsole(prefix + "Can not determine name.");
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
				WriteConsole(prefix + "Not a TK.");
				return;
			}

			// Auto-forgive any previous pending TKs.
			AutoForgive(killerName, victimName);

			_teamKills.Add(new TeamKill
			{
				KillerName = killerName,
				VictimName = victimName,
				At = DateTime.UtcNow,
				Status = TeamKillStatus.Pending
			});

			var allKillsByKiller = _teamKills
				.Where(tk => tk.KillerName == killerName)
				.ToList();

			const int maxPunish = 5;
			var totalPunishedCount = allKillsByKiller.Count(tk => tk.Status == TeamKillStatus.Punished);
			var punishesLeft = maxPunish - totalPunishedCount;

			var message = string.Format("{0} TEAMKILLED {1}. Watch your fire dum-dum! {0} has TK'd a total of {2} time{3}.",
				killerName,
				victimName,
				allKillsByKiller.Count,
				allKillsByKiller.Count == 1 ? string.Empty : "s"
				);

			AdminSayPlayer(killerName, message);
			AdminSayPlayer(victimName, message);

			var victimKillsByKiller = allKillsByKiller
				.Where(tk => tk.VictimName == victimName)
				.ToList();

			var killedVictimCount = victimKillsByKiller.Count;
			var punishedCount = victimKillsByKiller.Count(tk => tk.Status == TeamKillStatus.Punished);
			var forgivenCount = victimKillsByKiller.Count(tk => tk.Status == TeamKillStatus.Forgiven);
			var autoForgivenCount = victimKillsByKiller.Count(tk => tk.Status == TeamKillStatus.AutoForgiven);

			var sb = new StringBuilder(victimName + ": !p to punish, !f to forgive.");

			if (killedVictimCount == 0)
			{
				sb.AppendFormat(" This is the first time {0} has TK'd you.", killerName);
			}
			else
			{
				sb.AppendFormat(" Stats - {0} has TK'd you: {1}", killerName, killedVictimCount);

				if (punishedCount > 0)
					sb.AppendFormat(", punished: {0}", punishedCount);

				if (forgivenCount > 0)
					sb.AppendFormat(", forgiven: {0}", forgivenCount);

				if (autoForgivenCount > 0)
					sb.AppendFormat(", auto-forgiven: {0}", autoForgivenCount);

				sb.Append(".");
			}

			AdminSayPlayer(victimName, sb.ToString());
		}

		//    ExecuteCommand("procon.protected.tasks.add", "TeamKillTracker", "5", "1", "1", "procon.protected.send", "admin.kickPlayer", strSoldierName, "Boot!");

		#endregion

		private string ReplaceStaches(string s)
		{
			return s.Replace("{", "~(").Replace("}", ")~");
		}

		private void WriteConsole(string message)
		{
			ExecuteCommand("procon.protected.pluginconsole.write", "Team Kill Tracker: " + ReplaceStaches(message));
		}

		private void AdminSayAll(string format, params object[] args)
		{
			AdminSayAll(string.Format(format, args));
		}

		private void AdminSayAll(string message)
		{
			ExecuteCommand("procon.protected.send", "admin.say", message, "all");
			ExecuteCommand("procon.protected.chat.write", "(AdminSay) " + message);
		}

		private void AdminSayPlayer(string player, string format, params object[] args)
		{
			AdminSayPlayer(player, string.Format(format, args));
		}

		private void AdminSayPlayer(string player, string message)
		{
			ExecuteCommand("procon.protected.send", "admin.say", message, "player", player);
			ExecuteCommand("procon.protected.chat.write", "(AdminSay) " + message);
		}
	}
}