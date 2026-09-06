using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	/// <summary>Synthetic initial work/home state, not construction, adoption, or lodging acceptance.
	/// Allocations and failed attempts remain retained; only the exact temporary party hold is released.</summary>
	internal sealed class KingdomSubsidenceRungNativeFixture
	{
		internal const string WorkBlueprint = "r_KingdomHut";
		private readonly List<GameObject> Owned = new List<GameObject>();
		private GameObject Player;
		private Brain HeldBrain;
		private bool HoldAttempted, CompletedHeart;
		private Cell WorkCell;
		private KingdomCityBook City;
		private string Realm, Settlement, WorkId, PlotId, HomeObjectId;
		internal static KingdomSubsidenceRungNativeFixture LastAttempt { get; private set; }
		internal KingdomSubsidenceNativeFixture Base { get; private set; }
		internal GameObject Work { get; private set; }
		internal r_KingdomWear Wear { get; private set; }
		internal GameObject HomeResident { get; private set; }
		internal int HomeResidentId { get; private set; }
		internal KingdomSurvey Survey { get; private set; }
		internal int BeforeWear { get; private set; }
		internal int AfterWear { get; private set; }
		internal long Now { get; private set; }
		internal IReadOnlyList<GameObject> Allocations { get { return Owned; } }

		internal static bool TryCreate(Zone zone, out KingdomSubsidenceRungNativeFixture fixture,
			out string failure, bool CompleteFoundingHeart = false)
		{
			fixture = LastAttempt; failure = null;
			try
			{
				Require(fixture == null, "a prior native rung fixture remains retained");
				Require(The.Game != null && zone != null && KingdomSurvey.ActiveFor(zone) == null,
					"rung setup requires an unbound current game/zone");
				fixture = new KingdomSubsidenceRungNativeFixture { Player = The.Player,
					Now = The.Game.TimeTicks, CompletedHeart = CompleteFoundingHeart };
				LastAttempt = fixture;
				bool created = KingdomSubsidenceNativeFixture.TryCreate(zone,
					out KingdomSubsidenceNativeFixture basic, out string baseFailure, CompleteFoundingHeart);
				fixture.Base = basic;
				Require(created, baseFailure);
				fixture.City = basic.System.City;
				fixture.Realm = basic.System.CurrentRealmId;
				fixture.Settlement = basic.System.CurrentSettlementId;
				fixture.Build();
				return true;
			}
			catch (Exception error)
			{
				failure = "native rung fixture retained after " + error.GetType().Name;
				try { failure += ": " + error.Message; } catch (Exception) { }
				return false;
			}
		}

		private void Build()
		{
			RequireWorld();
			Require(Now >= 0 && KingdomLodging.Enabled && Base.System.Stage == GrowthStage.City
				&& Base.Bodies.Count == 50 && Base.ResidentIds.Count == 50,
				"rung setup requires enabled lodging and fifty City residents");
			ChooseIdentity();
			for (int y = 1; y < Base.Zone.Height - 1 && WorkCell == null; y++)
				for (int x = 1; x < Base.Zone.Width - 1 && WorkCell == null; x++)
					if (EmptyCell(Base.Zone.GetCell(x, y))) WorkCell = Base.Zone.GetCell(x, y);
			Require(WorkCell != null, "one empty safe work cell unavailable");
			GameObject returned = GameObject.Create(WorkBlueprint, BeforeObjectCreated: body =>
			{
				Require(body != null, "work factory exposed no allocation");
				Owned.Add(body);
				Require(Work == null, "work factory exposed multiple roots");
				Work = body;
				body.IDIfAssigned = WorkId;
				body.SetIntProperty("NoLoot", 1);
			});
			Require(ReferenceEquals(returned, Work) && GameObject.Validate(Work)
				&& Work.Blueprint == WorkBlueprint && Work.IDIfAssigned == WorkId && Work.Count == 1
				&& Work.CurrentCell == null && Work.InInventory == null && Work.Equipped == null
				&& Work.GetIntProperty("NoLoot") == 1 && Work.GetPart<r_KingdomWear>() == null,
				"factory returned foreign, worn, or occupied work custody");
			RequireEmptyContents(); RequireWorld();
			Require(EmptyCell(WorkCell), "work destination changed");
			Require(ReferenceEquals(WorkCell.AddObject(Work, NoStack: true), Work) && ExactWork(),
				"work placement did not retain its exact reference");
			// This one-cell plot and home assignment are synthetic initial conditions, not authored housing.
			PlotId = "native-rung-plot:" + WorkId;
			Work.SetIntProperty("KingdomBuilt", 1);
			Work.SetStringProperty(KingdomPlots.PlotIdProperty, PlotId);
			Work.SetIntProperty(KingdomPlots.PlotX1Property, WorkCell.X);
			Work.SetIntProperty(KingdomPlots.PlotY1Property, WorkCell.Y);
			Work.SetIntProperty(KingdomPlots.PlotX2Property, WorkCell.X);
			Work.SetIntProperty(KingdomPlots.PlotY2Property, WorkCell.Y);
			BeforeWear = KingdomLodgingRules.CondemnedWearPercent - 1;
			AfterWear = KingdomMaterialRules.AddWear(BeforeWear,
				KingdomSubsidenceRules.RolledRuinIncrement(Settlement, WorkId, (ulong)Now));
			Wear = new r_KingdomWear { Wear = BeforeWear };
			Work.AddPart(Wear);
			int parts = 0;
			foreach (IPart part in Work.PartsList) if (part is r_KingdomWear) parts++;
			Require(parts == 1 && ReferenceEquals(Work.GetPart<r_KingdomWear>(), Wear)
				&& ReferenceEquals(Wear.ParentObject, Work) && Wear.Wear == BeforeWear
				&& !Wear.LifecycleQuarantined && Wear.RepairEffortLeft == 0
				&& Wear.IncidentPhase == 0 && Wear.IncidentId == null && Wear.LeakPhase == 0
				&& Wear.LeakIncidentId == null && !KingdomLodgingRules.IsCondemned(BeforeWear)
				&& KingdomLodgingRules.IsCondemned(AfterWear), "fresh wear does not cross condemnation exactly");
			RequireWorld(); RequireEmptyContents();
			Require(ExactWork() && KingdomPlots.TryReadRect(Work, out KingdomPlotRules.PlotRect rect)
				&& rect.X1 == WorkCell.X && rect.X2 == WorkCell.X
				&& rect.Y1 == WorkCell.Y && rect.Y2 == WorkCell.Y, "synthetic plot changed");
			Survey = KingdomSurvey.Take(Base.Zone, Base.System);
			RequireExactSurvey();
			using (Survey.BindPass())
			{
				Require(KingdomPlots.TryCaptureGlobalLiveIds(new HashSet<string>(StringComparer.Ordinal) { WorkId },
					out Dictionary<string, GameObject> exact) && exact.Count == 1
					&& exact.TryGetValue(WorkId, out GameObject found) && ReferenceEquals(found, Work),
					"work full identity is not globally unique in known live custody");
				Require(Survey.TryBenefits(out _, out string benefitFailure),
					"synthetic fixture physical support reading refused: " + benefitFailure);
				KingdomCatalogueRules.SupportTally support = KingdomSubsidence.ScopedSupports(Base.System, Base.Zone, Survey);
				Require(KingdomSubsidenceRules.SupportedLevel(support, GrowthStage.City, Base.System.Shade) < 50,
					"synthetic work unexpectedly supports City population");
				PublishHome();
			}
			HoldHome();
		}

		private void RequireExactSurvey()
		{
			if (!CompletedHeart)
			{
				Require(Survey.Ground == Base.Zone && Survey.Built.Count == 1
					&& ReferenceEquals(Survey.Built[0], Work) && Survey.PlotRoots.Contains(Work)
					&& Survey.CitizenBodies.Count == 50 && Survey.Settlers.Count == 50,
					"fresh survey does not contain the exact one-work/fifty-body fixture");
				return;
			}
			// The base fixture publishes no named completed-heart property. Its own retained survey,
			// taken after completion and before this synthetic work existed, holds that exact body.
			Require(Base.Survey != null && Base.Survey.Built.Count == 1
				&& GameObject.Validate(Base.Survey.Built[0]),
				"base fixture exposes no single live completed heart by reference");
			GameObject heart = Base.Survey.Built[0];
			int work = 0, hearts = 0, foreign = 0;
			foreach (GameObject item in Survey.Built)
			{
				if (ReferenceEquals(item, Work)) work++;
				else if (ReferenceEquals(item, heart)) hearts++;
				else foreign++;
			}
			Require(Survey.Ground == Base.Zone && work == 1 && hearts == 1 && foreign == 0
				&& Survey.PlotRoots.Contains(Work) && Survey.PlotRoots.Contains(heart)
				&& Survey.CitizenBodies.Count == 50 && Survey.Settlers.Count == 50,
				"fresh survey does not contain the exact owned heart + one-work/fifty-body fixture");
		}

		private void ChooseIdentity()
		{
			List<string> candidates = new List<string>();
			for (int i = 0; i < 1024; i++)
			{
				// WorkStream has a bounded prefix: vary the counter before the long owner identity.
				string id = "native-rung-hut:" + i.ToString(CultureInfo.InvariantCulture) + ":" + Realm + ":" + Settlement;
				Require(id.Length <= 480, "fixture owner identity exceeds bounded work/plot identity");
				if (KingdomSubsidenceRules.RollRuin(Settlement, id, (ulong)Now, GrowthStage.City)) candidates.Add(id);
			}
			Require(candidates.Count > 0, "no selected native work identity within bound");
			Require(KingdomPlots.TryCaptureGlobalLiveIds(
				new HashSet<string>(candidates, StringComparer.Ordinal), out Dictionary<string, GameObject> live),
				"bounded selected-ID live census refused");
			foreach (string id in candidates) if (!live.ContainsKey(id)) { WorkId = id; break; }
			Require(WorkId != null, "all selected native work identities are already held");
		}

		private void PublishHome()
		{
			HomeResident = Base.Bodies[0]; HomeResidentId = Base.ResidentIds[0];
			HomeObjectId = HomeResident.IDIfAssigned;
			KingdomCityState before = null;
			Require(GameObject.Validate(HomeResident) && !string.IsNullOrEmpty(HomeObjectId)
				&& !HomeResident.HasStringProperty(KingdomLodging.HomePlotIdProperty)
				&& !HomeResident.HasIntProperty(KingdomLodging.HomePlotIdProperty)
				&& City.TryReadExact(out before, out _), "initial home authority is not empty/readable");
			string wire = City.SubsidenceModel;
			HomeResident.SetStringProperty(KingdomLodging.HomePlotIdProperty, PlotId);
			KingdomCityState refreshed = KingdomResidents.ReadRoster(Base.System, Base.Zone, Survey, before, Now);
			RequireWorld();
			Require(City.SubsidenceModel == wire && City.TryPublish(refreshed, out _)
				&& City.TryReadExact(out _, out _) && City.ResidentCount == 50
				&& KingdomResidents.OnRollCount(Base.System) == 50, "real roster home publication refused");
			VerifyHome();
		}

		private void VerifyHome()
		{
			Require(City.TryCaptureSubsidenceRoof(HomeResidentId, out KingdomCityBook.SubsidenceRoofRow row)
				&& row.HomeWorkId == KingdomCityRules.StableId(WorkId) && row.ZoneId == Base.Zone.ZoneID
				&& row.Standing == (int)KingdomResidentStanding.Resident && !row.RoofStanding
				&& row.Reached == 0 && row.Warned == 0
				&& Base.System.Bindings.TryReadExact(out KingdomBindingTable bindings, out _)
				&& bindings.TryGet(HomeResidentId, KingdomBindingKind.Resident, out KingdomBinding binding)
				&& binding.ObjectId == HomeObjectId && binding.ZoneId == Base.Zone.ZoneID
				&& ReferenceEquals(Survey.FindBoundBody(HomeObjectId, KingdomBindingKind.Resident), HomeResident)
				&& HomeResident.GetStringProperty(KingdomLodging.HomePlotIdProperty) == PlotId
				&& KingdomCitizenship.BelongsTo(Base.System, HomeResident), "home row/body/binding proof disagrees");
		}

		private void HoldHome()
		{
			RequireWorld(); HeldBrain = HomeResident.Brain;
			Require(GameObject.Validate(Player) && Player.Brain != null && HeldBrain != null
				&& HeldBrain.PartyLeader == null && !HomeResident.IsPlayerLed(), "home resident already held");
			HoldAttempted = true;
			HeldBrain.PartyLeader = Player;
			RequireWorld();
			Require(ReferenceEquals(HomeResident.Brain, HeldBrain) && ReferenceEquals(HeldBrain.PartyLeader, Player)
				&& HomeResident.IsPlayerLed(), "exact temporary home-resident hold refused");
			VerifyHome();
		}

		internal bool TryReleaseHeld(out string failure)
		{
			failure = null;
			if (!HoldAttempted) return true;
			try
			{
				Require(ReferenceEquals(The.Game, Base.Game) && ReferenceEquals(The.Player, Player)
					&& GameObject.Validate(HomeResident) && HomeResident.IDIfAssigned == HomeObjectId
					&& HomeResident.CurrentZone == Base.Zone && ReferenceEquals(HomeResident.Brain, HeldBrain)
					&& ReferenceEquals(HeldBrain.PartyLeader, Player), "temporary home custody changed; retained");
				HeldBrain.PartyLeader = null;
				Require(ReferenceEquals(HomeResident.Brain, HeldBrain) && HeldBrain.PartyLeader == null,
					"temporary home custody did not release exactly");
				HoldAttempted = false; return true;
			}
			catch (Exception error) { failure = "native home hold retained after " + error.GetType().Name; return false; }
		}

		private void RequireWorld()
		{
			Require(Base != null && ReferenceEquals(The.Game, Base.Game) && Base.Game.TimeTicks == Now
				&& ReferenceEquals(The.Player, Player) && ReferenceEquals(Player?.CurrentZone, Base.Zone)
				&& ReferenceEquals(The.ZoneManager?.ActiveZone, Base.Zone)
				&& ReferenceEquals(Base.Game.GetSystem<KingdomSystem>(), Base.System)
				&& ReferenceEquals(Base.System.City, City) && Base.System.CurrentRealmId == Realm
				&& Base.System.CurrentSettlementId == Settlement && Base.System.OwnedZone(Base.Zone.ZoneID),
				"native rung world/owner/clock changed");
		}

		private bool ExactWork()
		{
			if (!GameObject.Validate(Work) || Work.IDIfAssigned != WorkId || Work.Count != 1
				|| Work.CurrentCell != WorkCell || Work.CurrentZone != Base.Zone
				|| Work.InInventory != null || Work.Equipped != null) return false;
			int found = 0;
			foreach (GameObject item in WorkCell.Objects) if (ReferenceEquals(item, Work)) found++;
			return found == 1;
		}

		private static bool EmptyCell(Cell cell)
		{
			if (cell == null || !cell.IsPassable() || !cell.IsEmpty() || cell.HasOpenLiquidVolume()) return false;
			foreach (GameObject item in cell.GetObjects())
				if (!GameObject.Validate(item) || item.IsCreature
					|| KingdomPlots.ReadObject(item) != KingdomPlotRules.GroundKind.Bare) return false;
			return true;
		}

		private void RequireEmptyContents()
		{
			List<GameObject> contents = Work.GetInventoryDirectAndEquipment();
			Require((Work.Inventory == null || Work.Inventory.Objects.Count == 0)
				&& (contents == null || contents.Count == 0), "unexpected work contents retained");
		}

		private static void Require(bool condition, string failure)
		{
			if (!condition) throw new InvalidOperationException(failure ?? "native rung fixture refused");
		}
	}
}
