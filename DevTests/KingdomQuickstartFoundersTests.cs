#if TAF_TESTS
using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The founding cohort as pure rules: the wire that carries it, the ladder that constrains it,
	/// the moves it may make, and the source contracts the engine halves owe.
	/// </summary>
	[TestFixture]
	public sealed class KingdomQuickstartFoundersTests
	{
		private const string MarshZone = "JoppaWorld.8.22.1.1.10";

		// ---- save compatibility: a world made before founders can never gain them ----------

		[Test]
		public void APreChangeCompleteReceiptDecodesWithNoCohortReEncodesExactlyAndIsFinished()
		{
			// Rebuilt from the shipped eleven-field body and its own SHA-256, not through the
			// current encoder, so this fixture cannot agree with a mistake in the code it
			// constrains. This is the wire a v0.3.1 world at Complete actually holds.
			string wire = LegacyWire("q1", 6, "Watervine", "water-id", "larder-id",
				"materials-id", 1, "advisor-id");
			Assert.That(KingdomQuickstartRules.TryDecode(wire,
				out KingdomQuickstartReceipt old), Is.True);
			Assert.That(old.Phase, Is.EqualTo(KingdomQuickstartPhase.Complete));
			Assert.That(old.FoundersDisposition,
				Is.EqualTo(KingdomQuickstartFoundersDisposition.Omitted));
			Assert.That(old.FounderObjectIds.All(string.IsNullOrEmpty), Is.True);
			Assert.That(KingdomQuickstartRules.Encode(old), Is.EqualTo(wire));
			ClassicAssert.IsTrue(KingdomQuickstartRules.IsTerminal(old));
			// Nothing can move it: the two within-Complete moves both require Pending or Seeding.
			ClassicAssert.IsFalse(KingdomQuickstartRules.TryRestateFounders(old,
				KingdomQuickstartFoundersDisposition.Seeding, Ids(), out _));
			ClassicAssert.IsFalse(KingdomQuickstartRules.TryAdvance(old,
				KingdomQuickstartPhase.FoundersSeeded, "",
				KingdomQuickstartAdvisorDisposition.Unresolved, out _));
		}

		[Test]
		public void AShelterEraCompleteReceiptIsAlsoFinishedAndKeepsItsExactBytes()
		{
			string wire = LegacyWire("q2", 6, "Watervine", "water-id", "larder-id",
				"materials-id", 2, "");
			Assert.That(KingdomQuickstartRules.TryDecode(wire,
				out KingdomQuickstartReceipt old), Is.True);
			ClassicAssert.IsTrue(old.ShelterObligation);
			Assert.That(old.FoundersDisposition,
				Is.EqualTo(KingdomQuickstartFoundersDisposition.Omitted));
			Assert.That(KingdomQuickstartRules.Encode(old), Is.EqualTo(wire));
			ClassicAssert.IsTrue(KingdomQuickstartRules.IsTerminal(old));
		}

		[Test]
		public void AWorldFoundedWithTheOptionOffIsWrittenOnTheOldWireAndIsNeverReAttempted()
		{
			KingdomQuickstartReceipt off = Fresh(KingdomQuickstartFoundersDisposition.Omitted);
			string wire = KingdomQuickstartRules.Encode(off);
			Assert.That(wire.Split('|').Length, Is.EqualTo(11));
			StringAssert.StartsWith("q2|", wire);
			KingdomQuickstartReceipt complete = Complete(off);
			ClassicAssert.IsTrue(KingdomQuickstartRules.IsTerminal(complete));
			Assert.That(KingdomQuickstartRules.Encode(complete).Split('|').Length,
				Is.EqualTo(11));
			// The disposition is frozen at creation. There is no path that re-reads an option.
			string rules = TestMain.ReadRepositoryText("Core/KingdomQuickstartRules.cs");
			StringAssert.DoesNotContain("Options.", rules);
			string founders = TestMain.ReadRepositoryText(
				"Core/KingdomQuickstartRules.Founders.cs");
			StringAssert.DoesNotContain("Options.", founders);
			string bootstrap = TestMain.ReadRepositoryText(
				"World/KingdomQuickstartBootstrap.cs");
			Assert.That(Count(bootstrap, "KingdomQuickstartRules.FoundersOption"),
				Is.EqualTo(1));
		}

		[Test]
		public void AWorldFoundedWithTheOptionOnIsOwedACohortFromItsFirstPublish()
		{
			KingdomQuickstartReceipt on = Fresh(KingdomQuickstartFoundersDisposition.Pending);
			string wire = KingdomQuickstartRules.Encode(on);
			Assert.That(wire.Split('|').Length, Is.EqualTo(16));
			StringAssert.StartsWith("q4|", wire);
			ClassicAssert.IsFalse(KingdomQuickstartRules.IsTerminal(on));
			KingdomQuickstartReceipt complete = Complete(on);
			ClassicAssert.IsFalse(KingdomQuickstartRules.IsTerminal(complete));
		}

		[TestCase(KingdomQuickstartFoundersDisposition.Seeding)]
		[TestCase(KingdomQuickstartFoundersDisposition.Seeded)]
		[TestCase(KingdomQuickstartFoundersDisposition.Faulted)]
		public void OnlyOmittedOrPendingMayStartAWorld(
			KingdomQuickstartFoundersDisposition disposition)
		{
			ClassicAssert.IsFalse(KingdomQuickstartRules.TryCreateReceipt("marsh", MarshZone,
				disposition, out _));
		}

		// ---- the wire ----------------------------------------------------------------------

		[Test]
		public void TheFoundersWireIsSixteenFieldsAndRoundTripsExactly()
		{
			KingdomQuickstartReceipt seeding = Seeding();
			string wire = KingdomQuickstartRules.Encode(seeding);
			string[] fields = wire.Split('|');
			Assert.That(fields.Length, Is.EqualTo(16));
			Assert.That(fields[10], Is.EqualTo("2"));
			for (int i = 0; i < 4; i++)
				Assert.That(Text(fields[11 + i]), Is.EqualTo("founder-" + i));
			Assert.That(KingdomQuickstartRules.TryDecode(wire,
				out KingdomQuickstartReceipt decoded), Is.True);
			Assert.That(decoded.FoundersDisposition,
				Is.EqualTo(KingdomQuickstartFoundersDisposition.Seeding));
			CollectionAssert.AreEqual(Ids(), decoded.FounderObjectIds);
			Assert.That(KingdomQuickstartRules.Encode(decoded), Is.EqualTo(wire));
		}

		[Test]
		public void AWireOfTheWrongLengthOrDigestIsRefused()
		{
			string wire = KingdomQuickstartRules.Encode(Seeding());
			string[] fields = wire.Split('|');
			// Fifteen fields under the founders tag, and seventeen: both refused on length alone.
			ClassicAssert.IsFalse(KingdomQuickstartRules.TryDecode(
				string.Join("|", fields.Take(15)), out _));
			ClassicAssert.IsFalse(KingdomQuickstartRules.TryDecode(wire + "|extra", out _));
			// And the tag is inside the digest, so no edit can promote or demote a form in place.
			char swap = wire[wire.Length - 1] == '0' ? '1' : '0';
			ClassicAssert.IsFalse(KingdomQuickstartRules.TryDecode(
				wire.Substring(0, wire.Length - 1) + swap, out _));
			ClassicAssert.IsFalse(KingdomQuickstartRules.TryDecode(
				"q3" + wire.Substring(2), out _));
		}

		[Test]
		public void AMaximalFoundersWireStillFitsEveryDurableBudget()
		{
			KingdomQuickstartReceipt receipt = Complete(
				Fresh(KingdomQuickstartFoundersDisposition.Pending));
			string stem = new string('x', 511);
			string[] ids = { stem + "0", stem + "1", stem + "2", stem + "3" };
			Assert.That(KingdomQuickstartRules.TryRestateFounders(receipt,
				KingdomQuickstartFoundersDisposition.Seeding, ids,
				out KingdomQuickstartReceipt seeding), Is.True);
			string wire = KingdomQuickstartRules.Encode(seeding);
			Assert.That(wire, Is.Not.Null);
			// MaximumWireLength and the save snapshot's MaxReceiptChars are both 4096, and the
			// snapshot's own byte budget grows by this wire's frame.
			Assert.That(wire.Length, Is.LessThan(4096));
			Assert.That(KingdomQuickstartRules.TryDecode(wire, out _), Is.True);
			// A field one character longer than the field cap is refused outright.
			string[] over = { new string('x', 513), "b", "c", "d" };
			ClassicAssert.IsFalse(KingdomQuickstartRules.TryRestateFounders(receipt,
				KingdomQuickstartFoundersDisposition.Seeding, over, out _));
		}

		// ---- the ladder --------------------------------------------------------------------

		[Test]
		public void EveryDispositionAndIdCombinationOutsideTheTableIsRefused()
		{
			KingdomQuickstartReceipt complete = Complete(
				Fresh(KingdomQuickstartFoundersDisposition.Pending));
			// A cohort disposition with empty ids.
			ClassicAssert.IsFalse(KingdomQuickstartRules.Valid(
				With(complete, KingdomQuickstartFoundersDisposition.Seeding, Blank())));
			// A no-cohort disposition naming somebody.
			ClassicAssert.IsFalse(KingdomQuickstartRules.Valid(
				With(complete, KingdomQuickstartFoundersDisposition.Pending, Ids())));
			ClassicAssert.IsFalse(KingdomQuickstartRules.Valid(
				With(complete, KingdomQuickstartFoundersDisposition.Omitted, Ids())));
			// Four ids that are not four people.
			ClassicAssert.IsFalse(KingdomQuickstartRules.Valid(With(complete,
				KingdomQuickstartFoundersDisposition.Seeding,
				new[] { "a", "b", "c", "a" })));
			ClassicAssert.IsFalse(KingdomQuickstartRules.Valid(With(complete,
				KingdomQuickstartFoundersDisposition.Seeding,
				new[] { "a", "b", "c", "d|e" })));
			// A cohort below Complete cannot exist at all.
			KingdomQuickstartReceipt founded = Advance(
				Fresh(KingdomQuickstartFoundersDisposition.Pending),
				KingdomQuickstartPhase.Founded, "Watervine");
			ClassicAssert.IsFalse(KingdomQuickstartRules.Valid(
				With(founded, KingdomQuickstartFoundersDisposition.Seeding, Ids())));
			// An undefined disposition byte.
			ClassicAssert.IsFalse(KingdomQuickstartRules.Valid(
				With(complete, (KingdomQuickstartFoundersDisposition)9, Blank())));
			// Phase eight does not exist.
			KingdomQuickstartReceipt seeded = Seeded();
			KingdomQuickstartReceipt beyond = seeded.Copy();
			beyond.Phase = (KingdomQuickstartPhase)8;
			ClassicAssert.IsFalse(KingdomQuickstartRules.Valid(beyond));
		}

		[Test]
		public void SeededIsTheOnlyDispositionTheSeededPhaseAdmitsAndItsOnlyPhase()
		{
			KingdomQuickstartReceipt seeded = Seeded();
			ClassicAssert.IsTrue(KingdomQuickstartRules.Valid(seeded));
			foreach (KingdomQuickstartFoundersDisposition other in new[]
			{
				KingdomQuickstartFoundersDisposition.Seeding,
				KingdomQuickstartFoundersDisposition.Faulted
			})
				ClassicAssert.IsFalse(KingdomQuickstartRules.Valid(
					With(seeded, other, Ids())));
			KingdomQuickstartReceipt atComplete = seeded.Copy();
			atComplete.Phase = KingdomQuickstartPhase.Complete;
			ClassicAssert.IsFalse(KingdomQuickstartRules.Valid(atComplete));
		}

		[TestCase(KingdomQuickstartFoundersDisposition.Omitted, true)]
		[TestCase(KingdomQuickstartFoundersDisposition.Pending, false)]
		[TestCase(KingdomQuickstartFoundersDisposition.Seeding, false)]
		[TestCase(KingdomQuickstartFoundersDisposition.Faulted, true)]
		public void TerminalAtCompleteIsExactlyOmittedAndFaulted(
			KingdomQuickstartFoundersDisposition disposition, bool terminal)
		{
			KingdomQuickstartReceipt complete = Complete(Fresh(
				disposition == KingdomQuickstartFoundersDisposition.Omitted
					? KingdomQuickstartFoundersDisposition.Omitted
					: KingdomQuickstartFoundersDisposition.Pending));
			bool cohort = disposition == KingdomQuickstartFoundersDisposition.Seeding
				|| disposition == KingdomQuickstartFoundersDisposition.Faulted;
			KingdomQuickstartReceipt subject = With(complete, disposition,
				cohort ? Ids() : Blank());
			ClassicAssert.IsTrue(KingdomQuickstartRules.Valid(subject));
			Assert.That(KingdomQuickstartRules.IsTerminal(subject), Is.EqualTo(terminal));
			// And a receipt below Complete is never terminal, whatever it holds.
			ClassicAssert.IsFalse(KingdomQuickstartRules.IsTerminal(
				Fresh(KingdomQuickstartFoundersDisposition.Omitted)));
		}

		// ---- the moves ---------------------------------------------------------------------

		[Test]
		public void OnlyThreeRestatementsExistAndEachDemandsItsOwnIds()
		{
			KingdomQuickstartReceipt pending = Complete(
				Fresh(KingdomQuickstartFoundersDisposition.Pending));
			// Pending to Seeding names four; naming nobody, or three, or two of the same, refuses.
			ClassicAssert.IsTrue(KingdomQuickstartRules.TryRestateFounders(pending,
				KingdomQuickstartFoundersDisposition.Seeding, Ids(),
				out KingdomQuickstartReceipt seeding));
			ClassicAssert.IsFalse(KingdomQuickstartRules.TryRestateFounders(pending,
				KingdomQuickstartFoundersDisposition.Seeding, null, out _));
			ClassicAssert.IsFalse(KingdomQuickstartRules.TryRestateFounders(pending,
				KingdomQuickstartFoundersDisposition.Seeding, new[] { "a", "b", "c" }, out _));
			// Pending to Pending clears, and names nobody. It is a lawful no-op.
			ClassicAssert.IsTrue(KingdomQuickstartRules.TryRestateFounders(pending,
				KingdomQuickstartFoundersDisposition.Pending, null,
				out KingdomQuickstartReceipt cleared));
			Assert.That(KingdomQuickstartRules.Encode(cleared),
				Is.EqualTo(KingdomQuickstartRules.Encode(pending)));
			ClassicAssert.IsFalse(KingdomQuickstartRules.TryRestateFounders(pending,
				KingdomQuickstartFoundersDisposition.Pending, Ids(), out _));
			// Seeding to Faulted keeps the exact four it was seeding.
			ClassicAssert.IsTrue(KingdomQuickstartRules.TryRestateFounders(seeding,
				KingdomQuickstartFoundersDisposition.Faulted, null,
				out KingdomQuickstartReceipt faulted));
			CollectionAssert.AreEqual(Ids(), faulted.FounderObjectIds);
			// Every other pair is refused, including going back and being said twice.
			ClassicAssert.IsFalse(KingdomQuickstartRules.TryRestateFounders(seeding,
				KingdomQuickstartFoundersDisposition.Pending, null, out _));
			ClassicAssert.IsFalse(KingdomQuickstartRules.TryRestateFounders(seeding,
				KingdomQuickstartFoundersDisposition.Seeded, null, out _));
			ClassicAssert.IsFalse(KingdomQuickstartRules.TryRestateFounders(faulted,
				KingdomQuickstartFoundersDisposition.Faulted, null, out _));
			ClassicAssert.IsFalse(KingdomQuickstartRules.TryRestateFounders(faulted,
				KingdomQuickstartFoundersDisposition.Seeding, Ids(), out _));
			ClassicAssert.IsFalse(KingdomQuickstartRules.TryRestateFounders(Seeded(),
				KingdomQuickstartFoundersDisposition.Faulted, null, out _));
		}

		[Test]
		public void TheCohortClosesOnlyFromSeedingAndOnlyByOnePhaseAdvance()
		{
			KingdomQuickstartReceipt seeding = Seeding();
			Assert.That(KingdomQuickstartRules.TryAdvance(seeding,
				KingdomQuickstartPhase.FoundersSeeded, "",
				KingdomQuickstartAdvisorDisposition.Unresolved,
				out KingdomQuickstartReceipt seeded), Is.True);
			Assert.That(seeded.FoundersDisposition,
				Is.EqualTo(KingdomQuickstartFoundersDisposition.Seeded));
			CollectionAssert.AreEqual(Ids(), seeded.FounderObjectIds);
			ClassicAssert.IsTrue(KingdomQuickstartRules.IsTerminal(seeded));
			// A value, an advisor decision, or a disposition that is not Seeding all refuse.
			ClassicAssert.IsFalse(KingdomQuickstartRules.TryAdvance(seeding,
				KingdomQuickstartPhase.FoundersSeeded, "something",
				KingdomQuickstartAdvisorDisposition.Unresolved, out _));
			ClassicAssert.IsFalse(KingdomQuickstartRules.TryAdvance(seeding,
				KingdomQuickstartPhase.FoundersSeeded, "",
				KingdomQuickstartAdvisorDisposition.Included, out _));
			foreach (KingdomQuickstartFoundersDisposition other in new[]
			{
				KingdomQuickstartFoundersDisposition.Omitted,
				KingdomQuickstartFoundersDisposition.Pending,
				KingdomQuickstartFoundersDisposition.Faulted
			})
			{
				bool cohort = other == KingdomQuickstartFoundersDisposition.Faulted;
				ClassicAssert.IsFalse(KingdomQuickstartRules.TryAdvance(
					With(Complete(Fresh(KingdomQuickstartFoundersDisposition.Pending)), other,
						cohort ? Ids() : Blank()),
					KingdomQuickstartPhase.FoundersSeeded, "",
					KingdomQuickstartAdvisorDisposition.Unresolved, out _));
			}
			// And nothing advances past it.
			ClassicAssert.IsFalse(KingdomQuickstartRules.TryAdvance(seeded,
				(KingdomQuickstartPhase)8, "",
				KingdomQuickstartAdvisorDisposition.Unresolved, out _));
		}

		[Test]
		public void RecoveryStillDecidesEveryGrantCutOnceAtTheSeededPhase()
		{
			// The grant boundaries are unchanged by the cohort, but the current-phase ceiling
			// follows the enum: a seeded world resuming must still verify its published grants.
			for (int target = (int)KingdomQuickstartPhase.WaterStocked;
				target <= (int)KingdomQuickstartPhase.AdvisorResolved; target++)
				Assert.That(KingdomQuickstartRules.RecoveryAction(
					KingdomQuickstartPhase.FoundersSeeded, (KingdomQuickstartPhase)target,
					KingdomQuickstartGrantObservation.ExactPlaced),
					Is.EqualTo(KingdomQuickstartRecoveryAction.VerifyPublished));
			Assert.That(KingdomQuickstartRules.RecoveryAction(
				(KingdomQuickstartPhase)8, KingdomQuickstartPhase.WaterStocked,
				KingdomQuickstartGrantObservation.ExactPlaced),
				Is.EqualTo(KingdomQuickstartRecoveryAction.Refuse));
		}

		// ---- markers and ground ------------------------------------------------------------

		[Test]
		public void FounderMarkersAreFourDistinctStableMarksBoundToProfileGroundAndFood()
		{
			KingdomQuickstartReceipt pending = Complete(
				Fresh(KingdomQuickstartFoundersDisposition.Pending));
			string[] marks = new string[4];
			for (int i = 0; i < 4; i++)
			{
				marks[i] = KingdomQuickstartRules.FounderMarker(pending, i);
				Assert.That(marks[i], Is.Not.Null.And.StartWith("qg2|"));
				for (int j = 0; j < i; j++) Assert.That(marks[i], Is.Not.EqualTo(marks[j]));
				// A grant marker is never a founder marker.
				for (int phase = (int)KingdomQuickstartPhase.WaterStocked;
					phase <= (int)KingdomQuickstartPhase.AdvisorResolved; phase++)
					Assert.That(marks[i], Is.Not.EqualTo(KingdomQuickstartRules.GrantMarker(
						pending, (KingdomQuickstartPhase)phase)));
			}
			// Stable across the cohort's own state changes: the same mark proves a body on both
			// sides of the fence and on both sides of the closing advance.
			KingdomQuickstartReceipt seeded = Seeded();
			for (int i = 0; i < 4; i++)
				Assert.That(KingdomQuickstartRules.FounderMarker(seeded, i),
					Is.EqualTo(marks[i]));
			ClassicAssert.IsNull(KingdomQuickstartRules.FounderMarker(pending, -1));
			ClassicAssert.IsNull(KingdomQuickstartRules.FounderMarker(pending, 4));
			ClassicAssert.IsNull(KingdomQuickstartRules.FounderMarker(
				Fresh(KingdomQuickstartFoundersDisposition.Pending), 0));
			// Bound to the ground it was minted on.
			ClassicAssert.IsTrue(KingdomQuickstartRules.TryCreateReceipt("canyon",
				"JoppaWorld.14.17.1.1.10", KingdomQuickstartFoundersDisposition.Pending,
				out KingdomQuickstartReceipt canyon));
			Assert.That(KingdomQuickstartRules.FounderMarker(Complete(canyon), 0),
				Is.Not.EqualTo(marks[0]));
		}

		[Test]
		public void TheFourFounderCellsStandOnBaredApproachGroundAndOnNobodyElsesCell()
		{
			for (int i = 0; i < KingdomQuickstartRules.FounderCount; i++)
			{
				Assert.That(KingdomQuickstartRules.TryFounderCell(i, out int x, out int y),
					Is.True);
				// Inside the approach band the quickstart mask already bares.
				Assert.That(x, Is.InRange(29, 37));
				Assert.That(y, Is.InRange(11, 13));
				// Off the supply column, the four role cells, the rite, the heart rect, and both
				// tent-row lots (x21-26).
				Assert.That(x, Is.GreaterThan(30));
				Assert.That(x, Is.LessThan(38));
				Assert.That(x == KingdomQuickstartRules.StartCellX
					&& y == KingdomQuickstartRules.StartCellY, Is.False);
				for (int j = 0; j < i; j++)
				{
					KingdomQuickstartRules.TryFounderCell(j, out int ox, out int oy);
					Assert.That(x == ox && y == oy, Is.False, "founders " + i + " and " + j);
				}
			}
			ClassicAssert.IsFalse(KingdomQuickstartRules.TryFounderCell(-1, out _, out _));
			ClassicAssert.IsFalse(KingdomQuickstartRules.TryFounderCell(
				KingdomQuickstartRules.FounderCount, out _, out _));
		}

		// ---- the enrolment reason ------------------------------------------------------------

		[Test]
		public void FoundingIsAnAppendOnlyEnrolmentReasonAndNothingBeyondItIsValid()
		{
			Assert.That((int)KingdomCitizenshipEnrollmentReason.Founding, Is.EqualTo(5));
			ClassicAssert.IsTrue(KingdomCitizenshipRules.ValidReceiptShape(
				KingdomCitizenshipPhase.Applied, KingdomCitizenshipPriorKind.Absent,
				KingdomCitizenshipRules.RealmMembership,
				(int)KingdomCitizenshipEnrollmentReason.Founding, 0, 1L, 0L));
			ClassicAssert.IsFalse(KingdomCitizenshipRules.ValidReceiptShape(
				KingdomCitizenshipPhase.Applied, KingdomCitizenshipPriorKind.Absent,
				KingdomCitizenshipRules.RealmMembership,
				(int)KingdomCitizenshipEnrollmentReason.Founding + 1, 0, 1L, 0L));
		}

		// ---- source contracts ------------------------------------------------------------

		[Test]
		public void OnlyTheQuickstartBootstrapEmitsTheFoundingReason()
		{
			string[] files = System.IO.Directory.GetFiles(TestMain.RepositoryRoot, "*.cs",
				System.IO.SearchOption.AllDirectories);
			int emitters = 0;
			foreach (string file in files)
			{
				string body = System.IO.File.ReadAllText(file);
				if (body.Contains("KingdomCitizenshipEnrollmentReason.Founding")) emitters++;
			}
			// The enum's own declaration, this test, and the one bootstrap that enrols them.
			string enrollment = TestMain.ReadRepositoryText(
				"World/KingdomQuickstartBootstrap.Founders.Enrollment.cs");
			StringAssert.Contains("KingdomCitizenshipEnrollmentReason.Founding", enrollment);
			foreach (string file in files)
			{
				string relative = file.Substring(TestMain.RepositoryRoot.Length + 1)
					.Replace('\\', '/');
				if (!System.IO.File.ReadAllText(file).Contains(
					"KingdomCitizenshipEnrollmentReason.Founding")) continue;
				Assert.That(relative == "Core/KingdomCitizenshipRules.cs"
					|| relative == "World/KingdomQuickstartBootstrap.Founders.Enrollment.cs"
					|| relative.StartsWith("DevTests/", StringComparison.Ordinal), Is.True,
					relative);
			}
			Assert.That(emitters, Is.GreaterThanOrEqualTo(3));
		}

		[Test]
		public void TheCohortIsOneScopeAndItsIrreversibleHalfIsGuardedStepByStep()
		{
			string stage = TestMain.ReadRepositoryText(
				"World/KingdomQuickstartBootstrap.Founders.cs");
			// One scope for the whole cohort: four would keep the earlier founders on a later
			// failure, which is exactly what zero-or-four forbids.
			Assert.That(Count(stage, "TryCreateFreshGrant("), Is.EqualTo(1));
			// A body is allocated before its own gear, so the reverse unwind strips the gear
			// first, which is what exact-custody removal requires.
			int body = stage.IndexOf("scope.Create(() => GameObject.Create(",
				StringComparison.Ordinal);
			int gear = stage.IndexOf("TryRegisterFounderGear(scope, body)",
				StringComparison.Ordinal);
			Assert.That(body, Is.GreaterThanOrEqualTo(0));
			Assert.That(gear, Is.GreaterThan(body));
			// Rollback goes through the scope's exact-custody remover, never a direct destroy.
			StringAssert.DoesNotContain("Obliterate(", stage);

			string enrolment = TestMain.ReadRepositoryText(
				"World/KingdomQuickstartBootstrap.Founders.Enrollment.cs");
			// Every irreversible write is behind a witness that it has not already happened, so a
			// resumed cohort adds only the missing founders and no tally can be inflated.
			StringAssert.Contains(
				"string.IsNullOrEmpty(Body.GetStringProperty(\"KingdomName\"))", enrolment);
			StringAssert.Contains("Body.GetIntProperty(\"KingdomCitizen\") != 1", enrolment);
			StringAssert.Contains(
				"string.IsNullOrEmpty(Body.GetStringProperty(\"KingdomOrigin\"))", enrolment);
			int guard = enrolment.IndexOf(
				"string.IsNullOrEmpty(Body.GetStringProperty(\"KingdomOrigin\"))",
				StringComparison.Ordinal);
			int reconcile = enrolment.IndexOf("ReconcileFounderOrigins(System, Zone, Receipt);",
				StringComparison.Ordinal);
			Assert.That(guard, Is.GreaterThanOrEqualTo(0));
			Assert.That(reconcile, Is.GreaterThan(guard));
			// The roll row binds on the body's zone, so it is last.
			Assert.That(enrolment.IndexOf("KingdomResidents.TryEnsureRow(",
				StringComparison.Ordinal), Is.GreaterThan(reconcile));
			// Every path out of the irreversible half either publishes or is already terminal.
			StringAssert.Contains("KingdomQuickstartFoundersDisposition.Faulted", enrolment);
			StringAssert.Contains("KingdomQuickstartPhase.FoundersSeeded", enrolment);
		}

		[Test]
		public void TheCohortRunsAfterTheTentRowsAndLeavesTheGuideAlone()
		{
			string bootstrap = TestMain.ReadRepositoryText(
				"World/KingdomQuickstartBootstrap.cs");
			int stake = bootstrap.IndexOf("TryStakeShelter(system, zone, out Failure)",
				StringComparison.Ordinal);
			// The boot pass raises the cohort only after the rows are staked and every store is
			// verified. (The earlier occurrence is the resume branch, which stakes nothing.)
			int cohort = bootstrap.LastIndexOf("RunFounders(Game, system, zone, ref receipt",
				StringComparison.Ordinal);
			Assert.That(stake, Is.GreaterThanOrEqualTo(0));
			Assert.That(cohort, Is.GreaterThan(stake));
			// The stores are all granted and verified before a single founder is raised.
			int verify = bootstrap.LastIndexOf("VerifyComplete(system, zone, receipt, out Failure)",
				StringComparison.Ordinal);
			Assert.That(verify, Is.GreaterThanOrEqualTo(0));
			Assert.That(bootstrap.LastIndexOf("RunFounders(", StringComparison.Ordinal),
				Is.GreaterThan(verify));
			// The guide is untouched: it still knows exactly its five rule-shaped topics, and it
			// still never states the size of the roll, which is why four founders cannot make it
			// lie. (Its words are pinned in full by KingdomQuickstartGuideRulesTests.)
			Assert.That(KingdomQuickstartGuideRules.TopicCount, Is.EqualTo(5));
			string[] sizes = { "four settlers", "four citizens", "founding citizens",
				"four of you", "four people" };
			foreach (string size in sizes)
			{
				StringAssert.DoesNotContain(size, KingdomQuickstartGuideRules.Start);
				foreach (KingdomQuickstartGuideTopic topic in KingdomQuickstartGuideRules.Topics())
				{
					StringAssert.DoesNotContain(size, topic.Topic);
					StringAssert.DoesNotContain(size, topic.Answer);
				}
			}
		}

		[Test]
		public void BothHarnessGatesReadTheTerminalPredicateAndTheRosterHasEightSlots()
		{
			string boot = TestMain.ReadRepositoryText(
				"Harness/KingdomQuickstartBootstrap.NativeBoot.cs");
			StringAssert.Contains("!KingdomQuickstartRules.IsTerminal(receipt)", boot);
			StringAssert.DoesNotContain(
				"receipt.Phase != KingdomQuickstartPhase.Complete", boot);
			StringAssert.Contains("const int slots = 4 + KingdomQuickstartRules.FounderCount;",
				boot);
			StringAssert.Contains("role >= 4 && !seeded", boot);
			string codec = TestMain.ReadRepositoryText(
				"Harness/KingdomQuickstartSaveSnapshotCodec.cs");
			StringAssert.Contains("!KingdomQuickstartRules.IsTerminal(receipt)", codec);
		}

		[Test]
		public void TheFounderNoticesSayWhatTheWorldActuallyDid()
		{
			string bootstrap = TestMain.ReadRepositoryText(
				"World/KingdomQuickstartBootstrap.cs");
			StringAssert.Contains("founding citizens stand on the approach,", bootstrap);
			StringAssert.Contains("already on your roll and able to work. Until a row stands",
				bootstrap);
			StringAssert.Contains("they sleep rough, and the charter will say so.", bootstrap);
			// One wording, two places it can be said: with the completion notice, or on its own
			// when a later wake is what raised the cohort.
			Assert.That(Count(bootstrap, "FoundersArrived(founders)"), Is.EqualTo(2));
			StringAssert.Contains("{{W|Your founding party has arrived.}}", bootstrap);
			string refusal = TestMain.ReadRepositoryText(
				"World/KingdomQuickstartBootstrap.Founders.Recovery.cs");
			StringAssert.Contains("{{W|Your founding party is not whole.}}", refusal);
			StringAssert.Contains(
				"Every store the quickstart grants is standing and unaffected; only the ",
				refusal);
			string enrolment = TestMain.ReadRepositoryText(
				"World/KingdomQuickstartBootstrap.Founders.Enrollment.cs");
			StringAssert.Contains("was not here when the roll was written, and the ", enrolment);
			StringAssert.Contains("This is not retried.", enrolment);
			// The fault is NOT announced where it is published: one path says everything, so a
			// refused cohort can never produce two popups on the same wake.
			StringAssert.DoesNotContain("Popup.Show", enrolment);
		}

		[Test]
		public void ARefusedCohortNeverFailsTheBootstrapAndSaysSoExactlyOnce()
		{
			string bootstrap = TestMain.ReadRepositoryText(
				"World/KingdomQuickstartBootstrap.cs");
			// RunFounders returns nothing to test: the stores are all standing and verified before
			// a founder is raised, so a refusal must not cost the founder the completion notice or
			// be reported as "stopped before granting any further stock", which would not be true.
			StringAssert.Contains("RunFounders(Game, system, zone, ref receipt, out Founders);",
				bootstrap);
			StringAssert.DoesNotContain("|| !RunFounders(", bootstrap);
			StringAssert.DoesNotContain("&& RunFounders(", bootstrap);
			// And nothing after it refuses either: from the last founders call to the end of the
			// boot pass there is no way out but success.
			int last = bootstrap.LastIndexOf(
				"RunFounders(Game, system, zone, ref receipt, out Founders);",
				StringComparison.Ordinal);
			Assert.That(last, Is.GreaterThanOrEqualTo(0));
			StringAssert.DoesNotContain("return false", bootstrap.Substring(last));
			string founders = TestMain.ReadRepositoryText(
				"World/KingdomQuickstartBootstrap.Founders.cs");
			StringAssert.Contains("private static void RunFounders(", founders);
			// Every refusal arm ends in the one announce-once path.
			Assert.That(Count(founders, "AnnounceFoundersOnce("), Is.EqualTo(6));
			string refusal = TestMain.ReadRepositoryText(
				"World/KingdomQuickstartBootstrap.Founders.Recovery.cs");
			// The announce-once flag, and the block it is keyed to: any published progress changes
			// the wire and therefore clears it; a refusal that republishes the same bytes keeps it.
			StringAssert.Contains("private static string FoundersAnnouncedWire", refusal);
			StringAssert.Contains(
				"if (string.Equals(wire, FoundersAnnouncedWire, StringComparison.Ordinal)) return;",
				refusal);
			Assert.That(Count(refusal, "Popup.Show"), Is.EqualTo(1));
		}

		[Test]
		public void TheCommitToPublishGapIsRecoveredFromTheGroundAndNeverStagesASecondCohort()
		{
			string founders = TestMain.ReadRepositoryText(
				"World/KingdomQuickstartBootstrap.Founders.cs");
			// The scope commits four placed bodies BEFORE the fence publishes their ids. A lost
			// write, or a save cut across that instant, leaves four live marked unnamed bodies.
			// So the ground is read FIRST, on every wake that owes a cohort, and a new cohort is
			// staged only when the ground holds none.
			int observe = founders.IndexOf("TryObserveFounders(Zone, Receipt, out standing",
				StringComparison.Ordinal);
			int stage = founders.IndexOf("TryStageFounderBodies(Game, Zone, Receipt, out cohort",
				StringComparison.Ordinal);
			int fence = founders.IndexOf(
				"KingdomQuickstartFoundersDisposition.Seeding, ids, out failure)",
				StringComparison.Ordinal);
			Assert.That(observe, Is.GreaterThanOrEqualTo(0));
			Assert.That(stage, Is.GreaterThan(observe));
			Assert.That(fence, Is.GreaterThan(stage));
			// Four found: adopt those exact four. Anything between one and three, or a cohort that
			// no longer proves itself, fences the world rather than completing or replacing it.
			StringAssert.Contains(
				"if (found == KingdomQuickstartRules.FounderCount) cohort = standing;", founders);
			// One, two or three bodies is neither "nothing was committed" nor "a cohort to adopt".
			// Completing it would enrol a short party; replacing it would leave orphans.
			StringAssert.Contains(
				"|| (found != 0 && found != KingdomQuickstartRules.FounderCount)", founders);
			StringAssert.Contains(
				"|| (found != 0 && !VerifyFounderCohort(Zone, standing, Receipt, out failure))",
				founders);
			int quarantine = founders.IndexOf("QuarantineGrant(Game,", StringComparison.Ordinal);
			Assert.That(quarantine, Is.GreaterThan(observe));
			Assert.That(quarantine, Is.LessThan(stage));
			// A failed fence publish does NOT destroy the cohort: it is recoverable by the scan
			// above, and destroying it would throw four good bodies away for a transient write.
			StringAssert.DoesNotContain("TryUnwindCommittedCohort", founders);

			string recovery = TestMain.ReadRepositoryText(
				"World/KingdomQuickstartBootstrap.Founders.Recovery.cs");
			// The scan counts occurrences, not distinct references: one reservation worn twice is
			// not a second proof, and it refuses rather than picking one.
			StringAssert.Contains("A founder reservation was worn by more than one body.",
				recovery);
			// An adopted cohort is proved WITHOUT pinning the cell — it may have stood through
			// turns before its publication was recovered, and a founder walks — while Stage A's
			// own verification, in the call that placed them, does pin it.
			StringAssert.Contains("private static bool VerifyFounderCohort(", recovery);
			StringAssert.Contains("was not on its own reserved cell", founders);
			StringAssert.DoesNotContain("ExactRole(", recovery);
		}

		[Test]
		public void TheOriginTallyIsDerivedFromTheFoundersRatherThanCountedAsTheyAreWritten()
		{
			string enrolment = TestMain.ReadRepositoryText(
				"World/KingdomQuickstartBootstrap.Founders.Enrollment.cs");
			// An increment beside a property write has a gap: an interruption between them makes
			// the retry see the property, skip the counter, and lose that count forever.
			StringAssert.DoesNotContain("origins + 1", enrolment);
			StringAssert.Contains("ReconcileFounderOrigins(System, Zone, Receipt);", enrolment);
			StringAssert.Contains("if (recorded < carried) System.OriginCounts["
				+ "Receipt.ProfileKey] = carried;", enrolment);
			int write = enrolment.IndexOf(
				"Body.SetStringProperty(\"KingdomOrigin\", Receipt.ProfileKey)",
				StringComparison.Ordinal);
			int reconcile = enrolment.IndexOf("ReconcileFounderOrigins(System, Zone, Receipt);",
				StringComparison.Ordinal);
			Assert.That(write, Is.GreaterThanOrEqualTo(0));
			Assert.That(reconcile, Is.GreaterThan(write));
		}

		[Test]
		public void TheSaveSnapshotCodecAdmitsOnlyAFinishedNonFaultedReceipt()
		{
			string codec = TestMain.ReadRepositoryText(
				"Harness/KingdomQuickstartSaveSnapshotCodec.cs");
			// The same predicate the native boot uses, so the two gates cannot drift: a receipt
			// that still owes, is part-way through, or has faulted its cohort is not a world the
			// harness may snapshot, save or load.
			StringAssert.Contains("!KingdomQuickstartRules.IsTerminal(receipt)", codec);
			StringAssert.Contains("receipt.FoundersDisposition == "
				+ "KingdomQuickstartFoundersDisposition.Faulted", codec);
			StringAssert.DoesNotContain("receipt.Phase < KingdomQuickstartPhase.Complete", codec);
			StringAssert.DoesNotContain("receipt.Phase != KingdomQuickstartPhase.Complete", codec);
		}

		[Test]
		public void FourFoundersPutTheFirstTravellerAtSixThousandTicks()
		{
			// The arrival clock is 3600 plus 600 for every settler already living there, so the
			// cohort moves the first guest from three in-game days to five. The docs say 6000; this
			// is where that number comes from.
			Assert.That(KingdomRules.ArrivalIntervalTicks(0), Is.EqualTo(3600L));
			Assert.That(KingdomRules.ArrivalIntervalTicks(
				KingdomQuickstartRules.FounderCount), Is.EqualTo(6000L));
			// And the number the player is told matches the rule, not a copied constant.
			string quickstart = TestMain.ReadRepositoryText("docs/QUICKSTART.md");
			StringAssert.Contains("due at 6000 ticks rather than 3600", quickstart);
			StringAssert.Contains("6000 ticks rather than 3600",
				TestMain.ReadRepositoryText("CHANGELOG.md"));
		}

		// ---- fixtures ------------------------------------------------------------------------

		private static KingdomQuickstartReceipt Fresh(
			KingdomQuickstartFoundersDisposition disposition)
		{
			ClassicAssert.IsTrue(KingdomQuickstartRules.TryCreateReceipt("marsh", MarshZone,
				disposition, out KingdomQuickstartReceipt receipt));
			return receipt;
		}

		private static KingdomQuickstartReceipt Complete(KingdomQuickstartReceipt reserved)
		{
			KingdomQuickstartReceipt receipt = Advance(reserved,
				KingdomQuickstartPhase.Founded, "Watervine");
			receipt = Advance(receipt, KingdomQuickstartPhase.WaterStocked, "water-id");
			receipt = Advance(receipt, KingdomQuickstartPhase.FoodStocked, "larder-id");
			receipt = Advance(receipt, KingdomQuickstartPhase.MaterialsStocked, "materials-id");
			receipt = Advance(receipt, KingdomQuickstartPhase.AdvisorResolved, "",
				KingdomQuickstartAdvisorDisposition.Omitted);
			return Advance(receipt, KingdomQuickstartPhase.Complete, "");
		}

		private static KingdomQuickstartReceipt Seeding()
		{
			ClassicAssert.IsTrue(KingdomQuickstartRules.TryRestateFounders(
				Complete(Fresh(KingdomQuickstartFoundersDisposition.Pending)),
				KingdomQuickstartFoundersDisposition.Seeding, Ids(),
				out KingdomQuickstartReceipt seeding));
			return seeding;
		}

		private static KingdomQuickstartReceipt Seeded()
		{
			ClassicAssert.IsTrue(KingdomQuickstartRules.TryAdvance(Seeding(),
				KingdomQuickstartPhase.FoundersSeeded, "",
				KingdomQuickstartAdvisorDisposition.Unresolved,
				out KingdomQuickstartReceipt seeded));
			return seeded;
		}

		private static KingdomQuickstartReceipt Advance(KingdomQuickstartReceipt current,
			KingdomQuickstartPhase next, string value,
			KingdomQuickstartAdvisorDisposition advisor =
				KingdomQuickstartAdvisorDisposition.Unresolved)
		{
			Assert.That(KingdomQuickstartRules.TryAdvance(current, next, value, advisor,
				out KingdomQuickstartReceipt advanced), Is.True, next.ToString());
			return advanced;
		}

		/// <summary>
		/// A receipt built past the lawful moves, so the validity ladder can be asked about
		/// combinations no transition would ever produce.
		/// </summary>
		private static KingdomQuickstartReceipt With(KingdomQuickstartReceipt receipt,
			KingdomQuickstartFoundersDisposition disposition, string[] ids)
		{
			KingdomQuickstartReceipt copy = receipt.Copy();
			copy.FoundersDisposition = disposition;
			for (int i = 0; i < copy.FounderObjectIds.Length; i++)
				copy.FounderObjectIds[i] = ids[i];
			return copy;
		}

		private static string[] Ids()
		{
			return new[] { "founder-0", "founder-1", "founder-2", "founder-3" };
		}

		private static string[] Blank()
		{
			return new[] { "", "", "", "" };
		}

		private static string LegacyWire(string tag, int phase, string food, string water,
			string larder, string stockpile, int advisorDisposition, string advisorId)
		{
			string body = tag + "|" + Field("marsh") + "|" + Field(MarshZone)
				+ "|" + phase.ToString(CultureInfo.InvariantCulture)
				+ "|" + Field(food) + "|" + Field(water) + "|" + Field(larder)
				+ "|" + Field(stockpile)
				+ "|" + advisorDisposition.ToString(CultureInfo.InvariantCulture)
				+ "|" + Field(advisorId);
			byte[] digest;
			using (SHA256 sha = SHA256.Create())
				digest = sha.ComputeHash(Encoding.UTF8.GetBytes(body));
			StringBuilder text = new StringBuilder(64);
			foreach (byte value in digest)
				text.Append(value.ToString("x2", CultureInfo.InvariantCulture));
			return body + "|" + text;
		}

		private static string Field(string value)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? ""));
		}

		private static string Text(string value)
		{
			return Encoding.UTF8.GetString(Convert.FromBase64String(value));
		}

		private static int Count(string text, string fragment)
		{
			return text.Split(new[] { fragment }, StringSplitOptions.None).Length - 1;
		}
	}
}
#endif
