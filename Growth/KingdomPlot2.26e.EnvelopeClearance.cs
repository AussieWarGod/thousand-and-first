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

		/// <summary>Names what the crew did to the annexed ground, in the raising's own words.</summary>
		internal static void SayEnvelopeCleared(KingdomSystem System, GameObject Owner,
			string Name, int Moved, int Beasts, Cell Post)
		{
			SayPlotWorkCleared(System, Owner, Name, Moved - Beasts, true, null);
			SayPlotBeastsDriven(System, Owner, Name, Beasts, true, null);
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
			KingdomSurvey survey = Z == null ? null : KingdomSurvey.ActiveFor(Z);
			if (System == null || survey == null || !GameObject.Validate(Body)) return false;
			return KingdomPlotRules.IsMovableOccupant(ReasonFor(System, survey, Body));
		}

		/// <summary>One line naming a body that stands on ground an improvement wants.</summary>
		internal static void NameEnvelopeOccupant(KingdomSystem System, Zone Z, GameObject Body,
			ArchitecturePassability Passability)
		{
			KingdomSurvey survey = Z == null ? null : KingdomSurvey.ActiveFor(Z);
			Cell at = GameObject.Validate(Body) ? Body.CurrentCell : null;
			KingdomLog.Log("architecture: envelope occupant "
				+ (at == null ? "unknown" : Body.IDIfAssigned) + " ("
				+ (at == null ? "gone" : Body.Blueprint) + ") at "
				+ (at == null ? "nowhere" : at.X + "," + at.Y)
				+ " passability=" + Passability + " reason="
				+ (System == null || survey == null ? "unwitnessed"
					: ReasonFor(System, survey, Body).ToString()));
		}
	}
}
