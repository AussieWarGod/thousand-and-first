#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// SOURCE-ONLY CONTRACTS. Every assertion here reads repository TEXT and pins a CODE boundary:
	/// statement order, exact anchors, banned calls, and constants. Nothing executes the adapter, a
	/// game, a GameObject, a part, a save or a reload, so none of it is runtime, custody,
	/// durability, restart-safety or native proof. Counting assertions read <see cref="Code"/>, a
	/// comment-stripped view, so reflowing a comment can never move a count.
	/// </summary>
	[TestFixture]
	public sealed class KingdomSubsidenceWearRuntimeSourceTests
	{
		private const string Runtime = "Growth/KingdomSubsidenceWearRuntime.cs";
		private const string Effects = "Growth/KingdomSubsidenceRungRules.Effects.cs";
		private const string Plan = "Growth/KingdomSubsidenceRungPlan.cs";
		private const string Part = "Growth/KingdomWear.00.r_KingdomWear.cs";
		private const string Declarations = "Growth/KingdomWearRules.Declarations.cs";
		private const string Construction = "Growth/KingdomSubsidenceRungRuntime.Construction.cs";
		private const string Apply = "internal static bool TryApply(";

		private const string Signature = "internal static bool TryApply(KingdomSubsidenceRungPlan plan, "
			+ "int index, GameObject work, Func<bool> reprovesAuthority, out r_KingdomWear observed, "
			+ "out string refusal)";

		private const string Fence = "private static string FenceRefusal(GameObject work, "
			+ "r_KingdomWear part, KingdomSubsidenceRungPlan plan, KingdomSubsidenceRungWork row, "
			+ "int phase, int wear)";

		private const string WorkFence = "private static string WorkFenceRefusal(GameObject work, "
			+ "r_KingdomWear part)";

		/// <summary>The reading the admission is measured on, captured before the callback runs.</summary>
		private const string Capture = "bool hadPart = present != null; int wasPhase = hadPart ? "
			+ "present.IncidentPhase : (int)KingdomWearIncidentPhase.None, wasWear = hadPart ? "
			+ "present.Wear : 0, wasCause = hadPart ? present.IncidentCause : 0, wasBefore = hadPart "
			+ "? present.IncidentBeforeWear : 0, wasAfter = hadPart ? present.IncidentAfterWear : 0; "
			+ "string wasId = hadPart ? present.IncidentId : null;";

		/// <summary>The post-admission re-measure: the callback the compare-and-swap ran could have
		/// changed or replaced the part, so nothing is written until the same reference reads back
		/// exactly as captured, under every work fence.</summary>
		private const string Guard = "if (!KingdomSubsidenceRungRuntime.ConstructionAvailable(work)) "
			+ "{ refusal = RepairInHand; return false; } if (!hadPart) { if "
			+ "(work.GetPart<r_KingdomWear>() != null || Copies(work) != 0) { refusal = PartNotExact; return false; } } "
			+ "else { refusal = WorkFenceRefusal(work, present); if (refusal != null) return false; "
			+ "if (present.IncidentPhase != wasPhase || present.Wear != wasWear || "
			+ "present.IncidentCause != wasCause || present.IncidentBeforeWear != wasBefore || "
			+ "present.IncidentAfterWear != wasAfter || !string.Equals(present.IncidentId, wasId, "
			+ "StringComparison.Ordinal)) { refusal = ForeignIncident; return false; } }";

		/// <summary>Nothing is stamped before it is re-proved: last callback, then the complete
		/// fence set, then MutationIntent, then the single wear write, with nothing in between.</summary>
		private const string PreWrite = "if (!reprovesAuthority()) { refusal = AuthorityLost; return "
			+ "false; } if (observed.IncidentPhase == (int)KingdomWearIncidentPhase.Mutated) "
			+ "{ refusal = NotAdmitted; return false; } refusal = FenceRefusal(work, observed, plan, "
			+ "row, AnyBindablePhase, row.BeforeWear); if (refusal != null) return false; "
			+ "observed.IncidentPhase = (int)KingdomWearIncidentPhase.MutationIntent; "
			+ "observed.Wear = row.AfterWear;";

		/// <summary>After the write: authority first, then the complete fence set at the after-state,
		/// then the terminal stamp.</summary>
		private const string PostWrite = "if (!reprovesAuthority()) { refusal = AuthorityLost; return "
			+ "false; } refusal = FenceRefusal(work, observed, plan, row, "
			+ "(int)KingdomWearIncidentPhase.MutationIntent, row.AfterWear); if (refusal != null) "
			+ "return false; observed.IncidentPhase = (int)KingdomWearIncidentPhase.Mutated; "
			+ "refusal = null; return true;";

		/// <summary>The idempotent-confirm tail: same order, same fence set, no write.</summary>
		private const string ConfirmSeam = "if (!reprovesAuthority()) { refusal = AuthorityLost; "
			+ "return false; } refusal = FenceRefusal(work, observed, plan, row, AnyBindablePhase, "
			+ "row.AfterWear); if (refusal != null) return false; "
			+ "observed.IncidentPhase = (int)KingdomWearIncidentPhase.Mutated; refusal = null; "
			+ "return true;";

		[Test]
		public void SourceContractCarriesItsOneBoundaryDisclaimerAndWritesNoLastCause()
		{
			Has(Flat(TestMain.ReadRepositoryText(Runtime)),
				"This helper proves measured exact after-state only.");
			// The header's LastCause claim is behaviour, not a wish: the code never names the field.
			StringAssert.DoesNotContain("LastCause", Code(Runtime));
		}

		[Test]
		public void SourceContractPinsTheExactApplySignature()
		{
			Has(Flat(TestMain.ReadRepositoryText(Runtime)), Flat(Signature));
			Has(Flat(TestMain.ReadRepositoryText(Runtime)),
				"internal static class KingdomSubsidenceWearRuntime");
		}

		[Test]
		public void SourceContractPinsEveryFixedRefusalLiteral()
		{
			Has(Flat(TestMain.ReadRepositoryText(Runtime)),
				"MalformedRequest = \"the subsidence wear request is missing its plan, index, work, or authority proof\";",
				"PhaseNotArmed = \"the subsidence wear step is not armed at its persisted intent\";",
				"WorkNotExact = \"the subsidence wear work is not the exact planned object, blueprint, zone, and cell\";",
				"RepairInHand = \"the subsidence wear work holds an active repair or construction receipt\";",
				"LeakOpen = \"the subsidence wear work holds an open leak receipt\";",
				"QuarantineHeld = \"the subsidence wear work is quarantined and is never mutated through\";",
				"ForeignIncident = \"the subsidence wear work holds a foreign damage incident that is never replaced\";",
				"NotAdmitted = \"the subsidence wear evidence admits no exact before or after state\";",
				"PartNotExact = \"the subsidence wear part is not the exact allocated reference on the exact work\";",
				"AuthorityLost = \"the subsidence wear authority did not reprove across a mutation seam\";",
				"BeforeChanged = \"the subsidence wear before-state changed before the mutation\";",
				"AfterNotExact = \"the subsidence wear after-state did not measure exactly\";");
		}

		[Test]
		public void SourceContractRefusesMalformedInputPhaseAndInexactCustodyBeforeAnythingElse()
		{
			string body = Method(Runtime, Apply);
			Ordered(body, "observed = null;",
				"if (plan == null || plan.Works == null || index < 0 || index >= plan.Works.Count || work == null || reprovesAuthority == null) { refusal = MalformedRequest; return false; }",
				"KingdomSubsidenceRungWork row = plan.Works[index];",
				"if (row == null) { refusal = MalformedRequest; return false; }",
				"if (row.WearPhase != KingdomSubsidenceEffectPhase.Intent) { refusal = PhaseNotArmed; return false; }",
				"if (!GameObject.Validate(work) || !string.Equals(work.IDIfAssigned, row.ObjectId, StringComparison.Ordinal) || !string.Equals(work.Blueprint, row.Blueprint, StringComparison.Ordinal) || work.CurrentCell == null || work.CurrentZone == null || !string.Equals(work.CurrentZone.ZoneID, plan.ZoneId, StringComparison.Ordinal) || work.CurrentCell.X != row.X || work.CurrentCell.Y != row.Y) { refusal = WorkNotExact; return false; }",
				"r_KingdomWear present = work.GetPart<r_KingdomWear>();");
			// The exact plan fields the custody comparison names must still exist as pinned.
			Has(Flat(TestMain.ReadRepositoryText(Plan)),
				"internal readonly string StepId, RealmId, SettlementId, ZoneId;",
				"internal readonly int WorkId, X, Y, BeforeWear, AfterWear;",
				"internal readonly string ObjectId, Blueprint, PlotId, DesignStamp, Name;",
				"internal readonly bool HadWearPart;",
				"internal readonly KingdomSubsidenceEffectPhase WearPhase;");
		}

		[Test]
		public void SourceContractLeavesObservedNullUntilTheReceiptIsBound()
		{
			string code = Code(Runtime);
			int apply = code.IndexOf(Flat(Signature), StringComparison.Ordinal);
			Assert.That(apply, Is.GreaterThanOrEqualTo(0), "The adapter must carry its exact signature.");
			int fence = code.IndexOf("refusal = RepairInHand;", apply, StringComparison.Ordinal);
			Assert.That(fence, Is.GreaterThan(apply), "The first fence constant must follow the signature.");
			string prefix = code.Substring(apply, fence - apply);
			// The only assignment ahead of the first fence constant is the null every pre-binding
			// refusal returns; nothing is handed back that could be mistaken for a measurement.
			Assert.AreEqual(1, Count(prefix, "observed = "),
				"observed is bound only in the two arms, never before the fences.");
			Has(prefix, "observed = null;");
			int admitted = code.IndexOf("== KingdomSubsidenceEffectAction.Refuse", StringComparison.Ordinal);
			Assert.That(admitted, Is.GreaterThan(fence), "Admission follows the fences.");
			foreach (string bind in new[] { "observed = instance;", "observed = present;" })
				Assert.That(code.IndexOf(bind, StringComparison.Ordinal), Is.GreaterThan(admitted),
					"Binding follows admission: " + bind);
			Assert.AreEqual(4, Count(code, "observed = "),
				"One null, one existing-part bind, and the allocated instance on refusal and on success.");
		}

		[Test]
		public void SourceContractReadsIDIfAssignedAndNeverMintsAnObjectId()
		{
			string code = Code(Runtime);
			Has(code, "work.IDIfAssigned");
			Assert.That(Count(code, ".IDIfAssigned"), Is.GreaterThanOrEqualTo(1),
				"The runtime must read the non-minting id property at least once.");
			// Every ".ID" in the code is part of ".IDIfAssigned"; no bare id property is read.
			Assert.AreEqual(Count(code, ".IDIfAssigned"), Count(code, ".ID"),
				"The runtime may never read the minting ID property.");
			foreach (string banned in new[] { "new GameObject", "GameID", "SetStringProperty(",
				"SetIntProperty(", "SetStringGameState(", "SetInt64GameState(" })
				StringAssert.DoesNotContain(banned, Flat(TestMain.ReadRepositoryText(Runtime)));
		}

		[Test]
		public void SourceContractFencesRepairLeakQuarantineAndForeignIncidentBeforeAnyAttachment()
		{
			string code = Code(Runtime);
			int add = code.IndexOf("work.AddPart(", StringComparison.Ordinal);
			Assert.That(add, Is.GreaterThan(0), "The runtime must attach through AddPart.");
			foreach (string fence in new[] {
				"KingdomSubsidenceRungRuntime.ConstructionAvailable(work)", "present.RepairEffortLeft != 0",
				"present.LeakPhase != (int)KingdomWearLeakPhase.None", "present.LifecycleQuarantined",
				"present.IncidentPhase == (int)KingdomWearIncidentPhase.Quarantined",
				"BindablePhase(present.IncidentPhase)" })
			{
				int at = code.IndexOf(fence, StringComparison.Ordinal);
				Assert.That(at, Is.GreaterThanOrEqualTo(0), "Missing fence: " + fence);
				Assert.That(at, Is.LessThan(add), "Fence must precede attachment: " + fence);
			}
			Ordered(Method(Runtime, Apply),
				"if (!KingdomSubsidenceRungRuntime.ConstructionAvailable(work) || (present != null && present.RepairEffortLeft != 0)) { refusal = RepairInHand; return false; }",
				"if (present != null && present.LeakPhase != (int)KingdomWearLeakPhase.None) { refusal = LeakOpen; return false; }",
				"if (present != null && (present.LifecycleQuarantined || present.IncidentPhase == (int)KingdomWearIncidentPhase.Quarantined)) { refusal = QuarantineHeld; return false; }",
				"if (present != null && present.IncidentPhase != (int)KingdomWearIncidentPhase.None && (!BindablePhase(present.IncidentPhase) || !SameReceipt(present, plan, row))) { refusal = ForeignIncident; return false; }",
				"if (present != null && present.IncidentPhase == (int)KingdomWearIncidentPhase.Mutated && present.Wear != row.AfterWear) { refusal = NotAdmitted; return false; }",
				"if (present != null && present.IncidentPhase == (int)KingdomWearIncidentPhase.None && present.Wear != row.BeforeWear) { refusal = NotAdmitted; return false; }");
			// The construction fence is the root's own helper, proved before binding, again on the
			// post-admission re-measure, and again inside the work fences the mutation seams call.
			Has(Flat(TestMain.ReadRepositoryText(Construction)),
				"internal static bool ConstructionAvailable(GameObject work)");
			Assert.AreEqual(3, Count(code, "ConstructionAvailable("),
				"Construction is fenced before binding, after admission, and in WorkFenceRefusal.");
			Ordered(Method(Runtime, WorkFence),
				"if (!SameAttachment(work, part)) return PartNotExact;",
				"if (!KingdomSubsidenceRungRuntime.ConstructionAvailable(work) || part.RepairEffortLeft != 0) return RepairInHand;",
				"if (part.LeakPhase != (int)KingdomWearLeakPhase.None) return LeakOpen;",
				"if (part.LifecycleQuarantined || part.IncidentPhase == (int)KingdomWearIncidentPhase.Quarantined) return QuarantineHeld;",
				"return null;");
			foreach (string banned in new[] { "BlocksWork(", "ReceiptBlocksCurrent(", "HasActiveRepair(" })
				StringAssert.DoesNotContain(banned, Flat(TestMain.ReadRepositoryText(Runtime)));
			// The fenced fields must still be the ones the part actually carries.
			Has(Flat(TestMain.ReadRepositoryText(Part)), "public int Wear;", "public int RepairEffortLeft;",
				"public bool LifecycleQuarantined;", "public int LeakPhase;", "public string IncidentId;",
				"public int IncidentPhase;", "public int IncidentCause;", "public int IncidentBeforeWear;",
				"public int IncidentAfterWear;");
		}

		[Test]
		public void SourceContractReusesTheExistingCompareAndSwapAndTheSubsidenceCause()
		{
			Has(Method(Runtime, Apply), "if (KingdomSubsidenceRungRules.WearAction(plan, index, reprovesAuthority(), "
				+ "present != null, present == null ? 0 : present.Wear) == KingdomSubsidenceEffectAction.Refuse) "
				+ "{ refusal = NotAdmitted; return false; }");
			// The reused compare-and-swap must still demand exactAuthority in its own file.
			Has(Flat(TestMain.ReadRepositoryText(Effects)),
				"internal static KingdomSubsidenceEffectAction WearAction(KingdomSubsidenceRungPlan plan, int index, bool exactAuthority, bool hasPart, int observedWear)",
				"if (!exactAuthority || !AtFrontier(plan, index)) return KingdomSubsidenceEffectAction.Refuse;");
			string code = Code(Runtime);
			Assert.AreEqual(3, Count(code, "KingdomWearRules.WearCause.Subsidence"),
				"The cause is stamped on a new part, on an existing part, and compared in the receipt.");
			// The receipt phases reused here are the part's existing ones, unchanged.
			Has(Flat(TestMain.ReadRepositoryText(Declarations)), "public enum KingdomWearIncidentPhase",
				"Bound = 1,", "MutationIntent = 2,", "Mutated = 3,");
		}

		[Test]
		public void SourceContractReMeasuresTheCapturedReadingAfterAdmissionBeforeAnyBoundStamp()
		{
			string body = Method(Runtime, Apply);
			// The compare-and-swap runs the callback, so the captured reading is re-measured on the
			// same reference before a single Bound field is written.
			Ordered(body, Capture, "if (KingdomSubsidenceRungRules.WearAction(", Guard,
				"instance.IncidentPhase = (int)KingdomWearIncidentPhase.Bound;");
			string code = Code(Runtime);
			int guard = code.IndexOf(Guard, StringComparison.Ordinal);
			Assert.That(guard, Is.GreaterThan(0), "The post-admission re-measure must exist.");
			Assert.AreEqual(2, Count(code, "= (int)KingdomWearIncidentPhase.Bound;"),
				"Bound is stamped on the allocated instance and on an unbound existing part only.");
			foreach (string stamp in new[] { "instance.IncidentPhase = (int)KingdomWearIncidentPhase.Bound;",
				"present.IncidentPhase = (int)KingdomWearIncidentPhase.Bound;" })
				Assert.That(code.IndexOf(stamp, StringComparison.Ordinal), Is.GreaterThan(guard),
					"No Bound field is written before the re-measure: " + stamp);
			Assert.That(code.IndexOf(Capture, StringComparison.Ordinal),
				Is.LessThan(code.IndexOf("KingdomSubsidenceRungRules.WearAction(", StringComparison.Ordinal)),
				"The reading is captured before the callback runs.");
		}

		[Test]
		public void SourceContractStampsTheBoundReceiptBeforeItEverOffersThePart()
		{
			Ordered(Method(Runtime, Apply), "r_KingdomWear instance = new r_KingdomWear();",
				"instance.Wear = row.BeforeWear;", "instance.IncidentId = plan.StepId;",
				"instance.IncidentCause = (int)KingdomWearRules.WearCause.Subsidence;",
				"instance.IncidentBeforeWear = row.BeforeWear;",
				"instance.IncidentAfterWear = row.AfterWear;",
				"instance.IncidentPhase = (int)KingdomWearIncidentPhase.Bound;",
				"Exception attach = null;",
				"try { work.AddPart(instance); }",
				"catch (Exception raised) { attach = raised; }");
			string code = Code(Runtime);
			int allocated = code.IndexOf("new r_KingdomWear(", StringComparison.Ordinal);
			int attached = code.IndexOf("work.AddPart(", StringComparison.Ordinal);
			Assert.That(allocated, Is.GreaterThanOrEqualTo(0), "The part must be allocated here.");
			Assert.That(attached, Is.GreaterThan(allocated), "Allocation precedes attachment.");
			Assert.AreEqual(1, Count(code, "AddPart("), "Exactly one attachment call may exist.");
			Assert.AreEqual(1, Count(code, "new r_KingdomWear("),
				"Exactly one allocation may exist; a replacement is never minted.");
		}

		[Test]
		public void SourceContractRetainsTheAttachThrowAndReadsItsTypeOnlyInTheRefusal()
		{
			// The caught object is retained in a local and never inspected until the measured
			// attachment has failed; only then does its TYPE join the refusal text, and the evidence
			// root handed back there is the allocated instance, not GetPart's answer.
			Ordered(Method(Runtime, Apply), "catch (Exception raised) { attach = raised; }",
				"if (!SameAttachment(work, instance)) { observed = instance; refusal = PartNotExact "
					+ "+ (attach == null ? \"\" : \" (\" + attach.GetType().Name + \")\"); return false; }",
				"observed = instance; if (!reprovesAuthority()) { refusal = AuthorityLost; return false; }");
			string code = Code(Runtime);
			Assert.AreEqual(1, Count(code, "attach.GetType()"),
				"The exception's type is read once, inside the refusal text.");
			Assert.AreEqual(1, Count(code, "GetType()"), "No other exception metadata is read.");
			foreach (string banned in new[] { "attach.Message", "attach.ToString(", "attach.StackTrace",
				"raised.Message", "raised.GetType(", "raised.ToString(" })
				StringAssert.DoesNotContain(banned, code);
			Assert.That(code.IndexOf("attach.GetType()", StringComparison.Ordinal),
				Is.GreaterThan(code.IndexOf("if (!SameAttachment(work, instance))", StringComparison.Ordinal)),
				"Nothing about the throw is read before the attachment is measured.");
		}

		[Test]
		public void SourceContractMeasuresTheAttachmentBeforeAuthorityAndBeforeAnyMutation()
		{
			string body = Method(Runtime, Apply);
			Ordered(body, "try { work.AddPart(instance); }",
				"if (!SameAttachment(work, instance))",
				"if (!reprovesAuthority()) { refusal = AuthorityLost; return false; }",
				"observed.Wear = row.AfterWear;");
			// The duplicate scan reads the same list GetPart reads, so a replacement cannot hide.
			Ordered(Method(Runtime, "private static int Copies(GameObject work)"),
				"int count = work.PartsList == null ? 0 : work.PartsList.Count;",
				"for (int i = 0; i < count; i++) if (work.PartsList[i] is r_KingdomWear) copies++;");
			Ordered(Method(Runtime, "private static bool SameAttachment(GameObject work, r_KingdomWear part)"),
				"ReferenceEquals(work.GetPart<r_KingdomWear>(), part)",
				"ReferenceEquals(part.ParentObject, work)", "Copies(work) == 1");
			// An existing part keeps the exact same reference across every seam, and is never removed.
			Has(body, "observed = present; if (present.IncidentPhase == (int)KingdomWearIncidentPhase.None)",
				"present.IncidentPhase = (int)KingdomWearIncidentPhase.Bound;",
				"if (!SameAttachment(work, observed) || !SameReceipt(observed, plan, row)) { refusal = PartNotExact; return false; }");
			Assert.AreEqual(1, Count(Code(Runtime), "= work.GetPart<r_KingdomWear>();"),
				"The part is fetched into a local exactly once and never re-fetched.");
			// The type test in Copies is broader than GetPart's exact-type match, on purpose.
			Has(Flat(TestMain.ReadRepositoryText(Runtime)),
				"matches a SUBCLASS, which GetPart's exact-type comparison walks straight past; that is");
		}

		[Test]
		public void SourceContractReProvesEverythingBeforeStampingMutationIntentAndTheOneWrite()
		{
			string body = Method(Runtime, Apply);
			Ordered(body, "if (observed.Wear != row.AfterWear)",
				"if (observed.Wear != row.BeforeWear) { refusal = BeforeChanged; return false; }",
				PreWrite, PostWrite, ConfirmSeam);
			string code = Code(Runtime);
			Assert.AreEqual(1, Count(code, "observed.Wear = "), "Exactly one wear write may exist.");
			Assert.AreEqual(2, Count(code, ".Wear = "),
				"Only the pre-attachment before-state and the one mutation write a wear field.");
			int intent = code.IndexOf("observed.IncidentPhase = (int)KingdomWearIncidentPhase.MutationIntent;",
				StringComparison.Ordinal);
			int write = code.IndexOf("observed.Wear = ", StringComparison.Ordinal);
			int mutated = code.IndexOf("observed.IncidentPhase = (int)KingdomWearIncidentPhase.Mutated;",
				StringComparison.Ordinal);
			Assert.That(intent, Is.GreaterThanOrEqualTo(0), "MutationIntent must be persisted.");
			Assert.That(write, Is.GreaterThan(intent), "MutationIntent precedes the write.");
			Assert.That(mutated, Is.GreaterThan(write), "Mutated follows the write.");
			// The complete fence set is one helper, re-measured on the exact bound reference at both
			// mutation seams and on the confirm seam; the callback is trusted for custody alone.
			Ordered(Method(Runtime, Fence),
				"string blocked = WorkFenceRefusal(work, part);", "if (blocked != null) return blocked;",
				"if (!SameReceipt(part, plan, row) || !BindablePhase(part.IncidentPhase) || (phase != AnyBindablePhase && part.IncidentPhase != phase)) return ForeignIncident;",
				"if (part.Wear != wear) return wear == row.BeforeWear ? BeforeChanged : AfterNotExact;",
				"return null;");
			Assert.AreEqual(4, Count(code, " FenceRefusal("),
				"One declaration, both mutation seams, and the idempotent-confirm seam.");
			Assert.AreEqual(3, Count(code, "WorkFenceRefusal("),
				"One declaration, the post-admission re-measure, and the full fence set.");
			Has(Flat(TestMain.ReadRepositoryText(Runtime)),
				"phase and wear. reprovesAuthority proves owner, parent and work custody ONLY: never wear,",
				"does -- so this ONE recheck covers both.");
			// The receipt this step reads back is the exact frozen one, never a looser match.
			Ordered(Method(Runtime, "private static bool SameReceipt("),
				"string.Equals(part.IncidentId, plan.StepId, StringComparison.Ordinal)",
				"part.IncidentCause == (int)KingdomWearRules.WearCause.Subsidence",
				"part.IncidentBeforeWear == row.BeforeWear", "part.IncidentAfterWear == row.AfterWear");
			Ordered(Method(Runtime, "private static bool BindablePhase(int phase)"),
				"phase == (int)KingdomWearIncidentPhase.Bound",
				"phase == (int)KingdomWearIncidentPhase.MutationIntent",
				"phase == (int)KingdomWearIncidentPhase.Mutated");
		}

		[Test]
		public void SourceContractRefusesAMutatedReceiptWhoseWearNoLongerReadsTheAfterState()
		{
			string body = Method(Runtime, Apply);
			// A spent receipt is refused, never regressed and re-applied; a spent receipt whose
			// after-state still stands confirms idempotently, rewriting nothing but its own stamp.
			Ordered(body,
				"if (present != null && present.IncidentPhase == (int)KingdomWearIncidentPhase.Mutated && present.Wear != row.AfterWear) { refusal = NotAdmitted; return false; }",
				"if (KingdomSubsidenceRungRules.WearAction(",
				"if (observed.IncidentPhase == (int)KingdomWearIncidentPhase.Mutated) { refusal = NotAdmitted; return false; }",
				"observed.IncidentPhase = (int)KingdomWearIncidentPhase.MutationIntent;");
			Has(body, ConfirmSeam);
			string code = Code(Runtime);
			Assert.AreEqual(1, Count(code, "= (int)KingdomWearIncidentPhase.MutationIntent;"),
				"MutationIntent is stamped once, and never as a regression from Mutated.");
			Assert.AreEqual(0, Count(code, "observed.Wear = row.BeforeWear"),
				"The wear is never written back down to the before-state.");
			Assert.AreEqual(2, Count(code, "observed.IncidentPhase = (int)KingdomWearIncidentPhase.Mutated;"),
				"The terminal stamp is written on the mutation tail and on the confirm tail only.");
		}

		[Test]
		public void SourceContractNeverCallsTheOldIncidentPathTellsNothingAndRemovesNothing()
		{
			string source = Flat(TestMain.ReadRepositoryText(Runtime));
			foreach (string banned in new[] { "ApplyDamageIncident(", "RemovePart(", "Obliterate(",
				"Destroy(", "RequirePart<", "QuarantineWear(", "TellWearQuarantine(",
				"DeliverWearMessage(", "KingdomChronicle.", "MessageBox", "AddPlayerMessage",
				"IncidentLine", "LastCompletedIncidentId", "KingdomWearIncidentPhase.ChronicleDone",
				"KingdomWearIncidentPhase.Complete" })
				StringAssert.DoesNotContain(banned, source);
			// The old path still owns those phases; this file simply never enters them.
			Has(Flat(TestMain.ReadRepositoryText("Growth/KingdomWear.06.DamageIncidents.cs")),
				"private static bool ApplyDamageIncident(");
		}

		[Test]
		public void SourceContractOnlyReleaseMaySkipConstructionLocationAdmission()
		{
			Has(Method(Construction, "internal static bool ConstructionAvailable(GameObject work)"),
				"{ return ConstructionAvailable(work, true); }");
			Has(Method(Construction, "internal static bool ConstructionAvailableForRelease(GameObject work)"),
				"{ return ConstructionAvailable(work, false); }");
			string common = Method(Construction, "private static bool ConstructionAvailable(GameObject work, bool requireCell)");
			Ordered(common, "XRLGame game = The.Game;", "!GameObject.Validate(work)",
				"string.IsNullOrEmpty(work.IDIfAssigned)", "(requireCell && work.CurrentCell == null)",
				"game == null", "work.HasIntProperty(KingdomConstruction.ReceiptProperty)) return false;",
				"KingdomScenarioStateShape.TryAuthorityText(");
			Assert.AreEqual(1, Count(common, "requireCell && work.CurrentCell == null"));
			StringAssert.DoesNotContain("ConstructionAvailableForRelease(", Code(Runtime));
		}

		[Test]
		public void SourceContractReleaseSharesFiveTableShapeAndVersionedRegistryDecoder()
		{
			string common = Method(Construction, "private static bool ConstructionAvailable(GameObject work, bool requireCell)");
			foreach (string table in new[] { "String", "Int", "Int64", "Object", "Boolean" })
				Has(common, "game." + table + "GameState == null", "game.Has" + table + "GameState(key)");
			Ordered(common, "string key = KingdomConstruction.RegistryStateKey;",
				"KingdomDurableKeyObservation observed = new KingdomDurableKeyObservation",
				"if (!KingdomScenarioStateShape.TryAuthorityText(observed, out string wire, out bool present, out _)) return false;",
				"if (!present) return true;",
				"if (!KingdomConstructionRules.TryDecode(wire, out List<KingdomConstructionJob> jobs)) return false;",
				"foreach (KingdomConstructionJob job in jobs)");
			Assert.AreEqual(1, Count(Code(Construction), "KingdomScenarioStateShape.TryAuthorityText("));
			Assert.AreEqual(1, Count(Code(Construction), "KingdomConstructionRules.TryDecode("));
		}

		[Test]
		public void SourceContractUnplacedReleaseStillRejectsReceiptAndEveryBoundIdentityConflict()
		{
			string common = Method(Construction, "private static bool ConstructionAvailable(GameObject work, bool requireCell)");
			Ordered(common, "string receipt = work.GetStringProperty(KingdomConstruction.ReceiptProperty);",
				"foreach (KingdomConstructionJob job in jobs)", "if (KingdomConstructionRules.IsTerminal(job.Phase)) continue;",
				"if (job.Id == receipt || job.SubjectId == work.IDIfAssigned || job.SourceId == work.IDIfAssigned",
				"|| job.OutputId == work.IDIfAssigned || job.PhysicalItemId == work.IDIfAssigned",
				"|| job.PhysicalDestinationId == work.IDIfAssigned",
				"|| work.CurrentCell != null && job.ZoneId == work.CurrentZone?.ZoneID && job.X == work.CurrentCell.X && job.Y == work.CurrentCell.Y) return false;",
				"return true;");
			foreach (string banned in new[] { "RequireSystem", "RequirePart", "TryResumeFunding", "RetryConstruction",
				"InspectConstruction", "SetString", "SetInt", ".ID;", ".ID)", "GetZone(", ".Load(" })
				StringAssert.DoesNotContain(banned, Code(Construction));
		}

		private static int Count(string source, string needle)
		{
			int total = 0;
			for (int i = source.IndexOf(needle, StringComparison.Ordinal); i >= 0;
				i = source.IndexOf(needle, i + 1, StringComparison.Ordinal)) total++;
			return total;
		}
		private static string Flat(string source) => System.Text.RegularExpressions.Regex.Replace(source, @"\s+", " ");
		/// <summary>The flattened source with whole-line comments removed, so a count pin measures
		/// code and a reflowed comment can never move it.</summary>
		private static string Code(string path)
		{
			string[] lines = TestMain.ReadRepositoryText(path).Split('\n');
			System.Collections.Generic.List<string> kept = new System.Collections.Generic.List<string>();
			foreach (string line in lines)
				if (!line.TrimStart().StartsWith("//", StringComparison.Ordinal)) kept.Add(line);
			return Flat(string.Join("\n", kept));
		}
		private static void Has(string source, params string[] needles)
		{ foreach (string needle in needles) StringAssert.Contains(needle, source); }
		private static void Ordered(string source, params string[] needles)
		{
			int previous = -1;
			foreach (string needle in needles)
			{
				int current = source.IndexOf(needle, previous + 1, StringComparison.Ordinal);
				Assert.That(current, Is.GreaterThan(previous), needle);
				previous = current;
			}
		}
		private static string Method(string path, string signature)
		{
			string source = Flat(TestMain.ReadRepositoryText(path));
			int start = source.IndexOf(Flat(signature), StringComparison.Ordinal);
			Assert.That(start, Is.GreaterThanOrEqualTo(0), path + ": " + signature);
			int open = source.IndexOf('{', start), depth = 0;
			Assert.GreaterOrEqual(open, 0, "Unopened source method: " + path + ": " + signature);
			for (int i = open; i < source.Length; i++)
			{
				if (source[i] == '{') depth++;
				else if (source[i] == '}' && --depth == 0) return Flat(source.Substring(start, i - start + 1));
			}
			Assert.Fail("Unclosed source method: " + path + ": " + signature);
			return null;
		}
	}
}
#endif
