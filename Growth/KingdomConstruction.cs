using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine carrier for every costed construction route. Registry lives in already-serialized
	/// <c>XRLGame.StringGameState</c>; live water/material receipts never do. Each external debit
	/// and projection is bracketed by a persisted phase, and each involved object carries only a
	/// stable receipt property, also already serialized by <c>GameObject</c>.
	/// </summary>
	public static class KingdomConstruction
	{
		public const string RegistryStateKey = "r_TAF_ConstructionJobs";
		public const string ReceiptProperty = "KingdomConstructionReceipt";
		private const int MaxLoadedLookupObjects = 4096;

		private static bool Resolving;

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
				&& observed.TargetKey == Job.TargetKey;
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

		public static KingdomConstructionStartResult TryFundNew(KingdomConstructionJob Job,
			KingdomWaterDebit Water, KingdomMaterialDebit Material,
			out KingdomConstructionJob Published, out string Failure)
		{
			Published = Job;
			Failure = null;
			if (Job == null || Water == null || Material == null
				|| Water.State != KingdomWaterDebitState.Reserved
				|| Material.Reservation.Outcome != KingdomMaterialDebitOutcome.Reserved)
			{
				Water?.Rollback();
				Material?.Cancel();
				Failure = Water != null && Water.Failure != null
					? Water.Failure
					: (Material == null ? "The material receipt is absent." : Material.Reservation.Failure);
				return KingdomConstructionStartResult.Refused;
			}
			if (!TryPublish(Job, out Failure))
			{
				Water.Rollback();
				Material.Cancel();
				return KingdomConstructionStartResult.Refused;
			}
			Published = Job.Copy();
			return Fund(Published, Water, Material, true, out Published, out Failure);
		}

		/// <summary>Retries only claims proved outstanding by an earlier exact receipt.</summary>
		public static KingdomConstructionStartResult TryResumeFunding(KingdomConstructionJob Job,
			Zone Z, KingdomSurvey Survey, out KingdomConstructionJob Updated, out string Failure)
		{
			Updated = Job;
			Failure = null;
			if (Job == null || Z == null || Survey == null || Job.Claims == null || !Job.Claims.Exact
				|| KingdomConstructionRules.ResumeAction(Job) != KingdomConstructionResumeAction.ResumeFunding)
			{
				Failure = "The construction claim is not safe to retry automatically.";
				return KingdomConstructionStartResult.Outstanding;
			}
			KingdomMaterialDebitCost outstanding;
			if (!KingdomMaterialDebitCost.TryParseClaim(Job.Claims.MaterialOutstanding, out outstanding))
			{
				Failure = "The outstanding material claim cannot be read.";
				return KingdomConstructionStartResult.Outstanding;
			}
			KingdomWaterDebit water = Survey.ReserveExactWater(Job.Claims.WaterOutstanding);
			KingdomMaterialDebit material = KingdomMaterials.ReserveComposite(Z, outstanding);
			if (water.State != KingdomWaterDebitState.Reserved
				|| material.Reservation.Outcome != KingdomMaterialDebitOutcome.Reserved)
			{
				water.Rollback();
				material.Cancel();
				Failure = water.Failure ?? material.Reservation.Failure;
				return KingdomConstructionStartResult.Outstanding;
			}
			return Fund(Job.Copy(), water, material, false, out Updated, out Failure);
		}

		private static KingdomConstructionStartResult Fund(KingdomConstructionJob Job,
			KingdomWaterDebit Water, KingdomMaterialDebit Material, bool NewJob,
			out KingdomConstructionJob Updated, out string Failure)
		{
			Updated = Job;
			Failure = null;
			KingdomConstructionClaims beforeWater = Job.Claims.Copy();
			if (!TransitionAndPublish(ref Job, KingdomConstructionPhase.WaterPending, null, out Failure))
			{
				Water.Rollback();
				Material.Cancel();
				Updated = Job;
				return AcceptedResult(Job, NewJob);
			}
			bool waterCommitted = Water.Commit();
			KingdomConstructionClaims measured;
			if (!KingdomConstructionRules.TryApplyWaterAttempt(beforeWater, Water.Amount,
				Water.Spent, Water.Outstanding, Water.Lost, Water.MeasurementExact, out measured))
			{
				Job.Claims.Exact = false;
				TransitionAndPublish(ref Job, KingdomConstructionPhase.InspectionRequired,
					"The exact water receipt could not be reconciled.", out _);
				Material.Cancel();
				Updated = Job;
				Failure = Water.Failure ?? "The exact water receipt could not be reconciled.";
				return KingdomConstructionStartResult.Outstanding;
			}
			Job.Claims = measured;
			KingdomConstructionPhase waterPhase = Water.MeasurementExact
				? KingdomConstructionPhase.WaterSettled
				: KingdomConstructionPhase.InspectionRequired;
			if (!TransitionAndPublish(ref Job, waterPhase, Water.Failure, out Failure))
			{
				Material.Cancel();
				Updated = Job;
				return KingdomConstructionStartResult.Outstanding;
			}
			if (!waterCommitted)
			{
				Material.Cancel();
				Updated = Job;
				Failure = Water.Failure;
				if (Water.MeasurementExact && Water.Spent == 0 && NewJob)
				{
					TransitionAndPublish(ref Job, KingdomConstructionPhase.Compensated, Failure, out _);
					Updated = Job;
					return KingdomConstructionStartResult.Refused;
				}
				return KingdomConstructionStartResult.Outstanding;
			}

			if (!TransitionAndPublish(ref Job, KingdomConstructionPhase.MaterialPending, null, out Failure))
			{
				Material.Cancel();
				Updated = Job;
				return KingdomConstructionStartResult.Outstanding;
			}
			KingdomMaterialDebitResult result = Material.Commit();
			KingdomConstructionClaims materialMeasured;
			if (!KingdomConstructionRules.TryApplyMaterial(Job.Claims, result, out materialMeasured))
			{
				Job.Claims.Exact = false;
				TransitionAndPublish(ref Job, KingdomConstructionPhase.InspectionRequired,
					"The material receipt could not be reconciled.", out _);
				Updated = Job;
				Failure = result.Failure ?? "The material receipt could not be reconciled.";
				return KingdomConstructionStartResult.Outstanding;
			}
			Job.Claims = materialMeasured;
			if (result.Exact)
			{
				if (!TransitionAndPublish(ref Job, KingdomConstructionPhase.Funded, null, out Failure))
				{
					Updated = Job;
					return KingdomConstructionStartResult.Outstanding;
				}
				Updated = Job;
				return KingdomConstructionStartResult.Funded;
			}
			Failure = result.Failure;
			if (!result.Clean)
			{
				// Both partial outcomes carry an exact spent/outstanding split. Retry only that
				// persisted outstanding claim; quarantine outcomes that cannot prove such a split.
				TransitionAndPublish(ref Job, result.Partial
					? KingdomConstructionPhase.Outstanding : KingdomConstructionPhase.InspectionRequired,
					Failure, out _);
				Updated = Job;
				return KingdomConstructionStartResult.Outstanding;
			}

			// This attempt took no material. Return only this attempt's water into its exact vessels.
			if (!TransitionAndPublish(ref Job, KingdomConstructionPhase.CompensationPending,
				Failure, out _))
			{
				Updated = Job;
				return KingdomConstructionStartResult.Outstanding;
			}
			bool rolledBack = Water.Rollback();
			KingdomConstructionClaims afterRollback;
			if (!KingdomConstructionRules.TryApplyWaterAttempt(beforeWater, Water.Amount,
				Water.Spent, Water.Outstanding, Water.Lost, Water.MeasurementExact, out afterRollback))
			{
				Job.Claims.Exact = false;
			}
			else
			{
				// Keep material accounting already merged; this clean result added zero to it.
				afterRollback.MaterialSpent = Job.Claims.MaterialSpent;
				afterRollback.MaterialOutstanding = Job.Claims.MaterialOutstanding;
				afterRollback.MaterialLost = Job.Claims.MaterialLost;
				Job.Claims = afterRollback;
			}
			if (rolledBack && Water.MeasurementExact && NewJob)
			{
				TransitionAndPublish(ref Job, KingdomConstructionPhase.Compensated, Failure, out _);
				Updated = Job;
				return KingdomConstructionStartResult.Refused;
			}
			TransitionAndPublish(ref Job, Water.MeasurementExact
				? KingdomConstructionPhase.Outstanding : KingdomConstructionPhase.InspectionRequired,
				Failure ?? Water.Failure, out _);
			Updated = Job;
			return KingdomConstructionStartResult.Outstanding;
		}

		private static KingdomConstructionStartResult AcceptedResult(KingdomConstructionJob Job, bool NewJob)
		{
			// Reaching this helper means TryPublish already durably accepted the job. Even when the
			// first phase update failed before a debit, that accepted job is the civic commit boundary
			// and the semantic resolver owns it. Only explicit, durably compensated paths return Refused.
			return KingdomConstructionStartResult.Outstanding;
		}

		public static bool BeginProjection(ref KingdomConstructionJob Job, out string Failure)
		{
			return TransitionAndPublish(ref Job, KingdomConstructionPhase.ProjectionPending, null, out Failure);
		}

		public static bool FinishProjection(ref KingdomConstructionJob Job, bool Success,
			bool Working, string Failure = null)
		{
			string ignored;
			return TransitionAndPublish(ref Job,
				Success ? (Working ? KingdomConstructionPhase.Working : KingdomConstructionPhase.Complete)
					: KingdomConstructionPhase.Outstanding,
				Failure, out ignored);
		}

		public static bool Complete(ref KingdomConstructionJob Job, string Failure = null)
		{
			string ignored;
			return TransitionAndPublish(ref Job, KingdomConstructionPhase.Complete, Failure, out ignored);
		}

		/// <summary>Quarantines an ambiguous external mutation. No automatic retry may cross it.</summary>
		public static bool Quarantine(ref KingdomConstructionJob Job, string Failure)
		{
			string ignored;
			return TransitionAndPublish(ref Job, KingdomConstructionPhase.InspectionRequired,
				Failure, out ignored);
		}

		/// <summary>Publishes the exact live predecessor identity before work may advance.</summary>
		public static bool UpdateSubject(ref KingdomConstructionJob Job, string SubjectId)
		{
			if (Job == null || string.IsNullOrEmpty(SubjectId)
				|| SubjectId.Length > KingdomConstructionRules.MaxSubjectChars) return false;
			KingdomConstructionJob next = KingdomConstructionRules.Transition(Job, Job.Phase,
				The.Game == null ? Job.UpdatedTick : The.Game.TimeTicks, Job.Failure);
			next.SubjectId = SubjectId;
			string failure;
			if (!TryUpdate(next, out failure)) return false;
			Job = next;
			return true;
		}

		/// <summary>Publishes generated identity before first engine insertion callback.</summary>
		public static bool UpdateOutput(ref KingdomConstructionJob Job, string OutputId)
		{
			if (Job == null || (OutputId != null
				&& OutputId.Length > KingdomConstructionRules.MaxSubjectChars)) return false;
			// Generated identity is a write-once receipt boundary. Once published before an
			// engine callback it may neither be replaced nor cleared: doing either would let a
			// retry bless a different object after an ambiguous Add/Destroy cut.
			if (!string.IsNullOrEmpty(Job.OutputId)
				&& !string.Equals(Job.OutputId, OutputId, StringComparison.Ordinal)) return false;
			KingdomConstructionJob next = KingdomConstructionRules.Transition(Job, Job.Phase,
				The.Game == null ? Job.UpdatedTick : The.Game.TimeTicks, Job.Failure);
			next.OutputId = OutputId;
			string failure;
			if (!TryUpdate(next, out failure)) return false;
			Job = next;
			return true;
		}

		/// <summary>Advances the write-once output slot from an exact generated predecessor
		/// (works/scaffold) to its exact generated final successor. SubjectId retains the old
		/// identity as removal proof; no arbitrary overwrite or second advance is permitted.</summary>
		public static bool UpdateFinalOutput(ref KingdomConstructionJob Job,
			string PredecessorId, string OutputId)
		{
			if (Job == null || string.IsNullOrEmpty(PredecessorId)
				|| string.IsNullOrEmpty(OutputId) || OutputId.Length > KingdomConstructionRules.MaxSubjectChars
				|| Job.OutputId != PredecessorId
				|| (Job.Route != KingdomConstructionRoute.Improvement
					&& Job.SubjectId != PredecessorId)
				|| Job.Phase != KingdomConstructionPhase.ProjectionPending
				|| Job.PhysicalPhase == KingdomPhysicalPhase.FinalOutputSettled
				|| Job.PhysicalPhase == KingdomPhysicalPhase.FinalRemovalPending
				|| Job.PhysicalPhase == KingdomPhysicalPhase.FinalRemoved
				|| Job.PhysicalPhase == KingdomPhysicalPhase.EffectsPending
				|| Job.PhysicalPhase == KingdomPhysicalPhase.EffectsSettled) return false;
			KingdomConstructionJob next = KingdomConstructionRules.Transition(Job, Job.Phase,
				The.Game == null ? Job.UpdatedTick : The.Game.TimeTicks, Job.Failure);
			next.OutputId = OutputId;
			string failure;
			if (!TryUpdate(next, out failure)) return false;
			Job = next;
			return true;
		}

		public static bool UpdatePhysical(ref KingdomConstructionJob Job,
			KingdomPhysicalPhase Phase, int Index, int Amount, int Spilled,
			string ItemId, string DestinationId, string Receipt, string Failure = null)
		{
			if (Job == null || Index < 0 || Index > 4096 || Amount < 0 || Spilled < 0
				|| (ItemId != null && ItemId.Length > KingdomConstructionRules.MaxSubjectChars)
				|| (DestinationId != null && DestinationId.Length > KingdomConstructionRules.MaxSubjectChars)
				|| (Receipt != null && Receipt.Length > KingdomConstructionRules.MaxPhysicalReceiptChars)) return false;
			KingdomConstructionJob next = KingdomConstructionRules.Transition(Job, Job.Phase,
				The.Game == null ? Job.UpdatedTick : The.Game.TimeTicks, Failure ?? Job.Failure);
			next.PhysicalPhase = Phase;
			next.PhysicalIndex = Index;
			next.PhysicalAmount = Amount;
			next.PhysicalSpilled = Spilled;
			next.PhysicalItemId = ItemId;
			next.PhysicalDestinationId = DestinationId;
			next.PhysicalReceipt = Receipt;
			string failure;
			if (!TryUpdate(next, out failure)) return false;
			Job = next;
			return true;
		}

		public static bool UpdateOutbox(ref KingdomConstructionJob Job,
			KingdomConstructionOutbox Outbox)
		{
			if (Job == null || !KingdomConstructionRules.ValidOutbox(Outbox)) return false;
			KingdomConstructionJob next = KingdomConstructionRules.Transition(Job, Job.Phase,
				The.Game == null ? Job.UpdatedTick : The.Game.TimeTicks, Job.Failure);
			next.Outbox = Outbox == null ? null : Outbox.Copy();
			string failure;
			if (!TryUpdate(next, out failure)) return false;
			Job = next;
			return true;
		}

		public static bool Cancel(ref KingdomConstructionJob Job, string Failure = null)
		{
			string ignored;
			return TransitionAndPublish(ref Job, KingdomConstructionPhase.Cancelled, Failure,
				out ignored);
		}

		public static bool UpdateTiming(ref KingdomConstructionJob Job, long StartedTick, long DueTick)
		{
			if (Job == null || StartedTick < 0L || DueTick < StartedTick) return false;
			KingdomConstructionJob next = KingdomConstructionRules.Transition(Job, Job.Phase,
				The.Game == null ? Job.UpdatedTick : The.Game.TimeTicks, Job.Failure);
			next.StartedTick = StartedTick;
			next.DueTick = DueTick;
			string failure;
			if (!TryUpdate(next, out failure)) return false;
			Job = next;
			return true;
		}

		public static bool UpdatePayload(ref KingdomConstructionJob Job, string Payload)
		{
			if (Job == null || (Payload != null
				&& Payload.Length > KingdomConstructionRules.MaxPayloadChars)) return false;
			KingdomConstructionJob next = KingdomConstructionRules.Transition(Job, Job.Phase,
				The.Game == null ? Job.UpdatedTick : The.Game.TimeTicks, Job.Failure);
			next.Payload = Payload;
			string failure;
			if (!TryUpdate(next, out failure)) return false;
			Job = next;
			return true;
		}

		private static bool TransitionAndPublish(ref KingdomConstructionJob Job,
			KingdomConstructionPhase Phase, string Failure, out string PublishFailure)
		{
			long now = The.Game == null ? Job.UpdatedTick : The.Game.TimeTicks;
			KingdomConstructionJob next = KingdomConstructionRules.Transition(Job, Phase, now, Failure);
			if (!TryUpdate(next, out PublishFailure))
			{
				return false;
			}
			Job = next;
			return true;
		}

		public static void Bind(GameObject Object, KingdomConstructionJob Job)
		{
			if (GameObject.Validate(Object) && Job != null)
			{
				Object.SetStringProperty(ReceiptProperty, Job.Id);
			}
		}

		public static bool HasReceipt(GameObject Object, KingdomConstructionJob Job)
		{
			return GameObject.Validate(Object) && Job != null
				&& Object.GetStringProperty(ReceiptProperty) == Job.Id;
		}

		public static KingdomPhysicalLookupState FindExactId(Zone Z, string Id,
			out GameObject Exact)
		{
			Exact = null;
			if (Z == null || string.IsNullOrEmpty(Id)) return KingdomPhysicalLookupState.Absent;
			List<GameObject> loaded;
			if (!TryLoadedZoneObjects(Z, out loaded)) return KingdomPhysicalLookupState.Ambiguous;
			int count = 0;
			for (int i = 0; i < loaded.Count; i++)
			{
				GameObject item = loaded[i];
				if (item.ID != Id) continue;
				count++;
				if (count == 1) Exact = item;
			}
			KingdomPhysicalLookupState state = KingdomConstructionRules.PhysicalLookupState(
				count, Exact != null);
			if (state != KingdomPhysicalLookupState.Exact) Exact = null;
			return state;
		}

		public static KingdomPhysicalLookupState FindReceipt(Zone Z, KingdomConstructionJob Job,
			out GameObject Exact)
		{
			Exact = null;
			if (Z == null || Job == null) return KingdomPhysicalLookupState.Absent;
			List<GameObject> loaded;
			if (!TryLoadedZoneObjects(Z, out loaded)) return KingdomPhysicalLookupState.Ambiguous;
			int count = 0;
			for (int i = 0; i < loaded.Count; i++)
			{
				GameObject item = loaded[i];
				if (!HasReceipt(item, Job)) continue;
				count++;
				if (count == 1) Exact = item;
			}
			KingdomPhysicalLookupState state = KingdomConstructionRules.PhysicalLookupState(
				count, Exact != null);
			if (state != KingdomPhysicalLookupState.Exact) Exact = null;
			return state;
		}

		private static bool TryLoadedZoneObjects(Zone Z, out List<GameObject> Loaded)
		{
			Loaded = null;
			if (Z == null) return false;
			List<GameObject> pending = new List<GameObject>();
			foreach (GameObject root in Z.GetObjects())
			{
				if (!GameObject.Validate(root)) continue;
				if (root.CurrentZone != Z) return false;
				pending.Add(root);
			}
			List<GameObject> loaded = new List<GameObject>();
			HashSet<GameObject> seen = new HashSet<GameObject>();
			while (pending.Count > 0)
			{
				int last = pending.Count - 1;
				GameObject item = pending[last];
				pending.RemoveAt(last);
				if (!GameObject.Validate(item)) continue;
				if (!seen.Add(item) || loaded.Count >= MaxLoadedLookupObjects) return false;
				loaded.Add(item);
				Inventory inventory = item.Inventory;
				if (inventory == null) continue;
				for (int i = 0; i < inventory.Objects.Count; i++)
					pending.Add(inventory.Objects[i]);
			}
			Loaded = loaded;
			return true;
		}

		public static KingdomPhysicalLookupState FindSubject(Zone Z, KingdomConstructionJob Job,
			out GameObject Exact)
		{
			Exact = null;
			if (Z == null || Job == null) return KingdomPhysicalLookupState.Absent;
			if (!string.IsNullOrEmpty(Job.SubjectId))
			{
				KingdomPhysicalLookupState subject = FindExactId(Z, Job.SubjectId, out Exact);
				if (subject != KingdomPhysicalLookupState.Absent) return subject;
			}
			return FindReceipt(Z, Job, out Exact);
		}

		/// <summary>
		/// Always-running claimed-zone semantic step. Root calls this independently of settler
		/// arrivals. It resumes exact outstanding funding, retries funded projections, and advances
		/// every legacy or receipt-bearing plot from absolute world ticks.
		/// </summary>
		public static void OnSettlementPass(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (Resolving || System == null || !System.Founded || Z == null || Survey == null
				|| !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			Resolving = true;
			try
			{
				List<KingdomConstructionJob> jobs;
				string fault;
				if (!TryRead(out jobs, out fault))
				{
					KingdomLog.Log("construction: " + fault);
					return;
				}
				string owner = OwnerOf(System);
				List<GameObject> plots = new List<GameObject>();
				foreach (GameObject item in Z.GetObjects())
				{
					if (item.GetPart<r_KingdomPlotWorks>() != null) plots.Add(item);
				}
				for (int i = 0; i < plots.Count; i++)
				{
					GameObject plot = plots[i];
					r_KingdomPlotWorks works = plot.GetPart<r_KingdomPlotWorks>();
					if (works == null) continue;
					string receipt = plot.GetStringProperty(ReceiptProperty);
					bool mayAdvance = string.IsNullOrEmpty(receipt);
					for (int j = 0; !mayAdvance && j < jobs.Count; j++)
					{
						KingdomConstructionJob carried = jobs[j];
						mayAdvance = carried.Id == receipt && carried.OwnerKey == owner
							&& carried.ZoneId == Z.ZoneID
							&& !KingdomConstructionRules.IsTerminal(carried.Phase);
					}
					if (mayAdvance) KingdomPlots.Advance(works, The.Game.TimeTicks);
				}

				// Plot completion may have updated a row. Never dispatch the stale pre-advance copy.
				if (!TryRead(out jobs, out fault))
				{
					KingdomLog.Log("construction: " + fault);
					return;
				}
				for (int i = 0; i < jobs.Count; i++)
				{
					KingdomConstructionJob job = jobs[i];
					if (job.OwnerKey != owner || job.ZoneId != Z.ZoneID)
					{
						continue;
					}
					if (KingdomConstructionRules.IsTerminal(job.Phase))
					{
						if (job.Compacted) continue;
						// Every complete route gets one physical-only inspection so a save between
						// Complete and outbox publication can reconstruct route-owned frozen content.
						if (job.Phase == KingdomConstructionPhase.Complete)
							InspectProjection(System, Z, job);
						if (!TryFind(job.Id, out job) || job.Compacted) continue;
						if (job.Phase == KingdomConstructionPhase.Complete && job.Outbox == null
							&& job.Route == KingdomConstructionRoute.RoadPaving)
							KingdomCeremony.EnsureRoadPavedFromReceipt(System, ref job);
						else if (job.Phase != KingdomConstructionPhase.Complete && job.Outbox == null)
							KingdomCeremony.EnsureTerminalClosed(System, ref job);
						if (TryFind(job.Id, out job) && !job.Compacted && job.Outbox != null
							&& !KingdomConstructionRules.OutboxSettled(job.Outbox))
							KingdomCeremony.DispatchPending(System, ref job);
						continue;
					}
					KingdomConstructionResumeAction action = KingdomConstructionRules.ResumeAction(job);
					if (action == KingdomConstructionResumeAction.ResumeFunding)
					{
						KingdomConstructionStartResult resumed = TryResumeFunding(job, Z, Survey, out job, out fault);
						if (resumed != KingdomConstructionStartResult.Funded) continue;
						action = KingdomConstructionResumeAction.RetryProjection;
					}
					if (action == KingdomConstructionResumeAction.RetryProjection)
					{
						RetryProjection(System, Z, job);
					}
					else if (action == KingdomConstructionResumeAction.Inspect
						&& (job.Phase == KingdomConstructionPhase.WaterPending
							|| job.Phase == KingdomConstructionPhase.MaterialPending))
					{
						string diagnostic = KingdomConstructionRules.InterruptedFundingDiagnostic(job.Phase);
						TransitionAndPublish(ref job, KingdomConstructionPhase.InspectionRequired,
							diagnostic, out fault);
					}
					else if (action == KingdomConstructionResumeAction.AdvanceWork
						|| (action == KingdomConstructionResumeAction.Inspect
							&& job.Phase == KingdomConstructionPhase.ProjectionPending))
					{
						InspectProjection(System, Z, job);
					}
				}
			}
			finally
			{
				Resolving = false;
			}
		}

		private static void RetryProjection(KingdomSystem System, Zone Z, KingdomConstructionJob Job)
		{
			switch (Job.Route)
			{
			case KingdomConstructionRoute.CommissionScaffold:
				KingdomCommission.RetryConstruction(System, Z, Job);
				break;
			case KingdomConstructionRoute.PlanScaffold:
				KingdomPlanMarker.RetryConstruction(System, Z, Job);
				break;
			case KingdomConstructionRoute.PlotCommission:
			case KingdomConstructionRoute.PlotPlan:
				KingdomPlots.RetryConstruction(System, Z, Job);
				break;
			case KingdomConstructionRoute.SocketBuild:
			case KingdomConstructionRoute.SocketConvert:
			case KingdomConstructionRoute.SocketRedress:
				KingdomSocket.RetryConstruction(System, Z, Job);
				break;
			case KingdomConstructionRoute.Improvement:
				KingdomUpgrade.RetryConstruction(System, Z, Job);
				break;
			case KingdomConstructionRoute.RoadPaving:
				KingdomRoads.RetryConstruction(System, Z, Job);
				break;
			case KingdomConstructionRoute.WearRepair:
				KingdomWear.RetryConstruction(System, Z, Job);
				break;
			case KingdomConstructionRoute.Strike:
				KingdomMaterials.RetryConstruction(System, Z, Job);
				break;
			}
		}

		private static void InspectProjection(KingdomSystem System, Zone Z,
			KingdomConstructionJob Job)
		{
			switch (Job.Route)
			{
			case KingdomConstructionRoute.CommissionScaffold:
				KingdomCommission.InspectConstruction(System, Z, Job);
				break;
			case KingdomConstructionRoute.PlanScaffold:
				KingdomPlanMarker.InspectConstruction(System, Z, Job);
				break;
			case KingdomConstructionRoute.PlotCommission:
			case KingdomConstructionRoute.PlotPlan:
				KingdomPlots.InspectConstruction(System, Z, Job);
				break;
			case KingdomConstructionRoute.SocketBuild:
			case KingdomConstructionRoute.SocketConvert:
			case KingdomConstructionRoute.SocketRedress:
				KingdomSocket.InspectConstruction(System, Z, Job);
				break;
			case KingdomConstructionRoute.Improvement:
				KingdomUpgrade.InspectConstruction(System, Z, Job);
				break;
			case KingdomConstructionRoute.RoadPaving:
				KingdomRoads.InspectConstruction(System, Z, Job);
				break;
			case KingdomConstructionRoute.WearRepair:
				KingdomWear.InspectConstruction(System, Z, Job);
				break;
			case KingdomConstructionRoute.Strike:
				KingdomMaterials.InspectConstruction(System, Z, Job);
				break;
			}
		}
	}
}
