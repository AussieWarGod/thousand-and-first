using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPlots
	{
		/// <summary>
		/// Stands movable bodies off the ground an improvement is about to block, using exactly the
		/// plot clearance: same classification ladder, same destination rules, same plan before
		/// effect, same walk-back, same sentences. Newly blocked cells include renovation of
		/// formerly walkable or adjacent-use ground inside the standing lot. Existing walls and
		/// unmapped predecessor cells gain no movement authority; open paths remain usable.
		/// </summary>
		/// <param name="Before">The rect the work already stands on.</param>
		/// <param name="Moved">Bodies left standing off the newly blocked ground.</param>
		/// <param name="Beasts">How many of them were driven rather than stood aside.</param>
		/// <param name="Post">Where a moved post was set down, or null.</param>
		internal static bool TryClearEnvelopeOccupants(KingdomSystem System, Zone Z,
			GameObject Owner, KingdomArchitectureIntent Successor,
			KingdomPlotRules.PlotRect Before, out int Moved, out int Beasts, out Cell Post,
			out string Refusal)
		{
			Moved = 0;
			Beasts = 0;
			Post = null;
			Refusal = null;
			if (System == null || Z == null || Successor == null
				|| !GameObject.Validate(Owner))
				return ClearanceFault("no successor layout to clear for", out Refusal);
			if (!KingdomArchitectureRuntime.TryRead(Owner, out var before, out Refusal)
				|| before.Rect.X1 != Before.X1 || before.Rect.Y1 != Before.Y1
				|| before.Rect.X2 != Before.X2 || before.Rect.Y2 != Before.Y2)
				return ClearanceFault("the standing layout no longer matches its clearance boundary", out Refusal);
			if (!KingdomArchitectureStamper.TryNewBlockingCells(Z, before, Successor,
				out Dictionary<int, ArchitecturePassability> newlyBlocked, out Refusal)) return false;
			if (newlyBlocked.Count == 0) return true;
			// The whole successor rect is excluded as a destination by Rect, so nobody is stood on
			// ground the improvement is about to take.
			return TryClearManagedOccupants(System, Z, Owner, new HashSet<int>(newlyBlocked.Keys),
				newlyBlocked, Successor.Rect, out Moved, out Beasts, out Post,
				out KingdomPlotRules.OccupantVerdict ignoredVerdict, out Cell ignoredAnchor,
				out Refusal);
		}

		/// <summary>
		/// Names what the crew did to the newly blocked ground, in the raising's own words and with the
		/// improvement's real outcome: the ground is cleared before the transition, the reserve and
		/// the funding have had their say, so "the work goes on" is only true once they have.
		/// <para>The once-flags are the plot route's properties, but they are set on the WORK being
		/// improved, not on a plot-works root, so the two routes never share a flag even when both
		/// run in one pass.</para>
		/// </summary>
		/// <param name="Raised">Whether the improvement actually began.</param>
		/// <param name="Fault">Why it did not, when it did not.</param>
		internal static void SayEnvelopeCleared(KingdomSystem System, GameObject Owner,
			string Name, int Moved, int Beasts, Cell Post, bool Raised, string Fault)
		{
			SayPlotWorkCleared(System, Owner, Name,
				KingdomPlotRules.SettlersMoved(Moved, Beasts), Raised, Fault);
			SayPlotBeastsDriven(System, Owner, Name, Beasts, Raised, Fault);
			if (Post != null) SayPlotPostMoved(System, Owner, Name, Post);
		}

		/// <summary>
		/// Whether a body standing on ground an improvement wants is one the crew may move. Used by
		/// the envelope proof so the non-mutating preflight does not refuse ground the mutating
		/// path is about to clear.
		/// </summary>
		internal static bool IsMovableEnvelopeOccupant(KingdomSystem System, Zone Z,
			GameObject Body)
		{
			KingdomSurvey survey = EnvelopeSurvey(System, Z);
			if (System == null || survey == null || !GameObject.Validate(Body)) return false;
			return KingdomPlotRules.IsMovableOccupant(ReasonFor(System, survey, Body));
		}

		/// <summary>
		/// The ground reading this classification stands on: the pass's BOUND survey, never a
		/// fresh one. KingdomSurvey.Take is not a read -- it stamps legacy furnishing properties,
		/// observes legacy state, can put a message in front of the player and moves its own reuse
		/// counters -- and this runs once per occupant, from a menu preview among other places.
		/// Null means exactly one thing: no settlement pass is bound for this zone. That is what
		/// "unwitnessed" says, and it is the true answer rather than one bought with side effects.
		/// </summary>
		private static KingdomSurvey EnvelopeSurvey(KingdomSystem System, Zone Z)
		{
			return System == null || Z == null ? null : KingdomSurvey.ActiveFor(Z);
		}

		/// <summary>One line naming a body that stands on ground an improvement wants.</summary>
		internal static void NameEnvelopeOccupant(KingdomSystem System, Zone Z, GameObject Body,
			ArchitecturePassability Passability)
		{
			KingdomSurvey survey = EnvelopeSurvey(System, Z);
			Cell at = GameObject.Validate(Body) ? Body.CurrentCell : null;
			KingdomLog.Log(KingdomPlotRules.OccupantLine(at == null ? null : Body.IDIfAssigned,
				at == null ? null : Body.Blueprint,
				at == null ? null : at.X + "," + at.Y, Passability,
				survey == null ? null : ReasonFor(System, survey, Body).ToString()));
		}
	}
}
