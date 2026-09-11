using System.Collections.Generic;
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
		/// is a property of the save, not of this source. So on the very turn a rung is raised the
		/// seal may still capture the book row the pass has not rebuilt yet, and report one
		/// Malformed for that day. From the first pass after the climb onward this chain recovers
		/// the heart, the pass runs, the book row follows the successor and the seal agrees. The
		/// once-per-climb window can only be observed natively.</para>
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
				return false;
			string retired = prior.FinalId;
			KingdomConstructionJob job;
			GameObject successor;
			if (!TryImprovementSuccessorOf(retired, out job, out successor)) return false;
			bool receipt = KingdomConstruction.HasReceipt(successor, job);
			bool removal = r_KingdomScaffold.HasRemovalProof(successor, job.SubjectId);
			// Custody. TWO RECEIPT-BACKED FACTS AND ONE STAMP, named as what they are. RECEIPTS:
			// the construction receipt the successor carries is the job's own durable receipt for
			// exactly this output (KingdomConstruction.HasReceipt), and the scaffold removal proof
			// is the durable successor-side record naming the retired identity
			// (r_KingdomScaffold.HasRemovalProof); those two carry the link. STAMPS: the engine
			// identity and PlotFinalPredecessorProperty are plain property reads, REQUIRED
			// corroboration and never sufficient on their own -- a stamp alone proves nothing
			// here, and neither is asked before the receipts are. The finishing transaction's
			// PlotFinalRoot custody key would have been the durable custody row, but it is retired
			// when the job settles (KingdomPlot2.34.EffectsAndFurnishing) and cannot be read here.
			bool custody = successor.IDIfAssigned == job.OutputId
				&& successor.GetStringProperty(PlotFinalPredecessorProperty) == retired
				&& KingdomConstruction.HasReceipt(successor, job);
			if (!KingdomFoundingHeartChainRules.BindsGround(job.OutputId,
				FoundingHeartFinalId(plan), prior, retired, job.OutputId, receipt, removal,
				custody)) return false;
			// The retirement authority is asked LAST and about the identity the chain named, so a
			// chain that proved itself still cannot stand on a heart whose seal, reservations,
			// roster or retired custody do not.
			if (!ExactFoundingHeartRetiredAuthority(Z, retired,
				KingdomFoundingHeartRetiredGeneration.Final, out _)) return false;
			Successor = successor;
			return true;
		}

		/// <summary>The one COMPLETED improvement that retired this identity, and the object it
		/// produced. Exactly one job may name it, that job must have reached its terminal
		/// completion phase -- a job still working has retired nothing -- and its output must
		/// resolve to exactly one live object; anything else refuses rather than choosing.
		/// </summary>
		private static bool TryImprovementSuccessorOf(string RetiredId,
			out KingdomConstructionJob Job, out GameObject Successor)
		{
			Job = null;
			Successor = null;
			if (string.IsNullOrEmpty(RetiredId)
				|| !KingdomConstruction.TryRead(out List<KingdomConstructionJob> jobs, out _)
				|| jobs == null) return false;
			for (int i = 0; i < jobs.Count; i++)
			{
				KingdomConstructionJob row = jobs[i];
				if (row == null || row.Route != KingdomConstructionRoute.Improvement
					|| row.SubjectId != RetiredId) continue;
				if (Job != null) { Job = null; return false; }
				Job = row;
			}
			if (Job == null || Job.Phase != KingdomConstructionPhase.Complete
				|| !string.IsNullOrEmpty(Job.Failure) || string.IsNullOrEmpty(Job.OutputId)
				|| Job.OutputId == RetiredId) return false;
			return KingdomConstruction.FindGlobalLiveId(Job.OutputId, out Successor)
					== KingdomPhysicalLookupState.Exact
				&& GameObject.Validate(Successor);
		}

	}
}
