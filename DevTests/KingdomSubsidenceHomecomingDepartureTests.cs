#if TAF_TESTS
using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	// Executes the real departure receipt predicate and phase protocol. The two SourceContract
	// cases bind that predicate to Homecoming; these tests do not execute native display or reset.
	[TestFixture]
	public sealed class KingdomSubsidenceHomecomingDepartureTests
	{
		private const string Runtime = "Growth/KingdomSubsidenceStepRuntime.Homecoming.cs";
		private static readonly FieldInfo[] Fields = typeof(KingdomResidentDepartureOperation)
			.GetFields(BindingFlags.Public | BindingFlags.Instance)
			.OrderBy(field => field.Name, StringComparer.Ordinal).ToArray();

		[TestCase(false)] [TestCase(true)]
		public void ExplicitNoPendingContractAcceptsNullOrEmptyReceiptWithoutReplacement(bool present)
		{
			KingdomResidentDepartureOperation receipt = present ? KingdomResidentDepartureRules.Empty() : null;
			AssertEmptyWithoutMutation(receipt, true);
		}

		[Test]
		public void LegacyNullTextDefaultsRemainEmptyWithoutNormalization()
		{
			KingdomResidentDepartureOperation receipt = KingdomResidentDepartureRules.Empty();
			foreach (FieldInfo field in Fields)
				if (field.FieldType == typeof(string)) field.SetValue(receipt, null);
			AssertEmptyWithoutMutation(receipt, true);
			foreach (FieldInfo field in Fields)
				if (field.FieldType == typeof(string)) ClassicAssert.IsNull(field.GetValue(receipt), field.Name);
		}

		[TestCase((int)KingdomResidentDeparturePhase.Prepared)]
		[TestCase((int)KingdomResidentDeparturePhase.RolesPrepared)]
		[TestCase((int)KingdomResidentDeparturePhase.CitizenshipRemoved)]
		[TestCase((int)KingdomResidentDeparturePhase.CarriersRemoved)]
		[TestCase((int)KingdomResidentDeparturePhase.RolesClosed)]
		[TestCase((int)KingdomResidentDeparturePhase.EffectsPublished)]
		public void EveryRealPendingPhaseKeepsItsScalarWitnessAndRefusesEmptyAdmission(int phase)
		{
			KingdomResidentDepartureOperation receipt = Prepared();
			for (int current = receipt.Phase; current < phase; current++)
				ClassicAssert.IsTrue(KingdomResidentDepartureRules.Advance(receipt,
					(KingdomResidentDeparturePhase)current, (KingdomResidentDeparturePhase)(current + 1)));
			ClassicAssert.IsTrue(KingdomResidentDepartureRules.Valid(receipt));
			ClassicAssert.AreEqual(phase, receipt.Phase);
			AssertEmptyWithoutMutation(receipt, false);
			ClassicAssert.AreEqual(1, receipt.DeparturesBefore);
			ClassicAssert.AreEqual("A named departure awaits its ledger.", receipt.LedgerLine);
		}

		[Test]
		public void AnyRetainedFieldMakesAnOtherwiseEmptyReceiptRefuseWithoutRepair()
		{
			ClassicAssert.Greater(Fields.Length, 0);
			foreach (FieldInfo field in Fields)
			{
				KingdomResidentDepartureOperation receipt = KingdomResidentDepartureRules.Empty();
				object held;
				if (field.FieldType == typeof(string)) held = "held";
				else if (field.FieldType == typeof(int)) held = 1;
				else if (field.FieldType == typeof(long)) held = 1L;
				else if (field.FieldType == typeof(bool)) held = true;
				else if (field.FieldType == typeof(KingdomNamedCookReceipt)) held = new KingdomNamedCookReceipt();
				else if (field.FieldType == typeof(KingdomCivicOfficeReceipt)) held = new KingdomCivicOfficeReceipt();
				else if (field.FieldType == typeof(KingdomPolityNamedFigureRecord)) held = new KingdomPolityNamedFigureRecord();
				else { Assert.Fail("Uncovered departure carrier: " + field.Name); return; }
				field.SetValue(receipt, held);
				AssertEmptyWithoutMutation(receipt, false, field.Name);
			}
		}

		[TestCase(-1)] [TestCase(99)]
		public void MalformedPhaseIsNotAnEmptyReceipt(int phase)
		{
			KingdomResidentDepartureOperation receipt = KingdomResidentDepartureRules.Empty();
			receipt.Phase = phase;
			ClassicAssert.IsFalse(KingdomResidentDepartureRules.Valid(receipt));
			AssertEmptyWithoutMutation(receipt, false);
		}

		[TestCase(false)] [TestCase(true)]
		public void AValidForeignOwnerReceiptIsStillHeldEvidenceNotAbsence(bool otherRealm)
		{
			KingdomResidentDepartureOperation receipt = Prepared();
			if (otherRealm) receipt.RealmId = KingdomIdentityRules.RealmPrefix + new string('c', 64);
			else receipt.SettlementId = KingdomIdentityRules.SettlementPrefix + new string('d', 64);
			receipt.OperationId = KingdomResidentDepartureRules.Id(receipt.RealmId, receipt.SettlementId,
				receipt.ResidentId, receipt.BodyObjectId, receipt.PreparedTick);
			ClassicAssert.IsTrue(KingdomResidentDepartureRules.Valid(receipt));
			AssertEmptyWithoutMutation(receipt, false);
		}

		[Test]
		public void SourceContract_CentralBarrierReadsTheExactReceiptWithoutDefaultingOrRecovery()
		{
			string source = TestMain.ReadRepositoryText(Runtime);
			string exact = Body(source, "private static bool HomecomingExact(");
			Ordered(exact, "!OptionExact(frame.Owner)",
				"!KingdomResidentDepartureRules.IsEmpty(frame.Owner.System.ResidentDeparture)",
				"!ReferenceEquals(frame.Owner.System.Ledger, frame.Ledger)");
			ClassicAssert.AreEqual(1, Regex.Matches(exact, @"\bResidentDeparture\b").Count);
			StringAssert.DoesNotContain("NormalizeOldDefault", source);
			StringAssert.DoesNotContain("TryRecoverPending", exact);
			StringAssert.DoesNotContain("KingdomResidentDepartureRuntime.TryRecoverPending", source);
			ClassicAssert.IsFalse(Regex.IsMatch(source, @"\bResidentDeparture\s*=(?!=)"));
			ClassicAssert.IsFalse(Regex.IsMatch(source, @"\bDeparturesBefore\s*=(?!=)"));
		}

		[Test]
		public void SourceContract_ReceiptBarrierBracketsBothDisplaysAndRemainsImmediatelyBeforeReset()
		{
			string entry = Body(TestMain.ReadRepositoryText(Runtime), "internal static bool TryReadHomecoming(");
			Ordered(entry, "if (!HomecomingExact(frame)) return false;",
				"if (!ledger.Any && failures == KingdomSubsidenceReportArchive.None && noticeWarning.Length == 0 && departureWarnings.Length == 0)",
				"show(\"Nothing has happened here since you last stood on this ground.\");",
				"if (!HomecomingExact(frame)) return false;", "refusal = null; return true;",
				"string digest = ledger.Digest(system.SeatName, frame.Days);",
				"if (!HomecomingExact(frame)) return false;", "show(digest);",
				"if (!HomecomingExact(frame)", "TryPrepareHomecoming(",
				"if (wire != owner.Owner.Wire && !SaveOption(owner, next)) return false;",
				"if (!HomecomingExact(frame)) return false;", "ledger.Reset();",
				"system.HomecomingDays = 0;", "refusal = null; return true;");
			ClassicAssert.AreEqual(1, Regex.Matches(entry, @"\bledger\.Reset\(").Count);
		}

		private static KingdomResidentDepartureOperation Prepared()
		{
			KingdomResidentDepartureOperation receipt = new KingdomResidentDepartureOperation
			{
				Version = KingdomResidentDepartureOperation.CurrentVersion, Revision = 1,
				Phase = (int)KingdomResidentDeparturePhase.Prepared,
				RealmId = KingdomIdentityRules.RealmPrefix + new string('a', 64),
				SettlementId = KingdomIdentityRules.SettlementPrefix + new string('b', 64),
				ResidentId = 7, BodyObjectId = "homecoming-departure-body",
				ZoneId = "JoppaWorld.12.24.1.1.10", ResidentName = "Departure fixture",
				PreparedTick = 5300, DeparturesBefore = 1, Chronicled = true,
				ChronicleLine = "A named resident departed", LedgerLine = "A named departure awaits its ledger."
			};
			receipt.OperationId = KingdomResidentDepartureRules.Id(receipt.RealmId, receipt.SettlementId,
				receipt.ResidentId, receipt.BodyObjectId, receipt.PreparedTick);
			ClassicAssert.IsTrue(KingdomResidentDepartureRules.Valid(receipt));
			return receipt;
		}

		private static void AssertEmptyWithoutMutation(KingdomResidentDepartureOperation receipt,
			bool expected, string detail = "receipt")
		{
			object[] before = receipt == null ? null : Fields.Select(field => field.GetValue(receipt)).ToArray();
			ClassicAssert.AreEqual(expected, KingdomResidentDepartureRules.IsEmpty(receipt), detail);
			ClassicAssert.AreEqual(expected, KingdomResidentDepartureRules.IsEmpty(receipt), detail + " repeat");
			if (receipt == null) return;
			for (int i = 0; i < Fields.Length; i++)
				if (Fields[i].FieldType.IsValueType) ClassicAssert.AreEqual(before[i], Fields[i].GetValue(receipt), Fields[i].Name);
				else ClassicAssert.AreSame(before[i], Fields[i].GetValue(receipt), Fields[i].Name);
		}

		private static string Body(string source, string signature)
		{
			int start = source.IndexOf(signature, StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(start, 0, signature);
			int open = source.IndexOf('{', start), depth = 0;
			for (int i = open; i < source.Length; i++)
			{
				if (source[i] == '{') depth++;
				else if (source[i] == '}' && --depth == 0)
					return Regex.Replace(source.Substring(start, i - start + 1), @"\s+", " ");
			}
			Assert.Fail("Unclosed source method: " + signature); return null;
		}

		private static void Ordered(string source, params string[] tokens)
		{
			int cursor = 0;
			foreach (string token in tokens)
			{
				int at = source.IndexOf(token, cursor, StringComparison.Ordinal);
				ClassicAssert.GreaterOrEqual(at, cursor, "Missing or reordered source contract: " + token);
				cursor = at + token.Length;
			}
		}
	}
}
#endif
