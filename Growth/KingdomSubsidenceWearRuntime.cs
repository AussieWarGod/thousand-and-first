using System;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	/// <summary>
	/// Ordinary-production wear adapter for one frozen subsidence rung: it applies exactly one
	/// planned work's wear mutation through the EXISTING damage-incident receipt fields on
	/// <c>r_KingdomWear</c> and the EXISTING compare-and-swap
	/// <see cref="KingdomSubsidenceRungRules.WearAction"/>, the sole authority on admission.
	/// This helper proves measured exact after-state only. It does NOT prove global object custody,
	/// restart safety, or durability: the caller's reprovesAuthority callback proves full work
	/// custody, and the caller persists the parent WearPhase.Intent before calling in.
	/// Nothing here mints identity: an object id is only ever READ, only through IDIfAssigned.
	/// LastCause is deliberately never written here (the telling takes its WearCause explicitly);
	/// a behaviour choice, not a claim that any one file is its single writer.
	/// Nothing is removed, obliterated or destroyed, and the old ApplyDamageIncident path, its
	/// telling and its receipt retirement are never called: a refusal leaves every piece of
	/// evidence, including a part just attached, exactly as it stands.
	/// </summary>
	internal static class KingdomSubsidenceWearRuntime
	{
		internal const string MalformedRequest = "the subsidence wear request is missing its plan, index, work, or authority proof";
		internal const string PhaseNotArmed = "the subsidence wear step is not armed at its persisted intent";
		internal const string WorkNotExact = "the subsidence wear work is not the exact planned object, blueprint, zone, and cell";
		internal const string RepairInHand = "the subsidence wear work holds an active repair or construction receipt";
		internal const string LeakOpen = "the subsidence wear work holds an open leak receipt";
		internal const string QuarantineHeld = "the subsidence wear work is quarantined and is never mutated through";
		internal const string ForeignIncident = "the subsidence wear work holds a foreign damage incident that is never replaced";
		internal const string NotAdmitted = "the subsidence wear evidence admits no exact before or after state";
		internal const string PartNotExact = "the subsidence wear part is not the exact allocated reference on the exact work";
		internal const string AuthorityLost = "the subsidence wear authority did not reprove across a mutation seam";
		internal const string BeforeChanged = "the subsidence wear before-state changed before the mutation";
		internal const string AfterNotExact = "the subsidence wear after-state did not measure exactly";

		/// <summary>Phase argument to <see cref="FenceRefusal"/> admitting any phase this helper
		/// writes, for the seams where this step's own receipt may stand at more than one.</summary>
		private const int AnyBindablePhase = -1;

		/// <summary>
		/// Applies the one planned wear mutation for <paramref name="index"/>, true only with the
		/// measured exact after-state in <paramref name="observed"/>. A refusal before the receipt
		/// is bound leaves <paramref name="observed"/> null; after binding it is the reference this
		/// helper OBSERVED, not necessarily one it measured as exact -- on the attachment seam's
		/// PartNotExact it is the allocated instance, the evidence root, never what GetPart handed
		/// back. Every refusal is a fixed constant that leaves the evidence intact, with the attach
		/// exception TYPE appended on that one PartNotExact.
		/// </summary>
		internal static bool TryApply(KingdomSubsidenceRungPlan plan, int index, GameObject work,
			Func<bool> reprovesAuthority, out r_KingdomWear observed, out string refusal)
		{
			observed = null;
			if (plan == null || plan.Works == null || index < 0 || index >= plan.Works.Count
				|| work == null || reprovesAuthority == null)
			{ refusal = MalformedRequest; return false; }
			KingdomSubsidenceRungWork row = plan.Works[index];
			if (row == null) { refusal = MalformedRequest; return false; }
			// The caller persists the parent intent FIRST; replay lives on the PART's receipt phase.
			if (row.WearPhase != KingdomSubsidenceEffectPhase.Intent)
			{ refusal = PhaseNotArmed; return false; }
			// IDIfAssigned is read because GameObject's ID property MINTS an id for an object without.
			if (!GameObject.Validate(work)
				|| !string.Equals(work.IDIfAssigned, row.ObjectId, StringComparison.Ordinal)
				|| !string.Equals(work.Blueprint, row.Blueprint, StringComparison.Ordinal)
				|| work.CurrentCell == null || work.CurrentZone == null
				|| !string.Equals(work.CurrentZone.ZoneID, plan.ZoneId, StringComparison.Ordinal)
				|| work.CurrentCell.X != row.X || work.CurrentCell.Y != row.Y)
			{ refusal = WorkNotExact; return false; }
			r_KingdomWear present = work.GetPart<r_KingdomWear>();
			// observed stays null through every fence and is bound only in the two arms below.
			// ConstructionAvailable (Construction.cs:10) is a versioned decode of the exact five-table
			// shape; HasActiveRepair, ReceiptBlocksCurrent and BlocksWork are deliberately NOT used.
			// Labour left on the part is the other half of "a mending is under way".
			if (!KingdomSubsidenceRungRuntime.ConstructionAvailable(work)
				|| (present != null && present.RepairEffortLeft != 0))
			{ refusal = RepairInHand; return false; }
			if (present != null && present.LeakPhase != (int)KingdomWearLeakPhase.None)
			{ refusal = LeakOpen; return false; }
			if (present != null && (present.LifecycleQuarantined
				|| present.IncidentPhase == (int)KingdomWearIncidentPhase.Quarantined))
			{ refusal = QuarantineHeld; return false; }
			// Replay is admitted only from this step's own frozen receipt; any other is foreign.
			if (present != null && present.IncidentPhase != (int)KingdomWearIncidentPhase.None
				&& (!BindablePhase(present.IncidentPhase) || !SameReceipt(present, plan, row)))
			{ refusal = ForeignIncident; return false; }
			// REPLAY RULING: a Mutated receipt whose wear no longer reads the exact after-state is
			// REFUSED, never regressed and re-applied; Mutated WITH it confirms at the tail instead.
			if (present != null && present.IncidentPhase == (int)KingdomWearIncidentPhase.Mutated
				&& present.Wear != row.AfterWear)
			{ refusal = NotAdmitted; return false; }
			// An after-state with no receipt at all is another writer's mutation, never credited here.
			if (present != null && present.IncidentPhase == (int)KingdomWearIncidentPhase.None
				&& present.Wear != row.BeforeWear)
			{ refusal = NotAdmitted; return false; }
			// The reading the admission below is measured on, captured BEFORE the callback runs.
			bool hadPart = present != null;
			int wasPhase = hadPart ? present.IncidentPhase : (int)KingdomWearIncidentPhase.None,
				wasWear = hadPart ? present.Wear : 0,
				wasCause = hadPart ? present.IncidentCause : 0,
				wasBefore = hadPart ? present.IncidentBeforeWear : 0,
				wasAfter = hadPart ? present.IncidentAfterWear : 0;
			string wasId = hadPart ? present.IncidentId : null;
			// The existing compare-and-swap decides admission: exact authority, own frontier, armed
			// phase, exact before/after pair. That decision is reused here, never duplicated.
			if (KingdomSubsidenceRungRules.WearAction(plan, index, reprovesAuthority(),
				present != null, present == null ? 0 : present.Wear)
				== KingdomSubsidenceEffectAction.Refuse)
			{ refusal = NotAdmitted; return false; }
			// That call RAN THE CALLBACK, which can open a job, quarantine the work, or change or
			// replace the part and still return true, so everything the admission was measured on is
			// re-measured here, on the same reference, BEFORE any Bound field is written.
			if (!KingdomSubsidenceRungRuntime.ConstructionAvailable(work))
			{ refusal = RepairInHand; return false; }
			if (!hadPart)
			{
				if (work.GetPart<r_KingdomWear>() != null || Copies(work) != 0)
				{ refusal = PartNotExact; return false; }
			}
			else
			{
				refusal = WorkFenceRefusal(work, present);
				if (refusal != null) return false;
				if (present.IncidentPhase != wasPhase || present.Wear != wasWear
					|| present.IncidentCause != wasCause || present.IncidentBeforeWear != wasBefore
					|| present.IncidentAfterWear != wasAfter
					|| !string.Equals(present.IncidentId, wasId, StringComparison.Ordinal))
				{ refusal = ForeignIncident; return false; }
			}
			if (present == null)
			{
				// The BOUND receipt is stamped on the allocated reference BEFORE the work is offered
				// it, so no attached part of this type can ever exist without one.
				r_KingdomWear instance = new r_KingdomWear();
				instance.Wear = row.BeforeWear;
				instance.IncidentId = plan.StepId;
				instance.IncidentCause = (int)KingdomWearRules.WearCause.Subsidence;
				instance.IncidentBeforeWear = row.BeforeWear;
				instance.IncidentAfterWear = row.AfterWear;
				instance.IncidentPhase = (int)KingdomWearIncidentPhase.Bound;
				Exception attach = null;
				try { work.AddPart(instance); }
				catch (Exception raised) { attach = raised; }
				// The caught object is RETAINED and never inspected -- no Message, type or text --
				// until the measurement below has failed. AddPart returns its argument unconditionally
				// (GameObject.cs:10014-10033/:10041-10044) and AddPartInternals only INSERTS
				// (:9972-9993), so a second add DUPLICATES and GetPart<T> (:9886-9897) may hand back a
				// foreign first entry; only the measured attachment counts. A throw that measurement
				// AND authority both survive is IGNORED: the success path has no diagnostic channel.
				if (!SameAttachment(work, instance))
				{
					observed = instance;
					refusal = PartNotExact + (attach == null ? "" : " (" + attach.GetType().Name + ")");
					return false;
				}
				observed = instance;
				if (!reprovesAuthority()) { refusal = AuthorityLost; return false; }
			}
			else
			{
				// The existing part keeps its exact reference: bound once, never re-fetched.
				observed = present;
				if (present.IncidentPhase == (int)KingdomWearIncidentPhase.None)
				{
					present.IncidentId = plan.StepId;
					present.IncidentCause = (int)KingdomWearRules.WearCause.Subsidence;
					present.IncidentBeforeWear = row.BeforeWear;
					present.IncidentAfterWear = row.AfterWear;
					present.IncidentPhase = (int)KingdomWearIncidentPhase.Bound;
				}
			}
			if (!SameAttachment(work, observed) || !SameReceipt(observed, plan, row))
			{ refusal = PartNotExact; return false; }
			if (!reprovesAuthority()) { refusal = AuthorityLost; return false; }
			if (observed.Wear != row.AfterWear)
			{
				if (observed.Wear != row.BeforeWear) { refusal = BeforeChanged; return false; }
				// NOTHING IS STAMPED BEFORE IT IS RE-PROVED. This is the LAST callback before the
				// write; the COMPLETE fence set is re-measured after it and BEFORE MutationIntent is
				// stamped, so a callback-changed phase is never laundered into ours. Bindable minus
				// the Mutated refused below is exactly Bound or MutationIntent, the only two legal
				// here. No callback runs between that re-measure and the write -- only the stamp
				// does -- so this ONE recheck covers both.
				if (!reprovesAuthority()) { refusal = AuthorityLost; return false; }
				if (observed.IncidentPhase == (int)KingdomWearIncidentPhase.Mutated)
				{ refusal = NotAdmitted; return false; }
				refusal = FenceRefusal(work, observed, plan, row, AnyBindablePhase, row.BeforeWear);
				if (refusal != null) return false;
				observed.IncidentPhase = (int)KingdomWearIncidentPhase.MutationIntent;
				observed.Wear = row.AfterWear;
				// Authority first, the whole fence set again at the after-state, then the terminal
				// stamp. A failure leaves the write and MutationIntent standing as evidence.
				if (!reprovesAuthority()) { refusal = AuthorityLost; return false; }
				refusal = FenceRefusal(work, observed, plan, row,
					(int)KingdomWearIncidentPhase.MutationIntent, row.AfterWear);
				if (refusal != null) return false;
				observed.IncidentPhase = (int)KingdomWearIncidentPhase.Mutated;
				refusal = null;
				return true;
			}
			// Idempotent confirm: the exact after-state already stands under this step's own receipt.
			if (!reprovesAuthority()) { refusal = AuthorityLost; return false; }
			refusal = FenceRefusal(work, observed, plan, row, AnyBindablePhase, row.AfterWear);
			if (refusal != null) return false;
			observed.IncidentPhase = (int)KingdomWearIncidentPhase.Mutated;
			refusal = null;
			return true;
		}

		/// <summary>Work-and-attachment fences: exact attachment, no job, no labour, no leak, no
		/// quarantine on either latch.</summary>
		private static string WorkFenceRefusal(GameObject work, r_KingdomWear part)
		{
			if (!SameAttachment(work, part)) return PartNotExact;
			if (!KingdomSubsidenceRungRuntime.ConstructionAvailable(work)
				|| part.RepairEffortLeft != 0) return RepairInHand;
			if (part.LeakPhase != (int)KingdomWearLeakPhase.None) return LeakOpen;
			if (part.LifecycleQuarantined
				|| part.IncidentPhase == (int)KingdomWearIncidentPhase.Quarantined)
				return QuarantineHeld;
			return null;
		}

		/// <summary>The COMPLETE fence set re-measured on one exact bound reference at one expected
		/// phase and wear. reprovesAuthority proves owner, parent and work custody ONLY: never wear,
		/// receipt, repair, leak or quarantine state, so every seam re-measures all of them.</summary>
		private static string FenceRefusal(GameObject work, r_KingdomWear part,
			KingdomSubsidenceRungPlan plan, KingdomSubsidenceRungWork row, int phase, int wear)
		{
			string blocked = WorkFenceRefusal(work, part);
			if (blocked != null) return blocked;
			if (!SameReceipt(part, plan, row) || !BindablePhase(part.IncidentPhase)
				|| (phase != AnyBindablePhase && part.IncidentPhase != phase))
				return ForeignIncident;
			// Names which side of the one write failed; equal before/after reads as the before-state.
			if (part.Wear != wear) return wear == row.BeforeWear ? BeforeChanged : AfterNotExact;
			return null;
		}

		/// <summary>The only three receipt phases this helper writes or replays from.</summary>
		private static bool BindablePhase(int phase)
		{
			return phase == (int)KingdomWearIncidentPhase.Bound
				|| phase == (int)KingdomWearIncidentPhase.MutationIntent
				|| phase == (int)KingdomWearIncidentPhase.Mutated;
		}

		/// <summary>This step's exact frozen receipt, and no other: id, cause, before and after.</summary>
		private static bool SameReceipt(r_KingdomWear part, KingdomSubsidenceRungPlan plan,
			KingdomSubsidenceRungWork row)
		{
			return part != null
				&& string.Equals(part.IncidentId, plan.StepId, StringComparison.Ordinal)
				&& part.IncidentCause == (int)KingdomWearRules.WearCause.Subsidence
				&& part.IncidentBeforeWear == row.BeforeWear
				&& part.IncidentAfterWear == row.AfterWear;
		}

		/// <summary>Measured attachment: the work hands back that exact reference, it points back at
		/// that same work, and it is the only part of its type on it.</summary>
		private static bool SameAttachment(GameObject work, r_KingdomWear part)
		{
			return part != null && GameObject.Validate(work)
				&& ReferenceEquals(work.GetPart<r_KingdomWear>(), part)
				&& ReferenceEquals(part.ParentObject, work) && Copies(work) == 1;
		}

		/// <summary>Duplicate scan over the list GetPart reads. The type test also
		/// matches a SUBCLASS, which GetPart's exact-type comparison walks straight past; that is
		/// deliberately the stricter direction.</summary>
		private static int Copies(GameObject work)
		{
			int copies = 0;
			int count = work.PartsList == null ? 0 : work.PartsList.Count;
			for (int i = 0; i < count; i++) if (work.PartsList[i] is r_KingdomWear) copies++;
			return copies;
		}
	}
}
