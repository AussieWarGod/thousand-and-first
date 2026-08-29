#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomAssentingMootRulesTests
	{
		[Test]
		public void ActivationRequiresEveryCurrentSeatedGroundProof()
		{
			Assert.IsTrue(Eligible(true, true, true, true, true, true));
			for (int missing = 0; missing < 6; missing++)
			{
				bool[] proof = { true, true, true, true, true, true };
				proof[missing] = false;
				Assert.IsFalse(Eligible(proof[0], proof[1], proof[2], proof[3],
					proof[4], proof[5]), "proof " + missing + " must be mandatory");
			}
		}

		[Test]
		public void PreparationFreezesExactDeterministicAuthority()
		{
			KingdomAssentingMootReceipt first = Prepare();
			KingdomAssentingMootReceipt second = Prepare();
			Assert.AreEqual(KingdomAssentingMootPhase.Prepared, first.Phase);
			Assert.AreEqual(first.AuthorityId, second.AuthorityId);
			Assert.AreEqual(first.MembershipFingerprint, second.MembershipFingerprint);
			AssertValid(first);
			Assert.IsFalse(KingdomAssentingMootRules.TryPrepare("realm", "city", "City",
				"zone", "building", "lot", 0, 1, 10L, out _, out _));
		}

		[Test]
		public void MembershipIsBoundedSortedIndependentAndCopyOnWrite()
		{
			KingdomAssentingMootReceipt original = Prepare();
			KingdomAssentingMootReceipt receipt = Add(original,
				KingdomAssentingMootRole.Assent, 30);
			receipt = Add(receipt, KingdomAssentingMootRole.Assent, 10);
			receipt = Add(receipt, KingdomAssentingMootRole.Assent, 20);
			receipt = Add(receipt, KingdomAssentingMootRole.Exemption, 20);
			CollectionAssert.AreEqual(new[] { 10, 20, 30 }, receipt.AssentResidentIds);
			CollectionAssert.AreEqual(new[] { 20 }, receipt.ExemptResidentIds);
			Assert.AreEqual(0, original.AssentResidentIds.Count);
			Assert.IsTrue(KingdomAssentingMootRules.Contains(receipt,
				KingdomAssentingMootRole.Assent, 20));
			Assert.IsTrue(KingdomAssentingMootRules.Contains(receipt,
				KingdomAssentingMootRole.Exemption, 20));
			Assert.IsFalse(KingdomAssentingMootRules.TryChangeMember(receipt,
				KingdomAssentingMootRole.Assent, true, 20, "resident-20", "body-20",
				99L, out _, out _));
			AssertValid(receipt);
		}

		[Test]
		public void SixSeatCapsAndExemptionsSpendCurrentVoices()
		{
			KingdomAssentingMootReceipt receipt = Prepare();
			for (int i = 1; i <= KingdomAssentingMootRules.MaxAssents; i++)
				receipt = Add(receipt, KingdomAssentingMootRole.Assent, i);
			Assert.IsFalse(KingdomAssentingMootRules.TryChangeMember(receipt,
				KingdomAssentingMootRole.Assent, true, 99, "overflow", "body-overflow",
				99L, out _, out _));
			Assert.AreEqual(60, KingdomAssentingMootRules.StrengthFor(6, 0));
			Assert.AreEqual(40, KingdomAssentingMootRules.StrengthFor(6, 2));
			Assert.AreEqual(0, KingdomAssentingMootRules.StrengthFor(2, 4));
			Assert.AreEqual(60, KingdomAssentingMootRules.StrengthFor(999, -1));
		}

		[Test]
		public void ProjectionLifecycleSuspendsAndRepreparesWithoutMutation()
		{
			KingdomAssentingMootReceipt prepared = Add(Prepare(),
				KingdomAssentingMootRole.Assent, 7);
			KingdomAssentingMootReceipt applied =
				KingdomAssentingMootRules.Applied(prepared, 10, 110L);
			KingdomAssentingMootReceipt suspended =
				KingdomAssentingMootRules.Suspended(applied, "resident departed", 120L);
			KingdomAssentingMootReceipt again =
				KingdomAssentingMootRules.PrepareProjection(suspended, 130L);
			Assert.AreEqual(KingdomAssentingMootPhase.Prepared, prepared.Phase);
			Assert.AreEqual(KingdomAssentingMootPhase.Applied, applied.Phase);
			Assert.AreEqual(10, applied.Strength);
			Assert.AreEqual(KingdomAssentingMootPhase.Suspended, suspended.Phase);
			Assert.AreEqual(0, suspended.Strength);
			Assert.AreEqual(KingdomAssentingMootPhase.Prepared, again.Phase);
			AssertValid(applied);
			AssertValid(suspended);
			AssertValid(again);
		}

		[Test]
		public void RebindPreservesMembersButChangesExactPhysicalAuthority()
		{
			KingdomAssentingMootReceipt current = Add(Prepare(),
				KingdomAssentingMootRole.Assent, 7);
			string oldAuthority = current.AuthorityId;
			Assert.IsTrue(KingdomAssentingMootRules.TryRebind(current, "zone-b",
				"building-b", "lot-b", 901, 150L,
				out KingdomAssentingMootReceipt rebound, out string failure), failure);
			Assert.AreNotEqual(oldAuthority, rebound.AuthorityId);
			Assert.AreEqual(current.Generation + 1, rebound.Generation);
			CollectionAssert.AreEqual(current.AssentResidentIds, rebound.AssentResidentIds);
			AssertValid(rebound);
		}

		[Test]
		public void TamperingFailsClosedAndQuarantineIsBounded()
		{
			KingdomAssentingMootReceipt receipt = Add(Prepare(),
				KingdomAssentingMootRole.Assent, 7);
			receipt.AssentBodyObjectIds[0] = "foreign-body";
			AssertInvalid(receipt);
			KingdomAssentingMootReceipt quarantined =
				KingdomAssentingMootRules.Quarantined(receipt, new string('x', 900));
			Assert.AreEqual(KingdomAssentingMootPhase.Quarantined, quarantined.Phase);
			Assert.LessOrEqual(quarantined.Fault.Length,
				KingdomAssentingMootRules.MaxFaultChars);
			AssertValid(quarantined);
		}

		[Test]
		public void RuntimeUsesExactNativeWardOwnershipAndBodyVeto()
		{
			string zone = Read("Growth", "KingdomAssentingMoot.ZoneProjection.cs");
			string member = Read("Growth", "r_KingdomAssentingMootMember.cs");
			string owner = Read("Growth", "r_KingdomAssentingMoot.cs");
			string xml = Read("RuntimeData", "ObjectBlueprints.xml");
			StringAssert.Contains("new AmbientStabilization", zone);
			StringAssert.Contains("Zone.Parts[at + 1] as AmbientStabilization", zone);
			StringAssert.Contains("ambient.Owner = Building", zone);
			StringAssert.Contains("reality.Owner = Building", zone);
			StringAssert.Contains("ApplyAmbientRealityStabilized", member);
			StringAssert.Contains("ExemptionStillActive(this, ParentObject)", member);
			StringAssert.Contains("return false", member);
			StringAssert.Contains("TookDamageEvent", owner);
			StringAssert.Contains("BeforeDestroyObjectEvent", owner);
			StringAssert.Contains("if (!KingdomMaster.AutomaticWorkAllowed(system)) return;", owner);
			string ui = Read("Growth", "KingdomAssentingMoot.UI.cs");
			string transactions = Read("Growth", "KingdomAssentingMoot.Transactions.cs");
			StringAssert.Contains("if (!KingdomMaster.NewWorkAllowed(system))", ui);
			StringAssert.Contains("if (!KingdomMaster.NewWorkAllowed(System))", transactions);
			StringAssert.Contains("!KingdomMaster.NewWorkAllowed(Context.System)", transactions);
			StringAssert.Contains("<part Name=\"r_KingdomAssentingMoot\" />", xml);
			StringAssert.DoesNotContain("NormCore", zone + member + owner);
		}

		[Test]
		public void ActivationIsEphemeralCardinalSurfaceAndLifecycleRecoveryIsWired()
		{
			string shared = Read("Growth", "KingdomReopenedExoticActivation.cs");
			string activation = Read("Growth",
				"KingdomReopenedExoticActivation.AssentingMoot.cs");
			string hooks = Read("Core", "KingdomSystem.z20.Events.cs")
				+ Read("Core", "KingdomSystem.z22.Standings.cs")
				+ Read("Growth", "KingdomAssentingMoot.RecoveryHooks.cs");
			StringAssert.Contains("node:assent", activation);
			StringAssert.Contains("rite:Chavvah", activation);
			StringAssert.Contains("z != 10", activation);
			StringAssert.Contains("px - 1, py", activation);
			StringAssert.Contains("px + 1, py", activation);
			StringAssert.Contains("px, py - 1", activation);
			StringAssert.Contains("px, py + 1", activation);
			StringAssert.Contains("DescendsFrom(\"TerrainMoonStair\")", activation);
			StringAssert.Contains("r_KingdomAssentingMoot.RuntimeOwnerVersion", activation);
			StringAssert.DoesNotContain("Learn(", shared + activation);
			Assert.AreEqual(2, Count(hooks, "KingdomAssentingMoot.ReconcileZone"));
			Assert.AreEqual(1, Count(hooks, "KingdomAssentingMoot.ReconcileAll"));
			StringAssert.Contains("PruneLoadedMemberProjections(System, Zone)", hooks);
		}

		[Test]
		public void PhaseAndRoleEnumsAreAppendOnly()
		{
			Assert.AreEqual("0,1,2,3,4", JoinValues(typeof(KingdomAssentingMootPhase)));
			Assert.AreEqual("1,2", JoinValues(typeof(KingdomAssentingMootRole)));
		}

		private static KingdomAssentingMootReceipt Prepare()
		{
			Assert.IsTrue(KingdomAssentingMootRules.TryPrepare("realm-a", "city-a",
				"New Grit Gate", "zone-a", "building-a", "lot-a", 900, 1, 100L,
				out KingdomAssentingMootReceipt receipt, out string failure), failure);
			return receipt;
		}

		private static KingdomAssentingMootReceipt Add(KingdomAssentingMootReceipt receipt,
			KingdomAssentingMootRole role, int id)
		{
			Assert.IsTrue(KingdomAssentingMootRules.TryChangeMember(receipt, role, true, id,
				"resident-" + id, "body-" + id, 100L + id,
				out KingdomAssentingMootReceipt next, out string failure), failure);
			return next;
		}

		private static bool Eligible(bool a, bool b, bool c, bool d, bool e, bool f)
		{
			return KingdomAssentingMootRules.ActivationEligible(a, b, c, d, e, f);
		}

		private static void AssertValid(KingdomAssentingMootReceipt receipt)
		{
			Assert.IsTrue(KingdomAssentingMootRules.Validate(receipt, out string failure), failure);
		}

		private static void AssertInvalid(KingdomAssentingMootReceipt receipt)
		{
			Assert.IsFalse(KingdomAssentingMootRules.Validate(receipt, out string failure));
			Assert.IsNotEmpty(failure);
		}

		private static int Count(string source, string value)
		{
			int count = 0;
			for (int at = 0; (at = source.IndexOf(value, at,
				StringComparison.Ordinal)) >= 0; at += value.Length) count++;
			return count;
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
