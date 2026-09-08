#if TAF_TESTS
using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomPropertyRulesTests
	{
		private const string Realm = "taf:realm:one";
		private const string Settlement = "taf:settlement:one";
		private const string ObjectId = "object-123";

		[Test]
		public void DesignationRequiresOneExplicitFounderOwnedTakeableObject()
		{
			ClassicAssert.AreEqual(KingdomPropertyVerdict.Allowed, Judge());
			ClassicAssert.AreEqual(KingdomPropertyVerdict.Unfounded, Judge(founded: false));
			ClassicAssert.AreEqual(KingdomPropertyVerdict.UnclaimedGround, Judge(claimed: false));
			ClassicAssert.AreEqual(KingdomPropertyVerdict.NotFounder, Judge(founder: false));
			ClassicAssert.AreEqual(KingdomPropertyVerdict.NoPhysicalObject, Judge(physics: false));
			ClassicAssert.AreEqual(KingdomPropertyVerdict.Creature, Judge(creature: true));
			ClassicAssert.AreEqual(KingdomPropertyVerdict.Important, Judge(important: true));
			ClassicAssert.AreEqual(KingdomPropertyVerdict.Untakeable, Judge(takeable: false));
			ClassicAssert.AreEqual(KingdomPropertyVerdict.NotFounderOwned,
				Judge(founderOwned: false));
			ClassicAssert.AreEqual(KingdomPropertyVerdict.ForeignOwner,
				Judge(owner: "Joppa"));
			ClassicAssert.AreEqual(KingdomPropertyVerdict.AlreadyDesignated,
				Judge(receipt: true));
		}

		[Test]
		public void PreparedDesignationIsRecoverableButForeignMutationQuarantines()
		{
			ClassicAssert.AreEqual(KingdomPropertyMutation.ApplyRealmOwner,
				KingdomPropertyRules.JudgeApply(KingdomPropertyPhase.Prepared, "", Realm, ""));
			ClassicAssert.AreEqual(KingdomPropertyMutation.ObserveApplied,
				KingdomPropertyRules.JudgeApply(KingdomPropertyPhase.Prepared, "", Realm, Realm));
			ClassicAssert.AreEqual(KingdomPropertyMutation.Quarantine,
				KingdomPropertyRules.JudgeApply(KingdomPropertyPhase.Prepared, "", Realm, "Joppa"));
			ClassicAssert.AreEqual(KingdomPropertyMutation.Refuse,
				KingdomPropertyRules.JudgeApply(KingdomPropertyPhase.Designated, "", Realm, Realm));
		}

		[Test]
		public void ReleaseRestoresExactPriorOwnerAndNeverGuessesDivergence()
		{
			ClassicAssert.AreEqual(KingdomPropertyMutation.RestorePriorOwner,
				KingdomPropertyRules.JudgeRelease(KingdomPropertyPhase.Designated,
					"Player", Realm, Realm));
			ClassicAssert.AreEqual(KingdomPropertyMutation.ObserveReleased,
				KingdomPropertyRules.JudgeRelease(KingdomPropertyPhase.ReleasePrepared,
					"Player", Realm, "Player"));
			ClassicAssert.AreEqual(KingdomPropertyMutation.Quarantine,
				KingdomPropertyRules.JudgeRelease(KingdomPropertyPhase.Designated,
					"Player", Realm, "Joppa"));
		}

		[Test]
		public void ReceiptSchemaPinsEveryRecoverableAndTerminalPhase()
		{
			AssertValid(KingdomPropertyPhase.Prepared, 0L, "");
			AssertValid(KingdomPropertyPhase.Designated, 0L, "");
			AssertValid(KingdomPropertyPhase.ReleasePrepared, 0L, "");
			AssertValid(KingdomPropertyPhase.Released, 20L, "");
			AssertValid(KingdomPropertyPhase.Quarantined, 0L, "diverged");
			ClassicAssert.IsFalse(Valid(KingdomPropertyPhase.None, 0L, ""));
			ClassicAssert.IsFalse(Valid(KingdomPropertyPhase.Designated, 20L, ""));
			ClassicAssert.IsFalse(Valid(KingdomPropertyPhase.Released, 5L, ""));
			ClassicAssert.IsFalse(Valid(KingdomPropertyPhase.Quarantined, 0L, ""));
			ClassicAssert.IsFalse(KingdomPropertyRules.ValidReceiptShape(99,
				KingdomPropertyPhase.Designated, Realm, Settlement, Realm, ObjectId,
				"", 10L, 0L, ""));
		}

		[Test]
		public void AnyExistingReceiptRefusesFreshDesignationIncludingReleased()
		{
			foreach (KingdomPropertyPhase phase in Enum.GetValues(typeof(KingdomPropertyPhase)))
			{
				if (phase == KingdomPropertyPhase.None) continue;
				ClassicAssert.AreEqual(KingdomPropertyVerdict.AlreadyDesignated,
					Judge(receipt: true), phase.ToString());
			}
		}

		[Test]
		public void PhaseAndCharterEnumsAreAppendOnly()
		{
			ClassicAssert.AreEqual("0,1,2,3,4,5", JoinValues(typeof(KingdomPropertyPhase)));
			ClassicAssert.AreEqual(36, (int)KingdomCharterAction.DesignateProperty);
		}

		[Test]
		public void RuntimeUsesNativeOwnerThroughExactReceiptNotClaimStamping()
		{
			string transactions = Read("Core", "KingdomProperty.Transactions.cs");
			string selection = Read("Core", "KingdomProperty.cs");
			string part = Read("Core", "r_KingdomProperty.cs");
			StringAssert.Contains("Item.Physics.Owner = Receipt.FactionId;", transactions);
			StringAssert.Contains("Item.Physics.Owner = string.IsNullOrEmpty(receipt.PriorOwner)",
				transactions);
			StringAssert.Contains("System.ClaimedZones.Contains(zone.ZoneID)", selection);
			StringAssert.Contains("FounderOwned(Item)", selection);
			StringAssert.Contains("ID == CanBeReplicatedEvent.ID", part);
			StringAssert.Contains("ParentObject?.RemovePart(this);", part);
			StringAssert.DoesNotContain("SetZoneProperty", transactions + selection + part);
			StringAssert.DoesNotContain("foreach (string", transactions + selection + part);
		}

		[Test]
		public void GameObjectIdentityMintingIsRestrictedToConfirmedTransactionSeams()
		{
			string propertySelection = Read("Core", "KingdomProperty.cs")
				+ Read("Core", "KingdomPropertyRules.cs");
			string propertyTransaction = Read("Core", "KingdomProperty.Transactions.cs");
			string founding = Read("Core", "KingdomFoundingTransaction.03AuthorityProof.cs")
				+ Read("Core", "KingdomFoundingTransaction.10Begin.cs")
				+ Read("Core", "KingdomFoundingTransaction.10Staging.cs")
				+ Read("Core", "KingdomFoundingTransaction.17ReceiptValidation.cs");
			string inheritance = Read("Core", "KingdomInheritanceSpatial.Boundary.cs")
				+ Read("Core", "KingdomInheritanceSpatial.CaptureResult.cs")
				+ Read("Core", "KingdomInheritanceSpatial.Evidence.cs")
				+ Read("Core", "KingdomInheritanceSpatial.cs")
				+ Read("Core", "KingdomInheritanceSpatialRules.cs");
			string carry = Read("Experience", "KingdomCarryRuntime.cs")
				+ Read("Experience", "KingdomCarryRuntime.z01.DriveAndSinks.cs")
				+ Read("Experience", "KingdomCarryRuntime.z02.Designation.cs")
				+ Read("Experience", "KingdomCarryRuntime.z03.TrustedWorld.cs")
				+ Read("Experience", "KingdomCarryRuntime.z04.ScheduleObservations.cs");
			string remembrance = Read("Experience", "KingdomRemembranceRuntime.Commands.cs")
				+ Read("Experience", "KingdomRemembranceRuntime.Context.cs")
				+ Read("Experience", "KingdomRemembranceRuntime.Open.cs")
				+ Read("Experience", "KingdomRemembranceRuntime.Projection.cs")
				+ Read("Experience", "KingdomRemembranceRuntime.Reconcile.cs")
				+ Read("Experience", "KingdomRemembranceRuntime.Removal.cs");

			ClassicAssert.AreEqual(0, MintingReads(propertySelection), "property preview");
			ClassicAssert.AreEqual(1, MintingReads(propertyTransaction), "property consent seam");
			ClassicAssert.AreEqual(1, MintingReads(founding), "founding consent seam");
			ClassicAssert.AreEqual(0, MintingReads(inheritance), "inheritance witness");
			ClassicAssert.AreEqual(1, MintingReads(carry), "carry consent seam");
			ClassicAssert.AreEqual(1, MintingReads(remembrance), "remembrance consent seam");

			StringAssert.DoesNotContain("TryRetireReleased(", propertySelection);
			StringAssert.DoesNotContain("TryRetireReleased(", propertyTransaction);
			StringAssert.DoesNotContain("RemovePart(", propertyTransaction);
			StringAssert.Contains("receipt.Phase = KingdomPropertyPhase.Released;",
				propertyTransaction);
			StringAssert.Contains("receipt.ReleasedTick = Math.Max(receipt.DesignatedTick",
				propertyTransaction);
			StringAssert.Contains("if (existing.Phase != KingdomPropertyPhase.Prepared)",
				propertyTransaction);
			AssertOrdered(propertyTransaction,
				"if (existing.Phase != KingdomPropertyPhase.Prepared)",
				"Failure = KingdomPropertyRules.Refusal(");
			AssertOrdered(propertyTransaction, "KingdomPropertyVerdict.AlreadyDesignated",
				"ReceiptMatches(existing");
			StringAssert.Contains("Receipt.Phase == KingdomPropertyPhase.Prepared",
				propertySelection);
			StringAssert.Contains("Receipt.Phase == KingdomPropertyPhase.Designated",
				propertySelection);
			StringAssert.Contains("Receipt.Phase == KingdomPropertyPhase.ReleasePrepared",
				propertySelection);
			StringAssert.Contains("|| Receipt.Phase == KingdomPropertyPhase.ReleasePrepared;",
				propertySelection);
			StringAssert.Contains("Item.IDIfAssigned", propertyTransaction);
			StringAssert.Contains("Basin.ParentObject.IDIfAssigned", founding);
			StringAssert.Contains("StableId(item.IDIfAssigned)", inheritance);
			StringAssert.Contains("container.IDIfAssigned", carry);
			StringAssert.Contains("item.IDIfAssigned", carry);
			StringAssert.Contains("Carrier.IDIfAssigned", remembrance);
			AssertOrdered(propertyTransaction, "verdict != KingdomPropertyVerdict.Allowed",
				"AssignConfirmedPropertyIdentity(Item)");
			AssertOrdered(carry, "SameDesignation(plan", "TryAssignConfirmedIdentities(plan");
			AssertOrdered(remembrance, "TryExactOffer(System", "AssignConfirmedCarrierIdentity(Carrier)");
		}

		private static KingdomPropertyVerdict Judge(bool founded = true,
			bool claimed = true, bool founder = true, bool physics = true,
			bool creature = false, bool important = false, bool takeable = true,
			bool founderOwned = true, string owner = "", bool receipt = false)
		{
			return KingdomPropertyRules.JudgeDesignation(founded, claimed, founder, physics,
				creature, important, takeable, founderOwned, owner, Realm, receipt);
		}

		private static void AssertValid(KingdomPropertyPhase phase, long released,
			string fault)
		{
			ClassicAssert.IsTrue(Valid(phase, released, fault), phase.ToString());
		}

		private static bool Valid(KingdomPropertyPhase phase, long released, string fault)
		{
			return KingdomPropertyRules.ValidReceiptShape(
				KingdomPropertyRules.CurrentReceiptVersion, phase, Realm, Settlement, Realm,
				ObjectId, "", 10L, released, fault);
		}

		private static string JoinValues(Type type)
		{
			Array values = Enum.GetValues(type);
			string[] rows = new string[values.Length];
			for (int i = 0; i < values.Length; i++) rows[i] = Convert.ToInt32(values.GetValue(i)).ToString();
			return string.Join(",", rows);
		}

		private static int MintingReads(string source)
		{
			return Regex.Matches(source, @"\.ID(?![A-Za-z0-9_])").Count;
		}

		private static void AssertOrdered(string source, string earlier, string later)
		{
			int first = source.IndexOf(earlier, StringComparison.Ordinal);
			int second = source.IndexOf(later, first + earlier.Length, StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(first, 0, earlier);
			ClassicAssert.Greater(second, first, later);
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
