using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomWear
	{
		/// <summary>Consecutive full-stretch attended passes a work carries right now. A plain
		/// property rather than a part field: every crewed work implicitly carries this at zero,
		/// the same way it implicitly carries <c>KingdomEffectiveness</c> at zero, and giving a
		/// sound work a whole part just to hold one counter would mean every crewed building in
		/// the game grows one.</summary>
		public const string HardRunStreakProperty = "KingdomHardRunStreak";
		public const string SemanticPassTickProperty = "KingdomWearPassTick";
		public const string SemanticPassCompletedTickProperty = "KingdomWearLastPassTick";
		public const string SemanticPassCompletedProperty = "KingdomWearLastPassSet";
		public const string SemanticPassPhaseProperty = "KingdomWearPassPhase";
		public const string SemanticPassOriginalStreakProperty = "KingdomWearPassOriginalStreak";
		public const string SemanticPassTargetStreakProperty = "KingdomWearPassTargetStreak";
		public const string SemanticPassHardRollProperty = "KingdomWearPassHardRoll";
		public const string SemanticPassTemperRollProperty = "KingdomWearPassTemperRoll";
		public const string LastRaidIncidentTickProperty = "KingdomWearLastRaidTick";

		/// <summary>Tick a mending under way last had labour charged against it. Read and written
		/// through <c>KingdomMaterials.ReadTick</c>/<c>WriteTick</c>, exactly as
		/// <c>KingdomMaterials.StrikeWorkedProperty</c> is: the same "day since this was last
		/// worked" accounting a strike already uses, so a founder cannot speed a mending by
		/// stepping in and out of the zone, and a long absence still resolves honestly.</summary>
		public const string RepairWorkedProperty = "KingdomRepairWorked";
		public const string DisabledAnchorProperty = "KingdomWearDisabledAnchor";
		public const string RepairRemovalAttemptProperty = "KingdomWearRemovalAttempt";
		public const string RepairRemovalProofProperty = "KingdomWearRemovalProof";

		/// <summary>
		/// The property <c>KingdomGrowth.AssignWork</c> stamps a work's crew-only effectiveness
		/// onto, 0-100. Read here to learn this pass's crew stretch, and never written: this file
		/// used to fold the work's own condition back into it, which made the property mean two
		/// different things at two different points in the same pass and quietly double-counted
		/// wear for anything that read it before the next staffing pass. It is now exactly one
		/// thing everywhere &mdash; what the CREW manages &mdash; and every consumer folds
		/// condition in for itself through <see cref="KingdomWearRules.WorkEffectiveness"/>.
		/// </summary>
		private const string EffectivenessProperty = "KingdomEffectiveness";

		/// <summary>The design's declared crew demand, as the staffing pass stamps it. Zero means
		/// the work asks for nobody, which after Addendum 10(b) no longer means it is immune to
		/// its own damage.</summary>
		private const string StaffNeededProperty = "KingdomStaffNeeded";

		/// <summary>The founder's mark on a vessel dedicated to the settlement's water. A store
		/// carrying it is a work whose CONTENTS can run out of a hole in it.</summary>
		private const string StoresProperty = "KingdomStores";

		/// <summary>The food side of <see cref="StoresProperty"/>: what marks a container the
		/// settlement keeps its physical ingredients in. Wear may reduce the work's effectiveness,
		/// but never authorizes unattended loss of those ingredients.</summary>
		private const string LarderProperty = "KingdomLarder";

		/// <summary>
		/// One work's own wear, 0 when it carries no record at all. The single reader every
		/// consumer of <see cref="KingdomWearRules.WorkEffectiveness"/> goes through, so "absent
		/// means sound" is stated once rather than re-derived at four call sites.
		/// </summary>
		/// <param name="Work">Any object. Null and unvalidated read as sound.</param>
		public static int WearOf(GameObject Work)
		{
			if (!GameObject.Validate(Work))
			{
				return 0;
			}
			r_KingdomWear wear = Work.GetPart<r_KingdomWear>();
			return (wear != null && wear.Wear > 0) ? wear.Wear : 0;
		}

		/// <summary>
		/// What one finished work is worth to the settlement this pass, crewed or not: the
		/// staffing pass's own stretch for a work that asks for crew, its bare condition for one
		/// that does not, and 100 for a sound work either way (Addendum 10(b)).
		/// </summary>
		/// <param name="Work">A finished work. Null reads as carrying nothing.</param>
		public static int EffectivenessOf(GameObject Work)
		{
			if (!GameObject.Validate(Work))
			{
				return 0;
			}
			int crewAndCondition = KingdomWearRules.WorkEffectiveness(
				Work.GetIntProperty(StaffNeededProperty), Work.GetIntProperty(EffectivenessProperty), WearOf(Work));
			return KingdomCrews.ApplyAffinity(Work, crewAndCondition);
		}

		public static void OnZoneActivated(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (System == null || !System.Founded || Z == null || Survey == null
				|| !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			long now = The.Game.TimeTicks;
			if (!Enabled)
			{
				AnchorDisabledClocks(System, Z, Survey, now);
				return;
			}
			if (AnchorReenabledClocks(System, Z, Survey, now)) return;
			Resolve(System, Z, Survey);
		}

		private static void AnchorDisabledClocks(KingdomSystem System, Zone Z,
			KingdomSurvey Survey, long Now)
		{
			for (int i = 0; i < Survey.Built.Count; i++)
			{
				GameObject work = Survey.Built[i];
				if (!GameObject.Validate(work) || work.CurrentZone != Z) continue;
				ResolveSafeReceipts(System, Survey, work);
				work.SetIntProperty(DisabledAnchorProperty, 1);
				r_KingdomWear wear = work.GetPart<r_KingdomWear>();
				if (wear != null)
				{
					wear.LastLeakTick = Now;
					wear.LeakClockInitialized = true;
				}
				if ((wear != null && wear.RepairEffortLeft > 0)
					|| KingdomConstruction.ReceiptBlocksCurrent(work))
				{
					KingdomMaterials.WriteTick(work, RepairWorkedProperty, Now);
				}
			}
		}

		private static bool AnchorReenabledClocks(KingdomSystem System, Zone Z,
			KingdomSurvey Survey, long Now)
		{
			bool anchored = false;
			for (int i = 0; i < Survey.Built.Count; i++)
			{
				GameObject work = Survey.Built[i];
				if (!GameObject.Validate(work) || work.CurrentZone != Z
					|| work.GetIntProperty(DisabledAnchorProperty) != 1) continue;
				anchored = true;
				ResolveSafeReceipts(System, Survey, work);
				work.SetIntProperty(DisabledAnchorProperty, 0);
				r_KingdomWear wear = work.GetPart<r_KingdomWear>();
				if (wear != null)
				{
					wear.LastLeakTick = Now;
					wear.LeakClockInitialized = true;
				}
				KingdomMaterials.WriteTick(work, RepairWorkedProperty, Now);
				KingdomMaterials.WriteTick(work, SemanticPassCompletedTickProperty, Now);
				work.SetIntProperty(SemanticPassCompletedProperty, 1);
				work.SetIntProperty(SemanticPassPhaseProperty, (int)KingdomWearPassPhase.None);
				KingdomMaterials.WriteTick(work, SemanticPassTickProperty, 0L);
			}
			return anchored;
		}

		private static void ResolveSafeReceipts(KingdomSystem System, KingdomSurvey Survey,
			GameObject Work)
		{
			r_KingdomWear wear = Work.GetPart<r_KingdomWear>();
			if (wear == null) return;
			KingdomWearIncidentPhase incident = (KingdomWearIncidentPhase)wear.IncidentPhase;
			if (incident > KingdomWearIncidentPhase.None
				&& incident < KingdomWearIncidentPhase.Complete)
			{
				ApplyDamageIncident(System, Work, (KingdomWearRules.WearCause)wear.IncidentCause,
					wear.IncidentId);
			}
			KingdomWearLeakPhase leak = (KingdomWearLeakPhase)wear.LeakPhase;
			if (leak == KingdomWearLeakPhase.MutationIntent
				|| (leak >= KingdomWearLeakPhase.Mutated && leak <= KingdomWearLeakPhase.Complete))
			{
				ContinueBoundLeak(System, Survey, Work, wear);
			}
		}

	}
}
