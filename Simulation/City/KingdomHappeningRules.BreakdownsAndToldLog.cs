using System;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomHappeningRules
	{
		// ==================================================================================
		// Breakdowns — a stopped work is a small drama with a name on it
		// ==================================================================================

		/// <summary>
		/// The condition at or under which a work has stopped being one.
		/// <para>
		/// The lodging vocabulary's own line, read from the other side: a home is condemned at
		/// <c>KingdomLodgingRules.CondemnedWearPercent</c> of WEAR, and condition is what is left
		/// after wear, so the same building is condemned by the housing machinery and broken by
		/// the happenings layer at exactly the same number. Both are
		/// <c>KingdomRules.RuinStandingCeilingPercent</c>, and there is only the one constant.
		/// </para>
		/// </summary>
		internal const int BreakdownConditionFloor = 100 - KingdomLodgingRules.CondemnedWearPercent;

		/// <summary>
		/// What this work row is worth saying, given what the city last said about it.
		/// <list type="bullet">
		/// <item><description>A work the city believed was fine and is not any more is a
		/// breakdown, and the city says so once.</description></item>
		/// <item><description>A work the city believed was broken and that is running again is the
		/// UNSAYING &mdash; <c>KingdomWord.Unsay</c>'s own lane, for the founder who came home and
		/// mended it (Addendum 10(a): a warning withdrawn is owed from the same distance it was
		/// given).</description></item>
		/// <item><description>Anything the city already believes correctly is silence.</description></item>
		/// </list>
		/// <para>
		/// <b>The belief is the told-log ring's, not a snapshot's</b>, which is what makes this a
		/// rendering rather than a diff engine: there is no "before" state kept anywhere, only
		/// what the city has already told the founder, and that is already stored.
		/// </para>
		/// </summary>
		/// <returns>The happening, or <see cref="KingdomHappening.None"/>. Outcome carries the
		/// work's condition, and a NEGATIVE outcome marks the unsaying.</returns>
		internal static KingdomHappening Judge(KingdomWorkRow row, bool believedBroken, long nowTick)
		{
			if (row.WorkId <= 0)
			{
				return KingdomHappening.None;
			}
			bool broken = Broken(row);
			if (broken == believedBroken)
			{
				return KingdomHappening.None;
			}
			return new KingdomHappening(
				KingdomHappeningKind.Breakdown,
				nowTick,
				row.WorkId,
				0,
				row.ZoneId,
				broken ? row.ConditionPercent : (-1 - row.ConditionPercent));
		}

		/// <summary>
		/// Whether the row reads as a work that has stopped being one: worn past the condemned
		/// line, or standing with nobody on it when its kind cannot run without hands.
		/// <para>
		/// Both clauses are rows the model already keeps, which is the whole of why this is a
		/// rendering rather than a second wear system. <b>The crew clause is gated on kind on
		/// purpose:</b> a larder with nobody standing in it is a larder, and a growing ground
		/// grows whether or not anyone is watching it, so calling either of them broken would
		/// announce a drama that is not happening.
		/// </para>
		/// </summary>
		internal static bool Broken(KingdomWorkRow row)
		{
			return row.ConditionPercent <= BreakdownConditionFloor
				|| (NeedsHands(row.RunState.Kind) && row.CrewAssigned <= 0);
		}

		/// <summary>
		/// Whether a work of this kind stops when nobody is on it. A producer, refiner, power work
		/// and active raising are all a pair of hands away from silence; a store and a growing ground
		/// are not, and <c>Other</c> is not claimed either way because the model does not know it.
		/// </summary>
		internal static bool NeedsHands(KingdomWorkKind kind)
		{
			return kind == KingdomWorkKind.Producer || kind == KingdomWorkKind.Refiner
				|| kind == KingdomWorkKind.Power || kind == KingdomWorkKind.Construction;
		}

		/// <summary>Whether an outcome written by <see cref="JudgeWork"/> is the unsaying rather
		/// than the breakdown. The sign is the whole encoding, so the ring stays thirty-two
		/// bytes.</summary>
		internal static bool IsMending(int outcome)
		{
			return outcome < 0;
		}

		/// <summary>The condition an outcome written by <see cref="JudgeWork"/> carries, whichever
		/// side of the line it was written on.</summary>
		internal static int ConditionOf(int outcome)
		{
			return IsMending(outcome) ? (-1 - outcome) : outcome;
		}

		// ==================================================================================
		// The told-log vocabulary
		// ==================================================================================

		/// <summary>The ring's kind for a happening kind. One vocabulary, mapped rather than
		/// duplicated.</summary>
		internal static KingdomToldKind ToldKindOf(KingdomHappeningKind kind)
		{
			switch (kind)
			{
			case KingdomHappeningKind.Wedding:
				return KingdomToldKind.Wedding;
			case KingdomHappeningKind.Funeral:
				return KingdomToldKind.Funeral;
			case KingdomHappeningKind.Festival:
				return KingdomToldKind.Festival;
			case KingdomHappeningKind.Breakdown:
				return KingdomToldKind.Breakdown;
			case KingdomHappeningKind.Brownout:
				return KingdomToldKind.Brownout;
			default:
				return KingdomToldKind.None;
			}
		}

		/// <summary>The inverse, for a ring read back off a save.</summary>
		internal static KingdomHappeningKind KindOf(KingdomToldKind told)
		{
			switch (told)
			{
			case KingdomToldKind.Wedding:
				return KingdomHappeningKind.Wedding;
			case KingdomToldKind.Funeral:
				return KingdomHappeningKind.Funeral;
			case KingdomToldKind.Festival:
				return KingdomHappeningKind.Festival;
			case KingdomToldKind.Breakdown:
				return KingdomHappeningKind.Breakdown;
			case KingdomToldKind.Brownout:
				return KingdomHappeningKind.Brownout;
			default:
				return KingdomHappeningKind.None;
			}
		}

		/// <summary>
		/// Whether a happening of this kind about these subjects is already in the ring &mdash; the
		/// announce-once check (STANDARDS 7b), asked of the ring rather than of a second ledger.
		/// </summary>
		/// <param name="state">The city's book.</param>
		/// <param name="kind">What is about to be told.</param>
		/// <param name="subjectA">Its first subject.</param>
		/// <param name="subjectB">Its second, or zero.</param>
		internal static bool AlreadyTold(KingdomCityState state, KingdomHappeningKind kind, int subjectA, int subjectB)
		{
			if (state == null || kind == KingdomHappeningKind.None)
			{
				return false;
			}
			KingdomToldKind wanted = ToldKindOf(kind);
			for (int i = 0; i < state.ToldCount; i++)
			{
				KingdomToldRow row;
				if (state.TryTold(i, out row) && row.Kind == wanted && row.SubjectA == subjectA && row.SubjectB == subjectB)
				{
					return true;
				}
			}
			return false;
		}

	}
}
