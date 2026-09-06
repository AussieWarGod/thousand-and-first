using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	/// <summary>The actual completed founding heart this rung may legitimately select as a second
	/// ruin work, bound once from live state: its canonical final object, its production identity,
	/// and what production's own ruin draw says this breakpoint does with it.</summary>
	internal sealed class KingdomSubsidenceRungHeart
	{
		internal readonly GameObject Final;
		internal readonly string Id;
		internal readonly bool Selected;
		internal readonly int AfterWear;

		/// <summary>Production's own terminal authority for this heart, frozen whole at the moment it
		/// was bound: the exact zone-property wire and the pose it decodes to. A later stage proves the
		/// same authority by comparing these, so no second codec and no parallel model is needed.</summary>
		internal readonly string TerminalWire;
		internal readonly KingdomFoundingHeartTerminalPlan Terminal;

		internal KingdomSubsidenceRungHeart(GameObject final, string id, bool selected, int afterWear,
			string terminalWire, KingdomFoundingHeartTerminalPlan terminal)
		{
			Final = final; Id = id; Selected = selected; AfterWear = afterWear;
			TerminalWire = terminalWire; Terminal = terminal;
		}
	}

	/// <summary>Exact proofs about that heart, shared by the warm save side and the cold witness: it
	/// carries no wear at all while it is fresh, production's own RollRuin decides whether it is in
	/// this rung's work set, the fixed primary index 0 is ordinally earned rather than assumed, and a
	/// selected companion stands untouched at the cut. Nothing here removes, mends, re-rolls or
	/// reselects anything: every value is read from live state or produced by production's own rules.</summary>
	internal static class KingdomSubsidenceRungSaveHeartProof
	{
		internal const string PrePass = "pre-pass";
		internal const string BeforeSave = "before-save";
		internal const string AfterSave = "after-save";
		internal const string ColdPreactivation = "cold-preactivation";
		internal const string ColdRecovery = "cold-recovery";

		/// <summary>What one selected companion's release costs in admitted writes: fields 0, 1 and 2.
		/// A fresh heart's part carries no incident line, so production's own fourth field is already
		/// at its target and is never written (KingdomSubsidenceReleaseRules.TryNextWrite).</summary>
		internal const int CompanionWrites = 3;
		internal const string CompanionFields = "0,1,2";

		private const string WearPresent =
			"the completed founding heart already carries subsidence wear; retained, never erased, at ";
		private const string ShapeFault =
			"the frozen rung plan is not production's own expected work set at primary index 0"
			+ " (the expected work count is code-derived from RollRuin, never observed in a native log)";
		private const string CompanionFault =
			"the selected heart companion is not an untouched, unworn, roofless prepared row at index 1";
		private const string CompanionGroundFault =
			"the selected heart companion row's blueprint, plot, build key or cell is not the live"
			+ " completed heart's own typed designation and pose";
		private const string TerminalFault =
			"the completed heart's exact final object, terminal authority bytes or decoded pose"
			+ " changed at ";

		/// <summary>The heart's exact live final object, resolved through production's own canonical
		/// terminal binding rather than any harness-held reference, with its typed designation and
		/// ground re-proved against that binding.</summary>
		internal static GameObject Final(Zone Ground, out string HeartId, out string TerminalWire,
			out KingdomFoundingHeartTerminalPlan Terminal)
		{
			HeartId = null; TerminalWire = null; Terminal = null;
			Check(Ground != null, "the completed-heart proof has no ground");
			string wire = Ground.GetZoneProperty(KingdomPlots.FoundingHeartTerminalProperty, null);
			Check(KingdomFoundingHeartTerminalRules.TryDecode(wire,
					out KingdomFoundingHeartTerminalPlan terminal)
				&& KingdomFoundingHeartTerminalRules.Encode(terminal) == wire
				&& terminal.Phase == KingdomFoundingHeartTerminalPhase.EffectsSettled
				&& terminal.Raising == KingdomFoundingHeartSinkDisposition.Settled
				&& terminal.Heart == KingdomFoundingHeartSinkDisposition.Settled
				&& !string.IsNullOrEmpty(terminal.FinalId),
				"the ground carries no settled canonical completed-heart terminal binding");
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal) { terminal.FinalId };
			GameObject heart = null;
			Check(KingdomPlots.TryCaptureGlobalLiveIds(ids, out Dictionary<string, GameObject> live)
				&& live.Count == 1 && live.TryGetValue(terminal.FinalId, out heart)
				&& GameObject.Validate(heart) && heart.IDIfAssigned == terminal.FinalId,
				"the completed heart's canonical final identity is not one exact live object");
			KingdomSubsidenceRungSaveLiveProof.ProveHeartGround(heart, Ground, terminal.Blueprint,
				terminal.BuildKey, terminal.PlotId, terminal.X, terminal.Y,
				"the completed heart's typed designation, ground or terminal binding is not exact");
			Check(r_KingdomScaffold.HasRemovalProof(heart, terminal.PredecessorId),
				"the completed heart lost its typed predecessor removal proof");
			HeartId = terminal.FinalId; TerminalWire = wire; Terminal = terminal;
			return heart;
		}

		/// <summary>The heart, and production's own answer for this breakpoint's ordinal.</summary>
		internal static KingdomSubsidenceRungHeart Bind(Zone Ground, string SettlementId,
			long DueTick, GrowthStage From)
		{
			GameObject heart = Final(Ground, out string id, out string wire,
				out KingdomFoundingHeartTerminalPlan terminal);
			bool selected = ExpectedWorkSet(SettlementId, id, DueTick, From, out int afterWear);
			return new KingdomSubsidenceRungHeart(heart, id, selected, afterWear, wire, terminal);
		}

		/// <summary>The heart carries no wear part and no wear value at all. Called before the pass,
		/// before and after the real save, and at cold pre-activation. A heart that already carries
		/// wear REFUSES here; it is never mended, cleared or removed to make the witness pass.</summary>
		internal static void ProveFreshHeartAbsent(GameObject Heart, string Stage)
		{
			Check(GameObject.Validate(Heart), "the completed heart is not a live object at " + Stage);
			Check(Heart.GetPart<r_KingdomWear>() == null
				&& KingdomSubsidenceRungSaveLiveProof.WearCopies(Heart) == 0
				&& KingdomWear.WearOf(Heart) == 0, WearPresent + Stage);
		}

		/// <summary>Re-proves the same exact final object and its whole terminal authority from the
		/// canonical binding, then its unworn state, at a later stage of the same witness.</summary>
		internal static void ProveHeartStill(KingdomSubsidenceRungHeart Heart, Zone Ground, string Stage)
		{
			ProveTerminalExact(Heart, Ground, Stage);
			ProveFreshHeartAbsent(Heart.Final, Stage);
		}

		/// <summary>The heart's final terminal authority stands EXACT at a later stage: the same live
		/// final object and identity, production's own terminal wire byte-for-byte as it was bound, and
		/// the same decoded pose. Held through the cold RECOVERY as well as pre-activation, so a
		/// release that moved or re-designated the final could not pass unseen. No new codec is
		/// introduced: the wire is production's own zone property, which the frozen RungWire and the
		/// saved v2 authority baseline already cover, and Final() re-proves the live pose against it.</summary>
		internal static void ProveTerminalExact(KingdomSubsidenceRungHeart Heart, Zone Ground, string Stage)
		{
			Check(Heart != null && Heart.Terminal != null && !string.IsNullOrEmpty(Heart.TerminalWire),
				"the completed-heart binding carries no frozen terminal authority at " + Stage);
			GameObject observed = Final(Ground, out string id, out string wire,
				out KingdomFoundingHeartTerminalPlan terminal);
			Check(ReferenceEquals(observed, Heart.Final) && id == Heart.Id
				&& wire == Heart.TerminalWire && terminal.Blueprint == Heart.Terminal.Blueprint
				&& terminal.BuildKey == Heart.Terminal.BuildKey
				&& terminal.PlotId == Heart.Terminal.PlotId
				&& terminal.X == Heart.Terminal.X && terminal.Y == Heart.Terminal.Y
				&& terminal.PredecessorId == Heart.Terminal.PredecessorId
				&& terminal.CompletionSeal == Heart.Terminal.CompletionSeal
				&& terminal.TransactionId == Heart.Terminal.TransactionId
				&& terminal.ZoneId == Heart.Terminal.ZoneId, TerminalFault + Stage);
		}

		/// <summary>Whether production's own ruin draw takes this heart at this breakpoint, and the
		/// wear it would add. Both answers come from the production rules themselves over the heart's
		/// real IDIfAssigned; nothing here models, caches or second-guesses the draw.</summary>
		internal static bool ExpectedWorkSet(string SettlementId, string HeartId, long DueTick,
			GrowthStage From, out int HeartAfterWear)
		{
			Check(!string.IsNullOrEmpty(SettlementId) && !string.IsNullOrEmpty(HeartId) && DueTick >= 0L,
				"the expected ruin set has no settlement, heart identity or breakpoint ordinal");
			bool selected = KingdomSubsidenceRules.RollRuin(SettlementId, HeartId, (ulong)DueTick, From);
			HeartAfterWear = selected ? KingdomMaterialRules.AddWear(0,
				KingdomSubsidenceRules.RolledRuinIncrement(SettlementId, HeartId, (ulong)DueTick)) : 0;
			return selected;
		}

		/// <summary>Earns the fixed primary index 0 before anything uses it, exactly as production's
		/// own ordinal sort of the work rows decides it.</summary>
		internal static void ProveOrdinal(string PrimaryId, string HeartId)
		{
			Check(!string.IsNullOrEmpty(PrimaryId) && !string.IsNullOrEmpty(HeartId),
				"the ordinal proof has no primary or heart identity");
			Check(string.CompareOrdinal(PrimaryId, HeartId) < 0,
				"the fixed primary index 0 is unearned: ordinal '" + PrimaryId[0]
					+ "' does not sort before '" + HeartId[0] + "'");
		}

		/// <summary>The frozen plan is exactly the set production's draw predicted: the primary at
		/// index 0, and either the untouched selected companion at index 1 or no heart row at all.
		/// The set, index and companion state are validated by root's pure shape helper; the count
		/// itself is proved against the re-drawn expectation here.</summary>
		internal static KingdomSubsidenceRungWork ProveShape(KingdomSubsidenceRungPlan Plan,
			string PrimaryId, KingdomSubsidenceRungHeart Heart)
		{
			Check(Heart != null, "the completed-heart binding is absent for the frozen rung plan");
			ProveOrdinal(PrimaryId, Heart.Id);
			KingdomSubsidenceRungWork companion = null;
			Check(Plan != null && Plan.Works != null
				&& KingdomSubsidenceRungSaveShape.TryMatch(Plan, PrimaryId, Heart.Id, out companion)
				&& Plan.Works.Count == (Heart.Selected ? 2 : 1) && (companion != null) == Heart.Selected
				&& Plan.Works[0].ObjectId == PrimaryId, ShapeFault);
			if (!Heart.Selected)
			{
				foreach (KingdomSubsidenceRungWork row in Plan.Works)
					Check(row.ObjectId != Heart.Id,
						"an unselected completed heart stands in the frozen rung plan");
				return null;
			}
			Check(ReferenceEquals(Plan.Works[1], companion) && companion.ObjectId == Heart.Id
				&& companion.ReleasePhase == KingdomSubsidenceReleasePhase.Pending
				&& companion.WearPhase == KingdomSubsidenceEffectPhase.Prepared
				&& !companion.HadWearPart && companion.BeforeWear == 0
				&& companion.AfterWear == Heart.AfterWear
				&& companion.Roofs != null && companion.Roofs.Count == 0, CompanionFault);
			ProveCompanionGround(Heart, companion);
			return companion;
		}

		/// <summary>Ties the selected companion ROW to the SAME actual heart final object this witness
		/// proved, by typed designation and pose rather than identity alone: the blueprint, the raw plot
		/// id and build key exactly as KingdomSubsidenceStepRuntime.TryPrepareRung reads them from the
		/// live object (plot defaulted to the empty string when the property is absent), and the live
		/// cell. The live reference and IDIfAssigned are re-proved at this exact moment first, so the
		/// tie is to the object standing here, never to a stale capture; Final() has already tied that
		/// same object's plot property to the terminal binding's own PlotId.</summary>
		internal static void ProveCompanionGround(KingdomSubsidenceRungHeart Heart,
			KingdomSubsidenceRungWork Companion)
		{
			Check(Heart != null && Heart.Terminal != null && Companion != null
				&& GameObject.Validate(Heart.Final) && Heart.Final.IDIfAssigned == Heart.Id
				&& Companion.ObjectId == Heart.Id && Heart.Final.CurrentCell != null,
				"the companion tie has no live completed heart at this exact moment");
			Check(Companion.Blueprint == Heart.Final.Blueprint
				&& Companion.PlotId == (Heart.Final.GetStringProperty(KingdomPlots.PlotIdProperty) ?? "")
				&& Companion.DesignStamp == Heart.Final.GetStringProperty(KingdomUpgrade.BuildKeyProperty)
				&& Companion.X == Heart.Final.CurrentCell.X && Companion.Y == Heart.Final.CurrentCell.Y
				&& Companion.Blueprint == Heart.Terminal.Blueprint
				&& Companion.DesignStamp == Heart.Terminal.BuildKey
				&& Companion.X == Heart.Terminal.X && Companion.Y == Heart.Terminal.Y,
				CompanionGroundFault);
		}

		/// <summary>After a cold recovery the selected heart carries its one new wear attachment, and
		/// its ten receipt fields stand at production's OWN released target: the eight
		/// KingdomSubsidenceReleaseRules.TryPlan proves as a fixed point, plus the two fields a fresh
		/// part never leaves at anything but zero.</summary>
		internal static void ProveCompanionReleased(GameObject Heart, string StepId, int AfterWear)
		{
			Check(GameObject.Validate(Heart), "the recovered completed heart is not a live object");
			r_KingdomWear wear = Heart.GetPart<r_KingdomWear>();
			Check(wear != null && KingdomSubsidenceRungSaveLiveProof.WearCopies(Heart) == 1
				&& ReferenceEquals(wear.ParentObject, Heart) && !wear.LifecycleQuarantined
				&& wear.RepairEffortLeft == 0 && wear.LeakPhase == (int)KingdomWearLeakPhase.None,
				"the recovered heart did not receive its single exact new wear attachment");
			KingdomSubsidenceWearReceipt observed = new KingdomSubsidenceWearReceipt(wear.IncidentPhase,
				wear.IncidentId, wear.IncidentCause, wear.IncidentBeforeWear, wear.IncidentAfterWear,
				wear.Wear, wear.LastCause, wear.LastCompletedIncidentId, wear.IncidentLine,
				wear.IncidentMessageState);
			Check(KingdomSubsidenceReleaseRules.TryPlan(StepId, 0, AfterWear, observed,
					out KingdomSubsidenceWearReceipt target)
				&& KingdomSubsidenceReleaseRules.Same(observed, target)
				&& wear.LastCause == (int)KingdomWearRules.WearCause.None
				&& wear.IncidentMessageState == 0,
				"the recovered heart's ten wear-receipt fields are not production's released target");
		}

		/// <summary>The companion release lane, taken from the cut's own observed evidence. Never
		/// inferred from a native log line: the log does not expose these counts at all.</summary>
		internal static void ProveCompanionEvidence(bool Selected)
		{
			if (!Selected)
			{
				Check(KingdomSubsidenceRungReleaseCut.CompanionWrites == 0
					&& KingdomSubsidenceRungReleaseCut.CompanionFields == null
					&& !KingdomSubsidenceRungReleaseCut.CompanionPublishedIntent
					&& !KingdomSubsidenceRungReleaseCut.CompanionPublishedReleased,
					"an unselected completed heart raised observed companion release events");
				return;
			}
			Check(KingdomSubsidenceRungReleaseCut.CompanionPublishedIntent
				&& KingdomSubsidenceRungReleaseCut.CompanionPublishedReleased
				&& KingdomSubsidenceRungReleaseCut.CompanionWrites == CompanionWrites
				&& KingdomSubsidenceRungReleaseCut.CompanionFields == CompanionFields,
				"the selected heart's observed companion release is not its exact three admitted writes"
					+ " under a durable intent and release");
		}

		private static void Check(bool Condition, string Failure)
		{
			KingdomScenarioSaveFiles.Require(Condition, Failure ?? "native rung heart proof refused");
		}
	}
}
