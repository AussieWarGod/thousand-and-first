using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomPlots
	{
		/// <summary>
		/// The founding heart after a rung climb replaced its root, proved by the improvement's
		/// own receipt chain (issue #162, option 3 of the ruling).
		///
		/// <para>WHY THE TERMINAL CANNOT SAY THIS. The terminal record is a FIRST-GENERATION
		/// historical record: it truthfully describes the root the founding rite raised, and its
		/// whole witness set -- blueprint, build key, rect, footprint, roof, architecture intent,
		/// staff and display truth -- is bound to the frozen founding stake decoded from the
		/// receipt. A later design cannot be described by it without inventing durable state, and
		/// no phase in its ladder is true of a root it never created. So the terminal stays what
		/// it is, and the CHAIN is proved here instead.</para>
		///
		/// <para>WHERE THE DESIGN IS WITNESSED NOW, DELIBERATELY. Under this ruling the
		/// successor's design is witnessed by the improvement receipt chain and by the component
		/// census that retagged it, not by the founding stake. That is a documented RELOCATION of
		/// the design witness, decided by root, not a reduction: the successor still has to be the
		/// exact output of a completed improvement whose subject was the retired root, carrying
		/// that job's own receipt and its removal proof.</para>
		///
		/// <para>NO RESERVATION IS ASKED FOR. The heart's reservation store is keyed by
		/// deterministic role identities and refuses any row whose id is not
		/// StableId(transaction, zone, role), so a successor cannot be named in it at all. An
		/// earlier draft demanded one at the settle and thereby refused every heart climb; the
		/// clause is withdrawn, and the chain rests on the receipt records instead.</para>
		///
		/// <para>ORDERING, AND WHAT IT CANNOT PROMISE. The daily seal poll and the settlement pass
		/// are both EndTurnEvent handlers on game systems, and the engine dispatches them in the
		/// order the systems were added to XRLGame.Systems -- first RequireSystem call wins, which
		/// is a property of the save, not of this source. The handover destroys the predecessor
		/// and completes the receipt inside one synchronous call, so a poll sees either the old
		/// root standing or a completed climb this chain binds; the seal's witness asks the same
		/// chain, so a bound climb is witnessed rather than reported. THE EXPOSURE IS NOT A
		/// ONE-DAY WINDOW: if a step between the destroy and the completion refuses -- the rung
		/// failing to settle, a component retirement, a torn survey, or the vetoed-removal branch
		/// -- the receipt stays non-terminal and this chain refuses for as long as that stands,
		/// because it binds only a COMPLETED improvement. That is deliberate: a rung that never
		/// settled is not a heart standing. The seal classifies that case through the settlement's
		/// own inspection state instead of reporting a malformed root
		/// (KingdomSealPendingRules.ClimbUnderInspection), so an inspection is said once and
		/// waited on rather than raised every day.</para>
		///
		/// <para>IDENTITY, NEVER POSITION. Nothing here looks at what stands on the sealed cell. A
		/// foreign heart-shaped plot on that ground proves nothing, because the chain is read from
		/// the retired identity outward: the sealed terminal names it, the construction registry
		/// names exactly one improvement job that retired it, that job names the successor, and
		/// the successor carries the job's receipt and the removal proof for that same identity.
		/// Read-only, fail-closed on every unknown, and it writes nothing: the lawful writer is
		/// the settle.</para>
		/// </summary>
		private static bool TryChainedFoundingHeartRoot(Zone Z, FoundingHeartContext Context,
			out GameObject Successor)
		{
			Successor = null;
			KingdomFoundingHeartPlan plan = Context?.Plan;
			if (Z == null || !KingdomFoundingHeartRules.Valid(plan)
				|| !KingdomFoundingHeartTerminalRules.TryDecode(
					Z.GetZoneProperty(FoundingHeartTerminalProperty, null), out var prior))
				return HeartRefused("chain: bound identity");
			string retired = prior.FinalId;
			KingdomConstructionJob job;
			GameObject successor;
			int jobs;
			int objects;
			if (!TryImprovementSuccessorOf(retired, out job, out successor, out jobs, out objects))
				return HeartRefused("chain: improvement lookup: retired=" + retired
					+ "; jobs naming it=" + jobs + "; live outputs=" + objects
					+ "; output=" + (job == null ? "(none)" : job.OutputId)
					+ "; phase=" + (job == null ? "(none)" : job.Phase.ToString()));
			bool receipt = KingdomConstruction.HasReceipt(successor, job);
			if (!receipt) return HeartRefused("chain: receipt: job=" + job.Id
				+ "; output=" + job.OutputId);
			bool removal = r_KingdomScaffold.HasRemovalProof(successor, job.SubjectId);
			if (!removal) return HeartRefused("chain: removal proof: subject=" + job.SubjectId);
			// Custody. THE RECEIPTS CARRY IT; THE STAMP CAN ONLY CONTRADICT IT. The construction
			// receipt the successor carries is the job's own durable receipt for exactly this
			// output, and the scaffold removal proof is the durable successor-side record naming
			// the retired identity; those two are the link, and both were proved above. The
			// predecessor STAMP is written by the plot finish route alone
			// (KingdomPlot2.31.FinishOutput:135) and the improvement route never writes it, so an
			// absent stamp says nothing -- demanding it refused every real climb (native run 19).
			// Present, it must name the retired identity: a successor stamped with some other
			// predecessor did not replace this root. The finishing transaction's PlotFinalRoot
			// custody key would have been a durable custody row, but it is retired when the job
			// settles (KingdomPlot2.34.EffectsAndFurnishing) and cannot be read here at all.
			string stamp = successor.GetStringProperty(PlotFinalPredecessorProperty);
			bool custody = successor.IDIfAssigned == job.OutputId
				&& KingdomFoundingHeartChainRules.CorroboratesRetired(stamp, retired)
				&& KingdomConstruction.HasReceipt(successor, job);
			if (!custody) return HeartRefused("chain: custody corroboration: successor="
				+ successor.IDIfAssigned + "; output=" + job.OutputId + "; predecessor stamp="
				+ (string.IsNullOrEmpty(stamp) ? "(absent)" : stamp) + "; retired=" + retired);
			if (!KingdomFoundingHeartChainRules.BindsGround(job.OutputId,
				FoundingHeartFinalId(plan), prior, retired, job.OutputId, receipt, removal,
				custody))
				return HeartRefused("chain: binds ground: prior final=" + prior.FinalId
					+ "; prior predecessor=" + prior.PredecessorId + "; retired=" + retired
					+ "; output=" + job.OutputId + "; plan final=" + FoundingHeartFinalId(plan));
			// The retirement authority is asked LAST and about the identity the chain named, so a
			// chain that proved itself still cannot stand on a heart whose seal, reservations,
			// roster or retired custody do not.
			if (!ExactFoundingHeartRetiredAuthority(Z, retired,
				KingdomFoundingHeartRetiredGeneration.Final, out _))
				return HeartRefused("chain: retirement authority: retired=" + retired);
			Successor = successor;
			return true;
		}

		/// <summary>
		/// The same chain, asked by work-row identity instead of by recovery: the seal's spatial
		/// witness holds a row whose work id is the FOLD of an object identity
		/// (Simulation/City/KingdomCity.z09 derives it with KingdomCityRules.StableId), and after
		/// a climb that identity is the retired root. This re-links the row and answers with the
		/// successor the chain proves -- the very same proof the recovery path runs, called
		/// through the very same function, so the two sites cannot drift.
		///
		/// <para>It decides nothing about position. The caller matches the successor against its
		/// own anchor cell exactly as it always did, so this only ever ADDS a binding that the
		/// records already prove, and a root that is simply gone still refuses because no
		/// completed improvement names it.</para>
		/// </summary>
		internal static bool TryChainedWorkSuccessor(Zone Z, int RowWorkId,
			out GameObject Successor)
		{
			Successor = null;
			if (Z == null || RowWorkId == 0) return false;
			if (!KingdomFoundingHeartRules.TryDecode(
					Z.GetZoneProperty(FoundingHeartReceiptProperty, null), out var plan)
				|| !KingdomFoundingHeartRules.Complete(plan) || plan.ZoneId != Z.ZoneID)
				return false;
			if (!KingdomFoundingHeartTerminalRules.TryDecode(
					Z.GetZoneProperty(FoundingHeartTerminalProperty, null), out var prior)
				|| Simulation.City.KingdomCityRules.StableId(prior.FinalId) != RowWorkId)
				return false;
			if (!TryReadFoundingHeartContext(Z, plan, out FoundingHeartContext context))
				return false;
			return TryChainedFoundingHeartRoot(Z, context, out Successor);
		}

		/// <summary>
		/// Whether this work row's root is absent because a climb is IN FLIGHT or UNDER
		/// INSPECTION rather than because the root is gone.
		///
		/// <para>The handover destroys the predecessor before the receipt completes
		/// (<c>KingdomUpgrade.25.HandoverRemoval</c> settles the rung first on purpose, so a rung
		/// that cannot settle leaves the receipt non-terminal for the ordinary recovery path). If
		/// any step between those two points refuses, the job stays non-terminal -- quarantined,
		/// outstanding, or inspection-required -- and the chain refuses FOREVER, because it only
		/// binds a completed improvement. Recovery is right to refuse: a rung that never settled
		/// is not a heart standing. But the seal must not keep reporting a malformed root for a
		/// climb the settlement itself already knows is under inspection, so this says which case
		/// it is and the witness classifies it accordingly.</para>
		///
		/// <para>Read-only and fail-closed: unknown reads as "not pending", which leaves the
		/// stricter answer in place.</para>
		/// </summary>
		internal static bool HasPendingClimb(Zone Z, int RowWorkId)
		{
			if (Z == null || RowWorkId == 0) return false;
			if (!KingdomFoundingHeartTerminalRules.TryDecode(
					Z.GetZoneProperty(FoundingHeartTerminalProperty, null), out var prior)
				|| Simulation.City.KingdomCityRules.StableId(prior.FinalId) != RowWorkId)
				return false;
			if (!KingdomConstruction.TryRead(out List<KingdomConstructionJob> jobs, out _)
				|| jobs == null) return false;
			KingdomConstructionJob found = null;
			int named = 0;
			for (int i = 0; i < jobs.Count; i++)
			{
				KingdomConstructionJob row = jobs[i];
				if (row == null || row.Route != KingdomConstructionRoute.Improvement
					|| row.SubjectId != prior.FinalId) continue;
				named++;
				found = row;
			}
			// Exactly one job, and it has not completed: the climb is still owed an outcome.
			// A completed job is not pending, and no job at all is not pending either -- that is
			// a root that is simply gone, and it must stay malformed.
			bool pending = named == 1 && found.Phase != KingdomConstructionPhase.Complete
				&& found.Phase != KingdomConstructionPhase.Cancelled;
			AnnounceClimbUnderInspection(Z, prior.FinalId, found, pending);
			return pending;
		}

		/// <summary>
		/// The founder is told ONCE that a heart's climb is stuck, and the saying is taken back
		/// the moment it is not.
		///
		/// <para>A stuck climb is a permanent refusal: the seal reports the settlement as under
		/// inspection rather than malformed, which is correct, but it also dedupes its own line
		/// and the settlement's daily one is suppressed -- so without this the condition would be
		/// permanent AND silent. The standard once-only shape applies: a zone-side flag set the
		/// first time the case is classified, cleared when the job turns terminal, so a climb that
		/// sticks twice is said twice and one that stays stuck is said once.</para>
		/// </summary>
		private static void AnnounceClimbUnderInspection(Zone Z, string RetiredId,
			KingdomConstructionJob Job, bool Pending)
		{
			bool announced = !string.IsNullOrEmpty(
				Z.GetZoneProperty(FoundingHeartClimbHeldProperty, null));
			if (!Pending)
			{
				if (announced) Z.SetZoneProperty(FoundingHeartClimbHeldProperty, null);
				return;
			}
			if (announced) return;
			Z.SetZoneProperty(FoundingHeartClimbHeldProperty, Job.Id);
			if (Z.GetZoneProperty(FoundingHeartClimbHeldProperty, null) != Job.Id) return;
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			system?.Ledger?.Note("The heart's raised rung has not finished settling, so the "
				+ "kingdom's seal is waiting on it. Nothing is sealed until that improvement "
				+ "closes or is inspected.");
			KingdomLog.Log("founding heart: climb under inspection; retired=" + RetiredId
				+ "; job=" + Job.Id + "; phase=" + Job.Phase);
		}

		/// <summary>The one COMPLETED improvement that retired this identity, and the object it
		/// produced. Exactly one job may name it, that job must have reached its terminal
		/// completion phase -- a job still working has retired nothing -- and its output must
		/// resolve to exactly one live object; anything else refuses rather than choosing.
		/// </summary>
		private static bool TryImprovementSuccessorOf(string RetiredId,
			out KingdomConstructionJob Job, out GameObject Successor, out int Named,
			out int LiveOutputs)
		{
			Job = null;
			Successor = null;
			Named = 0;
			LiveOutputs = 0;
			if (string.IsNullOrEmpty(RetiredId)
				|| !KingdomConstruction.TryRead(out List<KingdomConstructionJob> jobs, out _)
				|| jobs == null) return false;
			for (int i = 0; i < jobs.Count; i++)
			{
				KingdomConstructionJob row = jobs[i];
				if (row == null || row.Route != KingdomConstructionRoute.Improvement
					|| row.SubjectId != RetiredId) continue;
				Named++;
				Job = row;
			}
			if (Named != 1 || Job.Phase != KingdomConstructionPhase.Complete
				|| !string.IsNullOrEmpty(Job.Failure) || string.IsNullOrEmpty(Job.OutputId)
				|| Job.OutputId == RetiredId)
			{
				if (Named != 1) Job = null;
				return false;
			}
			bool exact = KingdomConstruction.FindGlobalLiveId(Job.OutputId, out Successor)
				== KingdomPhysicalLookupState.Exact && GameObject.Validate(Successor);
			LiveOutputs = exact ? 1 : 0;
			return exact;
		}

	}
}
