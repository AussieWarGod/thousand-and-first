using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using XRL;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	/// <summary>Compare saved guest authority before activation and after load; never restore it.</summary>
	internal static class KingdomGuestSaveWitness
	{
		private const string Key = "r_TAF_ScenarioGuestSave_v1";
		private static XRLGame WitnessedGame;
		private static int Attempts;
		private static string Failure;
		private static void Require(bool value, string reason) => KingdomGuestActionsNativeProvider.Require(value, reason);

		internal static string Record(XRLGame game)
		{
			KingdomGuestSaveNativeProvider.RequireScript();
			Require(!KingdomScenarioSaveFiles.LoadPresent()
				&& !KingdomNativeRegressionContext.HasAnyState(game, Key), "guest save witness already exists");
			string wire = Capture(game, out GameObject body, out _);
			game.SetStringGameState(Key, wire);
			Require(KingdomScenarioDurableState.ProvesExactText(Key, wire), "guest witness did not persist exactly");
			return "native-guest-save witness=recorded; guest=" + body.IDIfAssigned
				+ "; population=" + game.GetSystem<KingdomSystem>().Population
				+ "; receipt-sha256=" + KingdomScenarioSaveFiles.HashText(wire) + "; world-repair=false";
		}

		internal static void BeforeActivation(XRLGame game)
		{
			try
			{
				Attempts++;
				Require(Attempts == 1 && WitnessedGame == null, "guest preactivation repeated");
				Require(KingdomScenarioLoadReaderWitness.Releases == 1 && !KingdomScenarioLoadReaderWitness.HadErrors,
					"guest reader did not complete exactly once without errors");
				Compare(game, out GameObject body, out _);
				WitnessedGame = game;
				Require(KingdomScenarioJournal.Append("guest-load-preactivation", true,
					"exact-guest-authority=true; before-AfterGameLoaded=true; world-repair=false; " + Stamp(game, body)) == null,
					"guest preactivation journal unavailable");
			}
			catch (Exception error)
			{
				Failure = error.GetType().Name + ": " + error.Message;
				KingdomScenarioJournal.Append("guest-load-preactivation", false, Failure);
			}
		}

		internal static void VerifyLoaded(XRLGame game)
		{
			Require(Failure == null && Attempts == 1 && ReferenceEquals(game, WitnessedGame),
				"guest preactivation witness missing: " + Failure);
			Compare(game, out GameObject body, out KingdomGrowthFirstGuestTerminalReceipt terminal);
			var actions = new Dictionary<string, InventoryAction>();
			GetInventoryActionsEvent.Send(The.Player, body, actions);
			foreach (InventoryAction action in actions.Values)
				Require(action.Command != "r_TAF_FirstGuestChoice", "loaded citizen offers stale guest choice");
			for (int retry = 0; retry < 2; retry++)
			{
				Require(!KingdomGrowth.CanUsePhysicalFirstGuest(body, The.Player, terminal.CandidateId,
					terminal.Opportunity.OpportunityId), "loaded citizen remains eligible for guest action");
				KingdomGrowth.OpenPhysicalFirstGuest(body, The.Player, terminal.CandidateId, terminal.Opportunity.OpportunityId);
				Compare(game, out GameObject same, out _);
				Require(ReferenceEquals(body, same) && !KingdomSurvey.HasBoundPass, "stale choice replaced body or leaked scope");
			}
			Require(KingdomScenarioJournal.Append("guest-load-verified", true,
				"same-citizen=true; " + Stamp(game, body)
				+ "; stale-choice-refused=2; duplicate-enrollment=false; extra-water-debit=false; world-repair=false") == null,
				"guest loaded journal unavailable");
		}

		private static string Stamp(XRLGame game, GameObject body) => "guest=" + body.IDIfAssigned
			+ "; population=" + game.GetSystem<KingdomSystem>().Population
			+ "; receipt-sha256=" + KingdomScenarioSaveFiles.HashText(game.GetStringGameState(Key));

		private static void Compare(XRLGame game, out GameObject body, out KingdomGrowthFirstGuestTerminalReceipt terminal)
		{
			KingdomGuestSaveNativeProvider.RequireScript();
			Require(KingdomScenarioLoadEntry.Armed && KingdomScenarioLoadEntry.LifecycleSnapshot != null,
				"guest witness requires the sealed lifecycle load");
			string raw = game.GetStringGameState(Key);
			Require(raw != null && raw.Length <= 16000 && KingdomScenarioDurableState.ProvesExactText(Key, raw),
				"guest save witness absent or malformed");
			Require(Capture(game, out body, out terminal) == raw, "loaded guest, citizenship, terminal receipt, home or resources differ");
		}

		// Transfer checks measure stored water independently; the save/load witness always includes it.
		internal static string CaptureTransferAuthority(XRLGame game) => Capture(game, out _, out _, false);

		private static string Capture(XRLGame game, out GameObject body, out KingdomGrowthFirstGuestTerminalReceipt terminal,
			bool includeStoredWater = true)
		{
			var system = game.GetSystem<KingdomSystem>();
			Zone zone = game.ZoneManager.ActiveZone;
			Require(system != null && zone != null && ReferenceEquals(zone, The.Player?.CurrentZone)
				&& system.Founded && system.Population >= 5 && KingdomResidents.OnRollCount(system) == system.Population,
				"guest save requires living original founders and an enrolled guest");
			terminal = system.LifecycleBook?.Growth?.FirstGuestTerminal;
			Require(terminal?.Opportunity != null && terminal.Result == KingdomGrowthArrivalDisposition.Joined
				&& !string.IsNullOrEmpty(terminal.CandidateObjectId), "joined first guest terminal receipt absent");
			body = null;
			foreach (GameObject item in zone.GetObjects())
			{
				if (!GameObject.Validate(item) || item.IDIfAssigned != terminal.CandidateObjectId) continue;
				Require(body == null, "duplicate first guest identity"); body = item;
			}
			Require(body != null && body.IsAlive && body.CurrentZone == zone && KingdomCitizenship.BelongsTo(system, body),
				"saved guest lost physical identity or citizenship");
			var citizenship = body.GetPart<r_KingdomCitizenship>();
			Require(citizenship != null && citizenship.Phase == KingdomCitizenshipPhase.Applied
				&& citizenship.EnrollmentReason == (int)KingdomCitizenshipEnrollmentReason.Arrival,
				"guest arrival citizenship receipt differs");
			Require(KingdomResidents.TryResident(system.City, KingdomResidents.IdOf(body), out var resident)
				&& KingdomResidentRules.OnTheRoll(resident), "guest arrival receipt or living roll differs");
			Require(terminal.ResidentId == KingdomResidents.IdOf(body)
				&& terminal.SettlementId == system.CurrentSettlementId
				&& !string.IsNullOrEmpty(terminal.ArrivalOperationId), "guest terminal resident or operation identity differs");
			foreach (string property in new[] { "r_TAF_GrowthArrivalEnrollment", "r_TAF_GrowthArrivalRoster", "r_TAF_GrowthArrivalCreed" })
				Require(body.GetStringProperty(property) == terminal.ArrivalOperationId, "guest domain receipt differs: " + property);
			Require(KingdomLodging.HomeDesignKeyOf(zone, body) == KingdomQuickstartRules.ShelterBuildKey,
				"guest does not occupy real starter housing");
			KingdomRecruitmentBodyWitness.Verify(body, terminal.Blueprint, terminal.PersonOrigin, Require);
			Require(body.GetStringProperty("KingdomName") == terminal.PersonName
				&& body.GetStringProperty("KingdomOrigin") == terminal.PersonOrigin
				&& resident.Name == terminal.PersonName && resident.Origin == terminal.PersonOrigin
				&& resident.BoundZoneId == zone.ZoneID, "saved recruit body, roll and native identity disagree");
			var opportunity = terminal.Opportunity;
			string wire = Encode("taf-guest-save-v1", game.GameID, game.TimeTicks, system.RealmId,
				system.CurrentSettlementId, zone.ZoneID, system.Population, includeStoredWater ? KingdomGrowth.CountStoredWater(zone) : 0,
				system.Ledger.Arrivals, system.Ledger.ArrivalCost,
				game.GetStringGameState(KingdomQuickstartRules.ReceiptState), body.IDIfAssigned,
				KingdomResidents.IdOf(body), body.DisplayName, body.GetStringProperty("KingdomName"),
				body.Blueprint, body.GetCulture(), body.GetSpecies(), body.GetStringProperty("KingdomOrigin"),
				body.GetStringProperty(KingdomCreed.CreedProperty), KingdomLodging.HomeDesignKeyOf(zone, body),
				body.GetStringProperty(KingdomLodging.HomePlotIdProperty),
				body.GetStringProperty("r_TAF_GrowthArrivalEnrollment"), body.GetStringProperty("r_TAF_GrowthArrivalRoster"),
				body.GetStringProperty("r_TAF_GrowthArrivalCreed"), body.GetStringProperty("r_TAF_GrowthArrivalConversation"),
				citizenship.ReceiptVersion, citizenship.Phase, citizenship.PriorKind, citizenship.PriorValue,
				citizenship.AppliedValue, citizenship.OwnerRealmId, citizenship.OwnerSettlementId, citizenship.FactionId,
				citizenship.BodyObjectId, citizenship.EnrollmentReason, citizenship.RemovalReason, citizenship.AppliedTick,
				citizenship.RemovedTick, citizenship.NoticePublished, citizenship.Fault,
				terminal.Version, terminal.ReceiptId, terminal.SettlementId, terminal.CandidateId, terminal.CandidateObjectId,
				terminal.Blueprint, terminal.PersonName, terminal.PersonOrigin, terminal.PersonCreed, terminal.ResidentId,
				terminal.Result, terminal.ArrivalOperationId, terminal.ArrivalOutboxEventId, terminal.TerminalTick,
				opportunity.RulesVersion, opportunity.OpportunityId, opportunity.CauseId, opportunity.CauseTick,
				opportunity.OfferedTick, opportunity.CadenceTicks, opportunity.FactsState, opportunity.CohortSize,
				opportunity.PopulationBefore, opportunity.PopulationCap, opportunity.SupportedLevel, opportunity.SupportCap,
				opportunity.WaterAvailable, opportunity.WaterRequired, opportunity.ChoiceState, opportunity.DeferredTick,
				opportunity.DeferredReceiptId, opportunity.DecisionTick, opportunity.DecisionReceiptId, opportunity.BodyReservationId,
				opportunity.BodyRealmId, opportunity.BodyOptionKind, opportunity.BodyEnableEpoch, opportunity.BodyReservedTick,
				opportunity.BodyLeaseState, opportunity.GuestPhase, opportunity.GuestTerminalState, opportunity.GuestActionTick,
				opportunity.GuestActionReceiptId, opportunity.GuestTerminalTick, opportunity.GuestTerminalReceiptId);
			Require(wire.Length <= 16000, "guest evidence exceeds bound");
			return wire;
		}

		// Null and empty remain distinct, including the uncreeded guest that previously failed enrollment.
		private static string Encode(params object[] values)
		{
			var fields = new List<string>();
			foreach (object value in values)
				fields.Add(value == null ? "-" : "+" + Convert.ToBase64String(Encoding.UTF8.GetBytes(
					Convert.ToString(value, CultureInfo.InvariantCulture))));
			return string.Join("\n", fields);
		}
	}
}
