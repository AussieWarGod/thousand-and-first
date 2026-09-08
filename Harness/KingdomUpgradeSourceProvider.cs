using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using HarmonyLib;
using XRL;
using XRL.Core;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	// New-name overlay compiled WITH unchanged v0.3.1 production and its original Harness.
	// Synthetic fresh-NPC species/born provenance; real founding/citizenship/census/retirement.
	// The second world's live-Empty reservation is also a developer arrangement, not ordinary UI.
	[KingdomScenarioVerbProvider]
	public sealed class KingdomUpgradeSourceProvider : IKingdomScenarioVerbProvider
	{
		internal const string DonorVerb = "upgrade-source-donor", ReservedVerb = "upgrade-source-reserved";
		internal const string DonorFile = "upgrade-donor-receipt.txt", InputFile = "upgrade-source-donor.txt";
		internal const string LinkFile = "upgrade-source-link.txt";
		internal static KingdomUpgradeSourceDriver Attempt;
		private static bool Spent;
		private static GameObject Resident;
		private static Cell ResidentCell;
		private static Physics ResidentPhysics;
		private static IPart ResidentBrain, ResidentBody, ResidentInventory;
		private static int BaseId;
		private static string BodyId;
		public int ScenarioVerbApiVersion { get { return KingdomScenarioVerbApi.Version; } }
		public IEnumerable<string> ScenarioVerbs { get { return new[] { DonorVerb, ReservedVerb }; } }

		public string RunScenarioVerb(string verb, string argument, out bool Ok)
		{
			Ok = false;
			try
			{
				Check((verb == DonorVerb || verb == ReservedVerb) && string.IsNullOrEmpty(argument), "upgrade source verb takes no arguments");
				Check(!Spent && Attempt == null, "upgrade source attempt already retained");
				Spent = true;
				KingdomUpgradeState.Engine();
				XRLGame game = The.Game; Zone zone = The.Player?.CurrentZone;
				Check(game != null && zone != null && ReferenceEquals(game.ZoneManager?.ActiveZone, zone)
					&& Thread.CurrentThread == XRLCore.CoreThread && KingdomMaster.ConfiguredEnabled
					&& !(game.GetSystem<KingdomSystem>()?.Founded ?? false)
					&& !KingdomNativeRegressionContext.HasQuickstartState(game), "requires fresh active synthetic scenario ground");
				Check(KingdomQuickstartRules.TryProfile("marsh", out var profile) && zone.ZoneID == profile.ZoneId,
					"source requires exact stamped marsh zone");
				Check(Options.GetOption("r_TAF_OptionLegacyImport", "No") == "No", "legacy import must be disabled at birth");
				Check(KingdomScenarioRealizer.TryBindStampedPlan(out var plan, out _, out string failure), failure);
				Check(plan.Key == "founding-first-city" && plan.AuthorityClass == KingdomScenarioFoundingStep.FoundingAuthority
					&& KingdomScenarioScript.TryRead(out var script, out failure) && script.Count == 3
					&& script[0] == "stagedigest" && script[1] == verb && script[2] == "stagedigest", "source script or provenance differs");
				Check(KingdomScenarioTransactionMarker.Observe(out _) == KingdomScenarioTransactionShape.None,
					"source requires unspent founding transaction");
				Attempt = new KingdomUpgradeSourceDriver(game, zone, verb);
				if (verb == DonorVerb) { FoundDonor(plan); Attempt.Donor(); }
				else Attempt.Reserved();
				Attempt.Owner(); Ok = true;
				return "upgrade source cases=1 passed=1 failed=0; case=" + verb
					+ "; game=" + game.GameID + "; synthetic=true; ordinary=false; cache=bind-after-quit"
					+ "; synthetic-arrangement=" + (verb == DonorVerb ? "born-NPC" : "live-empty-reservation");
			}
			catch (Exception error)
			{
				return "upgrade source cases=1 passed=0 failed=1; evidence retained; "
					+ KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message);
			}
		}

		private static void FoundDonor(KingdomScenarioPlan plan)
		{
			string name = null, failure;
			foreach (KingdomScenarioResolvedStep step in plan.Steps)
				if (step.Verb == KingdomScenarioVerb.FoundFirstCity)
					Check(name == null && step.Arguments.TryGetValue("CityName", out name) && !string.IsNullOrEmpty(name), "donor name missing or duplicated");
			Check(name != null && KingdomScenarioFoundingStep.TryProvePreconditions(Attempt.Zone, name, out failure), "donor founding preconditions refused");
			// Real opt-in after a real census: old empty profiles cannot export canonical phenotype.
			Options.SetOption("r_TAF_OptionSeal", "No"); Attempt.Owner(); Attempt.Pristine();
			Check(Options.GetOption("r_TAF_OptionSeal", "Yes") == "No", "seal option did not disable exactly");
			Check(KingdomScenarioTransactionMarker.TryBegin(out failure), failure);
			Check(KingdomScenarioFoundingStep.TryFound(Attempt.Zone, name, out _, out failure), failure);
			Attempt.AdoptFoundedSystem();
			Check(KingdomScenarioTransactionMarker.TryCommit(out failure), failure);
			KingdomSystem system = Attempt.System;
			Check(system.Population == 0 && system.City != null && system.City.ResidentCount == 0
				&& system.Bindings != null && system.Bindings.Count == 0 && KingdomResidents.OnRollCount(system) == 0,
				"donor founding did not leave an empty resident authority");
			for (int y = 1; y < Attempt.Zone.Height - 1 && ResidentCell == null; y++)
				for (int x = 1; x < Attempt.Zone.Width - 1 && ResidentCell == null; x++)
					if (Empty(Attempt.Zone.GetCell(x, y))) ResidentCell = Attempt.Zone.GetCell(x, y);
			Check(ResidentCell != null, "no untouched clear cell for donor resident");
			GameObject created = GameObject.Create("NPC", BeforeObjectCreated: body =>
			{
				Check(Resident == null && body != null, "factory original missing or repeated");
				Resident = body; BodyId = body.ID; BaseId = body._BaseID;
			});
			Attempt.Owner();
			Check(ReferenceEquals(created, Resident) && GameObject.Validate(Resident) && Resident.Blueprint == "NPC"
				&& Resident._BaseID == BaseId && BaseId > 0 && Resident.IDIfAssigned == BodyId
				&& Resident.Physics != null && Resident.Physics.CurrentCell == null && Resident.Physics.InInventory == null
				&& Resident.Brain != null && Resident.Body != null && Resident.Inventory != null && Resident.Count == 1
				&& Resident.Equipped == null && Resident.Implantee == null
				&& !Resident.IsPlayer() && !Resident.IsPlayerLed() && Resident.IsAlive, "foreign factory return or occupied original");
			ResidentPhysics = Resident.Physics; ResidentBrain = Resident.Brain;
			ResidentBody = Resident.Body; ResidentInventory = Resident.Inventory;
			Resident.SetStringProperty("Species", "human");
			Check(Resident.GetSpecies() == "human", "synthetic donor phenotype did not persist");
			Unplaced(); Attempt.Owner();
			Check(Resident.GetIntProperty("KingdomBorn") == 0 && Resident.GetIntProperty("KingdomCitizen") == 0
				&& Resident.GetPart<r_KingdomCitizenship>() == null, "donor NPC already carries resident provenance");
			// Explicit synthetic arrival provenance only; production owns enrollment and resident rows.
			Resident.SetIntProperty("KingdomBorn", 1); Unplaced(); Attempt.Owner();
			Check(Resident.GetIntProperty("KingdomBorn") == 1, "synthetic donor born provenance did not persist");
			Check(KingdomCitizenship.TryEnroll(system, Resident, KingdomCitizenshipEnrollmentReason.Arrival,
				Attempt.Tick, out failure), failure);
			Attempt.Owner();
			Unplaced(); Check(Empty(ResidentCell), "donor enrollment changed placement destination");
			Attempt.Owner(); Unplaced();
			Check(ReferenceEquals(ResidentCell.AddObject(Resident, NoStack: true), Resident), "donor native placement substituted original");
			BodyExact();
			Check(KingdomResidents.TryEnsureRow(system, Resident, "upgrade donor native fixture", null, Attempt.Tick,
				out var book, out int id) && ReferenceEquals(book, system.City) && id > 0, "donor lacks genuine resident row");
			KingdomSurvey survey = KingdomSurvey.Take(Attempt.Zone, system);
			Attempt.Owner(); BodyExact();
			Check(ReferenceEquals(survey.Ground, Attempt.Zone) && survey.Citizens == 1 && survey.Settlers.Count == 1
				&& ReferenceEquals(survey.Settlers[0], Resident) && system.Population == 1
				&& system.City.ResidentCount == 1 && system.Bindings.Count == 1, "donor census differs from actual sole citizen");
			KingdomResidentIdentity.Reconcile(system, survey.Settlers); Attempt.Owner(); BodyExact();
			Check(system.SpeciesCounts != null && system.SpeciesCounts.TryGetValue("human", out int humans) && humans == 1,
				"actual donor identity census lacks the one witnessed human");
			Check(KingdomPolityProfileRuntime.TryReconcile(system, Attempt.Tick, out failure), failure);
			Options.SetOption("r_TAF_OptionSeal", "Yes"); Attempt.Owner(); BodyExact(); Attempt.Pristine();
			Check(Options.GetOption("r_TAF_OptionSeal", "No") == "Yes", "seal opt-in did not persist");
			Check(KingdomSeal.TryFoundingCompleted(out failure), failure); Attempt.Owner(); BodyExact();
		}

		internal static void BodyExact()
		{
			if (Resident == null) return;
			PartsExact();
			Check(GameObject.Validate(Resident) && Resident.Blueprint == "NPC" && Resident._BaseID == BaseId
				&& Resident.IDIfAssigned == BodyId && ReferenceEquals(Resident.Physics, ResidentPhysics)
				&& ReferenceEquals(ResidentPhysics.ParentObject, Resident) && ReferenceEquals(ResidentPhysics.CurrentCell, ResidentCell)
				&& ReferenceEquals(ResidentCell.ParentZone, Attempt.Zone) && ResidentPhysics.InInventory == null
				&& Resident.Equipped == null && Resident.Implantee == null,
				"donor original body custody changed");
			int refs = 0, ids = 0;
			for (int y = 0; y < Attempt.Zone.Height; y++) for (int x = 0; x < Attempt.Zone.Width; x++)
				foreach (GameObject body in Attempt.Zone.GetCell(x, y).Objects)
				{ if (ReferenceEquals(body, Resident)) refs++; if (body != null && body.IDIfAssigned == BodyId) ids++; }
			Check(refs == 1 && ids == 1, "donor body reference or ID membership is not unique");
		}
		private static void Unplaced()
		{
			PartsExact();
			Check(GameObject.Validate(Resident) && Resident.Blueprint == "NPC" && Resident._BaseID == BaseId
				&& Resident.IDIfAssigned == BodyId && Resident.Count == 1 && ResidentPhysics.CurrentCell == null
				&& ResidentPhysics.InInventory == null && Resident.Equipped == null
				&& Resident.Implantee == null, "original donor allocation lost unplaced custody");
		}
		private static void PartsExact()
		{
			Check(ReferenceEquals(Resident.Physics, ResidentPhysics) && ReferenceEquals(Resident.Brain, ResidentBrain)
				&& ReferenceEquals(Resident.Body, ResidentBody) && ReferenceEquals(Resident.Inventory, ResidentInventory), "retained donor parts changed");
			foreach (IPart part in new IPart[] { ResidentPhysics, ResidentBrain, ResidentBody, ResidentInventory })
			{
				int found = 0;
				foreach (IPart row in Resident.PartsList) if (ReferenceEquals(row, part)) found++;
				Check(part != null && ReferenceEquals(part.ParentObject, Resident) && found == 1, "donor part is not uniquely parented");
			}
		}

		private static bool Empty(Cell cell)
		{
			if (cell == null || !cell.IsPassable() || !cell.IsEmpty() || cell.HasOpenLiquidVolume()) return false;
			foreach (GameObject body in cell.Objects)
				if (!GameObject.Validate(body) || body.IsCreature || KingdomPlots.ReadObject(body) != KingdomPlotRules.GroundKind.Bare) return false;
			return true;
		}
		internal static void Check(bool condition, string failure) { KingdomUpgradeFiles.Check(condition, failure); }
	}

	// Inert observers: no return replacement, exception swallowing, or save-path mutation.
	[HarmonyPatch(typeof(XRLGame), "GetCacheDirectory", new Type[] { typeof(string) })]
	internal static class KingdomUpgradeDonorCacheObservation
	{
		[HarmonyPostfix, HarmonyPriority(Priority.Last)]
		internal static void Postfix(XRLGame __instance, string __0, string __result)
		{ KingdomUpgradeSourceProvider.Attempt?.ObservePath(__instance, __0, __result); }
	}
	[HarmonyPatch(typeof(XRLGame), "SaveGameError")]
	internal static class KingdomUpgradeDonorErrorObservation
	{
		[HarmonyPrefix]
		internal static void Prefix(XRLGame __instance)
		{ KingdomUpgradeSourceProvider.Attempt?.ObserveError(__instance); }
	}
}
