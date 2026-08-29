#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst.Api;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomExternalOwnershipRulesTests
	{
		private static KingdomExternalOwnershipObservation Observation(string Owner = null)
		{
			return new KingdomExternalOwnershipObservation
			{
				ProviderId = "Hearthpyre",
				ProviderVersion = "2.2.3",
				OwnerGuid = Owner ?? "11111111-2222-3333-4444-555555555555",
				SectorGuid = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
				Evidence = "settlement+sector",
				ZoneId = "JoppaWorld.10.11.1.2.10",
				ParasangId = "JoppaWorld.10.11"
			};
		}

		[Test]
		public void NoneAndBindRoundTripCanonically()
		{
			KingdomExternalOwnershipBinding none = KingdomExternalOwnershipRules.None();
			string encodedNone = KingdomExternalOwnershipRules.Encode(none);
			Assert.IsTrue(KingdomExternalOwnershipRules.TryDecode(encodedNone, out var readNone));
			Assert.AreEqual(KingdomExternalOwnershipMode.None, readNone.Mode);
			Assert.IsNull(readNone.Observation);

			KingdomExternalOwnershipBinding bind =
				KingdomExternalOwnershipRules.Bind(Observation());
			string encodedBind = KingdomExternalOwnershipRules.Encode(bind);
			Assert.IsTrue(KingdomExternalOwnershipRules.TryDecode(encodedBind, out var readBind));
			Assert.AreEqual(encodedBind, KingdomExternalOwnershipRules.Encode(readBind));
			Assert.IsTrue(KingdomExternalOwnershipRules.SameObservation(
				bind.Observation, readBind.Observation));
		}

		[TestCase("11111111-2222-3333-4444-555555555555", true)]
		[TestCase("11111111-2222-3333-4444-55555555555A", false)]
		[TestCase("{11111111-2222-3333-4444-555555555555}", false)]
		[TestCase("00000000-0000-0000-0000-000000000000", false)]
		[TestCase("", false)]
		public void GuidEvidenceIsLowercaseNonEmptyDFormat(string value, bool valid)
		{
			Assert.AreEqual(valid, KingdomExternalOwnershipRules.ValidGuid(value));
		}

		[Test]
		public void CodecRejectsTamperNonCanonicalAndPartialRows()
		{
			string encoded = KingdomExternalOwnershipRules.Encode(
				KingdomExternalOwnershipRules.Bind(Observation()));
			Assert.IsFalse(KingdomExternalOwnershipRules.TryDecode(encoded + ".", out var extra));
			Assert.IsFalse(KingdomExternalOwnershipRules.TryDecode(
				encoded.Substring(0, encoded.Length - 1), out var truncated));
			Assert.IsFalse(KingdomExternalOwnershipRules.TryDecode("dGFm.!!!", out var invalid));
			Assert.IsFalse(KingdomExternalOwnershipRules.TryDecode(
				new string('a', KingdomExternalOwnershipRules.MaximumEncodedLength + 1),
				out var oversized));
		}

		[Test]
		public void BindingVerdictRequiresExactProviderOwnerSectorAndGround()
		{
			KingdomExternalOwnershipBinding binding =
				KingdomExternalOwnershipRules.Bind(Observation());
			KingdomExternalOwnershipReading exact = new KingdomExternalOwnershipReading
			{
				State = KingdomExternalOwnershipState.Owned,
				Observation = Observation()
			};
			Assert.AreEqual(KingdomExternalBindingVerdict.Exact,
				KingdomExternalOwnershipRules.Judge(binding, exact));
			exact.Observation.SectorGuid = "11111111-1111-1111-1111-111111111111";
			Assert.AreEqual(KingdomExternalBindingVerdict.Diverged,
				KingdomExternalOwnershipRules.Judge(binding, exact));
			exact.State = KingdomExternalOwnershipState.ProviderFailed;
			Assert.AreEqual(KingdomExternalBindingVerdict.ProviderUnavailable,
				KingdomExternalOwnershipRules.Judge(binding, exact));
		}

		[Test]
		public void ExplicitNoneDivergesWhenAnOwnerAppears()
		{
			KingdomExternalOwnershipBinding binding = KingdomExternalOwnershipRules.None();
			KingdomExternalOwnershipReading open = new KingdomExternalOwnershipReading
			{
				State = KingdomExternalOwnershipState.Unowned
			};
			Assert.AreEqual(KingdomExternalBindingVerdict.Open,
				KingdomExternalOwnershipRules.Judge(binding, open));
			open.State = KingdomExternalOwnershipState.Owned;
			open.Observation = Observation();
			Assert.AreEqual(KingdomExternalBindingVerdict.Diverged,
				KingdomExternalOwnershipRules.Judge(binding, open));
		}

		[Test]
		public void ParasangOnlyEvidenceTreatsNullAndEmptySectorAsCanonicalAbsence()
		{
			KingdomExternalOwnershipObservation current = Observation();
			current.SectorGuid = null;
			current.Evidence = "settlement";
			string encoded = KingdomExternalOwnershipRules.Encode(
				KingdomExternalOwnershipRules.Bind(current));
			Assert.IsTrue(KingdomExternalOwnershipRules.TryDecode(encoded, out var binding));
			Assert.AreEqual("", binding.Observation.SectorGuid);
			Assert.AreEqual(KingdomExternalBindingVerdict.Exact,
				KingdomExternalOwnershipRules.Judge(binding,
					new KingdomExternalOwnershipReading
					{
						State = KingdomExternalOwnershipState.Owned,
						Observation = current
					}));
		}

		[TestCase(null, null, false, true)]
		[TestCase(null, null, true, false)]
		[TestCase("authority", null, true, true)]
		[TestCase(null, "binding", true, true)]
		[TestCase("authority", "binding", true, true)]
		[TestCase("foreign", "binding", true, false)]
		[TestCase("authority", "foreign", true, false)]
		public void ReceiptPairCasAdmitsOnlyAbsentOrExactHalves(string currentAuthority,
			string currentBinding, bool requireEvidence, bool valid)
		{
			Assert.AreEqual(valid, KingdomExternalOwnershipRules.PairAbsentOrExact(
				currentAuthority, currentBinding, "authority", "binding", requireEvidence));
		}
	}
}
#endif
