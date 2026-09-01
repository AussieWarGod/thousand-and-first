using System;
using System.Collections.Generic;
using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		private void NormalizeState(bool AllowLegacyIdentityMigration)
		{
			if (FounderHistory == null)
			{
				FounderHistory = new KingdomFounderHistoryReceipt();
			}
			FounderHistory.Normalize();
			if (FounderHistory.Phase != KingdomFounderHistoryPhase.None
				&& FounderHistory.Phase != KingdomFounderHistoryPhase.Quarantined
				&& !KingdomFounderHistoryRules.Validate(FounderHistory,
					out string founderHistoryFailure))
			{
				FounderHistory.Phase = KingdomFounderHistoryPhase.Quarantined;
				FounderHistory.PublicationEnabled = true;
				FounderHistory.CommittedTick = 0L;
				FounderHistory.Fault = KingdomFounderHistoryRules.QuarantineReason(
					founderHistoryFailure);
			}
			if (!KingdomMasterRules.WellFormed(MasterOption, MasterOptionTick,
				MasterResumeToken, MasterAppliedResumeToken))
			{
				// Corrupt transition evidence is fail-closed. The player can re-enable from this
				// canonical disabled latch; no module clock is guessed during load normalization.
				MasterOption = KingdomMasterLatchValue.Disabled;
				MasterOptionTick = 0L;
				MasterResumeToken = 0L;
				MasterAppliedResumeToken = 0L;
			}
			// Preserve bounded valid bytes across capture/restore. The roster reader exposes a
			// canonical view; normalization owns only the hard heap bound.
			List<string> boundedKeepers;
			if (!KingdomZoningRules.TryDecodeRoster(KeepersRoster, out boundedKeepers))
			{
				KeepersRoster = "";
			}
			if (!Enum.IsDefined(typeof(GrowthStage), Stage))
			{
				Stage = GrowthStage.Camp;
			}
			if (LastMeal != KingdomRules.MealVerdict.None &&
				LastMeal != KingdomRules.MealVerdict.Scraps &&
				LastMeal != KingdomRules.MealVerdict.Plain &&
				LastMeal != KingdomRules.MealVerdict.Favored)
			{
				LastMeal = KingdomRules.MealVerdict.None;
			}
			if (Gate != KingdomRules.GatePolicy.Open &&
				Gate != KingdomRules.GatePolicy.Guarded)
			{
				Gate = KingdomRules.GatePolicy.Open;
			}
			if (Stores != KingdomRules.StoresPolicy.Plenty &&
				Stores != KingdomRules.StoresPolicy.Thrift)
			{
				Stores = KingdomRules.StoresPolicy.Plenty;
			}
			if (!Enum.IsDefined(typeof(KingdomRules.PetitionKind), PetitionKind))
			{
				PetitionKind = KingdomRules.PetitionKind.None;
			}
			if (!Enum.IsDefined(typeof(PetitionLifecycle), PetitionState))
			{
				PetitionState = PetitionLifecycle.None;
			}
			if (RaidState != 0 && RaidState != 1)
			{
				RaidState = 0;
				RaidFactionName = null;
				RaidDueTick = 0L;
			}
			if (City == null)
			{
				City = new Simulation.City.KingdomCityBook();
			}
			City.Normalize();
			// Old saves have no field and decode to null/default. Preserve any non-empty receipt
			// byte-for-byte: recovery validates it and never guesses through corruption.
			ResidentDeparture = KingdomResidentDepartureRules.NormalizeOldDefault(
				ResidentDeparture);
			StageLegacySettlementTopology();
			if (LifecycleBook == null)
			{
				LifecycleBook = new KingdomLifecycleBook();
			}
			KingdomLifecycleRules.Normalize(LifecycleBook);
			if (CarryBook == null)
			{
				CarryBook = new KingdomCarryBook();
			}
			KingdomLifecycleRules.Normalize(CarryBook);
			if (Jobs == null)
			{
				Jobs = new Simulation.City.KingdomJobRegistry();
			}
			Jobs.Normalize();
			if (LastSliceTick < 0L)
			{
				LastSliceTick = 0L;
			}
			if (Bindings == null)
			{
				Bindings = new Simulation.City.KingdomBindingRegistry();
			}
			Bindings.Normalize();
			// A counter below zero would hand out an id a body may already carry, and an id that is
			// not unique is not an identity. Fails closed to "nothing enrolled yet"; the ids already
			// on bodies keep working, and the next mint starts over rather than colliding with one
			// this realm has definitely issued.
			if (ResidentCounter < 0)
			{
				ResidentCounter = 0;
			}
			if (ResearchShelf == null)
			{
				ResearchShelf = new Dictionary<string, int>();
			}
			// The lab mints nothing, so a negative accrual or stamp is a corrupt reading rather
			// than a city that owes its own bench: both fail closed to "nothing worked out yet".
			if (ResearchAccrued < 0)
			{
				ResearchAccrued = 0;
			}
			if (ResearchTakenUpTick < 0L)
			{
				ResearchTakenUpTick = 0L;
			}
			// A founded save written before cities had names of their own carries only the realm's.
			// The seat is that first city, so it takes that name rather than arriving unnamed.
			if (Founded && string.IsNullOrEmpty(SettlementName))
			{
				SettlementName = KingdomDisplayName;
			}
			if (!string.IsNullOrEmpty(Vocation) && !KingdomSettlement.IsKnownVocation(Vocation))
			{
				Vocation = KingdomSettlement.NeutralVocation;
			}
			if (string.IsNullOrEmpty(Style)) Style = "common";
			Style = KingdomStyleRules.MigrateLegacyKey(Style);
			// A stored level or stamp below zero is a corrupt reading, not a settlement in
			// debt: subsidence mints nothing, so both fail closed to "nothing measured yet".
			if (LastSubsidenceTick < 0L)
			{
				LastSubsidenceTick = 0L;
			}
			if (LastSemanticTick < 0L)
			{
				LastSemanticTick = 0L;
			}
			if (HomecomingDays < 0)
			{
				HomecomingDays = 0;
			}
			if (!SemanticPassActive)
			{
				SemanticPassStartedTick = 0L;
				SemanticPassZoneId = null;
				SemanticPassStartedMask = 0L;
				SemanticPassCompletedMask = 0L;
			}
			else if (SemanticPassStartedTick < 0L || string.IsNullOrEmpty(SemanticPassZoneId)
				|| SemanticPassStartedMask < 0L || SemanticPassCompletedMask < 0L
				|| (SemanticPassCompletedMask & ~SemanticPassStartedMask) != 0L)
			{
				// A corrupt receipt cannot safely say which subsystem already mutated. Drop only
				// the scheduler receipt; every subsystem's own absolute clock remains authoritative.
				SemanticPassActive = false;
				SemanticPassStartedTick = 0L;
				SemanticPassZoneId = null;
				SemanticPassStartedMask = 0L;
				SemanticPassCompletedMask = 0L;
			}
			if (SupportedLevel < 0)
			{
				SupportedLevel = 0;
			}
			// Read the old field for ABI compatibility, then retire its economy unconditionally.
			// Optional civic titles cannot grant hidden capacity, including on legacy saves.
			NotableShade = 0;
			// Retire pre-ruling passive-food state unconditionally. Named fields stay serialized,
			// but no loaded value may create capacity, catch-up, departure, or a famine mark.
			HungerStreak = 0;
			Famished = false;
			ScrapsAnnounced = false;
			MealShade = 0;
			Seceded?.Normalize();
			if (Dissent < 0 || Dissent > KingdomCreedRules.DissentBreaking)
			{
				Dissent = (Dissent < 0) ? 0 : KingdomCreedRules.DissentBreaking;
			}
			if (DissentSpoken < 0 || DissentSpoken > (int)CityTemper.Secession)
			{
				DissentSpoken = 0;
			}
			if (ConversionShared == null)
			{
				ConversionShared = new Dictionary<string, int>();
			}
			if (ConversionToward == null)
			{
				ConversionToward = new Dictionary<string, string>();
			}
			if (ConversionResented == null)
			{
				ConversionResented = new Dictionary<string, int>();
			}
			NormalizeArchivedAndCollectionState(AllowLegacyIdentityMigration);
		}

	}
}
