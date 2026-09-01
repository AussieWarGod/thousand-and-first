using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomCreedRules
	{

		/// <summary>Lower-case name for a temper, Qud style, for reports and the dev log.</summary>
		public static string TemperName(CityTemper Temper)
		{
			switch (Temper)
			{
			case CityTemper.Muttering:
				return "muttering";
			case CityTemper.Quarrel:
				return "quarrelling";
			case CityTemper.Rupture:
				return "ruptured";
			case CityTemper.Secession:
				return "past mending";
			default:
				return "at peace";
			}
		}

		/// <summary>
		/// Founder-facing umbrella clause naming a city's covenant or allegiance. Lower-case,
		/// fit to follow a comma and truthful for religious and non-religious kinds.
		/// </summary>
		/// <param name="CreedDisplayName">The creed faction's display name, or null for a city of
		/// mixed people.</param>
		public static string CreedClause(string CreedDisplayName)
		{
			return string.IsNullOrEmpty(CreedDisplayName)
				? "a city of mixed people, holding to nothing in particular"
				: ("a city that holds with " + CreedDisplayName);
		}

		/// <summary>
		/// The standing line the Charter shows about the two cities. Always available, whatever the
		/// temper, so the founder can watch this coming rather than being told about it once.
		/// </summary>
		/// <param name="Temper">The temper now.</param>
		/// <param name="Dissent">Dissent standing now, shown plainly against its breaking point.</param>
		/// <param name="HereName">The seated city's name.</param>
		/// <param name="ThereName">The other city's name.</param>
		/// <param name="HereCreed">The seated city's creed display name, or null.</param>
		/// <param name="ThereCreed">The other city's creed display name, or null.</param>
		public static string TemperReport(CityTemper Temper, int Dissent, string HereName, string ThereName, string HereCreed, string ThereCreed)
		{
			string here = string.IsNullOrEmpty(HereName) ? "this city" : ("{{C|" + HereName + "}}");
			string there = string.IsNullOrEmpty(ThereName) ? "the other city" : ("{{C|" + ThereName + "}}");
			string body = here + " is " + CreedClause(HereCreed) + ".\n" + there + " is " + CreedClause(ThereCreed) + ".\n\n";
			switch (Temper)
			{
			case CityTemper.Muttering:
				return body + "They are {{Y|muttering}} about each other. Nothing has been said to anyone's face. {{K|(" + Dissent + " of " + DissentBreaking + ")}}";
			case CityTemper.Quarrel:
				return body + "They are {{W|quarrelling}} openly, and each has begun keeping its own account of who started it. {{K|(" + Dissent + " of " + DissentBreaking + ")}}";
			case CityTemper.Rupture:
				return body + "This is a {{R|rupture}}. One of them is deciding whether it needs you. {{K|(" + Dissent + " of " + DissentBreaking + ")}}";
			case CityTemper.Secession:
				return body + "It is {{R|past mending}}. {{K|(" + Dissent + " of " + DissentBreaking + ")}}";
			default:
				return body + "They are at peace with one another. {{K|(" + Dissent + " of " + DissentBreaking + ")}}";
			}
		}

		/// <summary>
		/// What the realm says on the pass a temper worsens. One line, non-modal, in the
		/// water-keepers' voice.
		/// </summary>
		/// <param name="Temper">The temper now. <see cref="CityTemper.Concord"/> returns empty;
		/// <see cref="CityTemper.Secession"/> has its own telling.</param>
		/// <param name="HereName">The seated city's name.</param>
		/// <param name="ThereName">The other city's name.</param>
		public static string TemperSpeech(CityTemper Temper, string HereName, string ThereName)
		{
			string here = string.IsNullOrEmpty(HereName) ? "this city" : ("{{C|" + HereName + "}}");
			string there = string.IsNullOrEmpty(ThereName) ? "the other city" : ("{{C|" + ThereName + "}}");
			switch (Temper)
			{
			case CityTemper.Muttering:
				return "{{Y|In " + here + " they have started saying " + there + " as though it were a place they had heard about.}}";
			case CityTemper.Quarrel:
				return "{{W|A carter came in from " + there + " and was served last. Nobody in " + here + " thought that worth remarking on.}}";
			case CityTemper.Rupture:
				return "{{R|" + here + " read its own charter aloud tonight and left out the part that names " + there + ".}}";
			default:
				return "";
			}
		}

		/// <summary>
		/// The same worsening as the founder's own book records it: lower-case clause, no trailing
		/// period, because the chronicle dates it and closes it. Kept apart from
		/// <see cref="TemperSpeech"/> because a chronicle entry is not a message with the colour
		/// stripped out &mdash; it is written a year later, by someone with an opinion.
		/// </summary>
		public static string TemperChronicle(CityTemper Temper, string HereName, string ThereName)
		{
			string here = string.IsNullOrEmpty(HereName) ? "the city" : HereName;
			string there = string.IsNullOrEmpty(ThereName) ? "the other city" : ThereName;
			switch (Temper)
			{
			case CityTemper.Muttering:
				return here + " began to speak of " + there + " as somewhere else";
			case CityTemper.Quarrel:
				return here + " and " + there + " fell to open quarrelling, and each kept its own account of who began it";
			case CityTemper.Rupture:
				return "the charter was read aloud in " + here + " with " + there + " left out of it";
			default:
				return "";
			}
		}
	}
}
