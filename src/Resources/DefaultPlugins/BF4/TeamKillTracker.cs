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
		private TimeSpan _punishWindowLength = TimeSpan.FromSeconds(45);

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
			var events = new[]
			{
				"OnLevelLoaded",
				"OnLoadingLevel",
				"OnLevelStarted",
				"OnRoundOver",
				"OnPlayerKilled",
				"OnGlobalChat",
				"OnTeamChat",
				"OnSquadChat"	
			};

			this.RegisterEvents(this.GetType().Name, events);
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
			return "<h1>Pure awesomeness</h1>";
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

		public override void OnGlobalChat(string speaker, string message)
		{
			OnChat(speaker, message);
		}

		public override void OnTeamChat(string speaker, string message, int teamId)
		{
			OnChat(speaker, message);
		}

		public override void OnSquadChat(string speaker, string message, int teamId, int squadId)
		{
			OnChat(speaker, message);
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
			WriteConsole("OnLevelStarted");

			lock (_lock)
			{
				_teamKills = new List<TeamKill>();
			}
		}

		public override void OnRoundOver(int winningTeamId)
		{
			Shame();
		}

		public override void OnPlayerKilled(Kill kill)
		{
			OnTeamKill(kill);
		}

		#endregion

		private void AutoForgive(string killer, string victim)
		{
			_teamKills
				.Where(tk => tk.KillerName == killer && tk.VictimName == victim && tk.Status == TeamKillStatus.Pending)
				.ToList()
				.ForEach(tk => tk.Status = TeamKillStatus.AutoForgiven);
		}

		private void OnTeamKill(Kill kill)
		{
			const string prefix = "OnTeamKill | ";

			if (kill == null || kill.Victim == null || kill.Killer == null)
			{
				WriteConsole(prefix + "Can not determine kill.");
				return;
			}

			// TODO: Uncomment. This is commented out to test fake TKs with suicides.
			//if (kill.IsSuicide)
			//{
			//    WriteConsole(message + "Was suicide.");
			//    return;
			//}

			var victimName = kill.Victim.SoldierName;
			var killerName = kill.Killer.SoldierName;

			// TODO: Remove safety check.
			if (string.IsNullOrEmpty(victimName) || string.IsNullOrEmpty(killerName) || victimName != "stajs")
			{
				WriteConsole(prefix + "Can not determine name.");
				return;
			}

			var isTeamKill = kill.Killer.TeamID == kill.Victim.TeamID;

			if (!isTeamKill)
			{
				WriteConsole(prefix + "Not a TK.");
				return;
			}

			// Auto-forgive any previous pending TKs for this killer and victim.
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

			var punishedCount = victimKillsByKiller.Count(tk => tk.Status == TeamKillStatus.Punished);
			var forgivenCount = victimKillsByKiller.Count(tk => tk.Status == TeamKillStatus.Forgiven);
			var autoForgivenCount = victimKillsByKiller.Count(tk => tk.Status == TeamKillStatus.AutoForgiven);

			//TODO: remove extra !. Currently there to avoid conflicting with PRoconRulz.
			var sb = new StringBuilder(victimName + ": !!p to punish, !!f to forgive.");

			if (victimKillsByKiller.Count == 0)
			{
				sb.AppendFormat(" This is the first time {0} has TK'd you.", killerName);
			}
			else
			{
				sb.AppendFormat(" Previous stats - TK's by {0} on you: {1}", killerName, victimKillsByKiller.Count);

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

		private void OnChat(string speaker, string message)
		{
			// TODO: remove safety check.    
			if (speaker != "stajs")
				return;

			if (message == "!!shame")
				Shame();

			if (message.StartsWith("!!add"))
				Add(message);

			if (message == ("!!p"))
				PunishKillerOf(speaker);

			if (message == ("!!f"))
				ForgiveKillerOf(speaker);
		}

		private void AutoForgivePastPunishWindow()
		{
			var punishWindowStart = DateTime.UtcNow.Add(_punishWindowLength.Negate());

			_teamKills
				.Where(tk => tk.Status == TeamKillStatus.Pending && tk.At < punishWindowStart)
				.ToList()
				.ForEach(tk => tk.Status = TeamKillStatus.AutoForgiven);
		}

		private List<TeamKill> GetPendingTeamKillsForVictim(string victim)
		{
			return _teamKills
				.Where(tk => tk.Status == TeamKillStatus.Pending && tk.VictimName == victim)
				.ToList();
		}

		private void PunishKillerOf(string victim)
		{
			AutoForgivePastPunishWindow();

			var kills = GetPendingTeamKillsForVictim(victim);

			if (!kills.Any())
				AdminSayPlayer(victim, "Could not find player to punish.");

			if (kills.Count > 1)
				WriteConsole("Players found to punish: " + kills.Count);

			foreach (var kill in kills)
				Punish(kill);
		}

		private void ForgiveKillerOf(string victim)
		{
			AutoForgivePastPunishWindow();

			var kills = GetPendingTeamKillsForVictim(victim);

			if (!kills.Any())
				AdminSayPlayer(victim, "Could not find player to forgive.");

			if (kills.Count > 1)
				WriteConsole("Players found to forgive: " + kills.Count);

			foreach (var kill in kills)
				Forgive(kill);
		}

		private void Punish(TeamKill teamKill)
		{
			var message = string.Format("Punished {0}.", teamKill.KillerName);

			// TODO: protect admins.
			// TODO: auto kick.
			//const int maxPunish = 5;
			//var totalPunishedCount = allKillsByKiller.Count(tk => tk.Status == TeamKillStatus.Punished);
			//var punishesLeft = maxPunish - totalPunishedCount;
			//    ExecuteCommand("procon.protected.tasks.add", "TeamKillTracker", "5", "1", "1", "procon.protected.send", "admin.kickPlayer", strSoldierName, "Boot!");
			
			ExecuteCommand("procon.protected.send", "admin.killPlayer", teamKill.KillerName);
			AdminSayPlayer(teamKill.KillerName, message);
			AdminSayPlayer(teamKill.VictimName, message);
			teamKill.Status = TeamKillStatus.Punished;
		}

		private void Forgive(TeamKill teamKill)
		{
			var message = string.Format("Forgiven {0}.", teamKill.KillerName);

			AdminSayPlayer(teamKill.KillerName, message);
			AdminSayPlayer(teamKill.VictimName, message);
			teamKill.Status = TeamKillStatus.Forgiven;
		}

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

		private string ReplaceStaches(string s)
		{
			return s.Replace("{", "~(").Replace("}", ")~");
		}

		private void WriteConsole(string message)
		{
			message = ReplaceStaches(message);
			ExecuteCommand("procon.protected.pluginconsole.write", "Team Kill Tracker: " + message);
		}

		private void AdminSayAll(string message)
		{
			message = ReplaceStaches(message);
			ExecuteCommand("procon.protected.send", "admin.say", message, "all");
			ExecuteCommand("procon.protected.chat.write", "(AdminSayAll) " + message);
		}

		private void AdminSayPlayer(string player, string message)
		{
			message = ReplaceStaches(message);
			ExecuteCommand("procon.protected.send", "admin.say", message, "player", player);
			ExecuteCommand("procon.protected.chat.write", "(AdminSayPlayer " + player + ") " + message);
		}
	}
}