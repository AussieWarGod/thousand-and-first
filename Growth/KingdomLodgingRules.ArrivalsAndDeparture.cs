using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomLodgingRules
	{
		// ==================================================================================
		// Addendum 4b -- housing binds; Addendum 10(a) -- the brink moderates. A settler joins
		// only if a home exists THEY would take, and a settler who loses every acceptable home is
		// at a BRINK (KingdomBrinkRules): the loss is recorded with the tick it happened, word is
		// PUSHED to the founder once wherever they are, naming what would arrest it, and from that
		// delivery they have GraceDays of WORLD TIME. Spend it and they leave through the
		// emigration machinery the settlement already has -- attended or not.
		//
		// The unit changed and the rope did not. What used to be two attended passes is the same
		// two attended passes restated at the cadence a present founder was always assumed to keep
		// (KingdomBrinkRules.CohabitationDaysPerAttendedPass), so a founder who comes home every
		// third day walks exactly the road they always walked. Only one who leaves sees any
		// difference -- and that difference is the ruling: with warning, coaching and fair time,
		// it is fair for things to happen while they are away.
		// ==================================================================================

		/// <summary>
		/// World-days a settler who has lost every acceptable home is given, counted from the day
		/// the word reached the founder rather than from the day the roof went. Six:
		/// <see cref="KingdomBrinkRules.RoofBrinkWindowDays"/>, which is
		/// <c>RoofBrinkWindowPasses</c> restated in world time. Long enough for a founder who
		/// hears the news on the road to come back and raise a bunk or stake a plan, short enough
		/// that the answer to "why is nobody moving out" is never "wait longer".
		/// </summary>
		public const int GraceDays = KingdomBrinkRules.RoofBrinkWindowDays;

		/// <summary>The cause a housing departure is chronicled and noted under, in both
		/// registers. Named here rather than written at the call site so the chronicle and the
		/// ledger cannot drift apart, and so a test can pin it.</summary>
		public const string DepartureCause = "for want of a roof they would live under";

		/// <summary>
		/// The one sentence the founder is owed when a settler's grace has run out and they are
		/// going (STANDARDS 7b). Names the person and the cause and nothing else; the departure
		/// itself is chronicled by the emigration machinery under
		/// <see cref="DepartureCause"/>.
		/// </summary>
		public static string LeavingLine(string ResidentName)
		{
			return LeavingLine(ResidentName, 0);
		}

		/// <summary>
		/// The same sentence, dated with how long they actually went without. The honest elapsed
		/// is the brink's whole contribution to this line: the founder is told the real number of
		/// days the settler slept outside, not the number a capped clock was willing to look at.
		/// </summary>
		/// <param name="ResidentName">The person, by name.</param>
		/// <param name="Days">Whole days since the roof was lost, from
		/// <see cref="KingdomBrinkRules.DaysStood"/>. Zero reads as a loss noticed tonight and
		/// drops the clause entirely.</param>
		public static string LeavingLine(string ResidentName, int Days)
		{
			string name = string.IsNullOrEmpty(ResidentName) ? "a settler" : ResidentName;
			if (Days <= 0)
			{
				return name + " has waited out the grace with nowhere in the settlement to live, and is leaving.";
			}
			return name + " has waited out the grace with nowhere in the settlement to live \u2014 "
				+ Days + ((Days == 1) ? " day" : " days") + " of it \u2014 and is leaving.";
		}

		/// <summary>One standing home as the arrival gate sees it. Whether its occupants refuse
		/// the newcomer is decided by <see cref="Conflicts"/> before a home reaches here, because
		/// that answer needs the occupants themselves and this file judges no objects.</summary>
		public readonly struct ArrivalHome
		{
			/// <summary>What the home offers, tags folded &mdash; the design's declared
			/// <c>Provides</c> plus what its roof gives.</summary>
			public readonly IReadOnlyList<string> Provides;

			/// <summary>Beds the home carries.</summary>
			public readonly int Capacity;

			/// <summary>Residents already assigned to it.</summary>
			public readonly int Occupants;

			/// <summary>Whether somebody already in it will not live beside the newcomer, or the
			/// newcomer beside them.</summary>
			public readonly bool OccupantsRefuse;

			public ArrivalHome(IReadOnlyList<string> Provides, int Capacity, int Occupants, bool OccupantsRefuse)
			{
				this.Provides = Provides;
				this.Capacity = Capacity;
				this.Occupants = Occupants;
				this.OccupantsRefuse = OccupantsRefuse;
			}
		}

		/// <summary>
		/// Addendum 4b's arrival gate, which is assignment-level and not a bed tally: whether
		/// SOME standing home would take this arrival &mdash; meets their Needs, has a bed free,
		/// and holds nobody either of them refuses. A settlement with ten empty beds and no
		/// charging post has no room for a robot, and a bed count can never say so.
		/// </summary>
		/// <param name="Homes">Every home standing in the settlement. Null or empty is a
		/// settlement with no roof at all.</param>
		/// <param name="Needs">The arrival's hard requirements. Null or empty asks nothing.
		/// </param>
		/// <param name="Reason">Why nobody would take them, in the order a founder should hear
		/// the reasons. <see cref="UnhousedReason.Housed"/> when one would.</param>
		/// <param name="Homes">Standing, un-condemned homes the caller gathered.</param>
		/// <param name="Needs">What the newcomer needs.</param>
		/// <param name="Reason">Why nobody would take them.</param>
		/// <param name="AnyCondemnedRoof">Whether the caller filtered any home out for being worn
		/// past <see cref="CondemnedWearPercent"/>. Without this a settlement whose every roof has
		/// fallen in would be told it had never built one, which is both untrue and the wrong
		/// remedy: mend, do not commission.</param>
		public static bool AnyWouldTake(IReadOnlyList<ArrivalHome> Homes, IReadOnlyList<string> Needs, out UnhousedReason Reason, bool AnyCondemnedRoof = false)
		{
			bool anyStanding = Homes != null && Homes.Count > 0;
			bool anyRoofAtAll = anyStanding || AnyCondemnedRoof;
			bool anyMeetsNeeds = false;
			bool anyHasCapacity = false;
			bool anyWithoutRefusal = false;
			if (anyStanding)
			{
				for (int i = 0; i < Homes.Count; i++)
				{
					if (!MeetsNeeds(Needs, Homes[i].Provides))
					{
						continue;
					}
					anyMeetsNeeds = true;
					if (!HasFreeBed(Homes[i].Capacity, Homes[i].Occupants))
					{
						continue;
					}
					anyHasCapacity = true;
					if (Homes[i].OccupantsRefuse)
					{
						continue;
					}
					anyWithoutRefusal = true;
				}
			}
			Reason = Diagnose(anyRoofAtAll, anyMeetsNeeds, anyHasCapacity, anyWithoutRefusal, anyStanding);
			return Reason == UnhousedReason.Housed;
		}

		/// <summary>The chronicle line for an arrival the settlement had no home for: the real
		/// reason, never a bed count. Addendum 4b's "no home they would take".</summary>
		public static string ArrivalRefusedChronicle(string Settlement, UnhousedReason Reason)
		{
			string where = string.IsNullOrWhiteSpace(Settlement) ? "the settlement" : Settlement.Trim();
			switch (Reason)
			{
			case UnhousedReason.NoRoofAtAll:
				return "a settler reached " + where + " and found no roof standing";
			case UnhousedReason.NeedsUnmet:
				return "a settler reached " + where + " and found no home they would take";
			case UnhousedReason.Full:
				return "a settler reached " + where + " and found every home already full";
			case UnhousedReason.Refused:
				return "a settler reached " + where + " and found no home they would take, for who was already in it";
			case UnhousedReason.Condemned:
				return "a settler reached " + where + " and found every roof in it fallen in";
			default:
				return "a settler reached " + where + " and found nowhere to live";
			}
		}

		/// <summary>The ledger note for the same refusal: what the founder can go and do about
		/// it.</summary>
		public static string ArrivalRefusedNote(UnhousedReason Reason)
		{
			switch (Reason)
			{
			case UnhousedReason.NoRoofAtAll:
				return "A settler came and found no roof standing. Commission housing and they will stay.";
			case UnhousedReason.NeedsUnmet:
				return "A settler came and found no home they would take. Commission housing that answers what they need, and they will stay.";
			case UnhousedReason.Full:
				return "A settler came and found every home full. Commission more housing and they will stay.";
			case UnhousedReason.Refused:
				return "A settler came and found no home they would take, for who was already living in it. Another roof would give them somewhere of their own.";
			case UnhousedReason.Condemned:
				return "A settler came and found every roof here fallen in. Mend one and they will stay.";
			default:
				return "A settler came and found nowhere to live.";
			}
		}
	}
}
