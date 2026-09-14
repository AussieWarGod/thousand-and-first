using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using XRL;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomCampHeartNativeChecks
	{
		internal static KingdomCampHeartChainSnapshot CaptureChainSaveWitness(XRLGame Game, Zone Zone,
			string ResidentId, string TrackId, out Dictionary<string, string> Records)
		{
			Records = null;
			Require(Game != null && ReferenceEquals(The.Game, Game) && Zone != null
				&& ReferenceEquals(The.ZoneManager?.ActiveZone, Zone) && !KingdomSurvey.HasBoundPass,
				"higher-heart capture requires its active game/zone and an unbound survey");
			var frame = new Frame(Game, Zone) { System = Game.GetSystem<KingdomSystem>() };
			Require(frame.System != null && frame.System.Founded && frame.System.ClaimedZones.Contains(Zone.ZoneID),
				"higher-heart capture has no founded city claiming this zone");
			long turns = Game.Turns, ticks = Game.TimeTicks;
			Require(KingdomSurvey.TryBindLocalOperation(Zone, frame.System, out var scope, out string failure), failure);
			KingdomCampHeartChainSnapshot snapshot;
			using (scope) snapshot = frame.CaptureChainInPass(ResidentId, TrackId, out Records);
			Require(!KingdomSurvey.HasBoundPass && Game.Turns == turns && Game.TimeTicks == ticks
				&& ReferenceEquals(The.Game, Game), "higher-heart observation changed its clock, game or survey scope");
			return snapshot;
		}

		private sealed partial class Frame
		{
			internal KingdomCampHeartChainSnapshot CaptureChainInPass(string ResidentId, string TrackId,
				out Dictionary<string, string> Records)
			{
				Records = new Dictionary<string, string>(StringComparer.Ordinal);
				Heart = StandingHeart(); HeartId = Heart.IDIfAssigned;
				int rung = KingdomPlots.HeartRung(Zone);
				Require((rung == 3 || rung == 4) && KingdomUpgrade.IsFunctionallyBuilt(Heart)
					&& KingdomUpgrade.DesignKeyOf(Heart) == (rung == 3 ? "heartmoot" : "heartcourt"),
					"higher-heart witness requires a completed moot or court");
				Require(KingdomArchitectureStamper.TryVerifyComplete(Heart, Zone, out string failure), failure);
				Require(KingdomArchitectureStamper.TryExactAnchoredComponent(Heart, Zone,
					StorageRole, out Store, out failure), failure);
				StoreId = Store.IDIfAssigned; StoreCell = Store.CurrentCell; RequireStoreIdentity();
				Require(KingdomArchitectureStamper.TryExactAnchoredComponent(Heart, Zone,
					KingdomPlots.HeartBasinRole, out var basin, out failure)
					&& BasinCapacity(Heart) == (rung == 3 ? "160" : "512"), failure ?? "higher basin capacity differs");
				Require(KingdomConstruction.FindExactId(Zone, TrackId, out var track) == KingdomPhysicalLookupState.Exact
					&& KingdomRoads.IsExactUnpaidTrack(track.CurrentCell, track)
					&& track.GetIntProperty(KingdomRoads.PathStateProperty) == (int)KingdomRoadRules.WearState.Path,
					"higher-heart retained track is absent, changed or ambiguous");
				Require(KingdomConstruction.FindExactId(Zone, ResidentId, out var resident) == KingdomPhysicalLookupState.Exact
					&& KingdomCitizenship.BelongsTo(System, resident) && resident.IsAlive,
					"higher-heart displaced resident is absent, foreign or dead");
				foreach (var item in new[] { Heart, Store, basin, track, resident }) ExactGround(Zone, item);
				RequireBookRow(this);
				JobId = Heart.GetStringProperty(KingdomConstruction.ReceiptProperty);
				Require(KingdomConstruction.TryFind(JobId, out var job) && job != null
					&& KingdomConstruction.Owns(System, Zone, job) && job.OutputId == HeartId
					&& job.TargetKey == KingdomUpgrade.DesignKeyOf(Heart)
					&& job.Route == KingdomConstructionRoute.Improvement && KingdomConstruction.HasReceipt(Heart, job)
					&& job.Phase == KingdomConstructionPhase.Complete && job.PhysicalPhase == KingdomPhysicalPhase.EffectsSettled,
					"higher-heart completed paid receipt differs");
				var survey = KingdomSurvey.ActiveFor(Zone);
				foreach (var root in survey.Built)
					if (KingdomUpgrade.DesignKeyOf(root) == "airwellcourt") ChainProducers.Add(root);
				RequireChainSupportInPass(survey);
				Require(System.City.TryReadExact(out var city, out var fault), "higher-heart city columns are torn: " + fault);
				string jobs = CaptureChainJobs(Records);
				string residents = CaptureChainResidents(city, survey, Records);
				string support = CaptureChainWorks(city, survey, Records);
				var custody = new List<string[]>();
				foreach (var unit in ContentUnits(out _))
					custody.Add(new[] { unit.Id, unit.Blueprint, unit.Holder, ChainNumber(unit.RawCount) });
				string held = ChainFacts("custody", custody, Records);
				var result = new KingdomCampHeartChainSnapshot(Game.GameID, System.RealmId,
					KingdomConstruction.OwnerOf(System), Zone.ZoneID, rung, ChainAnchor(Heart), ChainAnchor(basin),
					ChainAnchor(Store), ChainAnchor(track), resident.IDIfAssigned, JobId, jobs, residents, support, held,
					System.Population, survey.StoredWater, survey.FoodStored, Game.Turns, Game.TimeTicks);
				Require(KingdomCampHeartChainSnapshotCodec.Valid(result), "higher-heart physical witness is malformed");
				return result;
			}

			private string CaptureChainJobs(Dictionary<string, string> Records)
			{
				Require(KingdomConstruction.TryRead(out var jobs, out string failure), failure);
				var rows = new List<string[]>();
				foreach (var job in jobs)
				{
					if (job.OwnerKey != KingdomConstruction.OwnerOf(System)) continue;
					Require(KingdomConstructionRules.TryEncode(new[] { job }, out string wire), "higher paid row cannot encode");
					rows.Add(new[] { job.Id, Convert.ToBase64String(new UTF8Encoding(false, true).GetBytes(wire)) });
				}
				Require(rows.Count >= 3, "higher-heart witness has lost prior paid jobs");
				return ChainFacts("jobs", rows, Records);
			}

			private string CaptureChainResidents(KingdomCityState City, KingdomSurvey Survey,
				Dictionary<string, string> Records)
			{
				var rows = new List<string[]>();
				for (int i = 0; i < City.ResidentCount; i++)
				{
					Require(City.TryResident(i, out var row), "higher-heart resident row missing");
					rows.Add(new[] { "row:" + ChainNumber(row.ResidentId), KingdomResidentDeathCodec.Row(row) });
				}
				foreach (var body in ChainResidentBodies(Survey))
				{
					ExactGround(Zone, body);
					rows.Add(new[] { "body:" + body.IDIfAssigned, body.Blueprint, ChainNumber(KingdomResidents.IdOf(body)),
						ChainNumber(body.CurrentCell.X), ChainNumber(body.CurrentCell.Y),
						body.GetStringProperty(KingdomLodging.HomePlotIdProperty) });
				}
				return ChainFacts("residents", rows, Records);
			}

			private static string ChainFacts(string Domain, List<string[]> Rows, Dictionary<string, string> Records)
			{
				Require(KingdomCampHeartChainFacts.TryCapture(Domain, Rows, out string wire, out string digest),
					"higher-heart " + Domain + " facts are invalid, duplicated or too large");
				Records.Add(Domain, wire); return digest;
			}
			private static string ChainNumber(long Value) => Value.ToString(CultureInfo.InvariantCulture);
			private static KingdomCampHeartChainAnchor ChainAnchor(GameObject Item)
				=> new KingdomCampHeartChainAnchor(Item.IDIfAssigned, Item.CurrentCell.X, Item.CurrentCell.Y);
		}
	}
}
