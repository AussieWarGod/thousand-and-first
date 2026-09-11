using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The far half of the lifecycle: the commissioned job carried to a finished building by real
	/// engine turns, and the real save that a separate cold-load session can be imported from.
	/// </summary>
	internal static partial class KingdomQuickstartLifecycleSteps
	{
		/// <summary>
		/// Reads the exact commissioned job again and asks whether those turns finished it.
		///
		/// <para>A job that is still working is NOT a pass: the row is REFUSED and names the phase
		/// it stalled in and the turns spent, which is what a turn budget that expired looks like.
		/// A completion is only accepted when the production completion conditions themselves hold
		/// -- the same ones Growth/KingdomPlot2.34.EffectsAndFurnishing.cs:16-22 requires: the
		/// job's own output id resolves to a validated object standing on the job's own cell, that
		/// object carries the construction receipt for this job, reads as built, and carries this
		/// design's key.</para>
		/// </summary>
		internal static string Grown(XRLGame Game, Zone Zone, KingdomSystem System, out bool Ok)
		{
			Ok = false;
			KingdomDurableKeyObservation observed = KingdomScenarioDurableState.Observe(JobKey);
			if (observed == null || !observed.HasString || string.IsNullOrEmpty(observed.String))
				return Refuse(GrownStep, "no commissioned job identity; the paid commission step "
					+ "never completed in this game");
			string jobId = observed.String;
			if (!KingdomConstruction.TryRead(out List<KingdomConstructionJob> jobs, out string readFailure))
				return Refuse(GrownStep, readFailure ?? "the construction registry could not be read");
			KingdomConstructionJob job = null;
			foreach (KingdomConstructionJob candidate in jobs)
			{
				if (candidate?.Id != jobId) continue;
				if (job != null) return Refuse(GrownStep, "two registry rows carry the same job identity");
				job = candidate;
			}
			if (job == null)
				return Refuse(GrownStep, "the commissioned job is no longer in the registry");
			if (job.Phase != KingdomConstructionPhase.Complete)
				return Refuse(GrownStep, "the job has not completed: phase=" + job.Phase
					+ "; physical=" + job.PhysicalPhase + "; turns=" + Game.Turns
					+ "; the turn budget expired before this building stood");
			string failure = Standing(Zone, job, out GameObject building);
			if (failure != null) return Refuse(GrownStep, failure);
			Ok = true;
			// The finished work is reported linked to what was paid for: the completed registry
			// row's own identity, and the paid job it fulfils as the commission published it.
			// Where the row kept its identity the two read the same, and saying so plainly is
			// the point -- neither is minted, and neither is omitted when it exists.
			return "native-lifecycle step=engine-turn-build; " + Identities(System)
				+ "; plotId=" + Describe(job.SubjectId) + "; jobId=" + job.Id
				+ "; completedReceiptId=" + job.Id + "; forJobId=" + jobId
				+ "; buildingId=" + Describe(building.IDIfAssigned)
				+ "; building=" + Describe(building.IDIfAssigned)
				+ "; designKey=" + building.GetStringProperty(KingdomUpgrade.BuildKeyProperty)
				+ "; blueprint=" + building.Blueprint + "; built=1"
				+ "; at=" + job.X + "," + job.Y + "; turns=" + Game.Turns;
		}

		/// <summary>
		/// The lifecycle witness wire: the identities and counts this session actually read, for
		/// the cold-load session to compare its own reads against. Every value is read here, now,
		/// from live state -- nothing is taken from the durable receipts except the job identity
		/// that names which job to look at.
		/// </summary>
		private static string Witness(XRLGame Game, Zone Zone, KingdomSystem System, out string Failure)
		{
			Failure = null;
			string jobId = KingdomScenarioDurableState.Observe(JobKey).String;
			if (!KingdomConstruction.TryRead(out List<KingdomConstructionJob> jobs, out string readFailure))
			{ Failure = readFailure ?? "the construction registry could not be read for the witness"; return null; }
			KingdomConstructionJob job = null;
			foreach (KingdomConstructionJob candidate in jobs)
				if (candidate?.Id == jobId) job = candidate;
			if (job == null) { Failure = "the lifecycle job is not in the registry at save time"; return null; }
			if (job.Phase != KingdomConstructionPhase.Complete)
			{ Failure = "the lifecycle job is not complete; there is nothing finished to save"; return null; }
			string standing = Standing(Zone, job, out GameObject building);
			if (standing != null) { Failure = standing; return null; }
			if (!TryStockpile(Zone, out GameObject stockpile, out string stockpileFailure))
			{ Failure = stockpileFailure; return null; }
			if (!KingdomQuickstartBuildCensus.TakeStock(Zone, stockpile, false,
				out var stock, out string stockFailure)) { Failure = stockFailure; return null; }
			var snapshot = new KingdomQuickstartLifecycleSnapshot(Game.GameID, System.RealmId,
				KingdomConstruction.OwnerOf(System), Zone.ZoneID, job.Id, job.SubjectId,
				building.IDIfAssigned, job.TargetKey, job.X, job.Y, Timber(stock),
				KingdomGrowth.CountStoredWater(Zone), Game.Turns);
			if (!KingdomQuickstartLifecycleSnapshotCodec.TryEncode(snapshot, out string wire))
			{ Failure = "the lifecycle witness could not be encoded"; return null; }
			return wire;
		}

		/// <summary>The finished building, re-proved from the job's own recorded identity rather
		/// than from whatever happens to stand nearby.</summary>
		private static string Standing(Zone Zone, KingdomConstructionJob Job, out GameObject Building)
		{
			Building = null;
			if (string.IsNullOrEmpty(Job.OutputId))
				return "the completed job records no output identity";
			Cell cell = Zone.GetCell(Job.X, Job.Y);
			if (cell == null) return "the completed job's own cell is not in this zone";
			foreach (GameObject item in cell.GetObjects())
			{
				if (!GameObject.Validate(item) || item.IDIfAssigned != Job.OutputId) continue;
				if (Building != null)
					return "two objects on the job's cell report the same output identity";
				Building = item;
			}
			if (Building == null)
				return "no object with the job's own output identity stands on its cell";
			if (!ReferenceEquals(Building.CurrentZone, Zone) || Building.CurrentCell != cell)
				return "the built object is not standing in the zone and cell the job recorded";
			if (!KingdomConstruction.HasReceipt(Building, Job))
				return "the built object carries no construction receipt for this job";
			if (Building.GetIntProperty("KingdomBuilt") != 1)
				return "the built object does not read as a finished settlement building";
			if (Building.GetStringProperty(KingdomUpgrade.BuildKeyProperty) != Job.TargetKey)
				return "the built object's design key is not the key that was commissioned";
			return null;
		}

		/// <summary>
		/// A real save of this exact world, with a receipt a later cold-load session can bind to.
		/// Nothing here is synthetic: the engine's own SaveGame writes the files, and the receipt
		/// hashes what it wrote.
		/// </summary>
		internal static string Save(XRLGame Game, Zone Zone, KingdomSystem System, out bool Ok)
		{
			Ok = false;
			if (!Present(JobKey)) return Refuse(SaveStep, "there is no lifecycle job to save around");
			if (!Game.Running || Game.Transient || Game.DontSaveThisIsAReplay
				|| (Game.SaveTask != null && !Game.SaveTask.IsCompleted))
				return Refuse(SaveStep, "this game cannot perform a real save right now");
			string root = KingdomScenarioSaveFiles.Root();
			string directory = KingdomScenarioSaveFiles.SaveDirectory(root, Game.GameID);
			if (Directory.GetDirectories(directory).Length != 0)
				return Refuse(SaveStep, "the save directory already holds unexpected subdirectories");
			foreach (string existing in Directory.GetFiles(directory))
				if (Path.GetFileName(existing) != "Cache.db")
					return Refuse(SaveStep, "the save directory already holds primary or backup evidence");
			// The witness is written BEFORE the save, so what the cold-load session compares
			// against is what this world actually held at the moment it was serialized.
			string wire = Witness(Game, Zone, System, out string witnessFailure);
			if (wire == null) return Refuse(SaveStep, witnessFailure);
			Game.SetStringGameState(KingdomScenarioSaveFiles.SnapshotKey, wire);
			if (!KingdomScenarioDurableState.ProvesExactText(KingdomScenarioSaveFiles.SnapshotKey, wire))
				return Refuse(SaveStep, "the lifecycle save witness did not publish exactly");
			KingdomScenarioSaveFiles.WriteNew(Path.Combine(root, KingdomScenarioSaveFiles.SnapshotFile), wire);
			The.ZoneManager.CheckCached(true, true);
			long turns = Game.Turns;
			Task save = Game.SaveGame("Primary");
			save?.GetAwaiter().GetResult();
			if (!ReferenceEquals(The.Game, Game) || Game.Turns != turns)
				return Refuse(SaveStep, "the saved world changed across serialization");
			string primary = Path.Combine(directory, "Primary.sav.gz");
			string info = Path.Combine(directory, "Primary.json");
			if (!File.Exists(primary) || !File.Exists(info))
				return Refuse(SaveStep, "the save did not produce its primary artifacts");
			using (FileStream file = KingdomScenarioSaveFiles.Open(primary, KingdomScenarioSaveFiles.MaxSaveBytes))
				if (file.ReadByte() != 31 || file.ReadByte() != 139)
					return Refuse(SaveStep, "the primary save is not gzip");
			string primaryHash = KingdomScenarioSaveFiles.HashFile(primary, KingdomScenarioSaveFiles.MaxSaveBytes);
			string infoHash = KingdomScenarioSaveFiles.HashFile(info, 1048576);
			string jobId = KingdomScenarioDurableState.Observe(JobKey).String;
			string receipt = "taf-scenario-save-v1\n" + Game.GameID + "\n" + primaryHash + "\n"
				+ infoHash + "\n" + KingdomScenarioSaveFiles.HashText(wire) + "\n";
			KingdomScenarioSaveFiles.WriteNew(Path.Combine(root, KingdomScenarioSaveFiles.ReceiptFile), receipt);
			if (KingdomScenarioSaveFiles.ReadText(Path.Combine(root, KingdomScenarioSaveFiles.ReceiptFile), 512) != receipt)
				return Refuse(SaveStep, "the save receipt did not persist exactly");
			Ok = true;
			return "native-lifecycle step=save; witness=published; " + Identities(System)
				+ "; saveId=" + Game.GameID + "; jobId=" + jobId + "; real-save=true"
				+ "; turns=" + Game.Turns + "; cold-load=unproved-in-this-session";
		}
	}
}
