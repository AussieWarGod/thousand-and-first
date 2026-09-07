#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	public sealed class KingdomHostedDepartureStateTests
	{
		[Test]
		public void CodecRoundTripsExactSettledProjection()
		{
			KingdomHostedDepartureState state = Settled();
			string encoded = KingdomHostedDepartureCodec.Encode(state);
			ClassicAssert.IsNotEmpty(encoded);
			ClassicAssert.IsTrue(KingdomHostedDepartureCodec.TryDecode(encoded,
				out KingdomHostedDepartureState decoded));
			ClassicAssert.AreEqual(encoded, KingdomHostedDepartureCodec.Encode(decoded));
			ClassicAssert.AreEqual(8, decoded.Roof);
			ClassicAssert.AreEqual(2, decoded.Luxury);
			ClassicAssert.AreEqual(14, decoded.Food);
		}

		[Test]
		public void PendingStateMustCarryNoRevisionOrBenefit()
		{
			KingdomHostedDepartureState state = Settled();
			state.Phase = KingdomHostedDeparturePhase.Pending;
			ClassicAssert.IsFalse(state.Valid());
			state.ReceiptRevision = ""; state.Roof = 0; state.Luxury = 0; state.Food = 0;
			ClassicAssert.IsTrue(state.Valid());
		}

		[Test]
		public void IdentityMatchRejectsEveryAuthorityDrift()
		{
			KingdomHostedDepartureState state = Settled();
			KingdomHostedArcologyAuthority authority = Authority();
			ClassicAssert.IsTrue(KingdomHostedDepartureRules.Matches(state, 0, authority,
				KingdomHostedArcologyTopology.WardLotKey));
			authority.CarrierId = "other";
			ClassicAssert.IsFalse(KingdomHostedDepartureRules.Matches(state, 0, authority,
				KingdomHostedArcologyTopology.WardLotKey));
		}

		[Test]
		public void ProjectionExcludesExteriorAndExactInteriorSource()
		{
			KingdomHostedDepartureState state = Settled();
			ClassicAssert.AreEqual(0, KingdomHostedDepartureRules.LuxuryFor(
				state, "city", "outside"));
			ClassicAssert.AreEqual(0, KingdomHostedDepartureRules.LuxuryFor(
				state, "city", "inside"));
			ClassicAssert.AreEqual(2, KingdomHostedDepartureRules.LuxuryFor(
				state, "city", "other-zone"));
			ClassicAssert.AreEqual(0, KingdomHostedDepartureRules.BindingFor(state,
				KingdomCatalogueRules.SupportRoof, "city", "inside"));
			ClassicAssert.AreEqual(8, KingdomHostedDepartureRules.BindingFor(state,
				KingdomCatalogueRules.SupportRoof, "city", "other-zone"));
		}

		[Test]
		public void FixedSlotKeyMustMatchAuthoritySlotAndLot()
		{
			KingdomHostedDepartureState state = Settled();
			ClassicAssert.IsTrue(KingdomHostedDepartureRules.SlotKeyMatches(
				"r_TAF_HostedDepartureV1:0:ward", state));
			ClassicAssert.IsFalse(KingdomHostedDepartureRules.SlotKeyMatches(
				"r_TAF_HostedDepartureV1:1:ward", state));
			ClassicAssert.IsFalse(KingdomHostedDepartureRules.SlotKeyMatches(
				"r_TAF_HostedDepartureV1:0:terrace", state));
			state.LotKey = "foreign";
			ClassicAssert.IsFalse(KingdomHostedDepartureRules.SlotKeyMatches(
				"r_TAF_HostedDepartureV1:0", state));
		}

		private static KingdomHostedArcologyAuthority Authority()
		{
			return new KingdomHostedArcologyAuthority {
				Phase = KingdomHostedAuthorityPhase.Active, RealmId = "realm",
				SettlementId = "city", ZoneId = "outside", CarrierId = "shell",
				ConstructionJobId = "job" };
		}

		private static KingdomHostedDepartureState Settled()
		{
			return new KingdomHostedDepartureState {
				Phase = KingdomHostedDeparturePhase.Settled, AuthoritySlot = 0,
				RealmId = "realm", SettlementId = "city", ExteriorZoneId = "outside",
				CarrierId = "shell", AuthorityJobId = "job",
				LotKey = KingdomHostedArcologyTopology.WardLotKey,
				InteriorZoneId = "inside", ReceiptRevision = "revision", ObservedTick = 10,
				Roof = 8, Luxury = 2, Food = 14, FreshWater = true,
				Band = ReachBand.City, Headed = true };
		}
	}
}
#endif
