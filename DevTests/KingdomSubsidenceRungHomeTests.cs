#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	public sealed partial class KingdomSubsidenceRoofCarrierTests
	{
		private static string HomeWire(string zone = RungFixture.Zone, string plot = "plot-id")
		{
			ClassicAssert.IsTrue(KingdomResidenceRules.TryEncode(new KingdomResidence(zone, plot, "",
				null, new string[0], new string[0], new string[0]), out string wire));
			return wire;
		}

		[TestCase(false)] [TestCase(true)]
		public void HomeClaimPublishesAbsentOwnersOriginalLossAndReplayKeepsIt(bool standing)
		{
			var city = Carrier(standing);
			city.ResidentResidences[0] = HomeWire();
			city.ResidentBoundZoneIds[0] = "away";
			city.SubsidenceModel = Wire(Parent("roof-intent", standing, homeAuthority: true));
			ClassicAssert.IsTrue(city.TryCaptureSubsidenceRoof(11, out var prior));
			var before = new Snapshot(city);
			long reached = standing ? RungFixture.Due + 10 : RungFixture.Due;
			long warned = standing ? RungFixture.Due + 20 : KingdomBrinkRules.Unwarned;
			ClassicAssert.IsTrue(city.TryPublishSubsidenceRoof(city.SubsidenceModel, prior, true, reached, warned, out var after));
			before.Unchanged(city, "ResidentRoofStanding", "ResidentRoofTicks", "ResidentRoofWarnedTicks");
			ClassicAssert.AreSame(prior.Residences, after.Residences);
			ClassicAssert.AreEqual(prior.Residence, after.Residence);
			ClassicAssert.IsTrue(city.TryPublishSubsidenceRoof(city.SubsidenceModel, after, true, reached, warned, out _));
			Refuses(city, city.SubsidenceModel, after, true, reached + 1, warned);
			Refuses(city, city.SubsidenceModel, after, true, reached, warned + 1);
		}

		[TestCase("home")] [TestCase("plot")] [TestCase("root")] [TestCase("unknown")]
		[TestCase("homeless")] [TestCase("malformed")] [TestCase("blank-zone")]
		[TestCase("standing")] [TestCase("replacement-list")] [TestCase("changed-after-capture")]
		public void HomeClaimCannotPublishAgainstChangedOwnershipOrStaleCarriers(string change)
		{
			var city = Carrier();
			city.ResidentResidences[0] = HomeWire();
			city.ResidentBoundZoneIds[0] = "away";
			city.SubsidenceModel = Wire(Parent("roof-intent", homeAuthority: true));
			ClassicAssert.IsTrue(city.TryCaptureSubsidenceRoof(11, out var original));
			switch (change)
			{
				case "home": city.ResidentResidences[0] = HomeWire("other"); break;
				case "plot": city.ResidentResidences[0] = HomeWire(plot: "other"); break;
				case "root": city.ResidentHomeWorkIds[0]++; break;
				case "unknown": city.ResidentResidences[0] = ""; break;
				case "homeless": city.ResidentResidences[0] = HomeWire("", ""); break;
				case "malformed": city.ResidentResidences[0] = "broken"; break;
				case "blank-zone": city.ResidentBoundZoneIds[0] = ""; break;
				case "standing": city.ResidentStandings[0] = (int)KingdomResidentStanding.Expedition; break;
				case "replacement-list": city.ResidentResidences = new List<string>(city.ResidentResidences); break;
				default: city.ResidentResidences[0] = HomeWire("other"); break;
			}
			var prior = original;
			if (change != "replacement-list" && change != "changed-after-capture" && change != "malformed")
				ClassicAssert.IsTrue(city.TryCaptureSubsidenceRoof(11, out prior));
			Refuses(city, city.SubsidenceModel, prior, true, RungFixture.Due, KingdomBrinkRules.Unwarned);
		}

		[Test]
		public void LegacyClaimStillRequiresPhysicalHomeMapEvenWithCanonicalHome()
		{
			var city = Bound("roof-intent");
			city.ResidentResidences[0] = HomeWire(); city.ResidentBoundZoneIds[0] = "away";
			ClassicAssert.IsTrue(city.TryCaptureSubsidenceRoof(11, out var row));
			Refuses(city, city.SubsidenceModel, row, true, RungFixture.Due, KingdomBrinkRules.Unwarned);
		}
	}

	public sealed class KingdomSubsidenceRungHomeTests
	{
		[Test]
		public void MixedOwnershipClaimsRoundTripWithoutReinterpretingLegacyRecipients()
		{
			var home = new KingdomSubsidenceRungRoof(11, "body", false, 0, 0,
				KingdomSubsidenceEffectPhase.Prepared, RungFixture.Zone);
			var plan = RungFixture.Plan(RungFixture.Work(roofs: new[] { home, RungFixture.Roof(12) }));
			string wire = RungFixture.Wire(plan);
			StringAssert.StartsWith("sr3:", wire);
			var decoded = RungFixture.RoundTrip(plan);
			ClassicAssert.AreEqual(RungFixture.Zone, decoded.Works[0].Roofs[0].HomeZoneId);
			ClassicAssert.IsNull(decoded.Works[0].Roofs[1].HomeZoneId);
			decoded = RungFixture.ProveWear(decoded, 0);
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.TryArmRoof(decoded, 0, 0, out decoded));
			ClassicAssert.AreEqual(RungFixture.Zone, RungFixture.RoundTrip(decoded).Works[0].Roofs[0].HomeZoneId);
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.TryProveRoof(decoded, 0, 0,
				true, true, RungFixture.Due, KingdomBrinkRules.Unwarned, out decoded));
			ClassicAssert.AreEqual(RungFixture.Zone, RungFixture.RoundTrip(decoded).Works[0].Roofs[0].HomeZoneId);
			StringAssert.StartsWith("sr2:", RungFixture.Wire(RungFixture.Plan(RungFixture.Work(roofs: new[] { RungFixture.Roof(11) }))));
			ClassicAssert.IsFalse(KingdomSubsidenceRungCodec.TryDecode("sr2:" + wire.Substring(4), out _));
			byte[] bytes = Convert.FromBase64String(wire.Substring(4));
			for (int i = 0; i < bytes.Length; i++)
				ClassicAssert.IsFalse(KingdomSubsidenceRungCodec.TryDecode("sr3:" + Convert.ToBase64String(bytes, 0, i), out _), "cut " + i);
		}

		[TestCase("")] [TestCase("other")]
		public void ForeignOrEmptyFrozenHomeIsNotAValidClaim(string homeZone)
		{
			var roof = new KingdomSubsidenceRungRoof(11, "body", false, 0, 0,
				KingdomSubsidenceEffectPhase.Prepared, homeZone);
			ClassicAssert.IsFalse(KingdomSubsidenceRungCodec.TryEncode(RungFixture.Plan(RungFixture.Work(roofs: new[] { roof })), out _));
		}

		[TestCase(false)] [TestCase(true)]
		public void ObservedMissingHomePreservesExistingLossClockWithoutStartingWarning(bool existing)
		{
			var brink = existing ? new KingdomBrinkWindow(true, 42, 58) : KingdomBrinkWindow.None;
			var row = new KingdomResidentRow(7, "Tes", 0, 0, 1, 101, 0, 0, KingdomDayShape.Hearth,
				KingdomResidentStanding.Resident, KingdomStandingCause.None, "away", brink, KingdomBrinkWindow.None, null, 0);
			var lost = KingdomResidenceRules.ObserveHomeLoss(row, 200);
			ClassicAssert.IsTrue(lost.RoofBrink.Stands);
			ClassicAssert.AreEqual(existing ? 42 : 200, lost.RoofBrink.ReachedTick);
			ClassicAssert.AreEqual(existing ? 58 : KingdomBrinkRules.Unwarned, lost.RoofBrink.WarnedTick);
			var replay = KingdomResidenceRules.ObserveHomeLoss(lost, 900);
			ClassicAssert.AreEqual(lost.RoofBrink.ReachedTick, replay.RoofBrink.ReachedTick);
			ClassicAssert.AreEqual(lost.RoofBrink.WarnedTick, replay.RoofBrink.WarnedTick);
			ClassicAssert.AreEqual(row.HomeWorkId, replay.HomeWorkId);
			ClassicAssert.AreEqual(row.BoundZoneId, replay.BoundZoneId);
		}
	}
}
#endif
