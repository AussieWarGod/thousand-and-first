#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomStasisVaultRulesTests
	{
		[Test]
		public void PreparationFreezesExactDeterministicFourBayCustody()
		{
			KingdomStasisCustodyReceipt first = Prepare(0, 1, "body-a");
			KingdomStasisCustodyReceipt second = Prepare(0, 1, "body-a");
			Assert.AreEqual(KingdomStasisCustodyPhase.Prepared, first.Phase);
			Assert.AreEqual(first.CustodyId, second.CustodyId);
			Assert.AreEqual(first.CustodyId + ":field", first.FieldObjectId);
			Assert.AreEqual(0, first.Slot);
			AssertValid(first);
			for (int slot = 0; slot < KingdomStasisVaultRules.MaxSlots; slot++)
				AssertValid(Prepare(slot, slot + 1, "body-" + slot));
			AssertPrepareFails(-1, 1);
			AssertPrepareFails(KingdomStasisVaultRules.MaxSlots, 1);
			AssertPrepareFails(0, 0);
		}

		[Test]
		public void GenerationBodyAndVaultSeparateCustodyIdentity()
		{
			Assert.AreNotEqual(Prepare(0, 1, "body-a").CustodyId,
				Prepare(0, 2, "body-a").CustodyId);
			Assert.AreNotEqual(Prepare(0, 1, "body-a").CustodyId,
				Prepare(0, 1, "body-b").CustodyId);
			KingdomStasisCustodyReceipt changedVault = Prepare(0, 1, "body-a");
			changedVault.VaultObjectId = "vault-b";
			AssertInvalid(changedVault);
		}

		[Test]
		public void EntryVerdictsFailInStableSafetyOrder()
		{
			Assert.AreEqual(KingdomStasisVaultVerdict.Allowed, Judge());
			Assert.AreEqual(KingdomStasisVaultVerdict.Unfounded,
				Judge(founded: false, owned: false));
			Assert.AreEqual(KingdomStasisVaultVerdict.WrongGround,
				Judge(owned: false, exactVault: false));
			Assert.AreEqual(KingdomStasisVaultVerdict.NotDominating,
				Judge(dominated: false));
			Assert.AreEqual(KingdomStasisVaultVerdict.CradleOccupied,
				Judge(clear: false));
			Assert.AreEqual(KingdomStasisVaultVerdict.ForeignProjection,
				Judge(foreign: true));
		}

		[Test]
		public void LifecycleIsCopyOnWriteAndReleaseIsTerminallyDated()
		{
			KingdomStasisCustodyReceipt prepared = Prepare(1, 7, "body-a");
			KingdomStasisCustodyReceipt projected =
				KingdomStasisVaultRules.FieldProjected(prepared);
			KingdomStasisCustodyReceipt active =
				KingdomStasisVaultRules.Activated(projected);
			KingdomStasisCustodyReceipt releasing =
				KingdomStasisVaultRules.BeginRelease(active);
			KingdomStasisCustodyReceipt released =
				KingdomStasisVaultRules.Released(releasing, 101L);
			Assert.AreEqual(KingdomStasisCustodyPhase.Prepared, prepared.Phase);
			Assert.AreEqual(KingdomStasisCustodyPhase.FieldProjected, projected.Phase);
			Assert.AreEqual(KingdomStasisCustodyPhase.Active, active.Phase);
			Assert.AreEqual(KingdomStasisCustodyPhase.ReleasePrepared, releasing.Phase);
			Assert.AreEqual(KingdomStasisCustodyPhase.Released, released.Phase);
			Assert.AreEqual(101L, released.ReleasedTick);
			Assert.IsNull(KingdomStasisVaultRules.Released(releasing, 99L));
			AssertValid(released);
			KingdomStasisCustodyReceipt warningRelease =
				KingdomStasisVaultRules.Released(releasing, 102L,
					"whole-body evidence changed");
			Assert.AreEqual(KingdomStasisCustodyPhase.Released, warningRelease.Phase);
			Assert.AreEqual("whole-body evidence changed", warningRelease.Fault);
			AssertValid(warningRelease);
			warningRelease.Fault = new string('x',
				KingdomStasisVaultRules.MaxFaultChars + 1);
			AssertInvalid(warningRelease);
		}

		[Test]
		public void RecoveryNeverKeepsUnownedOrDivergedProjectionActive()
		{
			Assert.AreEqual(KingdomStasisRecoveryVerdict.KeepActive,
				Recover(true, true, true, true, true, true, true, true, true, true));
			Assert.AreEqual(KingdomStasisRecoveryVerdict.Release,
				Recover(true, false, true, true, true, true, true, true, true, true));
			Assert.AreEqual(KingdomStasisRecoveryVerdict.Release,
				Recover(true, true, true, true, true, false, true, true, true, true));
			Assert.AreEqual(KingdomStasisRecoveryVerdict.QuarantineAndRelease,
				Recover(true, true, true, true, true, true, false, true, true, true));
			Assert.AreEqual(KingdomStasisRecoveryVerdict.ContinueForward,
				Recover(true, true, true, true, true, true, true, true, false, true));
		}

		[Test]
		public void TamperingFailsClosedAndQuarantineCanBeRetired()
		{
			KingdomStasisCustodyReceipt receipt = Prepare(2, 4, "body-a");
			receipt.FieldObjectId = "foreign-field";
			AssertInvalid(receipt);
			KingdomStasisCustodyReceipt valid = Prepare(2, 4, "body-a");
			KingdomStasisCustodyReceipt quarantined =
				KingdomStasisVaultRules.Quarantined(valid, new string('x', 900));
			Assert.LessOrEqual(quarantined.Fault.Length,
				KingdomStasisVaultRules.MaxFaultChars);
			AssertValid(quarantined);
			KingdomStasisCustodyReceipt retired =
				KingdomStasisVaultRules.RetireQuarantine(quarantined, 150L);
			Assert.AreEqual(KingdomStasisCustodyPhase.Released, retired.Phase);
			AssertValid(retired);
		}

		[Test]
		public void MalformedQuarantineBoundsEveryPersistedField()
		{
			KingdomStasisCustodyReceipt broken = Prepare(0, 1, "body-a");
			broken.Slot = 99;
			broken.BodyName = new string('n', 900);
			broken.InventoryFingerprint = new string('f', 900);
			KingdomStasisCustodyReceipt quarantined =
				KingdomStasisVaultRules.QuarantineMalformed(broken, 3,
					new string('x', 900));
			Assert.AreEqual(3, quarantined.Slot);
			Assert.LessOrEqual(quarantined.BodyName.Length,
				KingdomStasisVaultRules.MaxNameChars);
			Assert.AreEqual("", quarantined.InventoryFingerprint);
			Assert.LessOrEqual(quarantined.Fault.Length,
				KingdomStasisVaultRules.MaxFaultChars);
			AssertValid(quarantined);
		}

		[Test]
		public void RuntimeUsesOneNativePhaseIsolatedFieldAndWholeBodyCustody()
		{
			string projection = Read("Growth", "KingdomStasisVault.Projection.cs");
			string evidence = Read("Growth", "KingdomStasisVault.Evidence.cs");
			string release = Read("Growth", "KingdomStasisVault.Release.cs");
			string marker = Read("Growth", "r_KingdomStasisCustody.cs");
			string xml = Read("RuntimeData", "ObjectBlueprints.xml");
			StringAssert.Contains("new Stasisfield", projection);
			StringAssert.Contains("new Phased(9999)", projection);
			StringAssert.Contains("ProcessStasis()", projection);
			StringAssert.Contains("InventoryFingerprint(body)",
				Read("Growth", "KingdomStasisVault.Entry.cs"));
			StringAssert.Contains("GetEquippedObjects", evidence);
			StringAssert.Contains("ShutdownStasis", release);
			StringAssert.Contains("body == null", release);
			StringAssert.Contains("bodyMarker == null", release);
			StringAssert.Contains("Released(releasing, now, warning)", release);
			StringAssert.Contains("DominationBroken", marker);
			StringAssert.Contains("CanBeReplicatedEvent", marker);
			StringAssert.Contains("<part Name=\"r_KingdomStasisVault\" />", xml);
			StringAssert.Contains("Name=\"r_KingdomStasisFieldAnchor\" Inherits=\"Object\"", xml);
			StringAssert.DoesNotContain("<part Name=\"Stasisfield\"", xml);
		}

		[Test]
		public void ActivationIsDerivedAndNeverPermanentlyLearned()
		{
			string shared = Read("Growth", "KingdomReopenedExoticActivation.cs");
			string stasis = Read("Growth", "KingdomReopenedExoticActivation.Stasis.cs");
			StringAssert.Contains("StasisVaultEligible", shared + stasis);
			StringAssert.Contains("node:chimerism", stasis);
			StringAssert.Contains("KingdomBuilt", stasis);
			StringAssert.DoesNotContain("Learn(", shared + stasis);
		}

		[Test]
		public void PhaseEnumsAreAppendOnly()
		{
			Assert.AreEqual("1,2,3,4,5,6", JoinValues(typeof(KingdomStasisCustodyPhase)));
			Assert.AreEqual("0,1,2,3", JoinValues(typeof(KingdomStasisRecoveryVerdict)));
		}

		private static KingdomStasisCustodyReceipt Prepare(int slot, int generation,
			string body)
		{
			string digest = KingdomStasisVaultRules.Fingerprint("empty");
			KingdomStasisCustodyReceipt receipt;
			string failure;
			Assert.IsTrue(KingdomStasisVaultRules.TryPrepare(slot, generation, "realm-a",
				"settlement-a", "zone-a", "vault-a", "lot-a", "cradle-a", body,
				"subject-a", "Humanoid", "the founder", digest, digest, digest, 100L,
				out receipt, out failure), failure);
			return receipt;
		}

		private static void AssertPrepareFails(int slot, int generation)
		{
			string digest = KingdomStasisVaultRules.Fingerprint("empty");
			Assert.IsFalse(KingdomStasisVaultRules.TryPrepare(slot, generation, "realm",
				"city", "zone", "vault", "lot", "cradle", "body", "subject", "Humanoid",
				"founder", digest, digest, digest, 0L, out _, out _));
		}

		private static KingdomStasisVaultVerdict Judge(bool founded = true,
			bool owned = true, bool exactVault = true, bool dominated = true,
			bool clear = true, bool foreign = false)
		{
			return KingdomStasisVaultRules.JudgeEntry(founded, owned, exactVault, dominated,
				true, true, true, false, false, true, clear, foreign, true);
		}

		private static KingdomStasisRecoveryVerdict Recover(bool authority, bool owned,
			bool vault, bool cradle, bool body, bool domination, bool marker, bool field,
			bool stasis, bool phased)
		{
			return KingdomStasisVaultRules.JudgeRecovery(authority, owned, vault, cradle,
				body, domination, marker, field, stasis, phased);
		}

		private static void AssertValid(KingdomStasisCustodyReceipt receipt)
		{
			Assert.IsTrue(KingdomStasisVaultRules.Validate(receipt, out string failure), failure);
		}

		private static void AssertInvalid(KingdomStasisCustodyReceipt receipt)
		{
			Assert.IsFalse(KingdomStasisVaultRules.Validate(receipt, out string failure));
			Assert.IsNotEmpty(failure);
		}

		private static string JoinValues(Type type)
		{
			Array values = Enum.GetValues(type);
			string[] rows = new string[values.Length];
			for (int i = 0; i < values.Length; i++)
				rows[i] = Convert.ToInt32(values.GetValue(i)).ToString();
			return string.Join(",", rows);
		}

		private static string Read(params string[] parts)
		{
			string path = TestMain.RepositoryRoot;
			for (int i = 0; i < parts.Length; i++) path = Path.Combine(path, parts[i]);
			return File.ReadAllText(path);
		}
	}
}
#endif
