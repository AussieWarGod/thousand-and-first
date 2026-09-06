#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	public class KingdomSubsidenceStorageMigrationTests
	{
		[Test]
		public void FreshBookHasExplicitVersionedUnadmittedStorage()
		{
			KingdomCityBook city = new KingdomCityBook();
			Assert.AreEqual(4, city.SchemaVersion);
			Assert.AreEqual(KingdomSubsidenceStepCodec.FreshWire, city.SubsidenceModel);
			Assert.IsTrue(city.HasValidSubsidenceStorage());
		}

		[TestCase(1)]
		[TestCase(2)]
		[TestCase(3)]
		public void NamedLegacyReadStagesAdmissionWithoutAdvancingAnyClock(int schema)
		{
			KingdomCityBook city = new KingdomCityBook();
			city.ReadNamedState(() =>
			{
				Assert.AreEqual(0, city.SchemaVersion);
				Assert.IsNull(city.SubsidenceModel);
				city.SchemaVersion = schema;
				city.ProcessedThroughTick = 777L;
			});
			Assert.AreEqual(4, city.SchemaVersion);
			Assert.AreEqual(KingdomSubsidenceStepCodec.LegacyWire, city.SubsidenceModel);
			Assert.AreEqual(777L, city.ProcessedThroughTick);
			Assert.IsTrue(city.HasValidSubsidenceStorage());
		}

		[TestCase("ss1:new")]
		[TestCase("ss1:legacy")]
		public void CurrentNamedReadPreservesExplicitRecord(string wire)
		{
			KingdomCityBook city = new KingdomCityBook();
			city.ReadNamedState(() => { city.SchemaVersion = 4; city.SubsidenceModel = wire; });
			Assert.AreEqual(wire, city.SubsidenceModel);
			Assert.IsFalse(city.SubsidenceReadFailed);
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("broken")]
		[TestCase("ss2:new")]
		public void CurrentMissingOrMalformedReadIsPreservedAndRefused(string wire)
		{
			KingdomCityBook city = new KingdomCityBook();
			Assert.Throws<InvalidDataException>(() => city.ReadNamedState(() =>
			{
				city.SchemaVersion = 4;
				city.SubsidenceModel = wire;
			}));
			city.Normalize();
			Assert.AreEqual(wire, city.SubsidenceModel);
			Assert.IsTrue(city.SubsidenceReadFailed);
			Assert.IsFalse(city.HasValidSubsidenceStorage());
		}

		[TestCase(-1)]
		[TestCase(0)]
		[TestCase(5)]
		public void MissingOrUnknownNamedSchemaCannotAuthorizeLegacyAdmission(int schema)
		{
			KingdomCityBook city = new KingdomCityBook();
			Assert.Throws<InvalidDataException>(() =>
				city.ReadNamedState(() => city.SchemaVersion = schema));
			Assert.IsNull(city.SubsidenceModel);
			Assert.IsTrue(city.SubsidenceReadFailed);
		}

		[TestCase(1, "ss1:new")]
		[TestCase(2, "ss1:legacy")]
		[TestCase(3, "")]
		public void OldSchemaCannotAdoptContradictoryNewField(int schema, string wire)
		{
			KingdomCityBook city = new KingdomCityBook();
			Assert.Throws<InvalidDataException>(() => city.ReadNamedState(() =>
			{
				city.SchemaVersion = schema;
				city.SubsidenceModel = wire;
			}));
			Assert.AreEqual(wire, city.SubsidenceModel);
			Assert.IsTrue(city.SubsidenceReadFailed);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void NamedReadCallbackFailureCannotBecomeHealthyAfterSeatTransfer(bool afterFields)
		{
			KingdomSettlement original = new KingdomSettlement { LastSubsidenceTick = 1234L };
			KingdomCityBook city = original.City;
			Assert.Throws<InvalidOperationException>(() => city.ReadNamedState(() =>
			{
				if (afterFields) { city.SchemaVersion = 4; city.SubsidenceModel = "ss1:new"; }
				throw new InvalidOperationException("interrupted named reader");
			}));
			KingdomSettlement captured = new KingdomSettlement();
			captured.ReadFrom(original);
			KingdomSettlement restored = new KingdomSettlement();
			captured.WriteTo(restored);
			Assert.AreSame(city, restored.City);
			Assert.AreEqual(1234L, restored.LastSubsidenceTick);
			Assert.IsTrue(restored.City.SubsidenceReadFailed);
			Assert.IsFalse(restored.City.HasValidSubsidenceStorage());
		}

		[Test]
		public void LegacyPendingAdmissionAndInheritedCheckpointSurviveRepeatedSeatSwaps()
		{
			KingdomSettlement current = new KingdomSettlement { LastSubsidenceTick = 9876L };
			current.City.ReadNamedState(() => current.City.SchemaVersion = 3);
			KingdomCityBook exact = current.City;
			for (int i = 0; i < 3; i++)
			{
				KingdomSettlement captured = new KingdomSettlement();
				captured.ReadFrom(current);
				KingdomSettlement restored = new KingdomSettlement();
				captured.WriteTo(restored);
				Assert.AreSame(exact, restored.City);
				Assert.AreEqual(9876L, restored.LastSubsidenceTick);
				Assert.AreEqual("ss1:legacy", restored.City.SubsidenceModel);
				current = restored;
			}
		}

		[TestCase(false)]
		[TestCase(true)]
		public void PendingAndQuarantinedWireSurviveNamedReadAndSeatTransfer(bool faulted)
		{
			string wire = PendingWire(faulted);
			KingdomSettlement source = new KingdomSettlement { LastSubsidenceTick = 9876L };
			source.City.ReadNamedState(() => { source.City.SchemaVersion = 4; source.City.SubsidenceModel = wire; });
			KingdomSettlement captured = new KingdomSettlement();
			captured.ReadFrom(source);
			KingdomSettlement restored = new KingdomSettlement();
			captured.WriteTo(restored);
			Assert.AreSame(source.City, restored.City);
			Assert.AreEqual(wire, restored.City.SubsidenceModel);
			Assert.AreEqual(9876L, restored.LastSubsidenceTick);
			Assert.IsTrue(restored.City.HasValidSubsidenceStorage());
		}

		internal static string PendingWire(bool faulted)
		{
			Assert.IsTrue(KingdomIdentityRules.TryMintRealm(new string('1', 32),
				out string realm, out KingdomIdentityFault fault));
			Assert.IsTrue(KingdomIdentityRules.TryMintSettlement(realm, new string('2', 32),
				out string settlement, out fault));
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryDecode("ss1:legacy", out KingdomSubsidenceStepBook legacy));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryAdmit(legacy, realm, settlement,
				out KingdomSubsidenceStepBook admitted));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryBegin(admitted, 9876L,
				9876L + KingdomSubsidenceStepRules.StepTicks, GrowthStage.City, 5,
				out KingdomSubsidenceStepBook begun, 0, "water"));
			KingdomResidentDepartureOperation departure = new KingdomResidentDepartureOperation
			{
				Version = KingdomResidentDepartureOperation.CurrentVersion,
				Phase = (int)KingdomResidentDeparturePhase.Prepared, Revision = 1,
				RealmId = realm, SettlementId = settlement, ResidentId = 1,
				BodyObjectId = "subsidence-migration-body", ZoneId = "JoppaWorld.12.24.1.1.10",
				ResidentName = "Subsidence migration resident", PreparedTick = begun.Active.DueTick
			};
			departure.OperationId = KingdomResidentDepartureRules.Id(realm, settlement,
				departure.ResidentId, departure.BodyObjectId, departure.PreparedTick);
			Assert.IsTrue(KingdomResidentDepartureRules.Valid(departure));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(begun, departure,
				out KingdomSubsidenceStepBook pending));
			if (faulted) pending = pending.With(pending.Active.Copy(
				phase: KingdomSubsidenceStepPhase.Quarantined, fault: "storage-fixture-cut"), pending.Sequence);
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(pending, out string wire));
			return wire;
		}

	}
}
#endif
