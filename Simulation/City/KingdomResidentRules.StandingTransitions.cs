using System;
using System.Collections.Generic;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomResidentRules
	{
		/// <summary>Whether a standing is one a body may still be bound for. A dead or absent
		/// person has no body in this city's ground, so the registry holds nothing for them &mdash;
		/// which is what makes "live bindings" and "resident rows" the same count.</summary>
		internal static bool Bindable(KingdomResidentStanding standing)
		{
			return standing == KingdomResidentStanding.Resident
				|| standing == KingdomResidentStanding.Expedition;
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
			case KingdomResidentStanding.Expedition:
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

	}
}
