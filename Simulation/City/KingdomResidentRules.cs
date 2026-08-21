using System;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// What the ground says about one bound body, on the pass that stood in its zone. The witness
	/// the transition rules are total over.
	/// <para>
	/// Deliberately not "alive / dead": the interesting case is a body that is standing right there
	/// and no longer belongs to the city, which is what <see cref="Led"/> is. That is the case
	/// LIVING-CITY-ARCHITECTURE &sect;8.3 calls the honest residual risk &mdash; a founder who
	/// charms half the settlement and walks them across Qud &mdash; and the design's answer is to
	/// SAY SO rather than to prevent it.
	/// </para>
	/// </summary>
	internal enum KingdomBodyWitness : byte
	{
		/// <summary>A live body carrying this id stands in the zone, on the city's roll.</summary>
		Present = 0,

		/// <summary>A live body carrying this id stands in the zone and follows the founder. It is
		/// no longer one of the city's own hands, however close it is standing.</summary>
		Led = 1,

		/// <summary>A body carrying this id is in the zone and dead.</summary>
		Killed = 2,

		/// <summary>No body carrying this id is in the zone at all, and the zone is attended, so
		/// there is nowhere in it left to look.</summary>
		Missing = 3
	}

	/// <summary>The roll, counted. LIVING-CITY-ARCHITECTURE &sect;8.3's third invariant is an
	/// equation between these numbers and the registry's.</summary>
	internal readonly struct KingdomResidentTally
	{
		internal readonly int Resident;

		internal readonly int Abroad;

		internal readonly int Dead;

		internal KingdomResidentTally(int resident, int abroad, int dead)
		{
			Resident = resident;
			Abroad = abroad;
			Dead = dead;
		}

		/// <summary>Everybody the city still counts as one of its people. The dead are off the
		/// roll; the abroad are on it and doing nothing, which is the whole point of having the
		/// word.</summary>
		internal int OnTheRoll
		{
			get { return Resident + Abroad; }
		}
	}

	/// <summary>
	/// The resident row's own rules: how a standing changes, who labours, and what the roll must
	/// add up to.
	/// <para>
	/// Pure and engine-free. LIVING-CITY-ARCHITECTURE &sect;8.3 sequences this wave explicitly:
	/// <b>W2 ships the rows and the binding but not the placement.</b> So everything here is a
	/// verdict about a row, and nothing here moves a body &mdash; placement is W3, and doing
	/// identity and movement in one wave is how a settler ends up in two places.
	/// </para>
	/// </summary>
	internal static class KingdomResidentRules
	{
		/// <summary>An origin the row does not name. Zero is "no origin", which is what a settler
		/// the ground never told us about actually has.</summary>
		internal const int NoOrigin = 0;

		/// <summary>
		/// Whether this row's person is doing the city's work today.
		/// <para>
		/// &sect;8.3: a body the player took away is <i>"still on the roll, contributing no labour,
		/// and honestly reported as such"</i>. This is that sentence as a predicate. <b>W2 ships
		/// the predicate; wiring it into the crew and upkeep arithmetic is W3</b>, because a
		/// labour figure that changed this wave would be a placement decision made a wave early.
		/// </para>
		/// </summary>
		internal static bool Labours(KingdomResidentRow row)
		{
			return row.Standing == KingdomResidentStanding.Resident;
		}

		/// <summary>Whether the city still counts this person as one of its own. The dead are off
		/// the roll and the abroad are on it.</summary>
		internal static bool OnTheRoll(KingdomResidentRow row)
		{
			return row.Standing != KingdomResidentStanding.Dead;
		}

		/// <summary>Whether a standing is one a body may still be bound for. A dead or absent
		/// person has no body in this city's ground, so the registry holds nothing for them &mdash;
		/// which is what makes "live bindings" and "resident rows" the same count.</summary>
		internal static bool Bindable(KingdomResidentStanding standing)
		{
			return standing == KingdomResidentStanding.Resident;
		}

		/// <summary>
		/// Whether a cause is a coherent reason for a standing.
		/// <para>
		/// A <c>Resident</c> carries no cause; a <c>Dead</c> row carries one of the four death
		/// causes and nothing else; an <c>Abroad</c> row carries one of the three absences. A cause
		/// from the wrong family is refused rather than stored, because a row that says a living
		/// settler died defending the stores is worse than a row that says nothing.
		/// </para>
		/// </summary>
		internal static bool CauseFits(KingdomResidentStanding standing, KingdomStandingCause cause)
		{
			switch (standing)
			{
			case KingdomResidentStanding.Resident:
				return cause == KingdomStandingCause.None;
			case KingdomResidentStanding.Dead:
				return cause >= KingdomStandingCause.Unwitnessed && cause <= KingdomStandingCause.Founder;
			case KingdomResidentStanding.Abroad:
				return cause >= KingdomStandingCause.Followed && cause <= KingdomStandingCause.Astray;
			default:
				return false;
			}
		}

		/// <summary>
		/// The one bridge from a row's cause to the funeral the city already tells.
		/// <para>
		/// The ordinal is <c>KingdomOfficeRules.DeathCause</c>'s own, so
		/// <c>KingdomOfficeRules.CauseClause</c> keeps being the ONE telling and no second cause
		/// vocabulary is ever written. False for a cause that is not a death &mdash; an absence has
		/// no clause on a cairn, and inventing one would put a living person on a memorial.
		/// </para>
		/// </summary>
		internal static bool TryDeathCauseOrdinal(KingdomStandingCause cause, out int ordinal)
		{
			if (cause < KingdomStandingCause.Unwitnessed || cause > KingdomStandingCause.Founder)
			{
				ordinal = 0;
				return false;
			}
			ordinal = (int)cause - (int)KingdomStandingCause.Unwitnessed;
			return true;
		}

		/// <summary>
		/// What one pass's witness does to a row. The dead/abroad vocabulary as transitions, total
		/// over every representable pair.
		/// <list type="bullet">
		/// <item><description><b>Present</b> &rarr; <c>Resident</c>. A person who was abroad and is
		/// standing here again is home, and the cause is cleared: they are not partly away.</description></item>
		/// <item><description><b>Led</b> &rarr; <c>Abroad</c>, cause <c>Followed</c>. The body is
		/// right there and is not the city's.</description></item>
		/// <item><description><b>Killed</b> &rarr; <c>Dead</c>, with the death cause the caller
		/// witnessed.</description></item>
		/// <item><description><b>Missing</b> &rarr; <c>Abroad</c>, cause <c>Astray</c>. The zone is
		/// attended and there is nowhere left in it to look, so the honest word is that they are
		/// somewhere else &mdash; never that they died, which nobody saw.</description></item>
		/// </list>
		/// <para>
		/// <b>Dead is terminal.</b> A dead row never transitions again, whatever the ground says
		/// next: the id is spent, and a witness that appears to contradict it is a body the sweep
		/// or the founder should be told about rather than a resurrection the model performs
		/// silently.
		/// </para>
		/// </summary>
		internal static bool TryTransition(
			KingdomResidentRow row,
			KingdomBodyWitness witness,
			KingdomStandingCause deathCause,
			out KingdomResidentRow next,
			out KingdomCityFault fault)
		{
			next = row;
			if (row.Standing == KingdomResidentStanding.Dead)
			{
				fault = KingdomCityFault.TerminalStanding;
				return false;
			}
			switch (witness)
			{
			case KingdomBodyWitness.Present:
				next = row.WithStanding(KingdomResidentStanding.Resident, KingdomStandingCause.None);
				fault = KingdomCityFault.None;
				return true;
			case KingdomBodyWitness.Led:
				next = row.WithStanding(KingdomResidentStanding.Abroad, KingdomStandingCause.Followed);
				fault = KingdomCityFault.None;
				return true;
			case KingdomBodyWitness.Missing:
				next = row.WithStanding(KingdomResidentStanding.Abroad, KingdomStandingCause.Astray);
				fault = KingdomCityFault.None;
				return true;
			case KingdomBodyWitness.Killed:
				if (!CauseFits(KingdomResidentStanding.Dead, deathCause))
				{
					// Not a defensive check: KingdomOfficeRules already classifies every death the
					// engine reports, so a caller with no cause has not looked. Unwitnessed is a
					// real answer and is spelled; it is never the default for not asking.
					fault = KingdomCityFault.CauseRequired;
					return false;
				}
				next = row.WithStanding(KingdomResidentStanding.Dead, deathCause);
				fault = KingdomCityFault.None;
				return true;
			default:
				fault = KingdomCityFault.NullArgument;
				return false;
			}
		}

		/// <summary>
		/// The unbinding a transition implies, or <see cref="KingdomUnbindCause.None"/> when it
		/// implies none. One place decides which of the registry's causes a standing means, so the
		/// row and the registry can never disagree about why a body stopped being bound.
		/// </summary>
		internal static KingdomUnbindCause UnbindFor(KingdomResidentStanding standing)
		{
			switch (standing)
			{
			case KingdomResidentStanding.Dead:
				return KingdomUnbindCause.Death;
			case KingdomResidentStanding.Abroad:
				return KingdomUnbindCause.Abroad;
			default:
				return KingdomUnbindCause.None;
			}
		}

		/// <summary>The roll, counted off the rows.</summary>
		internal static bool TryTally(KingdomCityState state, out KingdomResidentTally tally)
		{
			tally = default(KingdomResidentTally);
			if (state == null)
			{
				return false;
			}
			int resident = 0;
			int abroad = 0;
			int dead = 0;
			for (int i = 0; i < state.ResidentCount; i++)
			{
				KingdomResidentRow row;
				if (!state.TryResident(i, out row))
				{
					return false;
				}
				switch (row.Standing)
				{
				case KingdomResidentStanding.Resident:
					resident++;
					break;
				case KingdomResidentStanding.Abroad:
					abroad++;
					break;
				default:
					dead++;
					break;
				}
			}
			tally = new KingdomResidentTally(resident, abroad, dead);
			return true;
		}

		/// <summary>
		/// LIVING-CITY-ARCHITECTURE &sect;8.3 invariant 3, checked rather than assumed:
		/// <i>the roll == live bindings + <c>Abroad</c></i>.
		/// <para>
		/// Which unpacks into two facts this walks: every <c>Resident</c> row has exactly one live
		/// binding, and no <c>Abroad</c> or <c>Dead</c> row has one. Both directions matter — a
		/// resident with no binding is a person the city thinks is working and has no body for, and
		/// a dead row with a binding is a corpse the registry will still hand out as a place to
		/// move a settler to.
		/// </para>
		/// <para>
		/// The registry is realm-scope and this walks ONE city, so it counts only bindings whose
		/// key belongs to a row of this book. A key the other city minted is not this city's to
		/// reconcile, and treating it as one would fail the audit every time a realm held two
		/// cities.
		/// </para>
		/// </summary>
		internal static bool TryReconcile(KingdomCityState state, KingdomBindingTable table, out KingdomResidentTally tally, out KingdomCityFault fault)
		{
			tally = default(KingdomResidentTally);
			if (state == null || table == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (!TryTally(state, out tally))
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			int bound = 0;
			for (int i = 0; i < state.ResidentCount; i++)
			{
				KingdomResidentRow row;
				if (!state.TryResident(i, out row))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				bool holds = table.Holds(row.ResidentId, KingdomBindingKind.Resident);
				if (holds != Bindable(row.Standing))
				{
					fault = holds ? KingdomCityFault.DuplicateBinding : KingdomCityFault.UnknownBinding;
					return false;
				}
				if (holds)
				{
					bound++;
				}
			}
			if (bound + tally.Abroad != tally.OnTheRoll)
			{
				fault = KingdomCityFault.RowCapExceeded;
				return false;
			}
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>
		/// Where a person's day puts them, derived from the work they are posted to.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;1.2(d): the day shape is <b>derived from Job and the
		/// settlement's standing policy, never authored per settler</b>, and it holds no times. It
		/// is read against the calendar's own bands, and the mapping from band to PLACE is what W3
		/// adds on top of this; what a person's day is ABOUT is this.
		/// </para>
		/// <para>
		/// A settler with no post keeps the hearth. That is not a placeholder for a wave that has
		/// not shipped: somebody the works have no room for genuinely spends their day at home, and
		/// it is the same fact <c>KingdomGrowth.AssignWork</c> reports as an idle work.
		/// </para>
		/// </summary>
		internal static KingdomDayShape DayShapeFor(int jobWorkId, KingdomWorkKind jobKind)
		{
			if (jobWorkId == 0)
			{
				return KingdomDayShape.Hearth;
			}
			switch (jobKind)
			{
			case KingdomWorkKind.Growing:
				return KingdomDayShape.Field;
			case KingdomWorkKind.Store:
				return KingdomDayShape.Market;
			case KingdomWorkKind.Producer:
			case KingdomWorkKind.Refiner:
				return KingdomDayShape.Craft;
			case KingdomWorkKind.Power:
				return KingdomDayShape.Yard;
			default:
				return KingdomDayShape.Hearth;
			}
		}

		/// <summary>The stable code for one of <c>KingdomRules.Origins</c>, or
		/// <see cref="NoOrigin"/>. The district idiom, applied to where a settler walked in
		/// from: the row carries a code and the name stays in one place.</summary>
		internal static int OriginCode(string origin)
		{
			if (string.IsNullOrEmpty(origin))
			{
				return NoOrigin;
			}
			for (int i = 0; i < KingdomRules.Origins.Length; i++)
			{
				if (string.Equals(KingdomRules.Origins[i], origin, StringComparison.Ordinal))
				{
					return i + 1;
				}
			}
			return NoOrigin;
		}

		/// <summary>The origin a code names, or null. The inverse of <see cref="OriginCode"/> over
		/// every representable input.</summary>
		internal static string OriginKey(int code)
		{
			int index = code - 1;
			if (index < 0 || index >= KingdomRules.Origins.Length)
			{
				return null;
			}
			return KingdomRules.Origins[index];
		}
	}
}
