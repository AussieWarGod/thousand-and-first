using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	public partial class KingdomSettlement
	{
		/// <summary>True if <paramref name="Vocation"/> is one this build knows.</summary>
		public static bool IsKnownVocation(string Vocation)
		{
			for (int i = 0; i < Vocations.Length; i++)
			{
				if (Vocations[i] == Vocation)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Founder-facing clause naming what a city is for. Lower-case, article included, fit to
		/// follow "founded as " or stand after a comma.
		/// </summary>
		/// <param name="Vocation">A vocation, or null.</param>
		/// <returns>The clause; the neutral clause for anything unrecognised, empty for null.</returns>
		public static string VocationClause(string Vocation)
		{
			if (string.IsNullOrEmpty(Vocation))
			{
				return "";
			}
			switch (Vocation)
			{
			case "waystation":
				return "a waystation on the long roads";
			case "refuge":
				return "a refuge for whoever reaches it";
			case "reliquary":
				return "a reliquary for what should not be lost";
			default:
				return "a holding of the realm";
			}
		}

		/// <summary>The vocation clause as a trailing comma clause, or empty. For sentences that
		/// name the city first and its purpose second.</summary>
		public static string VocationSuffix(string Vocation)
		{
			string clause = VocationClause(Vocation);
			return string.IsNullOrEmpty(clause) ? "" : (", " + clause);
		}

		/// <summary>The menu blurb for a vocation, or empty for one this build does not know.</summary>
		public static string VocationBlurb(string Vocation)
		{
			for (int i = 0; i < Vocations.Length && i < VocationBlurbs.Length; i++)
			{
				if (Vocations[i] == Vocation)
				{
					return VocationBlurbs[i];
				}
			}
			return "";
		}

		/// <summary>
		/// Why an additional-city founding may not proceed, or <see cref="SecondFoundingVerdict.Allowed"/>.
		/// Pure so the rule is tabled rather than discovered in the field.
		/// </summary>
		public enum SecondFoundingVerdict
		{
			Allowed,
			NothingFoundedYet,
			GroundIsAlreadyOurs,
			GroundIsTooClose,
			RealmIsFull
		}

		/// <summary>
		/// Judges whether the founding rite, performed on ground the founder is standing on,
		/// founds the realm's next additional city before the three-city cap. The public verdict
		/// name remains historical API vocabulary.
		/// </summary>
		/// <param name="Founded">Whether the realm exists at all.</param>
		/// <param name="SettlementsHeld">Cities the realm already holds, seat included.</param>
		/// <param name="GroundIsClaimed">Whether this zone is already the realm's ground.</param>
		/// <param name="GroundIsAdjacent">Whether this zone borders the realm's ground. Bordering
		/// ground is an expansion of a city, not a new one, and is claimed rather than founded.</param>
		public static SecondFoundingVerdict JudgeSecondFounding(bool Founded, int SettlementsHeld, bool GroundIsClaimed, bool GroundIsAdjacent)
		{
			if (!Founded)
			{
				return SecondFoundingVerdict.NothingFoundedYet;
			}
			if (SettlementsHeld >= MaxSettlements)
			{
				return SecondFoundingVerdict.RealmIsFull;
			}
			if (GroundIsClaimed)
			{
				return SecondFoundingVerdict.GroundIsAlreadyOurs;
			}
			if (GroundIsAdjacent)
			{
				return SecondFoundingVerdict.GroundIsTooClose;
			}
			return SecondFoundingVerdict.Allowed;
		}

		/// <summary>
		/// What the founder is told when the rite will not found a second city. Written as the
		/// water-keepers would say it, not as a rule.
		/// </summary>
		/// <param name="Verdict">The refusal. <see cref="SecondFoundingVerdict.Allowed"/> returns empty.</param>
		/// <param name="RealmName">The realm's display name, for the line that names it.</param>
		public static string SecondFoundingRefusal(SecondFoundingVerdict Verdict, string RealmName)
		{
			string realm = string.IsNullOrEmpty(RealmName) ? "the realm" : ("{{C|" + RealmName + "}}");
			switch (Verdict)
			{
			case SecondFoundingVerdict.RealmIsFull:
				return realm + " holds three cities, the full reach of this charter. Pouring here would only take the water from a place that already drinks it.";
			case SecondFoundingVerdict.GroundIsAlreadyOurs:
				return "This ground is already " + realm + "'s. Water poured here is water poured twice.";
			case SecondFoundingVerdict.GroundIsTooClose:
				return "This ground borders " + realm + " already. It is claimed, not founded — walk out past the horizon of what you hold if you mean to begin somewhere new.";
			case SecondFoundingVerdict.NothingFoundedYet:
				return "There is nothing to found a second city for yet.";
			default:
				return "";
			}
		}

		/// <summary>
		/// One line describing this settlement for a tester: who it is, what it is for, and the
		/// clocks that decide what it does the next time the founder stands in it.
		/// </summary>
		public string Describe()
		{
			StringBuilder sb = new StringBuilder();
			sb.Append(string.IsNullOrEmpty(SettlementName) ? "(unnamed)" : SettlementName);
			if (!string.IsNullOrEmpty(Vocation))
			{
				sb.Append(" [").Append(Vocation).Append("]");
			}
			sb.Append(" ").Append(Style).Append(" ").Append(Stage).Append(Withered ? " (withered)" : "")
				.Append(" pop=").Append(Population)
				.Append(" dry=").Append(DryStreak)
				.Append(" claims=").Append(ClaimedZones.Count)
				.Append(" founded=").Append(FoundedTick)
				.Append(" semantic=").Append(LastSemanticTick)
				.Append(" visit=").Append(LastVisitTick)
				.Append(" heartbeat=").Append(LastHeartbeatTick)
				.Append(" nextArrival=").Append(NextArrivalTick)
				.Append(" raid=").Append(RaidState).Append("/").Append(RaidFactionName ?? "-")
				.Append(" petition=").Append(PetitionKind);
			return sb.ToString();
		}

	}
}
