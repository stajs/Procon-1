using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PRoCon.Core;
using PRoCon.Core.Plugin;

namespace PRoConEvents
{
	public class TeamKillTracker : PRoConPluginAPI, IPRoConPluginInterface
	{
		private struct VariableGroup
		{
			public const string Commands = "Commands|";
			public const string Timing = "Timing|";
			public const string Messages = "Messages|";
		}

		private struct VariableName
		{
			public const string PunishCommand = "Punish";
			public const string ForgiveCommand = "Forgive";
			public const string TeamKillMessage = "Team kill";
			public const string VictimPromptMessage = "Victim prompt";
			public const string ShowStatsOnVictimPromptMessage = "Show stats on victim prompt?";
			public const string NoOneToPunishMessage = "No one to punish";
			public const string NoOneToForgiveMessage = "No one to forgive";
			public const string PunishedMessage = "Punished";
			public const string ForgivenMessage = "Forgiven";
			public const string PunishWindow = "Punish window (seconds)";
		}

		private static readonly Dictionary<string, object> Defaults = new Dictionary<string, object>
		{
			{ VariableName.PunishCommand, "!p"},
			{ VariableName.ForgiveCommand, "!f"},
			{ VariableName.TeamKillMessage, "{killer} TEAM KILLED {victim}. Watch your fire dum-dum! {killer} has TK'd a total of {killCount} {killCount:time|times}."},
			{ VariableName.VictimPromptMessage, "!p to punish, !f to forgive."},
			{ VariableName.ShowStatsOnVictimPromptMessage, enumBoolYesNo.Yes},
			{ VariableName.NoOneToPunishMessage, "No one to punish (auto-forgive after {window} seconds)."},
			{ VariableName.NoOneToForgiveMessage, "No one to forgive (auto-forgive after {window} seconds)."},
			{ VariableName.PunishedMessage, "Punished {killer}."},
			{ VariableName.ForgivenMessage, "Forgiven {killer}."},
			{ VariableName.PunishWindow, TimeSpan.FromSeconds(45)}
		};

		private const int PunishWindowMin = 20;
		private const int PunishWindowMax = 120;

		private string _punishCommand = Defaults[VariableName.PunishCommand].ToString();
		private string _forgiveCommand = Defaults[VariableName.ForgiveCommand].ToString();
		private string _teamKillMessage = Defaults[VariableName.TeamKillMessage].ToString();
		private string _victimPromptMessage = Defaults[VariableName.VictimPromptMessage].ToString();
		private enumBoolYesNo _showStatsOnVictimPromptMessage = (enumBoolYesNo)Defaults[VariableName.ShowStatsOnVictimPromptMessage];
		private string _noOneToPunishMessage = Defaults[VariableName.NoOneToPunishMessage].ToString();
		private string _noOneToForgiveMessage = Defaults[VariableName.NoOneToForgiveMessage].ToString();
		private string _punishedMessage = Defaults[VariableName.PunishedMessage].ToString();
		private string _forgivenMessage = Defaults[VariableName.ForgivenMessage].ToString();
		private TimeSpan _punishWindow = (TimeSpan)Defaults[VariableName.PunishWindow];

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
			_teamKills = new List<TeamKill>();
			WriteConsole("^2Enabled.^0");
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
			return "battlelog.battlefield.com/bf4/soldier/stajs/stats/904562646/pc/";
		}

		public string GetPluginDescription()
		{
			return GetDescriptionHtml();
		}

		public List<CPluginVariable> GetDisplayPluginVariables()
		{
			return GetSettings();
		}

		public List<CPluginVariable> GetPluginVariables()
		{
			return GetSettings();
		}

		public void SetPluginVariable(string variable, string value)
		{
			SaveSetting(variable, value);
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
		}

		public override void OnRoundOver(int winningTeamId)
		{
			ShameAll();
			_teamKills = new List<TeamKill>();
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

		private string GetTeamKillMessage(string killer, string victim, int killCount)
		{
			var ret = _teamKillMessage
				.Replace("{killer}", killer)
				.Replace("{victim}", victim)
				.Replace("{killCount}", killCount.ToString());

			var regex = new Regex("{killCount:.*}");
			var match = regex.Match(ret);

			if (!match.Success)
				return ReplaceStaches(ret);

			var matchString = match.ToString();

			var units = matchString
				.Replace("{killCount:", string.Empty)
				.Replace("}", string.Empty)
				.Split(new[] { "|" }, StringSplitOptions.RemoveEmptyEntries);

			if (units.Length == 0)
				return ReplaceStaches(ret);

			var killCountSingular = units.First();
			var killCountPlural = units.Last();

			ret = ret.Replace(matchString, killCount == 1 ? killCountSingular : killCountPlural);

			return ReplaceStaches(ret);
		}

		private void OnTeamKill(Kill kill)
		{
			if (kill == null || kill.Victim == null || kill.Killer == null)
				return;

			// TODO: Uncomment. Commented out for testing.
			//if (kill.IsSuicide)
			//	return;

			var victimName = kill.Victim.SoldierName;
			var killerName = kill.Killer.SoldierName;

			if (string.IsNullOrEmpty(victimName) || string.IsNullOrEmpty(killerName))
				return;

			var isTeamKill = kill.Killer.TeamID == kill.Victim.TeamID;

			if (!isTeamKill)
				return;

			// Auto-forgive any previous pending TKs for this killer and victim - there should only be one pending
			// at a time for this combination, and it's about to be added.
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

			var message = GetTeamKillMessage(killerName, victimName, allKillsByKiller.Count);

			AdminSayPlayer(killerName, message);
			AdminSayPlayer(victimName, message);

			var sb = new StringBuilder(victimName)
				.Append(": ")
				.Append(_victimPromptMessage);

			if (_showStatsOnVictimPromptMessage == enumBoolYesNo.Yes)
			{
				var victimKillsByKiller = allKillsByKiller
					.Where(tk => tk.VictimName == victimName)
					.ToList();

				var punishedCount = victimKillsByKiller.Count(tk => tk.Status == TeamKillStatus.Punished);
				var forgivenCount = victimKillsByKiller.Count(tk => tk.Status == TeamKillStatus.Forgiven);
				var autoForgivenCount = victimKillsByKiller.Count(tk => tk.Status == TeamKillStatus.AutoForgiven);

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
			}

			AdminSayPlayer(victimName, sb.ToString());
		}

		private void OnChat(string speaker, string message)
		{
			if (message == "!shame")
				ShamePlayer(speaker);

			// TODO: Remove.
			if (speaker == "stajs" && message.StartsWith("!add"))
				Add(message);

			if (message == (_punishCommand))
				PunishKillerOf(speaker);

			if (message == (_forgiveCommand))
				ForgiveKillerOf(speaker);
		}

		private void AutoForgivePastPunishWindow()
		{
			var punishWindowStart = DateTime.UtcNow.Add(_punishWindow.Negate());

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
				AdminSayPlayer(victim, ReplaceStaches(_noOneToPunishMessage.Replace("{window}", _punishWindow.TotalSeconds.ToString())));

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
				AdminSayPlayer(victim, ReplaceStaches(_noOneToForgiveMessage.Replace("{window}", _punishWindow.TotalSeconds.ToString())));

			if (kills.Count > 1)
				WriteConsole("Players found to forgive: " + kills.Count);

			foreach (var kill in kills)
				Forgive(kill);
		}

		// TODO: figure out how to determine admins.
		private bool IsAdmin(string player)
		{
			var privileges = GetAccountPrivileges(player);

			if (privileges == null)
				return false;

			return privileges.CanKillPlayers;
		}

		private void Punish(TeamKill kill)
		{
			var message = ReplaceStaches(_punishedMessage.Replace("{killer}.", kill.KillerName));

			// TODO: auto kick.
			//const int maxPunish = 5;
			//var totalPunishedCount = allKillsByKiller.Count(tk => tk.Status == TeamKillStatus.Punished);
			//var punishesLeft = maxPunish - totalPunishedCount;
			//    ExecuteCommand("procon.protected.tasks.add", "TeamKillTracker", "5", "1", "1", "procon.protected.send", "admin.kickPlayer", strSoldierName, "Boot!");

			AdminSayPlayer(kill.KillerName, message);
			AdminSayPlayer(kill.VictimName, message);

			kill.Status = TeamKillStatus.Punished;

			if (IsAdmin(kill.KillerName))
				AdminSayPlayer(kill.KillerName, "Protected from kill.");
			else
				ExecuteCommand("procon.protected.send", "admin.killPlayer", kill.KillerName);
		}

		private void Forgive(TeamKill kill)
		{
			var message = ReplaceStaches(_forgivenMessage.Replace("{killer}.", kill.KillerName));

			AdminSayPlayer(kill.KillerName, message);
			AdminSayPlayer(kill.VictimName, message);

			kill.Status = TeamKillStatus.Forgiven;
		}

		private string GetWorstTeamKillerMessage()
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
				return "Wow! We got through a round without a single teamkill!";

			var sb = new StringBuilder();

			for (int i = 0; i < worstTeamKillers.Count; i++)
			{
				var killer = worstTeamKillers[i];

				sb.AppendFormat("{0} ({1}){2}",
					  killer.KillerName,
					  killer.Count,
					  i + 1 < worstTeamKillers.Count ? ", " : ".");
			}

			return "Worst team killers: " + sb;
		}

		private void ShamePlayer(string player)
		{
			AdminSayPlayer(player, GetWorstTeamKillerMessage());
		}

		private void ShameAll()
		{
			AdminSayAll(GetWorstTeamKillerMessage());
		}

		// TODO: Remove.
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

		private List<CPluginVariable> GetSettings()
		{
			return new List<CPluginVariable>
			{
				new CPluginVariable(VariableGroup.Commands + VariableName.PunishCommand, typeof(string), _punishCommand),
				new CPluginVariable(VariableGroup.Commands + VariableName.ForgiveCommand, typeof(string), _forgiveCommand),
				new CPluginVariable(VariableGroup.Messages + VariableName.TeamKillMessage, typeof(string), _teamKillMessage),
				new CPluginVariable(VariableGroup.Messages + VariableName.VictimPromptMessage, typeof(string), _victimPromptMessage),
				new CPluginVariable(VariableGroup.Messages + VariableName.ShowStatsOnVictimPromptMessage, typeof(enumBoolYesNo), _showStatsOnVictimPromptMessage),
				new CPluginVariable(VariableGroup.Messages + VariableName.PunishedMessage, typeof(string), _punishedMessage),
				new CPluginVariable(VariableGroup.Messages + VariableName.ForgivenMessage, typeof(string), _forgivenMessage),
				new CPluginVariable(VariableGroup.Messages + VariableName.NoOneToPunishMessage, typeof(string), _noOneToPunishMessage),
				new CPluginVariable(VariableGroup.Messages + VariableName.NoOneToForgiveMessage, typeof(string), _noOneToForgiveMessage),
				new CPluginVariable(VariableGroup.Timing + VariableName.PunishWindow, typeof(int), _punishWindow.TotalSeconds)
			};
		}

		private void SaveSetting(string setting, string value)
		{
			switch (setting)
			{
				case VariableName.PunishWindow:
					int i;
					if (!int.TryParse(value, out i))
						return;

					if (i < PunishWindowMin)
						i = PunishWindowMin;

					if (i > PunishWindowMax)
						i = PunishWindowMax;

					_punishWindow = TimeSpan.FromSeconds(i);

					break;

				case VariableName.TeamKillMessage:
					_teamKillMessage = value;
					break;

				case VariableName.ShowStatsOnVictimPromptMessage:
					_showStatsOnVictimPromptMessage = value == "Yes" ? enumBoolYesNo.Yes : enumBoolYesNo.No;
					break;

				case VariableName.PunishCommand:
					_punishCommand = value;
					break;

				case VariableName.ForgiveCommand:
					_forgiveCommand = value;
					break;

				case VariableName.VictimPromptMessage:
					_victimPromptMessage = value;
					break;

				case VariableName.NoOneToPunishMessage:
					_noOneToPunishMessage = value;
					break;

				case VariableName.NoOneToForgiveMessage:
					_noOneToForgiveMessage = value;
					break;

				case VariableName.PunishedMessage:
					_punishedMessage = value;
					break;

				case VariableName.ForgivenMessage:
					_forgivenMessage = value;
					break;
			}
		}

		private string GetDescriptionHtml()
		{
			return @"

<style type=""text/css"">
	p { font-size: 1em; }
	p.default-value { color: #5e5e5e; }
	table th { text-transform: none; }
	h2 { margin-top: 1.1em; }
	h2.group { color: #111; font-size: 1.6em; margin-top: 1.6em; margin-bottom: 0; }
	h3 { font-size: 1.4em; font-family: Vera, Helvetica, Georgia; margin-top: 1.6em; font-weight: 400; padding-bottom: 6px; border-bottom: 1px solid #dcdcdb; }
	h4 { color: #555; font-size: 1em; margin-top: 0; }
</style>

<h2>Description</h2>
<p>Track team kill statistics and allow victims to punish their killers.</p>

<h2>Plugin Settings</h2>

<h2 class=""group"">Commands</h2>

<h3>" + VariableName.PunishCommand + @"</h3>
<p>The command to punish a team killer. This can be issued in global, team, or squad chat.</p>

<h4>Default value</h4>
<p class=""default-value"">" + Defaults[VariableName.PunishCommand] + @"</p>

<h3>" + VariableName.ForgiveCommand + @"</h3>
<p>The command to forgive a team killer. This can be issued in global, team, or squad chat.</p>

<h4>Default value</h4>
<p class=""default-value"">" + Defaults[VariableName.ForgiveCommand] + @"</p>

<h2 class=""group"">Messages</h2>

<h3>" + VariableName.TeamKillMessage + @"</h3>
<p>Sent to both the killer and victim when a team kill is detected.</p>

<h4>Available substitutions</h4>
<table>
<tr>
	<th>Placeholder</th>
	<th>Description</th>
</tr>
<tr>
	<td><strong>{killer}</strong></td>
	<td>Player name of killer.</td>
</tr>
<tr>
	<td><strong>{victim}</strong></td>
	<td>Player name of victim.</td>
</tr>
<tr>
	<td><strong>{killCount}</strong></td>
	<td>Total team kill count for killer.</td>
</tr>
<tr>
	<td><strong>{killCount:<em>&lt;singular&gt;</em>|<em>&lt;plural&gt;</em>}</strong></td>
	<td>The units to use for the kill count. If kill count is one, the value of <em>&lt;singular&gt;</em> is used, otherwise <em>&lt;plural&gt;</em> is used.</td>
</tr>
</table>

<h4>Default value</h4>
<p class=""default-value"">" + Defaults[VariableName.TeamKillMessage] + @"</p>

<h3>" + VariableName.VictimPromptMessage + @"</h3>
<p>Sent to the victim when a team kill is detected.</p>

<h4>Default value</h4>
<p class=""default-value"">" + Defaults[VariableName.VictimPromptMessage] + @"</p>

<h3>" + VariableName.ShowStatsOnVictimPromptMessage + @"</h3>
<p>Show statistics with the victim prompt. This includes:</p>
<ul>
	<li>The number of times the killer has killed the victim.</li>
	<li>The number of times the victim has punished or forgiven the killer.</li>
	<li>The number of times the killer has been auto-forgiven for a team kill on this victim.</li>
</ul>

<h4>Default value</h4>
<p class=""default-value"">" + Defaults[VariableName.ShowStatsOnVictimPromptMessage] + @"</p>

<h3>" + VariableName.PunishedMessage + @"</h3>
<p>Sent to both the killer and victim when a <em>punish</em> command is successful.</p>

<h4>Available substitutions</h4>
<table>
<tr>
	<th>Placeholder</th>
	<th>Description</th>
</tr>
<tr>
	<td><strong>{killer}</strong></td>
	<td>Player name of killer.</td>
</tr>
</table>

<h4>Default value</h4>
<p class=""default-value"">" + Defaults[VariableName.PunishedMessage] + @"</p>

<h3>" + VariableName.ForgivenMessage + @"</h3>
<p>Sent to both the killer and victim when a <em>forgive</em> command is successful.</p>

<h4>Available substitutions</h4>
<table>
<tr>
	<th>Placeholder</th>
	<th>Description</th>
</tr>
<tr>
	<td><strong>{killer}</strong></td>
	<td>Player name of killer.</td>
</tr>
</table>

<h4>Default value</h4>
<p class=""default-value"">" + Defaults[VariableName.ForgivenMessage] + @"</p>

<h3>" + VariableName.NoOneToPunishMessage + @"</h3>
<p>Sent to both the killer and victim when a <em>punish</em> command is unsuccessful.</p>

<h4>Available substitutions</h4>
<table>
<tr>
	<th>Placeholder</th>
	<th>Description</th>
</tr>
<tr>
	<td><strong>{window}</strong></td>
	<td>Length (in seconds) of the punish window.</td>
</tr>
</table>

<h4>Default value</h4>
<p class=""default-value"">" + Defaults[VariableName.NoOneToPunishMessage] + @"</p>

<h3>" + VariableName.NoOneToForgiveMessage + @"</h3>
<p>Sent to both the killer and victim when a <em>forgive</em> command is unsuccessful.</p>

<h4>Available substitutions</h4>
<table>
<tr>
	<th>Placeholder</th>
	<th>Description</th>
</tr>
<tr>
	<td><strong>{window}</strong></td>
	<td>Length (in seconds) of the punish window.</td>
</tr>
</table>

<h4>Default value</h4>
<p class=""default-value"">" + Defaults[VariableName.NoOneToForgiveMessage] + @"</p>

<h2 class=""group"">Timing</h2>

<h3>" + VariableName.PunishWindow + @"</h3>
<p>How long (in seconds) to allow a victim to punish or forgive before the killer is auto-forgiven.</p>

<h4>Range</h4>
<table>
<tr>
	<th>Minimum</th>
	<td>" + PunishWindowMin + @"</td>
</tr>
<tr>
	<th>Maximum</th>
	<td>" + PunishWindowMax + @"</td>
</tr>
</table>

<h4>Default value</h4>
<p class=""default-value"">" + ((TimeSpan)Defaults[VariableName.PunishWindow]).TotalSeconds + @"</p>
";
		}
	}
}