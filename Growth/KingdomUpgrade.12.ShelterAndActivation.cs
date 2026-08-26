using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomUpgrade
	{
		public const string HousingCategory = "housing";

		/// <summary>The lifting support the luxury lane is denominated in. A design that lifts it
		/// houses somebody with a standard; one that does not houses settlers.</summary>
		public const string LuxurySupport = "luxury";

		/// <summary>
		/// Shelter rank of a design's own tier. A design that is not a plot has no roof state of
		/// its own and is read as a walled room, because a single-cell work the settlement raised
		/// stands as an object with its own walls rather than as open ground.
		/// </summary>
		/// <param name="Key">Registry key of the design.</param>
		public static int ShelterOf(string Key)
		{
			KingdomPlotRules.PlotSpec spec;
			if (string.IsNullOrEmpty(Key) || !KingdomPlots.TryGetSpec(Key, out spec) || spec == null)
			{
				return KingdomUpgradeRules.RoomShelter;
			}
			return KingdomPlotRules.ShelterRank(spec.Roof);
		}

		/// <summary>
		/// Whether the settlement's craft and learning reach a design. The district and territory
		/// gates are deliberately NOT applied: the predecessor is already standing on this ground,
		/// so re-asking where it may stand would refuse improvements the founder sited legitimately
		/// and could no longer do anything about.
		/// </summary>
		public static bool CraftReaches(KingdomSystem System, Zone Z, string Key)
		{
			KingdomRules.BuildEntry entry;
			if (System == null || string.IsNullOrEmpty(Key) || !KingdomData.TryGetBuilding(Key, out entry))
			{
				return true;
			}
			ZoningJudgement judgement = KingdomZoning.Judge(System, Z?.ZoneID, entry);
			return judgement.Verdict != ZoningVerdict.RefusedUnlearned
				&& judgement.Verdict != ZoningVerdict.RefusedTechLevel;
		}

		/// <summary>
		/// The settlement's improvement pass: completes any handover that finished while the
		/// founder was away, then starts at most one improvement and says at most one thing.
		/// Called from the settlement's zone-activated pass after growth, because growth is what
		/// decides which settlers are already spoken for.
		/// </summary>
		/// <param name="System">The kingdom.</param>
		/// <param name="Z">Zone the founder is standing in.</param>
		/// <param name="Survey">This pass's survey.</param>
		public static void OnZoneActivated(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (!Enabled || System == null || !System.Founded || Z == null || Survey == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			// HandOver asks for one more pass so a founder who stands and watches sees the next
			// work start. That call must not re-enter this one: the settlement betters one work
			// per visit, and a pass that started an improvement inside its own handover would
			// start as many as there were works to hand over.
			if (_resolving)
			{
				return;
			}
			_resolving = true;
			try
			{
				Resolve(System, Z, Survey);
			}
			finally
			{
				_resolving = false;
			}
		}

		// True while OnZoneActivated is inside its own pass. Not serialized and not state: it
		// describes the call stack, not the settlement.
	}
}
