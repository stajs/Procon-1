using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using PRoConEvents;

namespace Stajs.PRoCon.Plugin.Description
{
	class Program
	{
		static void Main(string[] args)
		{
			var description = new TeamKillTracker()
				.GetDescriptionHtml();

			var endOfStyleTag = "</style>";
			var endOfStyleIndex = description.IndexOf(endOfStyleTag, StringComparison.OrdinalIgnoreCase);

			description = string.Format
				(
					"<h1>Team Kill Tracker [{0}]</h1>\r\nFreeware, public domain.{1}",
					TeamKillTracker.Version,
					new string(description.Skip(endOfStyleIndex + endOfStyleTag.Length).ToArray())
				)
				.Replace("<h1>", "[SIZE=6]").Replace("</h1>", "[/SIZE]")
				.Replace("<h2>", "[SIZE=5][COLOR=\"#6F7073\"]").Replace("<h2 class=\"group\">", "[SIZE=5][COLOR=\"#C23725\"]").Replace("</h2>", "[/COLOR][/SIZE]")
				.Replace("<h3>", "[SIZE=4][COLOR=\"#257BC2\"]").Replace("</h3>", "[/COLOR][/SIZE]")
				.Replace("<h4>", "[SIZE=3]").Replace("</h4>", "[/SIZE]")
				.Replace("<h5>", "[SIZE=2]").Replace("</h5>", "[/SIZE]")
				.Replace("<h6>", "[SIZE=1]").Replace("</h6>", "[/SIZE]")

				.Replace("<strong>", "[B]").Replace("</strong>", "[/B]")
				.Replace("<em>", "[I]").Replace("</em>", "[/I]")

				.Replace("<table>", "[table=\"class: outer_border\"]").Replace("</table>", "[/table]")
				.Replace("<tr>", "[tr]").Replace("</tr>", "[/tr]")
				.Replace("<td>", "[td]").Replace("</td>", "[/td]")
				.Replace("<th>", "[th]").Replace("</th>", "[/th]") // TODO: check.

				.Replace("<p>", "[COLOR=\"#4D5153\"]").Replace("<p class=\"default-value\">", "[COLOR=\"#0EB535\"]").Replace("</p>", "[/COLOR]")

				.Replace("<ol>", "[LIST=1]").Replace("</ol>", "[/LIST]")
				.Replace("<ul>", "[LIST]").Replace("</ul>", "[/LIST]")
				.Replace("<li>", "[*]").Replace("</li>", "")
				;

			var missedAnyHtmlTags = description.Any(c => c == '<' || c == '>');
		}
	}
}