#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomResidentDepartureCapacityTests
	{
		private static KingdomResidentDepartureOperation Departure(int id = 1, char realm = 'a', char settlement = 'b')
		{
			var operation = new KingdomResidentDepartureOperation
			{
				Version = 1, Phase = (int)KingdomResidentDeparturePhase.RolesClosed, Revision = 5,
				RealmId = KingdomIdentityRules.RealmPrefix + new string(realm, 64),
				SettlementId = KingdomIdentityRules.SettlementPrefix + new string(settlement, 64),
				ResidentId = id, BodyObjectId = "departure-body:" + id, ZoneId = "JoppaWorld.12.24.1.1.10",
				ResidentName = "resident " + id, PreparedTick = 100, DeparturesBefore = id - 1, Chronicled = true,
				ChronicleLine = "resident " + id + " left the settlement for wetter country",
				LedgerLine = "A named resident left for wetter country."
			};
			operation.OperationId = KingdomResidentDepartureRules.Id(operation.RealmId, operation.SettlementId,
				id, operation.BodyObjectId, operation.PreparedTick);
			Assert.IsTrue(KingdomResidentDepartureRules.Valid(operation)); return operation;
		}

		private static KingdomChronicleCapacityWitness Witness(KingdomResidentDepartureOperation operation, string raw = null)
		{
			Assert.IsTrue(KingdomResidentDepartureCapacityArchive.Fingerprint(operation, out string fingerprint));
			Assert.IsTrue(KingdomChronicleCapacityRules.TryObserve(ChronicleCapacityFixture.Shape(raw),
				KingdomResidentDepartureCapacityArchive.EventId(operation), fingerprint, out var witness));
			return witness;
		}

		private static string Retain(string archive, KingdomResidentDepartureOperation operation)
		{
			Assert.IsTrue(KingdomResidentDepartureCapacityArchive.TryRetain(archive, operation, Witness(operation), out string next));
			return next;
		}

		[Test]
		public void FullRegistrySettlesStoryAndAdvancesRealDepartureWithoutEvictingOrPublishing()
		{
			var operation = Departure(); string registry = ChronicleCapacityFixture.Full;
			string archive = KingdomResidentDepartureCapacityArchive.None;
			int publisherCalls = 0, observations = 0, proofs = 0, saves = 0;
			Assert.IsTrue(KingdomResidentDepartureStoryRules.TrySettle(operation, archive, () => true,
				() => { observations++; return Witness(operation, registry); }, () => { proofs++; return true; },
				next => { saves++; archive = next; return true; }, () => { publisherCalls++; return false; }));
			Assert.AreEqual(1, observations); Assert.AreEqual(2, proofs); Assert.AreEqual(1, saves);
			Assert.AreEqual(0, publisherCalls); Assert.AreSame(ChronicleCapacityFixture.Full, registry);
			Assert.IsTrue(KingdomResidentDepartureRules.Advance(operation, KingdomResidentDeparturePhase.RolesClosed,
				KingdomResidentDeparturePhase.EffectsPublished));
			Assert.AreEqual(0, operation.DeparturesBefore); Assert.AreEqual("departure-body:1", operation.BodyObjectId);
			Assert.IsTrue(KingdomResidentDepartureCapacityArchive.TryMatch(archive, operation, out bool retained));
			Assert.IsTrue(retained);
			Assert.IsTrue(KingdomResidentDepartureCapacityArchive.TryPrepareRead(archive, operation.RealmId,
				operation.SettlementId, out string warning, out string acknowledged));
			StringAssert.Contains(operation.ChronicleLine, warning);
			StringAssert.Contains("Chronicle not published", warning); StringAssert.Contains("4096 replay receipts retained", warning);
			Assert.AreEqual(KingdomResidentDepartureCapacityArchive.None, acknowledged);
		}

		[Test]
		public void SavedExactCapacityRowResumesWithoutAnotherObservationOrPublisherCall()
		{
			var operation = Departure(); string archive = Retain(KingdomResidentDepartureCapacityArchive.None, operation);
			Assert.IsTrue(KingdomResidentDepartureStoryRules.TrySettle(operation, archive, () => true,
				() => { Assert.Fail("retained refusal must not be observed again"); return null; },
				() => { Assert.Fail("retained refusal must not be reclassified"); return false; },
				value => { Assert.Fail("retained refusal must not be saved again"); return false; },
				() => { Assert.Fail("retained refusal must not reach Chronicle"); return false; }));
			Assert.AreEqual(5, operation.Revision); Assert.AreEqual((int)KingdomResidentDeparturePhase.RolesClosed, operation.Phase);
		}

		[TestCase(true)] [TestCase(false)]
		public void ExistingLegacyEventAtFullCapacityAlwaysUsesOriginalFingerprintPath(bool sameFingerprint)
		{
			var operation = Departure(); string id = KingdomResidentDepartureCapacityArchive.EventId(operation);
			Assert.IsTrue(KingdomResidentDepartureCapacityArchive.Fingerprint(operation, out string fingerprint));
			Assert.IsTrue(KingdomChronicleReceiptRules.TryFingerprint(id, operation.ChronicleLine, false, null, out string legacy));
			Assert.AreEqual(legacy, fingerprint);
			Assert.IsTrue(KingdomChronicleReceiptRules.TryParseRegistry(ChronicleCapacityFixture.Full,
				out List<KingdomChronicleReceipt> rows, out _, out _));
			rows[0].EventId = id; rows[0].Fingerprint = sameFingerprint ? fingerprint : new string('f', 64);
			Assert.IsTrue(KingdomChronicleReceiptRules.TryWriteRegistry(rows, out string registry, out _));
			string archive = KingdomResidentDepartureCapacityArchive.None; int records = 0, saves = 0;
			Assert.AreEqual(sameFingerprint, KingdomResidentDepartureStoryRules.TrySettle(operation, archive, () => true,
				() =>
				{
					Assert.IsFalse(KingdomChronicleCapacityRules.TryObserve(ChronicleCapacityFixture.Shape(registry), id,
						fingerprint, out var witness)); return witness;
				}, () => { Assert.Fail("existing event cannot be capacity evidence"); return false; },
				value => { saves++; archive = value; return true; },
				() => { records++; return rows[0].Fingerprint == fingerprint && KingdomChronicleReceiptRules.IsTerminal(rows[0]); }));
			Assert.AreEqual(1, records); Assert.AreEqual(0, saves);
			Assert.AreEqual(KingdomResidentDepartureCapacityArchive.None, archive);
		}

		[TestCase(true)] [TestCase(false)]
		public void OrdinaryPublisherResultWithoutCapacityNeverCreatesRefusal(bool result)
		{
			int calls = 0;
			Assert.AreEqual(result, KingdomResidentDepartureStoryRules.TrySettle(Departure(), KingdomResidentDepartureCapacityArchive.None,
				() => true, () => null, () => { Assert.Fail(); return false; },
				value => { Assert.Fail(); return false; }, () => { calls++; return result; }));
			Assert.AreEqual(1, calls);
		}

		[TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
		public void FailedOwnerWitnessOrSaveNeverAuthorizesStoryContinuation(int failure)
		{
			var operation = Departure(); int proofCalls = 0, saves = 0, records = 0;
			Assert.IsFalse(KingdomResidentDepartureStoryRules.TrySettle(operation, KingdomResidentDepartureCapacityArchive.None,
				() => failure != 0, () => Witness(operation), () => ++proofCalls != failure,
				value => { saves++; return failure != 3; }, () => { records++; return true; }));
			Assert.AreEqual(0, records); Assert.AreEqual(failure <= 1 ? 0 : 1, saves);
			Assert.AreEqual((int)KingdomResidentDeparturePhase.RolesClosed, operation.Phase);
			Assert.AreEqual(0, operation.DeparturesBefore);
		}

		[Test]
		public void ExactWitnessCannotChangeTextBodyCreditOrRegistryOnReplay()
		{
			var operation = Departure(); var witness = Witness(operation);
			string archive = Retain(KingdomResidentDepartureCapacityArchive.None, operation);
			foreach (Action<KingdomResidentDepartureOperation> mutate in new Action<KingdomResidentDepartureOperation>[] {
				value => value.ChronicleLine += " changed", value => value.LedgerLine += " changed",
				value => value.ZoneId += "changed", value => value.ResidentName += "changed", value => value.DeparturesBefore++ })
			{
				var changed = operation.Copy(); mutate(changed);
				Assert.IsFalse(KingdomResidentDepartureCapacityArchive.TryMatch(archive, changed, out _));
				Assert.IsFalse(KingdomResidentDepartureCapacityArchive.TryRetain(archive, changed, witness, out _));
			}
			var other = Witness(operation, ChronicleCapacityFixture.Registry(4096, true));
			Assert.IsFalse(KingdomResidentDepartureCapacityArchive.TryRetain(archive, operation, other, out _));
			Assert.IsFalse(KingdomResidentDepartureCapacityArchive.TryRetain(KingdomResidentDepartureCapacityArchive.None,
				Departure(2), witness, out _));
		}

		[Test]
		public void EightWarningsRefuseNewAdmissionWithoutEvictionButExistingRowStillResumes()
		{
			string archive = KingdomResidentDepartureCapacityArchive.None;
			for (int i = 1; i <= 8; i++)
			{
				Assert.IsTrue(KingdomResidentDepartureCapacityArchive.CanAdmit(archive));
				archive = Retain(archive, Departure(i));
			}
			string before = archive;
			Assert.IsFalse(KingdomResidentDepartureCapacityArchive.CanAdmit(archive));
			Assert.IsFalse(KingdomResidentDepartureCapacityArchive.TryRetain(archive, Departure(9), Witness(Departure(9)), out _));
			Assert.AreEqual(before, Retain(archive, Departure(8))); Assert.AreEqual(before, archive);
			Assert.IsTrue(KingdomResidentDepartureCapacityArchive.TryMatch(archive, Departure(1), out bool retained));
			Assert.IsTrue(retained);
		}

		[Test]
		public void ReadingOneOwnerPreservesOtherSettlementAndOtherRealmRowsByteForByte()
		{
			KingdomResidentDepartureOperation own = Departure(), foreignSeat = Departure(2, settlement: 'c'), foreignRealm = Departure(3, realm: 'd');
			string foreign = Retain(Retain(KingdomResidentDepartureCapacityArchive.None, foreignSeat), foreignRealm);
			string all = Retain(foreign, own);
			Assert.IsTrue(KingdomResidentDepartureCapacityArchive.TryPrepareRead(all, own.RealmId, own.SettlementId,
				out string warning, out string acknowledged));
			Assert.AreEqual(foreign, acknowledged); StringAssert.Contains(own.ChronicleLine, warning);
			StringAssert.DoesNotContain(foreignSeat.ChronicleLine, warning); StringAssert.DoesNotContain(foreignRealm.ChronicleLine, warning);
			Assert.IsTrue(KingdomResidentDepartureCapacityArchive.TryPrepareRead(acknowledged, own.RealmId, own.SettlementId,
				out string repeated, out string unchanged));
			Assert.AreEqual("", repeated); Assert.AreEqual(foreign, unchanged);
			Assert.AreNotEqual(acknowledged, all, "projection alone cannot erase unread source evidence");
		}

		[TestCase(null)] [TestCase("")] [TestCase("dc1:")] [TestCase("dc2:none")] [TestCase("dc1:none ")]
		public void OnlyExplicitNamedFieldDefaultMeansEmpty(string wire)
		{
			Assert.IsFalse(KingdomResidentDepartureCapacityArchive.CanAdmit(wire));
			Assert.IsFalse(KingdomResidentDepartureCapacityArchive.TryPrepareRead(wire, Departure().RealmId,
				Departure().SettlementId, out _, out _));
			Assert.IsTrue(KingdomResidentDepartureCapacityArchive.CanAdmit(KingdomResidentDepartureCapacityArchive.None));
		}

		[Test]
		public void MaximumValidUtf8RowsFitReservedArchiveRoom()
		{
			string archive = KingdomResidentDepartureCapacityArchive.None;
			for (int i = 1; i <= 8; i++)
			{
				var operation = Departure(i); operation.BodyObjectId = new string('\u0800', 511) + i;
				operation.ZoneId = operation.ResidentName = new string('\u0800', 512);
				operation.ChronicleLine = operation.LedgerLine = new string('\u0800', 4096);
				operation.OperationId = KingdomResidentDepartureRules.Id(operation.RealmId, operation.SettlementId,
					operation.ResidentId, operation.BodyObjectId, operation.PreparedTick);
				operation.Phase = (int)KingdomResidentDeparturePhase.Prepared;
				Assert.IsTrue(KingdomResidentDepartureCapacityArchive.CanAdmit(archive, operation));
				operation.Phase = (int)KingdomResidentDeparturePhase.RolesClosed;
				archive = Retain(archive, operation);
			}
			Assert.Less(archive.Length, KingdomResidentDepartureCapacityArchive.MaximumWireChars);
		}

		[TestCase(0)] [TestCase(2)] [TestCase(4)] [TestCase(8)] [TestCase(16)] [TestCase(31)]
		public void MalformedFiveTableAuthorityCannotCreateAWarningOrSettleAFailedPublisher(int mask)
		{
			var operation = Departure(); int saves = 0;
			var shape = new KingdomDurableKeyObservation
			{
				HasString = (mask & 1) != 0, String = ChronicleCapacityFixture.Full,
				HasInt = (mask & 2) != 0, HasInt64 = (mask & 4) != 0,
				HasObject = (mask & 8) != 0, HasBoolean = (mask & 16) != 0
			};
			Assert.IsTrue(KingdomResidentDepartureCapacityArchive.Fingerprint(operation, out string fingerprint));
			Assert.IsFalse(KingdomResidentDepartureStoryRules.TrySettle(operation, KingdomResidentDepartureCapacityArchive.None,
				() => true, () =>
				{
					Assert.IsFalse(KingdomChronicleCapacityRules.TryObserve(shape,
						KingdomResidentDepartureCapacityArchive.EventId(operation), fingerprint, out var witness));
					return witness;
				}, () => true, value => { saves++; return true; }, () => false));
			Assert.AreEqual(0, saves); Assert.AreSame(ChronicleCapacityFixture.Full, shape.String);
		}

		[TestCase(0)] [TestCase(4095)] [TestCase(4097)]
		public void WitnessCountMustBeExactlyThePermanentRegistryLimit(int count)
		{
			var operation = Departure(); var exact = Witness(operation);
			var wrong = new KingdomChronicleCapacityWitness(exact.EventId, exact.Fingerprint, count, exact.RegistryHash);
			Assert.IsFalse(KingdomResidentDepartureCapacityArchive.TryRetain(KingdomResidentDepartureCapacityArchive.None,
				operation, wrong, out _));
		}

		[TestCase("\n")] [TestCase(" ")]
		public void NoncanonicalArchiveTextCannotBeAcknowledgedOrReplayed(string suffix)
		{
			var operation = Departure(); string wire = Retain(KingdomResidentDepartureCapacityArchive.None, operation) + suffix;
			Assert.IsFalse(KingdomResidentDepartureCapacityArchive.TryMatch(wire, operation, out _));
			Assert.IsFalse(KingdomResidentDepartureCapacityArchive.TryPrepareRead(wire, operation.RealmId,
				operation.SettlementId, out _, out _));
		}

		[TestCase("Origin")] [TestCase("Cause")] [TestCase("PolityConclusionRef")]
		[TestCase("AuthorizationKind")] [TestCase("AuthorizationEventId")]
		[TestCase("AuthorizationOwnerObjectId")] [TestCase("AuthorizationCauseDigest")]
		[TestCase("PriorCook")] [TestCase("PriorOffice")] [TestCase("PriorCook.Fault")]
		[TestCase("PriorOffice.SettlementName")] [TestCase("PriorPolity.DeedSummary")]
		public void CallbackCannotSettleStoryAfterValidSameRevisionAuthorityMutation(string field)
		{
			var operation = Departure();
			Assert.IsTrue(KingdomNamedCookRules.TryPrepare(operation.RealmId, operation.SettlementId, "fixture seat",
				operation.ResidentId, operation.ResidentName, operation.BodyObjectId, 1, 10, out var cook, out _));
			operation.PriorCook = KingdomNamedCookRules.Applied(cook);
			operation.PriorOffice = new KingdomCivicOfficeReceipt { Phase = KingdomCivicOfficePhase.Held,
				Generation = 1, SettlementId = operation.SettlementId, SettlementName = "fixture seat", WorkId = 1,
				HolderResidentId = operation.ResidentId, HolderObjectId = operation.BodyObjectId, HolderName = operation.ResidentName };
			operation.PriorPolity = new KingdomPolityNamedFigureRecord { DisplayName = operation.ResidentName,
				Origin = KingdomPolityFigureOrigin.PromotedByDeed, Phase = KingdomPolityFigurePhase.Active,
				ResidentId = operation.ResidentId, ResidentSettlementId = operation.SettlementId, DeedSummary = "held deed" };
			operation.PolityConclusionRef = "held conclusion";
			operation.AuthorizationKind = (int)Simulation.City.KingdomResidentDestructionAuthorizationKind.LabRefusalDeparture;
			operation.AuthorizationEventId = "held event"; operation.AuthorizationOwnerObjectId = "held owner";
			operation.AuthorizationCauseDigest = "held cause";
			Assert.IsTrue(KingdomResidentDepartureRules.Valid(operation));
			var frozen = operation.Copy(); int callbacks = 0;
			Assert.IsTrue(EqualGraph(operation, frozen));
			Assert.IsFalse(KingdomResidentDepartureStoryRules.TrySettle(operation, KingdomResidentDepartureCapacityArchive.None,
				() => EqualGraph(operation, frozen), () => null, () => false, value => false, () =>
				{
					callbacks++;
					if (field == "AuthorizationKind")
					{
						operation.AuthorizationKind = 0;
						operation.AuthorizationEventId = operation.AuthorizationOwnerObjectId = operation.AuthorizationCauseDigest = "";
					}
					else
					{
						string[] path = field.Split('.'); object target = operation;
						if (path.Length == 2) target = typeof(KingdomResidentDepartureOperation).GetField(path[0]).GetValue(operation);
						FieldInfo member = target.GetType().GetField(path[path.Length - 1]);
						member.SetValue(target, field == "PriorCook.Fault" || member.FieldType != typeof(string)
							? null : (string)member.GetValue(target) + " changed");
					}
					Assert.IsTrue(KingdomResidentDepartureRules.Valid(operation), field + " remains valid under the old barrier");
					Assert.AreEqual(frozen.Revision, operation.Revision);
					return true;
				}));
			Assert.AreEqual(1, callbacks); Assert.AreEqual(frozen.Phase, operation.Phase);
			Assert.IsTrue(KingdomResidentDepartureRules.Valid(operation), field + " remains valid under the old barrier");
			Assert.AreEqual(frozen.Revision, operation.Revision);
			Assert.AreEqual(frozen.DeparturesBefore, operation.DeparturesBefore);
			Assert.IsFalse(EqualGraph(operation, frozen));
		}

		// Independent reflected oracle for the real pure publication frontier; source inventory
		// pins bind every field to the engine closure's typed comparisons without executing XRL.
		private static bool EqualGraph(object value, object frozen)
		{
			if (value == null || frozen == null) return value == frozen;
			Type type = value.GetType();
			if (type != frozen.GetType()) return false;
			if (type == typeof(string) || type.IsValueType) return value.Equals(frozen);
			foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
				if (!EqualGraph(field.GetValue(value), field.GetValue(frozen))) return false;
			return true;
		}
	}
}
#endif
