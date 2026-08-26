using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomConstruction
	{
		public static string OwnerOf(KingdomSystem System)
		{
			return System == null ? null : KingdomConstructionRules.OwnerKey(
				System.KingdomFactionName, System.FoundedTick, System.SeatName);
		}

		public static KingdomConstructionJob NewJob(KingdomSystem System, Zone Z,
			KingdomConstructionRoute Route, Cell Cell, GameObject Subject, string TargetKey,
			string Payload, int Water, KingdomMaterialDebitCost Material, long StartedTick = 0L,
			long DueTick = 0L)
		{
			long now = The.Game == null ? 0L : The.Game.TimeTicks;
			return new KingdomConstructionJob
			{
				Id = Guid.NewGuid().ToString("N"),
				OwnerKey = OwnerOf(System),
				ZoneId = Z == null ? null : Z.ZoneID,
				Route = Route,
				Phase = KingdomConstructionPhase.Published,
				Projection = KingdomConstructionRules.ProjectionFor(Route),
				X = Cell == null ? -1 : Cell.X,
				Y = Cell == null ? -1 : Cell.Y,
				SubjectId = GameObject.Validate(Subject) ? Subject.ID : null,
				SourceId = GameObject.Validate(Subject) ? Subject.ID : null,
				TargetKey = TargetKey,
				Payload = Payload,
				CreatedTick = now,
				StartedTick = StartedTick > 0L ? StartedTick : now,
				DueTick = DueTick > 0L ? DueTick : now,
				UpdatedTick = now,
				Revision = 1,
				Claims = KingdomConstructionRules.NewClaims(Water, Material)
			};
		}

		public static bool TryRead(out List<KingdomConstructionJob> Jobs, out string Failure)
		{
			Jobs = null;
			Failure = null;
			if (The.Game == null)
			{
				Failure = "The game has no durable construction store.";
				return false;
			}
			string written = The.Game.GetStringGameState(RegistryStateKey, null);
			if (string.IsNullOrEmpty(written))
			{
				Jobs = new List<KingdomConstructionJob>();
				return true;
			}
			if (!KingdomConstructionRules.TryDecode(written, out Jobs))
			{
				Failure = "The durable construction registry cannot be read. It was left untouched.";
				return false;
			}
			return true;
		}

		public static bool TryPublish(KingdomConstructionJob Job, out string Failure)
		{
			Failure = null;
			List<KingdomConstructionJob> jobs;
			if (!KingdomConstructionRules.ValidJob(Job) || !TryRead(out jobs, out Failure))
			{
				Failure = Failure ?? "The construction job is not valid.";
				return false;
			}
			int active = 0;
			for (int i = 0; i < jobs.Count; i++)
			{
				if (jobs[i].Id == Job.Id)
				{
					Failure = "The construction receipt already exists.";
					return false;
				}
				if (!jobs[i].Compacted) active++;
			}
			if (KingdomConstructionRules.CapacityInspectionRequired(jobs.Count, active))
			{
				const string diagnostic = "Construction replay-proof capacity is exhausted. This receipt is InspectionRequired; no debit or projection was attempted.";
				KingdomConstructionJob inspection = KingdomConstructionRules.Transition(Job,
					KingdomConstructionPhase.InspectionRequired,
					The.Game == null ? Job.UpdatedTick : The.Game.TimeTicks, diagnostic);
				jobs.Add(inspection);
				if (!TryWrite(jobs, out Failure))
				{
					Failure = diagnostic + " The diagnostic row itself could not be retained.";
					return false;
				}
				Failure = diagnostic;
				return false;
			}
			jobs.Add(Job.Copy());
			return TryWrite(jobs, out Failure);
		}

		public static bool TryUpdate(KingdomConstructionJob Job, out string Failure)
		{
			Failure = null;
			List<KingdomConstructionJob> jobs;
			if (!KingdomConstructionRules.ValidJob(Job) || !TryRead(out jobs, out Failure))
			{
				Failure = Failure ?? "The construction job update is not valid.";
				return false;
			}
			for (int i = 0; i < jobs.Count; i++)
			{
				if (jobs[i].Id != Job.Id)
				{
					continue;
				}
				if (!KingdomConstructionRules.ValidRegistryUpdate(jobs[i], Job))
				{
					Failure = "The construction receipt changed before its update could publish.";
					return false;
				}
				jobs[i] = Job.Copy();
				return TryWrite(jobs, out Failure);
			}
			Failure = "The construction receipt is absent.";
			return false;
		}

		public static bool TryFind(string Id, out KingdomConstructionJob Job)
		{
			Job = null;
			List<KingdomConstructionJob> jobs;
			string failure;
			if (string.IsNullOrEmpty(Id) || !TryRead(out jobs, out failure)) return false;
			for (int i = 0; i < jobs.Count; i++)
			{
				if (jobs[i].Id == Id)
				{
					Job = jobs[i];
					return true;
				}
			}
			return false;
		}

		/// <summary>Re-reads one exact revision after callbacks before another mutation may begin.</summary>
		public static bool IsCurrent(KingdomConstructionJob Job)
		{
			KingdomConstructionJob observed;
			return Job != null && TryFind(Job.Id, out observed)
				&& observed.Revision == Job.Revision && observed.OwnerKey == Job.OwnerKey
				&& observed.ZoneId == Job.ZoneId && observed.Route == Job.Route
				&& observed.Phase == Job.Phase && observed.SubjectId == Job.SubjectId
				&& observed.SourceId == Job.SourceId && observed.OutputId == Job.OutputId
				&& observed.PhysicalPhase == Job.PhysicalPhase
				&& observed.TargetKey == Job.TargetKey
				&& observed.BuildTruthSchema == Job.BuildTruthSchema
				&& observed.BuildHasPlot == Job.BuildHasPlot
				&& observed.BuildFrontier == Job.BuildFrontier
				&& observed.BuildDefence == Job.BuildDefence;
		}

		/// <summary>Exact realm, founding, settlement and zone ownership for one registry row.</summary>
		public static bool Owns(KingdomSystem System, Zone Z, KingdomConstructionJob Job)
		{
			if (The.Game == null || System == null || !System.Founded || Z == null || Job == null
				|| !ReferenceEquals(The.Game.RequireSystem<KingdomSystem>(), System)
				|| System.ClaimedZones == null || !System.ClaimedZones.Contains(Z.ZoneID)) return false;
			string owner = OwnerOf(System);
			return !string.IsNullOrEmpty(owner) && Job.OwnerKey == owner && Job.ZoneId == Z.ZoneID;
		}

		public static bool CanSupersedeTerminalReceipt(KingdomSystem System, Zone Z,
			GameObject Object, KingdomConstructionJob Job)
		{
			if (!GameObject.Validate(Object) || Object.CurrentZone != Z || !Owns(System, Z, Job))
				return false;
			string receipt = Object.GetStringProperty(ReceiptProperty);
			return KingdomConstructionRules.CanSupersedeTerminal(Job, OwnerOf(System), Z.ZoneID,
				receipt, Object.ID);
		}

		/// <summary>Copies current-owner active rows, failing closed with no partial list.</summary>
		public static bool TryOwnedActive(KingdomSystem System, Zone Z,
			out List<KingdomConstructionJob> Active)
		{
			Active = null;
			List<KingdomConstructionJob> jobs;
			string failure;
			if (System == null || Z == null || !TryRead(out jobs, out failure)
				|| string.IsNullOrEmpty(OwnerOf(System))) return false;
			List<KingdomConstructionJob> active = new List<KingdomConstructionJob>();
			for (int i = 0; i < jobs.Count; i++)
			{
				if (Owns(System, Z, jobs[i])
					&& !KingdomConstructionRules.IsTerminal(jobs[i].Phase))
				{
					active.Add(jobs[i].Copy());
				}
			}
			Active = active;
			return true;
		}

		/// <summary>Fail-closed active-route probe used to prevent two jobs claiming one route.</summary>
		public static bool HasActive(KingdomSystem System, Zone Z, KingdomConstructionRoute Route)
		{
			List<KingdomConstructionJob> jobs;
			if (!TryOwnedActive(System, Z, out jobs)) return true;
			for (int i = 0; i < jobs.Count; i++)
			{
				KingdomConstructionJob job = jobs[i];
				if (job.Route == Route) return true;
			}
			return false;
		}

		public static bool HasActiveSubject(KingdomSystem System, Zone Z,
			KingdomConstructionRoute Route, GameObject Subject)
		{
			if (!GameObject.Validate(Subject)) return true;
			List<KingdomConstructionJob> jobs;
			if (!TryOwnedActive(System, Z, out jobs)) return true;
			for (int i = 0; i < jobs.Count; i++)
			{
				KingdomConstructionJob job = jobs[i];
				if (job.Route == Route && job.SubjectId == Subject.ID) return true;
			}
			return false;
		}

		/// <summary>Fail-closed reservation probe for a projection whose object does not exist yet.</summary>
		public static bool HasActiveAt(KingdomSystem System, Zone Z, Cell Cell)
		{
			if (Cell == null || Cell.ParentZone != Z) return true;
			List<KingdomConstructionJob> jobs;
			if (!TryOwnedActive(System, Z, out jobs)) return true;
			for (int i = 0; i < jobs.Count; i++)
			{
				if (jobs[i].X == Cell.X && jobs[i].Y == Cell.Y) return true;
			}
			return false;
		}

		/// <summary>
		/// Whether an object's receipt belongs to active work of the current founding. A malformed
		/// registry blocks; a bounded-away terminal row or a row from an old founding does not.
		/// </summary>
		public static bool ReceiptBlocksCurrent(GameObject Object)
		{
			if (!GameObject.Validate(Object)) return false;
			string receipt = Object.GetStringProperty(ReceiptProperty);
			if (string.IsNullOrEmpty(receipt)) return false;
			List<KingdomConstructionJob> jobs;
			string failure;
			if (!TryRead(out jobs, out failure) || The.Game == null) return true;
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			Zone zone = Object.CurrentZone;
			for (int i = 0; i < jobs.Count; i++)
			{
				if (jobs[i].Id != receipt) continue;
				return Owns(system, zone, jobs[i])
					&& !KingdomConstructionRules.IsTerminal(jobs[i].Phase);
			}
			return false;
		}

		private static bool TryWrite(IList<KingdomConstructionJob> Jobs, out string Failure)
		{
			Failure = null;
			string written;
			if (!KingdomConstructionRules.TryEncode(Jobs, out written))
			{
				Failure = "The durable construction registry is full or invalid.";
				return false;
			}
			The.Game.SetStringGameState(RegistryStateKey, written);
			if (The.Game.GetStringGameState(RegistryStateKey, null) != written)
			{
				Failure = "The durable construction registry did not retain its update.";
				return false;
			}
			return true;
		}

	}
}
