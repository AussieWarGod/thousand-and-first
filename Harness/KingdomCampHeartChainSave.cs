using System;
using System.Collections.Generic;
using System.IO;
using XRL;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomCampHeartNativeChecks
	{
		internal static string SaveChain(XRLGame Game)
		{
			Require(Retained != null && Retained.Done && !Retained.Saved && ReferenceEquals(Game, The.Game)
				&& ReferenceEquals(Game, Retained.Game) && !KingdomScenarioAdvance.Pending && !KingdomScenarioFrames.Pending,
				"higher-heart save has no exact completed source attempt or already claimed its save");
			Retained.Saved = true;
			return Retained.SaveCompletedChain();
		}

		private sealed partial class Frame
		{
			internal string SaveCompletedChain()
			{
				Require(ChainPhase == 8 && ChainTarget == 4 && Game.Running && !Game.Transient
					&& !Game.DontSaveThisIsAReplay && (Game.SaveTask == null || Game.SaveTask.IsCompleted),
					"higher-heart save requires complete rung-four next-day recovery");
				CheckChainComplete();
				PreflightChainNextWork(false);
				string root = KingdomScenarioSaveFiles.Root();
				string directory = KingdomScenarioSaveFiles.SaveDirectory(root, Game.GameID);
				Require(!KingdomScenarioSaveFiles.LoadPresent()
					&& !File.Exists(Path.Combine(root, KingdomScenarioSaveFiles.ReceiptFile))
					&& !File.Exists(Path.Combine(root, KingdomScenarioSaveFiles.SnapshotFile))
					&& !KingdomNativeRegressionContext.HasAnyState(Game, KingdomScenarioSaveFiles.SnapshotKey)
					&& Directory.GetDirectories(directory).Length == 0, "higher-heart save evidence is not empty");
				foreach (string file in Directory.GetFiles(directory))
					Require(Path.GetFileName(file) == "Cache.db", "higher-heart primary or backup already exists");
				foreach (string domain in new[] { "jobs", "residents", "support", "custody" })
					Require(!File.Exists(ChainFactsPath(root, domain)), "higher-heart fact evidence already exists");
				var extra = new List<string>();
				Mint(KingdomMaterial.Timber, 1, extra);
				var witness = CaptureChainSaveWitness(Game, Zone, FixtureResidents[0].IDIfAssigned,
					ChainTrackId, out var records);
				Require(witness.Rung == 4 && witness.JobId == ChainJobId && witness.Basin.Id == ChainBasin.IDIfAssigned
					&& witness.Store.Id == StoreId && witness.Track.Id == ChainTrackId,
					"higher-heart witness lost the source chain identities");
				var units = ContentUnits(out var bodies);
				Require(units.Count == 22 && bodies.FindAll(body => body.IDIfAssigned == extra[0]).Count == 1,
					"higher-heart save did not add exactly one next-job timber");
				foreach (var original in ChainBrush) Require(bodies.Contains(original), "higher-heart save replaced original brush");
				Require(KingdomCampHeartChainSnapshotCodec.TryEncode(witness, out string wire), "higher-heart snapshot cannot encode");
				Game.SetStringGameState(KingdomScenarioSaveFiles.SnapshotKey, wire);
				Require(KingdomScenarioDurableState.ProvesExactText(KingdomScenarioSaveFiles.SnapshotKey, wire),
					"higher-heart snapshot did not publish exactly");
				foreach (var record in records) KingdomScenarioSaveFiles.WriteNew(ChainFactsPath(root, record.Key), record.Value);
				KingdomScenarioSaveFiles.WriteNew(Path.Combine(root, KingdomScenarioSaveFiles.SnapshotFile), wire);
				The.ZoneManager.CheckCached(true, true);
				Game.SaveGame("Primary")?.GetAwaiter().GetResult();
				var after = CaptureChainSaveWitness(Game, Zone, witness.ResidentId, witness.Track.Id, out _);
				Require(KingdomCampHeartChainSnapshotCodec.TryEncode(after, out string afterWire) && afterWire == wire,
					"higher-heart physical state or clock changed across serialization");
				string primary = Path.Combine(directory, "Primary.sav.gz");
				using (var file = KingdomScenarioSaveFiles.Open(primary, KingdomScenarioSaveFiles.MaxSaveBytes))
					Require(file.ReadByte() == 31 && file.ReadByte() == 139, "higher-heart primary is not gzip");
				string receipt = "taf-scenario-save-v1\n" + Game.GameID + "\n"
					+ KingdomScenarioSaveFiles.HashFile(primary, KingdomScenarioSaveFiles.MaxSaveBytes) + "\n"
					+ KingdomScenarioSaveFiles.HashFile(Path.Combine(directory, "Primary.json"), 1048576) + "\n"
					+ KingdomScenarioSaveFiles.HashText(wire) + "\n";
				KingdomScenarioSaveFiles.WriteNew(Path.Combine(root, KingdomScenarioSaveFiles.ReceiptFile), receipt);
				Require(KingdomScenarioSaveFiles.ReadText(Path.Combine(root, KingdomScenarioSaveFiles.ReceiptFile), 512) == receipt,
					"higher-heart save receipt changed");
				return "paid-heart-chain-save=true; " + KingdomCampHeartChainLoadFacts.Identity(witness)
					+ "; normal-next-quote=true; outside-final-heart=true; synthetic-next-job-timber=1; brush=21; save=" + Game.GameID
					+ "; snapshot-sha256=" + KingdomScenarioSaveFiles.HashText(wire) + "; physical-state-preserved=true";
			}

			private void PreflightChainNextWork(bool BeforeUpgrades)
			{
				long tick = Game.TimeTicks, turns = Game.Turns;
				int water = Census().StoredWater;
				string custody = KingdomCampHeartSaveSnapshotCodec.CustodyDigest(ContentUnits(out _));
				string jobs = Game.GetStringGameState(KingdomConstruction.RegistryStateKey);
				Require(KingdomScenarioDurableState.ProvesExactText(KingdomConstruction.RegistryStateKey, jobs),
					"higher-heart preflight lacks exact paid-registry state");
				Require(KingdomData.TryGetBuilding("fire", out var entry) && entry.CostDrams == 2,
					"higher-heart next fire design differs");
				Require(KingdomPlots.TryQuoteCommission(System, Zone, entry, null, KingdomPlotRules.PlotSize.None,
					out var quote, out string failure), failure ?? "higher-heart next fire quote refused");
				var checkedRect = quote.Rect;
				if (BeforeUpgrades)
				{
					// A siting hint does not bind the planner. Prove the reserved future worksite
					// directly; the actual completed-heart save and load still use ordinary quotes.
					checkedRect = KingdomCampHeartChainGrid.NextWork;
					Require(KingdomPlotRules.TryInterior(Zone.Width, Zone.Height, out var interior)
						&& KingdomPlotRules.Fits(checkedRect, interior)
						&& !KingdomPlotRules.CrowdsExisting(checkedRect, KingdomPlots.ReadPlots(Zone))
						&& KingdomCampHeartChainGrid.ClearsWaterFootprints(checkedRect)
						&& !new KingdomPlots.GroundGrid(Zone).AnyRefusal(checkedRect),
						"spare next-work lot or its lane is obstructed");
					Require(KingdomPlots.TryPreparePlotPayload(System, Zone, checkedRect, entry.Key, entry.Category, null,
						out var intent, out _, out failure), failure);
					Require(KingdomArchitectureRuntime.TryDecode(intent, out var snapshot, out failure)
						&& KingdomArchitectureRuntime.TryVerifyPhysicalIngressRoutes(Zone, checkedRect, snapshot, out failure), failure);
				}
				Require(KingdomPlots.TryHeartRectFor(Zone, 4, out var final)
					&& !KingdomPlotRules.Overlaps(final, KingdomPlotRules.Reserved(checkedRect)),
					"higher-heart next work consumes future heart ground");
				var tally = new KingdomMaterialTally(); tally.Add(KingdomMaterial.Timber, 1);
				Require(quote.WaterDrams == 2 && quote.MaterialClaim.ToClaimString()
					== new KingdomMaterialDebitCost(tally).ToClaimString() && Game.TimeTicks == tick && Game.Turns == turns,
					"higher-heart next fire quote has unexpected costs or changed time");
				Require(Census().StoredWater == water && KingdomCampHeartSaveSnapshotCodec.CustodyDigest(ContentUnits(out _)) == custody
					&& KingdomScenarioDurableState.ProvesExactText(KingdomConstruction.RegistryStateKey, jobs),
					"higher-heart next fire quote changed payment, custody or paid jobs");
				if (BeforeUpgrades) Require(KingdomScenarioJournal.Append("camp-heart-chain-next-preflight", true,
					"spare-site-preflight=true; ordinary-quote-price=true; outside-final-heart=true; water=2; timber=1; no-debit=true") == null,
					"higher-heart next-job preflight journal unavailable");
			}
			private static string ChainFactsPath(string Root, string Domain)
				=> Path.Combine(Root, "camp-heart-chain-" + Domain + "-facts.txt");
		}
	}
}
