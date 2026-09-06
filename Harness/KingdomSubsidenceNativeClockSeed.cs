using System;
using System.Collections.Generic;
using System.Linq;
using XRL;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	/// <summary>Explicit synthetic elapsed checkpoint, not elapsed ordinary play or a production
	/// option transition. A failed publication is retained, never rolled back or retried here.</summary>
	internal static class KingdomSubsidenceNativeClockSeed
	{
		private sealed class Frame
		{
			internal XRLGame Game;
			internal KingdomSystem System;
			internal KingdomCityBook City;
			internal KingdomBindingRegistry Bindings;
			internal KingdomLedger Ledger;
			internal KingdomResidentDepartureOperation Departure;
			internal KingdomSubsidenceNativeFixture Fixture;
			internal Zone Zone;
			internal KingdomSurvey Survey;
			internal GameObject Player;
			internal string Realm, Settlement, Receipt, Key, Binding;
			internal long Now, Clock, Token;
			internal int Support, Departures;
			internal Dictionary<string, string> Strings;
			internal object[] Tables;
			internal List<string>[] Lists;
			internal string[][] Text;
			internal int[] Counts, ResidentIds;
			internal GameObject[] Bodies;
			internal string[] BodyIds;
		}
		private static Frame Attempt;

		internal static bool TrySeed(KingdomSystem system, Zone zone, KingdomSurvey survey,
			long anchor, out string failure)
		{
			failure = "Synthetic subsidence clock seed requires an untouched native fixture.";
			try
			{
				if (Attempt != null || !TryCapture(system, zone, survey, anchor, out Frame frame)) return false;
				if (!KingdomSubsidenceStepCodec.TryDecode(KingdomSubsidenceStepCodec.FreshWire,
					out KingdomSubsidenceStepBook fresh) || !Empty(fresh)
					|| !KingdomSubsidenceStepRules.TryAdmit(fresh, frame.Realm, frame.Settlement,
						out KingdomSubsidenceStepBook admitted) || !Empty(admitted)
					|| !KingdomSubsidenceStepCodec.TryEncode(admitted, out string wire)) return false;
				KingdomElapsedOptionDecision decision = KingdomElapsedOptionRules.Observe(
					KingdomElapsedOptionRecord.Unobserved, true, frame.Token, anchor);
				string option = KingdomElapsedOptionRules.Encode(decision.Record);
				if (!decision.Valid || decision.Action != KingdomElapsedOptionAction.AnchorEnabled
					|| string.IsNullOrEmpty(option) || !KingdomElapsedOptionRules.TryDecode(option, out var decoded)
					|| decoded.State != KingdomElapsedOptionState.Enabled || decoded.ObservedTick != anchor
					|| decoded.MasterResumeToken != frame.Token || KingdomElapsedOptionRules.Encode(decoded) != option)
					return false;
				// NativeChecks freezes its expected summary cause before the first production pass.
				KingdomCatalogueRules.SupportTally tally = KingdomSubsidence.ScopedSupports(system, zone, survey);
				int support = KingdomSubsidenceRules.SupportedLevel(tally, GrowthStage.City, system.Shade);
				string binding = KingdomSubsidenceRules.BindingSupportFor(tally, GrowthStage.City);
				if (support < KingdomCatalogueRules.FloorLevel || support >= 50
					|| binding != "water" && binding != "roof"
					|| !Exact(frame, KingdomSubsidenceStepCodec.FreshWire, null, frame.Clock, frame.Support, frame.Binding))
					return false;
				Attempt = frame;
				failure = "Synthetic subsidence seed publication lost exact authority; all published evidence is retained.";
				frame.City.SubsidenceModel = wire;
				if (!Exact(frame, wire, null, frame.Clock, frame.Support, frame.Binding)) return false;
				frame.Strings.Add(frame.Key, option);
				if (!Exact(frame, wire, option, frame.Clock, frame.Support, frame.Binding)) return false;
				system.LastSubsidenceTick = anchor;
				if (!Exact(frame, wire, option, anchor, frame.Support, frame.Binding)) return false;
				system.SupportedLevel = support;
				if (!Exact(frame, wire, option, anchor, support, frame.Binding)) return false;
				system.SubsidenceBinding = binding;
				if (!Exact(frame, wire, option, anchor, support, binding)) return false;
				failure = null; return true;
			}
			catch (Exception error)
			{
				failure = "Synthetic subsidence clock seed refused after " + error.GetType().Name
					+ "; fixture and any published evidence are retained.";
				return false;
			}
		}

		private static bool TryCapture(KingdomSystem system, Zone zone, KingdomSurvey survey,
			long anchor, out Frame frame)
		{
			frame = null;
			XRLGame game = The.Game;
			long now = game?.TimeTicks ?? -1L;
			KingdomSubsidenceNativeFixture fixture = KingdomSubsidenceNativeFixture.LastAttempt;
			if (game == null || fixture == null || system == null || zone == null || survey == null
				|| anchor < 0 || now < anchor || system.City == null || system.Bindings == null
				|| system.Ledger == null || system.LastSubsidenceTick < 0 || system.LastSubsidenceTick > now
				|| system.City.SubsidenceModel != KingdomSubsidenceStepCodec.FreshWire
				|| !Scenario(out string receipt) || !ReferenceEquals(fixture.System, system)
				|| fixture.Bodies.Count != 50 || fixture.ResidentIds.Count != 50) return false;
			List<string>[] lists = Lists(system);
			if (lists.Any(list => list == null || list.Count > KingdomChronicle.MaxEntries)) return false;
			Frame value = new Frame { Game = game, System = system, City = system.City,
				Bindings = system.Bindings, Ledger = system.Ledger, Departure = system.ResidentDeparture,
				Fixture = fixture, Zone = zone, Survey = survey, Player = The.Player,
				Realm = system.CurrentRealmId, Settlement = system.CurrentSettlementId,
				Receipt = receipt, Now = now, Clock = system.LastSubsidenceTick,
				Token = system.MasterAppliedResumeToken, Support = system.SupportedLevel,
				Binding = system.SubsidenceBinding, Departures = system.Ledger.Departures,
				Strings = game.StringGameState, Tables = Tables(game), Lists = lists,
				Text = lists.Select(list => list.ToArray()).ToArray(), Counts = Counts(system.Ledger),
				Bodies = fixture.Bodies.ToArray(), ResidentIds = fixture.ResidentIds.ToArray() };
			value.Key = KingdomSubsidence.OptionStatePrefix + value.Settlement;
			value.BodyIds = value.Bodies.Select(body => body?.IDIfAssigned).ToArray();
			if (value.Tables.Any(table => table == null)
				|| !Exact(value, KingdomSubsidenceStepCodec.FreshWire, null, value.Clock, value.Support, value.Binding)) return false;
			frame = value; return true;
		}

		private static bool Exact(Frame frame, string wire, string option, long clock, int support, string binding)
		{
			if (!Scenario(out string receipt) || receipt != frame.Receipt) return false;
			KingdomSystem system = frame.System;
			if (!ReferenceEquals(The.Game, frame.Game) || frame.Game.TimeTicks != frame.Now
				|| !ReferenceEquals(The.Player, frame.Player) || !GameObject.Validate(frame.Player)
				|| !ReferenceEquals(frame.Player.CurrentZone, frame.Zone)
				|| !ReferenceEquals(The.ZoneManager?.ActiveZone, frame.Zone)
				|| !ReferenceEquals(frame.Game.GetSystem<KingdomSystem>(), system)
				|| !ReferenceEquals(KingdomSubsidenceNativeFixture.LastAttempt, frame.Fixture)
				|| !ReferenceEquals(frame.Fixture.Game, frame.Game) || !ReferenceEquals(frame.Fixture.System, system)
				|| !ReferenceEquals(frame.Fixture.Zone, frame.Zone)
				|| !ReferenceEquals(system.City, frame.City) || !ReferenceEquals(system.Bindings, frame.Bindings)
				|| !ReferenceEquals(system.Ledger, frame.Ledger) || !ReferenceEquals(system.ResidentDeparture, frame.Departure)
				|| !KingdomResidentDepartureRules.IsEmpty(system.ResidentDeparture)
				|| !system.Founded || !system.OwnedZone(frame.Zone.ZoneID) || system.ClaimedZones.Count != 1
				|| system.NonSeatSettlementCount != 0 || system.CurrentRealmId != frame.Realm
				|| system.CurrentSettlementId != frame.Settlement || frame.City.SettlementId != frame.Settlement
				|| system.MasterAppliedResumeToken != frame.Token || !KingdomMaster.NewWorkAllowed(system)
				|| !KingdomSubsidence.Enabled || system.SubsidenceAnnounced || system.Shade != 0
				|| system.Population != 50 || system.Stage != GrowthStage.City
				|| system.LastSubsidenceTick != clock || system.SupportedLevel != support || system.SubsidenceBinding != binding
				|| frame.City.SubsidenceModel != wire || !frame.City.HasValidSubsidenceStorage()
				|| !ReferenceEquals(KingdomSurvey.ActiveFor(frame.Zone), frame.Survey)
				|| !ReferenceEquals(frame.Survey.Ground, frame.Zone) || !FixtureSurvey(frame)
				|| !SameReferences(frame.Tables, Tables(frame.Game)) || !SameReferences(frame.Lists, Lists(system))
				|| !frame.Counts.SequenceEqual(Counts(frame.Ledger)) || frame.Ledger.Departures != frame.Departures)
				return false;
			for (int i = 0; i < frame.Lists.Length; i++)
				if (!frame.Lists[i].SequenceEqual(frame.Text[i], StringComparer.Ordinal)) return false;
			return OptionExact(frame, option) && ResidentsExact(frame);
		}

		private static bool FixtureSurvey(Frame frame)
		{
			if (ReferenceEquals(frame.Fixture.Survey, frame.Survey)) return true;
			KingdomSubsidenceRungNativeFixture rung = KingdomSubsidenceRungNativeFixture.LastAttempt;
			return rung != null && ReferenceEquals(rung.Base, frame.Fixture)
				&& ReferenceEquals(rung.Survey, frame.Survey) && rung.Now == frame.Now;
		}

		private static bool ResidentsExact(Frame frame)
		{
			if (!frame.City.TryReadExact(out KingdomCityState city, out _)
				|| !frame.Bindings.TryReadExact(out KingdomBindingTable bindings, out _)
				|| city.ResidentCount != 50 || bindings.Count != 50 || frame.Survey.Settlers.Count != 50
				|| frame.Fixture.Bodies.Count != 50 || frame.Fixture.ResidentIds.Count != 50) return false;
			HashSet<GameObject> bodies = new HashSet<GameObject>();
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < frame.Bodies.Length; i++)
			{
				GameObject body = frame.Bodies[i]; int id = frame.ResidentIds[i];
				if (!ReferenceEquals(frame.Fixture.Bodies[i], body) || frame.Fixture.ResidentIds[i] != id
					|| !GameObject.Validate(body) || !bodies.Add(body) || !ids.Add(frame.BodyIds[i])
					|| string.IsNullOrEmpty(frame.BodyIds[i]) || body.IDIfAssigned != frame.BodyIds[i]
					|| !ReferenceEquals(body.CurrentZone, frame.Zone) || !frame.Survey.Settlers.Contains(body)
					|| body.InInventory != null || body.Equipped != null || body.Count != 1
					|| KingdomResidents.IdOf(body) != id || !city.TryResidentIndex(id, out int index)
					|| !city.TryResident(index, out KingdomResidentRow row) || row.Standing != KingdomResidentStanding.Resident
					|| !bindings.TryGet(id, KingdomBindingKind.Resident, out KingdomBinding resident)
					|| resident.ObjectId != frame.BodyIds[i] || resident.ZoneId != frame.Zone.ZoneID) return false;
			}
			return true;
		}

		private static bool OptionExact(Frame frame, string expected)
		{
			XRLGame game = frame.Game; string key = frame.Key;
			if (game.HasIntGameState(key) || game.HasInt64GameState(key)
				|| game.HasObjectGameState(key) || game.HasBooleanGameState(key)) return false;
			return expected == null ? !game.HasStringGameState(key)
				: game.HasStringGameState(key) && game.GetStringGameState(key) == expected;
		}

		private static bool Scenario(out string receipt)
		{
			receipt = null;
			if (!KingdomScenarioRealizer.TryBindStampedPlan(out KingdomScenarioPlan plan, out _, out _)
				|| plan.Key != "founding-first-city" || plan.AuthorityClass != KingdomScenarioFoundingStep.FoundingAuthority
				|| KingdomScenarioTransactionMarker.Observe(out _) != KingdomScenarioTransactionShape.Committed
				|| !KingdomScenarioScript.TryRead(out IList<string> script, out _) || script.Count != 3
				|| script[0] != "stagedigest" || script[2] != "stagedigest") return false;
			if (script[1] == KingdomSubsidenceNativeProvider.Verb) receipt = KingdomSubsidenceNativeProvider.Receipt;
			else if (KingdomSubsidenceRungNativeProvider.ExactScript(script[1])) receipt = KingdomSubsidenceRungNativeProvider.Receipt;
			else if (script[1] == KingdomScenarioSaveProvider.Verb) receipt = KingdomScenarioSaveProvider.Receipt;
			else if (script[1] == KingdomSubsidenceRungSaveProvider.Verb) receipt = KingdomSubsidenceRungSaveProvider.Receipt;
			return receipt != null && KingdomScenarioDurableState.ProvesExactText(receipt, "intent");
		}

		private static bool Empty(KingdomSubsidenceStepBook book)
		{
			return book != null && book.Sequence == 0 && book.Active == null && book.LastRetiredTick == 0
				&& book.OptionModel == KingdomSubsidenceStepRules.NoOption
				&& book.BatchModel == KingdomSubsidenceBatchRules.None && book.FailureModel == KingdomSubsidenceReportArchive.None;
		}
		private static object[] Tables(XRLGame game) { return new object[] { game.StringGameState,
			game.IntGameState, game.Int64GameState, game.ObjectGameState, game.BooleanGameState }; }
		private static List<string>[] Lists(KingdomSystem system) { return new[] { system.Ledger.Notes,
			system.Ledger.BrinkLines, system.Ledger.ExpeditionLines, system.ChronicleEntries, system.OutsiderEntries }; }
		private static int[] Counts(KingdomLedger ledger) { return new[] { ledger.Fetched, ledger.UpkeepDrawn,
			ledger.ArrivalCost, ledger.Delivered, ledger.Harvested, ledger.Foraged, ledger.RationsDrawn,
			ledger.Milled, ledger.HarvestLost, ledger.Plundered, ledger.Arrivals, ledger.Departures }; }
		private static bool SameReferences(object[] before, object[] after)
		{
			if (before.Length != after.Length) return false;
			for (int i = 0; i < before.Length; i++) if (!ReferenceEquals(before[i], after[i])) return false;
			return true;
		}
	}
}
