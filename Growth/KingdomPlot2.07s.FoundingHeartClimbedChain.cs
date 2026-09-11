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
			// Custody, read from the successor's own durable predecessor stamp and its unique
			// global identity. The finishing transaction's PlotFinalRoot key is retired when the
			// job settles, so it cannot be read here; what remains is the successor naming the
			// exact identity it replaced, which no plot that did not replace it can name.
			bool custody = successor.GetStringProperty(PlotFinalPredecessorProperty) == retired
				&& successor.IDIfAssigned == job.OutputId;
			bool reservation = HasExactFoundingHeartReservation(plan, job.OutputId, "final");
			if (!KingdomFoundingHeartChainRules.BindsGround(job.OutputId,
				FoundingHeartFinalId(plan), prior, retired, job.OutputId, receipt, removal,
				custody, reservation)) return false;
			// The retirement authority is asked LAST and about the identity the chain named, so a
			// chain that proved itself still cannot stand on a heart whose seal, reservations,
			// roster or retired custody do not.
			if (!ExactFoundingHeartRetiredAuthority(Z, retired,
				KingdomFoundingHeartRetiredGeneration.Final, out _)) return false;
			Successor = successor;
			return true;
		}

		/// <summary>The one completed improvement that retired this identity, and the object it
		/// produced. Exactly one job may name it, and its output must resolve to exactly one live
		/// object; anything else refuses rather than choosing.</summary>
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
			if (Job == null || string.IsNullOrEmpty(Job.OutputId)
				|| Job.OutputId == RetiredId) return false;
			return KingdomConstruction.FindGlobalLiveId(Job.OutputId, out Successor)
					== KingdomPhysicalLookupState.Exact
				&& GameObject.Validate(Successor);
		}

		/// <summary>Whether the reservation store already holds the exact reservation this plan
		/// would issue for one identity and role. A read: recovery never issues one, because a
		/// recovery that could reserve would be minting the authority it is meant to check.
		/// </summary>
		private static bool HasExactFoundingHeartReservation(KingdomFoundingHeartPlan Plan,
			string Id, string Role)
		{
			if (!KingdomFoundingHeartRules.Valid(Plan) || string.IsNullOrEmpty(Id)) return false;
			string key = FoundingHeartReservationPrefix + Id;
			string expected = FoundingHeartReservation(Plan, Id, Role);
			if (string.IsNullOrEmpty(expected)) return false;
			FoundingHeartReservationStore store = new FoundingHeartReservationStore();
			return store.Current
				&& KingdomFoundingHeartReservationState.TryExpected(key, expected,
					store.Observe(key), out bool absent) && !absent && store.Current;
		}

		/// <summary>
		/// The settle's own write, and the only one in this chain: the successor's identity is
		/// reserved under the heart's final role before the rung is stamped. Called from the
		/// improvement settle, never from recovery. A climb on ground that carries no founding
		/// heart, or whose sealed terminal does not name this retiring root, owes nothing and
		/// says so by returning true.
		/// </summary>
		internal static bool TryReserveClimbedFoundingHeartRoot(Zone Z, string RetiredId,
			string SuccessorId)
		{
			if (Z == null || string.IsNullOrEmpty(RetiredId) || string.IsNullOrEmpty(SuccessorId)
				|| RetiredId == SuccessorId) return false;
			if (!KingdomFoundingHeartRules.TryDecode(
					Z.GetZoneProperty(FoundingHeartReceiptProperty, null), out var plan)
				|| !KingdomFoundingHeartRules.Complete(plan) || plan.ZoneId != Z.ZoneID)
				return true;
			if (!KingdomFoundingHeartTerminalRules.TryDecode(
					Z.GetZoneProperty(FoundingHeartTerminalProperty, null), out var prior)
				|| prior.FinalId != RetiredId) return true;
			// Plan before effect: the reservation is proved absent-or-exact, issued, and read
			// back, and the plan's own reservations must still stand afterwards.
			FoundingHeartReservationStore store = new FoundingHeartReservationStore();
			if (!store.CheckPlan(plan)) return HeartRefused("climb: plan reservations");
			if (!EnsureFoundingHeartReservation(store, plan, SuccessorId, "final"))
				return HeartRefused("climb: successor reservation");
			return HasExactFoundingHeartReservation(plan, SuccessorId, "final")
				&& store.CheckPlan(plan) || HeartRefused("climb: reservation readback");
		}
	}
}
