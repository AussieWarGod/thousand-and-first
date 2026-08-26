using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomSubsidenceRules
	{
		// ==================================================================================
		// 6. What it says (STANDARDS 7b: once, by name, and unsaid when it stops).
		// ==================================================================================

		/// <summary>The clause both registers name a subsidence departure by, handed to
		/// <c>KingdomGrowth.Emigrate</c> so the chronicle and the ledger cannot disagree about why
		/// somebody left.</summary>
		/// <param name="Binding">Which good is holding the level, from
		/// <see cref="BindingSupportFor"/>.</param>
		public static string DepartureCause(string Binding)
		{
			switch (NormalizedBinding(Binding))
			{
			case KingdomCatalogueRules.SupportWater:
				return "because the water here was never enough for this many";
			case KingdomCatalogueRules.SupportFood:
				return "because the fields here never fed this many";
			case KingdomCatalogueRules.SupportRoof:
				return "because there was never a roof here for this many";
			default:
				// No binding good named, so none is blamed. Reached only by a settlement no pass
				// has measured yet, or by a saved name from a build with a different vocabulary -
				// and in both cases inventing a cause would be worse than naming none.
				return "because the works here never carried this many";
			}
		}

		/// <summary>The once-only line that says a settlement has begun to settle back, and what
		/// is holding it where it is going.</summary>
		/// <param name="Name">The settlement's display name.</param>
		/// <param name="Binding">The good holding the level.</param>
		/// <param name="Level">Where it is going.</param>
		/// <param name="Population">Where it is standing now.</param>
		public static string BeganNote(string Name, string Binding, int Level, int Population)
		{
			return Name + " is settling back. There are " + Population + " here, and "
				+ KingdomCatalogueRules.LimitLine(Binding, Level)
				+ " Raise what it lacks and the slide stops where it stands.";
		}

		/// <summary>The chronicle's own telling of the same moment.</summary>
		public static string BeganChronicle(string Name, string Binding, int Level)
		{
			return "the works of " + Name + " no longer carried the people in it, and it began to settle back toward "
				+ Level + ", " + HeldBy(Binding);
		}

		/// <summary>The unsaying: the block has lifted, so 7b's flag clears and says why.</summary>
		public static string ArrestedNote(string Name, int Level, int Population)
		{
			return (Population < Level)
				? (Name + " has stopped settling. The works carry " + Level + ", and there are " + Population + " here.")
				: (Name + " has settled. The works carry " + Level + ", and that is what stands here now.");
		}

		/// <summary>The chronicle's own telling of an arrest.</summary>
		public static string ArrestedChronicle(string Name, int Level)
		{
			return Name + " stopped settling back, and stood at the " + Level + " its works honestly carry";
		}

		// --- The chronicle budget ----------------------------------------------------------
		//
		// The register keeps two hundred entries and trims the oldest. A City standing at fifty
		// people sliding to Camp's own floor of four is forty-six departures, four rungs, and up
		// to two ruined works a rung: fifty-eight lines, better than a quarter of the whole
		// record, for one event. A founder who came home to that would find the settlement's
		// entire memory replaced by the story of losing it.
		//
		// So a long slide is TOLD IN RUNGS, which is what BreakpointChronicle's own doc comment
		// already claimed it was: the rungs are always all told, first and last included, because
		// there are at most four of them and each is the place becoming a different place. What
		// is sampled is the departures underneath them - the first few by name, the last by name
		// because the last one out is the one the founder remembers, and everybody in between
		// counted in one line. Nobody vanishes from the count; what they lose is a chronicle
		// entry each, and what they were owed was the truth about the slide, which the rungs and
		// the summary tell between them.

		/// <summary>
		/// Departures of one slide that get a chronicle entry to themselves. Three: enough that a
		/// short slide reads exactly as it always did (a handful of people leaving IS the story
		/// at that size), few enough that a collapse spends three lines and not fifty.
		/// </summary>
		public const int NamedDeparturesPerSlide = 3;

		/// <summary>
		/// Whether the <paramref name="Index"/>-th departure of a slide is chronicled by name.
		/// Keeps the FIRST and the LAST always: the first is when it started going, the last is
		/// who turned the lights off, and a sample that dropped either would be a worse record
		/// than a shorter one.
		/// </summary>
		/// <param name="Index">Which departure, from zero.</param>
		/// <param name="Departed">How many are going in this slide.</param>
		public static bool TellsDeparture(int Index, int Departed)
		{
			if (Index < 0 || Departed <= 0 || Index >= Departed)
			{
				return false;
			}
			if (Departed <= NamedDeparturesPerSlide)
			{
				return true;
			}
			return Index < NamedDeparturesPerSlide - 1 || Index == Departed - 1;
		}

		/// <summary>How many of a slide's departures are chronicled by name. Never more than
		/// <see cref="NamedDeparturesPerSlide"/>, and never more than went.</summary>
		public static int NamedDepartures(int Departed)
		{
			if (Departed <= 0)
			{
				return 0;
			}
			return (Departed < NamedDeparturesPerSlide) ? Departed : NamedDeparturesPerSlide;
		}

		/// <summary>
		/// The one line that carries everybody the sample did not name. Null when the sample
		/// named them all, which is the caller's signal to say nothing.
		/// <para>
		/// Takes the named count rather than deriving it, because a slide the settlement cut
		/// short &mdash; people standing in another claimed zone, the loyal core refusing to go
		/// &mdash; loses fewer than the trajectory called for and may name fewer than the sample
		/// planned. The summary counts what actually happened, so the two numbers always add up
		/// to the departures the ledger recorded.
		/// </para>
		/// </summary>
		/// <param name="Name">The settlement's display name.</param>
		/// <param name="Departed">How many actually went.</param>
		/// <param name="Named">How many of those were chronicled by name.</param>
		/// <param name="Cause">The departure cause, from <see cref="DepartureCause"/>, so the
		/// summary blames exactly what the named departures blamed.</param>
		public static string SlideDepartureSummary(string Name, int Departed, int Named, string Cause)
		{
			int unnamed = Departed - ((Named > 0) ? Named : 0);
			if (unnamed <= 0)
			{
				return null;
			}
			string who = (unnamed == 1) ? "one more" : (unnamed + " more");
			return who + " went from " + Name + " over the same days, " + Cause;
		}

		// --- The ruins of one rung, told the same way --------------------------------------
		//
		// Addendum 10(c) broadened the DAMAGE and must not broaden the TELLING with it: a rung
		// that leaves eleven works the worse for it is eleven real ruins and two chronicle
		// entries. So the ruins of a rung are sampled exactly as its departures are - a couple
		// named, everybody else carried by one line that counts them and names how bad the worst
		// of them got. Nobody vanishes from the count; what they lose is an entry each.

		/// <summary>Ruined works of one rung that get a chronicle entry to themselves. One: a
		/// rung already spends an entry on itself, and the sentence that matters after it is how
		/// many went and how far, not which shed was third.</summary>
		public const int NamedRuinsPerBreakpoint = 1;

		/// <summary>Whether the <paramref name="Index"/>-th work this rung ruined is chronicled
		/// by name. The first ones, in the order the pass found them.</summary>
		/// <param name="Index">Which ruined work, from zero.</param>
		public static bool TellsRuin(int Index)
		{
			return Index >= 0 && Index < NamedRuinsPerBreakpoint;
		}

		/// <summary>The line one named ruined work gets, in the ledger and the register alike.
		/// </summary>
		/// <param name="WorkName">The work, as the founder refers to it.</param>
		/// <param name="Name">The settlement's display name.</param>
		public static string RuinedWorkLine(string WorkName, string Name)
		{
			string work = string.IsNullOrEmpty(WorkName) ? "a work" : WorkName;
			return work + " fell into disrepair as " + Name + " settled back, with nobody left who kept it";
		}

		/// <summary>
		/// The one line carrying every work of this rung the sample did not name. Null when the
		/// sample named them all, which is the caller's signal to say nothing.
		/// </summary>
		/// <param name="Name">The settlement's display name.</param>
		/// <param name="Ruined">Works this rung actually left the worse for it.</param>
		/// <param name="Named">How many of those were chronicled by name.</param>
		/// <param name="DeepestWear">The worst wear any of them was left standing at, for
		/// <c>KingdomMaterialRules.ConditionWord</c> to put a stage on.</param>
		public static string RuinSummary(string Name, int Ruined, int Named, int DeepestWear)
		{
			int unnamed = Ruined - ((Named > 0) ? Named : 0);
			if (unnamed <= 0)
			{
				return null;
			}
			string how = (unnamed == 1) ? "one more work" : (unnamed + " more works");
			return how + " at " + Name + " went the same way over those days, the worst of them "
				+ KingdomMaterialRules.ConditionWord(DeepestWear);
		}

		/// <summary>
		/// How many chronicle entries one slide writes, so a test can hold the budget rather than
		/// trusting the arithmetic to stay small. Named departures, the summary line if there is
		/// one, one entry per rung, and per rung its
		/// <see cref="NamedRuinsPerBreakpoint"/> named ruins plus the one line that carries the
		/// rest of them.
		/// <para>
		/// Note what is NOT in here: how many works a rung actually ruined. That is the point of
		/// the coarsening &mdash; Addendum 10(c) let a rung reach the whole settlement, and the
		/// register's share of a collapse did not move a line for it.
		/// </para>
		/// </summary>
		/// <param name="Departed">People the slide took.</param>
		/// <param name="Rungs">Rungs it fell, which is what <c>Trajectory.Breakpoints</c>
		/// holds.</param>
		public static int ChronicleEntriesFor(int Departed, int Rungs)
		{
			int named = NamedDepartures(Departed);
			int summary = (Departed > named) ? 1 : 0;
			int rungs = (Rungs > 0) ? Rungs : 0;
			return named + summary + rungs + rungs * (NamedRuinsPerBreakpoint + 1);
		}

		/// <summary>
		/// The ceiling <see cref="ChronicleEntriesFor"/> promises for any slide this build can
		/// produce, including a City falling all the way to Camp. Stated as a number rather than
		/// read off <c>KingdomChronicle.MaxEntries</c> because that constant is the chronicle's
		/// own and this file must fold to fit it, not reach into it: the register keeps two
		/// hundred, and one collapse may not spend more than a tenth of them.
		/// </summary>
		public const int ChronicleBudgetPerSlide = 20;

		/// <summary>One rung, dated against the day the founder is being told about it. This is
		/// the sample the chronicle keeps: the slide itself is a hundred small departures, and
		/// what is worth writing down is the four times the place stopped being one thing.
		/// </summary>
		/// <param name="Name">The settlement's display name.</param>
		/// <param name="From">What it was.</param>
		/// <param name="To">What it became.</param>
		/// <param name="DaysAgo">Days before now that this happened. Zero and below read as
		/// today, which is what a slide that finished this morning is.</param>
		public static string BreakpointChronicle(string Name, GrowthStage From, GrowthStage To, int DaysAgo)
		{
			string when = (DaysAgo <= 0)
				? "today"
				: ((DaysAgo == 1) ? "a day before you saw it" : (DaysAgo + " days before you saw it"));
			return Name + " was a " + From.ToString().ToLowerInvariant() + " and became a "
				+ To.ToString().ToLowerInvariant() + ", " + when;
		}

		private static string HeldBy(string Binding)
		{
			switch (NormalizedBinding(Binding))
			{
			case KingdomCatalogueRules.SupportWater:
				return "and it is the water that holds it there";
			case KingdomCatalogueRules.SupportFood:
				return "and it is the harvest that holds it there";
			case KingdomCatalogueRules.SupportRoof:
				return "and there are only so many roofs";
			default:
				return "and that is what its works carry";
			}
		}
	}
}
