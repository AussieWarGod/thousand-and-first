#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomSubsidenceDepartureIdentityTests
	{
		private const long Anchor = 700L;
		private static readonly long Due = Anchor + KingdomSubsidenceStepRules.StepTicks;
		private static readonly string Realm = KingdomIdentityRules.RealmPrefix + new string('a', 64);
		private static readonly string Settlement = KingdomIdentityRules.SettlementPrefix + new string('b', 64);

		private static KingdomSubsidenceStepBook Admitted()
		{
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryDecode("ss1:new", out KingdomSubsidenceStepBook fresh));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryAdmit(fresh, Realm, Settlement,
				out KingdomSubsidenceStepBook admitted));
			return admitted;
		}

		private static KingdomSubsidenceStepBook Begin(int storage = 512, string binding = "roof")
		{
			Assert.IsTrue(KingdomSubsidenceStepRules.TryBegin(Admitted(), Anchor, Due,
				GrowthStage.City, 5, out KingdomSubsidenceStepBook book, storage, binding));
			return book;
		}

		private static KingdomResidentDepartureOperation Departure()
		{
			KingdomResidentDepartureOperation departure = new KingdomResidentDepartureOperation
			{
				Version = KingdomResidentDepartureOperation.CurrentVersion,
				Phase = (int)KingdomResidentDeparturePhase.Prepared, Revision = 1,
				RealmId = Realm, SettlementId = Settlement, ResidentId = 7,
				BodyObjectId = "exact-subsidence-body", ZoneId = "JoppaWorld.12.24.1.1.10",
				ResidentName = "Exact departure fixture", PreparedTick = Due
			};
			Rehash(departure);
			return departure;
		}

		private static void Rehash(KingdomResidentDepartureOperation departure)
		{
			departure.OperationId = KingdomResidentDepartureRules.Id(departure.RealmId,
				departure.SettlementId, departure.ResidentId, departure.BodyObjectId, departure.PreparedTick);
			Assert.IsTrue(KingdomResidentDepartureRules.Valid(departure));
		}

		private static string Wire(KingdomSubsidenceStepBook book)
		{
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(book, out string wire));
			return wire;
		}

		private static KingdomSubsidenceStepBook RoundTrip(KingdomSubsidenceStepBook book)
		{
			string wire = Wire(book);
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(wire, out KingdomSubsidenceStepBook decoded));
			Assert.AreEqual(wire, Wire(decoded));
			return decoded;
		}

		private static KingdomSubsidenceStepOperation Rebuild(KingdomSubsidenceStepOperation op,
			string pendingId, KingdomSubsidenceDepartureIdentity pending, int storage, string binding)
		{
			return new KingdomSubsidenceStepOperation(op.Id, op.AnchorTick, op.DueTick,
				op.FromStage, op.ReachedStage, op.Quota, op.Completed, op.Phase, pendingId,
				op.PendingCredited, op.CancelRequested, op.CancelTick, op.CancelToken,
				op.RungModel, op.Fault, storage, binding, op.LastActivityTick, op.CreditedDepartureIds, pending);
		}

		private static void RefusesWire(KingdomSubsidenceStepBook corrupt)
		{
			Assert.IsFalse(KingdomSubsidenceStepRules.Valid(corrupt));
			Assert.IsFalse(KingdomSubsidenceStepCodec.TryEncode(corrupt, out string wire));
			Assert.IsNull(wire);
		}

		[Test]
		public void AssociationFreezesEveryCarrierFieldAndExactRepeatSurvivesRoundTrip()
		{
			KingdomSubsidenceStepBook book = Begin();
			KingdomResidentDepartureOperation departure = Departure();
			KingdomResidentDepartureOperation original = departure.Copy();
			Assert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, departure, out book));
			departure.ResidentId = 8; departure.BodyObjectId = "changed-body";
			departure.ZoneId = "changed-zone"; departure.PreparedTick++;
			book = RoundTrip(book);
			Assert.AreEqual(original.OperationId, book.Active.PendingDepartureId);
			Assert.AreEqual(original.ResidentId, book.Active.PendingIdentity.ResidentId);
			Assert.AreEqual(original.BodyObjectId, book.Active.PendingIdentity.BodyObjectId);
			Assert.AreEqual(original.ZoneId, book.Active.PendingIdentity.ZoneId);
			Assert.AreEqual(original.PreparedTick, book.Active.PendingIdentity.PreparedTick);
			Assert.IsTrue(book.Active.PendingIdentity.Matches(original));
			Assert.IsFalse(book.Active.PendingIdentity.Matches(departure));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, original,
				out KingdomSubsidenceStepBook repeated));
			Assert.AreSame(book, repeated);
		}

		[TestCase("realm")]
		[TestCase("settlement")]
		[TestCase("phase")]
		public void ValidDepartureFromWrongOwnerOrPhaseCannotAssociate(string changed)
		{
			KingdomSubsidenceStepBook book = Begin();
			string before = Wire(book);
			KingdomResidentDepartureOperation departure = Departure();
			if (changed == "realm") departure.RealmId = KingdomIdentityRules.RealmPrefix + new string('c', 64);
			if (changed == "settlement") departure.SettlementId = KingdomIdentityRules.SettlementPrefix + new string('d', 64);
			if (changed == "phase") departure.Phase = (int)KingdomResidentDeparturePhase.RolesPrepared;
			Rehash(departure);
			Assert.IsFalse(KingdomSubsidenceStepRules.TryAssociate(book, departure,
				out KingdomSubsidenceStepBook refused));
			Assert.IsNull(refused);
			Assert.AreEqual(before, Wire(book));
		}

		[Test]
		public void ValidDeparturePreparedBeforeStepDueCannotAssociate()
		{
			KingdomSubsidenceStepBook book = Begin();
			string before = Wire(book);
			KingdomResidentDepartureOperation departure = Departure();
			departure.PreparedTick = Due - 1;
			Rehash(departure);
			Assert.IsFalse(KingdomSubsidenceStepRules.TryAssociate(book, departure,
				out KingdomSubsidenceStepBook refused));
			Assert.IsNull(refused);
			Assert.AreEqual(before, Wire(book));
		}

		[Test]
		public void CancellationCannotPredateItsExactPendingPreparation()
		{
			KingdomSubsidenceStepBook book = Begin();
			KingdomResidentDepartureOperation departure = Departure();
			departure.PreparedTick = Due + 100;
			Rehash(departure);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, departure, out book));
			string before = Wire(book);
			Assert.IsFalse(KingdomSubsidenceStepRules.TryCancel(book, departure.PreparedTick - 1,
				4, out KingdomSubsidenceStepBook refused));
			Assert.IsNull(refused);
			RefusesWire(book.With(book.Active.Copy(cancelRequested: true,
				cancelTick: departure.PreparedTick - 1, cancelToken: 4), book.Sequence));
			Assert.AreEqual(before, Wire(book));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryCancel(book, departure.PreparedTick, 4, out book));
			book = RoundTrip(book);
			Assert.AreEqual(departure.PreparedTick, book.Active.CancelTick);
			Assert.AreEqual(departure.PreparedTick, book.Active.PendingIdentity.PreparedTick);
		}

		[TestCase("resident")]
		[TestCase("body")]
		[TestCase("tick")]
		public void PendingTupleCannotDisagreeWithItsDepartureHash(string changed)
		{
			KingdomSubsidenceStepBook book = Begin();
			Assert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, Departure(), out book));
			string before = Wire(book);
			KingdomSubsidenceDepartureIdentity held = book.Active.PendingIdentity;
			KingdomSubsidenceDepartureIdentity corrupt = new KingdomSubsidenceDepartureIdentity(
				changed == "resident" ? held.ResidentId + 1 : held.ResidentId,
				changed == "body" ? "another-body" : held.BodyObjectId, held.ZoneId,
				changed == "tick" ? held.PreparedTick + 1 : held.PreparedTick);
			RefusesWire(book.With(book.Active.Copy(pendingIdentity: corrupt), book.Sequence));
			Assert.AreEqual(before, Wire(book));
		}

		[Test]
		public void DifferentZoneWithSameOperationIdCannotRepeatAnAssociation()
		{
			KingdomSubsidenceStepBook book = Begin();
			KingdomResidentDepartureOperation departure = Departure();
			Assert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, departure, out book));
			book = RoundTrip(book);
			string before = Wire(book);
			departure.ZoneId = "JoppaWorld.12.24.1.1.11";
			Assert.IsTrue(KingdomResidentDepartureRules.Valid(departure));
			Assert.AreEqual(book.Active.PendingDepartureId, departure.OperationId);
			Assert.IsFalse(KingdomSubsidenceStepRules.TryAssociate(book, departure,
				out KingdomSubsidenceStepBook refused));
			Assert.IsNull(refused);
			Assert.AreEqual(before, Wire(book));
		}

		[Test]
		public void OrphanTupleWithoutPendingIdCannotBeSerialized()
		{
			KingdomSubsidenceStepBook book = Begin();
			KingdomResidentDepartureOperation departure = Departure();
			KingdomSubsidenceDepartureIdentity orphan = new KingdomSubsidenceDepartureIdentity(
				departure.ResidentId, departure.BodyObjectId, departure.ZoneId, departure.PreparedTick);
			RefusesWire(book.With(book.Active.Copy(pendingIdentity: orphan), book.Sequence));
		}

		[Test]
		public void PendingIdWithoutItsTupleCannotBeSerialized()
		{
			KingdomSubsidenceStepBook book = Begin();
			Assert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, Departure(), out book));
			RefusesWire(book.With(Rebuild(book.Active, book.Active.PendingDepartureId, null,
				book.Active.StorageCapacity, book.Active.BindingSupport), book.Sequence));
		}

		[TestCase(0, "water")]
		[TestCase(512, "roof")]
		[TestCase(int.MaxValue, "water")]
		public void FrozenStorageAndBindingSurviveAssociationCreditAndRelease(int storage, string binding)
		{
			KingdomSubsidenceStepBook book = Begin(storage, binding);
			string id = book.Active.Id;
			KingdomResidentDepartureOperation departure = Departure();
			Assert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, departure, out book));
			book = RoundTrip(book);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryCredit(book, departure.OperationId, GrowthStage.City, out book));
			book = RoundTrip(book);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(book, departure.OperationId, out book));
			book = RoundTrip(book);
			Assert.AreEqual(id, book.Active.Id);
			Assert.AreEqual(storage, book.Active.StorageCapacity);
			Assert.AreEqual(binding, book.Active.BindingSupport);
			Assert.AreEqual("", book.Active.PendingDepartureId);
			Assert.IsNull(book.Active.PendingIdentity);
			Assert.AreEqual(1, book.Active.Completed);
		}

		[Test]
		public void ChangingFrozenInputsChangesStepIdentityAndCannotReuseOldId()
		{
			KingdomSubsidenceStepBook original = Begin(512, "water");
			Assert.AreNotEqual(original.Active.Id, Begin(513, "water").Active.Id);
			Assert.AreNotEqual(original.Active.Id, Begin(512, "roof").Active.Id);
			RefusesWire(original.With(Rebuild(original.Active, "", null, 513, "water"), original.Sequence));
			RefusesWire(original.With(Rebuild(original.Active, "", null, 512, "roof"), original.Sequence));
		}

		[TestCase(false)]
		[TestCase(true)]
		public void ReleasedAssociationRetainsMonotoneTimeForTheNextAttemptAndCancellation(bool credited)
		{
			KingdomSubsidenceStepBook book = Begin();
			KingdomResidentDepartureOperation departure = Departure();
			departure.PreparedTick = Due + 500;
			Rehash(departure);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, departure, out book));
			if (credited)
			{
				Assert.IsTrue(KingdomSubsidenceStepRules.TryCredit(book, departure.OperationId, GrowthStage.City, out book));
				Assert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(book, departure.OperationId, out book));
			}
			else Assert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRolledBack(book, departure.OperationId, out book));
			book = RoundTrip(book);
			Assert.IsNull(book.Active.PendingIdentity);
			Assert.AreEqual(Due + 500, book.Active.LastActivityTick);
			Assert.IsFalse(KingdomSubsidenceStepRules.TryCancel(book, Due + 100, 2, out KingdomSubsidenceStepBook _));
			RefusesWire(book.With(book.Active.Copy(cancelRequested: true, cancelTick: Due + 100,
				cancelToken: 2, phase: KingdomSubsidenceStepPhase.Settling), book.Sequence));
			departure.PreparedTick = Due + 100; departure.ResidentId++;
			Rehash(departure);
			Assert.IsFalse(KingdomSubsidenceStepRules.TryAssociate(book, departure, out KingdomSubsidenceStepBook _));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryCancel(book, Due + 500, 2, out book));
			Assert.AreEqual(Due + 500, RoundTrip(book).Active.CancelTick);
		}

		[Test]
		public void OverdueBeginRetainsItsActualObservationTick()
		{
			Assert.IsTrue(KingdomSubsidenceStepRules.TryBegin(Admitted(), Anchor, Due + 500,
				GrowthStage.City, 5, out KingdomSubsidenceStepBook book, 512, "roof"));
			book = RoundTrip(book);
			Assert.AreEqual(Due + 500, book.Active.LastActivityTick);
			KingdomResidentDepartureOperation departure = Departure();
			departure.PreparedTick = Due + 499; Rehash(departure);
			Assert.IsFalse(KingdomSubsidenceStepRules.TryAssociate(book, departure, out KingdomSubsidenceStepBook _));
			Assert.IsFalse(KingdomSubsidenceStepRules.TryCancel(book, Due + 499, 2, out KingdomSubsidenceStepBook _));
			departure.PreparedTick = Due + 500; Rehash(departure);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, departure, out book));
			Assert.AreEqual(Due + 500, RoundTrip(book).Active.PendingIdentity.PreparedTick);
		}

		[TestCase(-1, "water")]
		[TestCase(0, null)]
		[TestCase(0, "")]
		[TestCase(0, "food")]
		[TestCase(0, "Water")]
		public void InvalidFrozenInputsCannotBeginOrBecomeAValidWire(int storage, string binding)
		{
			KingdomSubsidenceStepBook admitted = Admitted();
			string before = Wire(admitted);
			Assert.IsFalse(KingdomSubsidenceStepRules.TryBegin(admitted, Anchor, Due, GrowthStage.City,
				5, out KingdomSubsidenceStepBook refused, storage, binding));
			Assert.IsNull(refused);
			Assert.AreEqual(before, Wire(admitted));
			KingdomSubsidenceStepBook begun = Begin();
			RefusesWire(begun.With(Rebuild(begun.Active, "", null, storage, binding), begun.Sequence));
		}
	}
}
#endif
