using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomLabRules
	{
		// --- The slate (DIVERSITY §3.8) ---------------------------------------------------------
		//
		// Two levels of Popup.PickOption and no new screen class, which is the golem's own shape,
		// Playable Golem's shape and the control menu's shape at once. The strings are here because
		// every one of them is a pure function of model state and none of them needs an engine.

		/// <summary>The mark a slot with something on it carries. Vanilla's own, from the golem
		/// mound's option list.</summary>
		public const string MarkFilled = "{{green|[þ]}}";

		/// <summary>The mark an empty slot carries.</summary>
		public const string MarkEmpty = "{{red|[X]}}";

		/// <summary>The prefix every effect line takes, so a founder reads a consequence in the same
		/// colour wherever the game shows them one.</summary>
		public const string EffectPrefix = "{{rules|--}} ";

		/// <summary>The slate's own heading.</summary>
		public static string SlateTitle(string CityName)
		{
			return "the grafting hall of " + Named(CityName);
		}

		/// <summary>
		/// The two lines above the list: who does the work, and what there is to work with. Both are
		/// facts a founder would otherwise have to go and count.
		/// </summary>
		/// <param name="Savant">The lodged savant's name, or null when the hall has none.</param>
		/// <param name="Was">What they were before they came, or null.</param>
		/// <param name="Kept">Preserved parts in the vat-house.</param>
		public static string SlateIntro(string Savant, string Was, int Kept)
		{
			System.Text.StringBuilder text = new System.Text.StringBuilder();
			if (string.IsNullOrEmpty(Savant))
			{
				// 7b: a hall with nobody in it will work no days at all, and that is the single
				// most important thing on this screen.
				text.Append("{{r|No savant is lodged here. The hall opens nothing until somebody who knows the work lives in this city.}}");
			}
			else
			{
				text.Append("savant: {{W|").Append(Savant).Append("}}");
				if (!string.IsNullOrEmpty(Was))
				{
					text.Append(", who was ").Append(Was);
				}
			}
			text.Append("\npreserved parts in the vat-house: ");
			text.Append((Kept > 0) ? ("{{C|" + Kept + "}}") : "{{K|none}}");
			return text.ToString();
		}

		/// <summary>
		/// One row of the slate: a place on the founder's body and what is on it.
		/// </summary>
		/// <param name="SlotName">The part, as the founder would say it &mdash; "your left arm".</param>
		/// <param name="GraftedName">What is grafted there, or null.</param>
		/// <param name="Offers">Whether the hall has anything at all it could put there.</param>
		public static string SlotRow(string SlotName, string GraftedName, bool Offers)
		{
			if (!string.IsNullOrEmpty(GraftedName))
			{
				return Named(SlotName) + "  " + MarkFilled + " " + GraftedName;
			}
			return Named(SlotName) + "  " + (Offers ? (MarkEmpty + " {{K|<nothing grafted>}}") : "{{K|nothing the hall knows would go there}}");
		}

		/// <summary>
		/// One candidate row, with its price stated before anything is committed. The fix for the
		/// one documented complaint about the vanilla picker (DIVERSITY &sect;3.0d): players treat
		/// the golem's atzmus as a lottery because the payoff is opaque at the point of choosing,
		/// and ours is not a lottery, so ours has no excuse.
		/// </summary>
		public static string CandidateRow(LabProcedure Procedure, int Kept)
		{
			if (Procedure == null)
			{
				return "";
			}
			System.Text.StringBuilder text = new System.Text.StringBuilder(Procedure.Named);
			text.Append("  {{K|[kept x").Append(Kept).Append("]}}");
			for (int i = 0; i < Procedure.Discloses.Count; i++)
			{
				text.Append("\n  ").Append(EffectPrefix).Append(Procedure.Discloses[i]);
			}
			text.Append("\n  ").Append(EffectPrefix).Append(PriceLine(Procedure));
			return text.ToString();
		}

		/// <summary>The whole price in one sentence, in the units the founder already reads
		/// everywhere else in the mod.</summary>
		public static string PriceLine(LabProcedure Procedure)
		{
			if (Procedure == null)
			{
				return "";
			}
			System.Text.StringBuilder text = new System.Text.StringBuilder();
			text.Append(Procedure.Cost).Append(" drams");
			if (!string.IsNullOrEmpty(Procedure.Bits))
			{
				text.Append(", ").Append(Procedure.Bits).Append(" in bits");
			}
			text.Append(", ").Append(Procedure.Preserved)
				.Append((Procedure.Preserved == 1) ? " kept part" : " kept parts");
			text.Append(", and ").Append(Procedure.StaffDays)
				.Append((Procedure.StaffDays == 1) ? " day" : " days").Append(" of the hall's work");
			List<KeyValuePair<string, int>> standing = StandingCost(Procedure.Creeds, StandingPerCreed);
			if (standing.Count > 0)
			{
				text.Append("; standing ");
				for (int i = 0; i < standing.Count; i++)
				{
					if (i > 0)
					{
						text.Append(", ");
					}
					text.Append(standing[i].Value).Append(" with ").Append(standing[i].Key);
				}
			}
			return text.ToString();
		}

		/// <summary>
		/// The line a founder is owed before a graft that will change what they can do in the world
		/// at all.
		/// <para>
		/// Playable Golem's dominant complaint is that its golems cannot equip most gear or enter
		/// the Spindle (DIVERSITY &sect;3.0c, &sect;3.9 risk 4). Every Class III procedure needs an
		/// explicit answer to <i>"what does this stop you doing?"</i> stated before commitment, and
		/// the honest general answer &mdash; that the hall can take it off again &mdash; is stated
		/// with it, because that is the consent story.
		/// </para>
		/// </summary>
		public static string ReversibilityLine()
		{
			return "{{rules|--}} Whatever this stops you doing, the hall can take it off again. It costs less than the graft and returns nothing.";
		}

		/// <summary>The three-way consent prompt, in the precedent's own words. The third answer
		/// writes to a permanent exclusion list, so a founder who never wants to see a thing again
		/// never does.</summary>
		public static readonly string[] ConsentOptions = new string[3]
		{
			"Have it done.",
			"Not now.",
			"Never offer this again."
		};

		/// <summary>What the hall says when a commission is staked. Commissioning is not clicking:
		/// the crews work it over world-days and the founder may walk away and come home to it done,
		/// which is the whole mod's grammar and the lab may not be the one place that breaks it.</summary>
		public static string StakedLine(string ProcedureName, int StaffDays)
		{
			return "The hall has taken it on. " + Named(ProcedureName) + " wants {{C|" + StaffDays
				+ "}}" + ((StaffDays == 1) ? " day" : " days")
				+ " of real work from the people who live here. Go and do something else; it will be done when it is done.";
		}

		/// <summary>What the founder is told the day it is finished, wherever they are.</summary>
		public static string DoneLine(string ProcedureName, string CityName)
		{
			return "{{G|It is done. " + Named(ProcedureName) + " was performed on you at " + Named(CityName) + ".}}";
		}

		/// <summary>The same moment, dated, for the chronicle.</summary>
		public static string DoneTelling(string ProcedureName, string CityName)
		{
			return "the hall at " + Named(CityName) + " performed " + Named(ProcedureName) + ", and the founder walked out changed";
		}

		/// <summary>What the founder is told when a graft is taken off. Said plainly, including the
		/// part nobody wants to hear: it returns nothing.</summary>
		public static string RemovedTelling(string ProcedureName, string CityName)
		{
			return "the hall at " + Named(CityName) + " took " + Named(ProcedureName) + " back off again, and nothing was given back for it";
		}

		/// <summary>Vanilla's own register for a slot with no legal candidate at all
		/// (<c>Popup.ShowFail</c>'s <c>NO_REQUIRED</c> shape).</summary>
		public static string NothingMeetsRequirement(string SlotName)
		{
			return "You have nothing that meets the requirement of the hall for " + Named(SlotName) + ".";
		}

		/// <summary>A name as a founder would say it, or an honest word when nothing named one.</summary>
		public static string Named(string Text)
		{
			return string.IsNullOrEmpty(Text) ? "the work" : Text.Trim();
		}
	}
}
