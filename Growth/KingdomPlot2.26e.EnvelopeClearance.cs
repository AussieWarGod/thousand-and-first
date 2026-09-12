using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPlots
	{
		/// <summary>
		/// Stands movable bodies off the ground an improvement is about to annex, using exactly the
		/// plot clearance: same classification ladder, same destination rules, same plan before
		/// effect, same walk-back, same sentences. Only the ANNEXED cells are scanned -- a body
		/// inside the standing footprint is not new ground being taken -- and only those whose
		/// successor slot is declared Blocked, so the founder standing on what will still be a
		/// path or a yard is not in anybody's way.
		/// </summary>
		/// <param name="Before">The rect the work already stands on.</param>
		/// <param name="Moved">Bodies left standing off the annexed ground.</param>
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
			if (!KingdomArchitectureStamper.TryPlacementPassability(Successor, Z,
				out Dictionary<int, ArchitecturePassability> slots, out Refusal)) return false;
			Dictionary<int, ArchitecturePassability> annexed =
				new Dictionary<int, ArchitecturePassability>();
			foreach (KeyValuePair<int, ArchitecturePassability> slot in slots)
			{
				if (!KingdomPlotRules.SlotBlocksOccupant(slot.Value)) continue;
				if (Before.Contains(slot.Key % Z.Width, slot.Key / Z.Width)) continue;
				annexed[slot.Key] = slot.Value;
			}
			if (annexed.Count == 0) return true;
			// The whole successor rect is excluded as a destination by Rect, so nobody is stood on
			// ground the improvement is about to take.
			return TryClearManagedOccupants(System, Z, Owner, new HashSet<int>(annexed.Keys),
				annexed, Successor.Rect, out Moved, out Beasts, out Post,
				out KingdomPlotRules.OccupantVerdict ignoredVerdict, out Cell ignoredAnchor,
				out Refusal);
		}

		/// <summary>
		/// Names what the crew did to the annexed ground, in the raising's own words and with the
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
		/// The ground reading this classification stands on. KingdomSurvey.Take reuses the pass's
		/// bound survey when there is one and captures a fresh one when there is not, so a
		/// menu-time preview or a harness proof outside a settlement pass names the real reason
		/// instead of "unwitnessed" -- which is then left for the one case that means it: no zone,
		/// no system, or a survey that could not be taken at all.
		/// </summary>
		private static KingdomSurvey EnvelopeSurvey(KingdomSystem System, Zone Z)
		{
			return System == null || Z == null ? null : KingdomSurvey.Take(Z, System);
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
