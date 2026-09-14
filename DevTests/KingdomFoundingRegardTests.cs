#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	public class KingdomFoundingRegardTests
	{
		private static Dictionary<string, int> Empty() => new Dictionary<string, int>(StringComparer.Ordinal);
		private static List<KeyValuePair<string, int>> Frozen() => new List<KeyValuePair<string, int>>
		{
			new KeyValuePair<string, int>("Farmers", 400),
			new KeyValuePair<string, int>("Flowers", 0),
			new KeyValuePair<string, int>("Snapjaws", -475)
		};

		[TestCase(1)]
		[TestCase(2)]
		public void FrozenVersionRoundTripsWithoutChangingReputationOrOrder(int version)
		{
			var rows = Frozen();
			ClassicAssert.IsTrue(KingdomFoundingRegardRules.TryEncode(version, rows, out var text));
			ClassicAssert.IsTrue(KingdomFoundingRegardRules.TryDecode(text, out var decodedVersion, out var decoded));
			ClassicAssert.AreEqual(version, decodedVersion);
			CollectionAssert.AreEqual(rows, decoded);
		}

		[Test]
		public void FreshBaselineIncludesNeutralAndHostileValuesWithoutSharingMutableRoots()
		{
			var before = Empty(); var observed = Empty();
			ClassicAssert.IsTrue(KingdomFoundingRegardRules.TryPreparePublication(2, Frozen(), before,
				Empty(), Empty(), observed, out var after, out var nextObserved));
			ClassicAssert.AreEqual(3, after.Count);
			ClassicAssert.AreEqual(-475, after["Snapjaws"]);
			ClassicAssert.IsTrue(after.ContainsKey("Flowers"));
			ClassicAssert.AreEqual(0, before.Count); ClassicAssert.AreEqual(0, observed.Count);
			after["Farmers"] += 100;
			ClassicAssert.AreEqual(400, nextObserved["Farmers"]);
		}

		[TestCase(0, 0)] [TestCase(1, 0)] [TestCase(0, 1)]
		[TestCase(2, 1)] [TestCase(1, 3)] [TestCase(3, 3)]
		public void InterruptedPublicationCompletesOnlyFrozenSubsets(int standingCount, int observedCount)
		{
			var frozen = Frozen(); var standings = Empty(); var observations = Empty();
			for (int i = 0; i < standingCount; i++) standings.Add(frozen[i].Key, frozen[i].Value);
			for (int i = 0; i < observedCount; i++) observations.Add(frozen[i].Key, frozen[i].Value);
			ClassicAssert.IsTrue(KingdomFoundingRegardRules.TryPreparePublication(2, frozen, standings,
				Empty(), Empty(), observations, out var after, out var observed));
			ClassicAssert.AreEqual(3, after.Count); ClassicAssert.AreEqual(3, observed.Count);
			ClassicAssert.AreEqual(standingCount, standings.Count);
			ClassicAssert.AreEqual(observedCount, observations.Count);
		}

		[TestCase("standing")] [TestCase("observation")] [TestCase("policy")] [TestCase("carry")]
		public void ChangedOrForeignAuthorityRefusesWithoutMutatingAnyInput(string target)
		{
			var standings = Empty(); var observations = Empty(); var policy = Empty(); var carry = Empty();
			var changed = target == "standing" ? standings : target == "observation" ? observations
				: target == "policy" ? policy : carry;
			changed["Farmers"] = 401;
			ClassicAssert.IsFalse(KingdomFoundingRegardRules.TryPreparePublication(2, Frozen(), standings,
				policy, carry, observations, out var after, out var nextObserved));
			ClassicAssert.IsNull(after); ClassicAssert.IsNull(nextObserved);
			ClassicAssert.AreEqual(401, changed["Farmers"]);
			changed.Clear(); changed["Foreign"] = 0;
			ClassicAssert.IsFalse(KingdomFoundingRegardRules.TryPreparePublication(2, Frozen(), standings,
				policy, carry, observations, out after, out nextObserved));
			ClassicAssert.IsTrue(changed.ContainsKey("Foreign"));
		}

		[Test]
		public void LegacyFrozenTargetsRemainObservationOnlyAndCannotOverwriteExistingCity()
		{
			ClassicAssert.IsTrue(KingdomFoundingRegardRules.TryPreparePublication(1, Frozen(), Empty(),
				Empty(), Empty(), Empty(), out var after, out var observed));
			ClassicAssert.AreEqual(0, after.Count); ClassicAssert.AreEqual(3, observed.Count);
			after["Farmers"] = 400;
			ClassicAssert.IsFalse(KingdomFoundingRegardRules.TryPreparePublication(1, Frozen(), after,
				Empty(), Empty(), observed, out _, out _));
			ClassicAssert.AreEqual(400, after["Farmers"]);
		}

		[Test]
		public void FrozenBaselineSurvivesInputChangesAndLaterCityEffectsAreNotReset()
		{
			var personal = Frozen();
			ClassicAssert.IsTrue(KingdomFoundingRegardRules.TryEncode(2, personal, out var saved));
			personal[0] = new KeyValuePair<string, int>("Farmers", -600);
			ClassicAssert.IsTrue(KingdomFoundingRegardRules.TryDecode(saved, out var version, out var frozen));
			ClassicAssert.IsTrue(KingdomFoundingRegardRules.TryPreparePublication(version, frozen, Empty(),
				Empty(), Empty(), Empty(), out var after, out var observed));
			ClassicAssert.AreEqual(400, after["Farmers"]);
			ClassicAssert.IsTrue(KingdomStandingRules.TrySpillover(after["Farmers"], 0, 400, 500,
				GrowthStage.Camp, out var standing, out var remainder));
			after["Farmers"] = standing + 50;
			ClassicAssert.IsFalse(KingdomFoundingRegardRules.TryPreparePublication(version, frozen, after,
				Empty(), Empty(), observed, out _, out _));
			ClassicAssert.AreEqual(standing + 50, after["Farmers"]);
		}

		[TestCase(null)] [TestCase("")] [TestCase("v3")] [TestCase("v2;")]
		[TestCase("v2;Rg==:+1")] [TestCase("v2;Rg==:01")] [TestCase("v2;Rg==:2147483648")]
		[TestCase("v2;Rg==:1;Rg==:1")] [TestCase("v2;/w==:0")] [TestCase("v2;Kg==:0")]
		[TestCase("v2;UGxheWVy:0")] [TestCase("v2;Rw==:0;Rg==:0")]
		public void TornOrNoncanonicalSnapshotsRefuse(string encoded)
		{
			ClassicAssert.IsFalse(KingdomFoundingRegardRules.TryDecode(encoded, out var version, out var rows));
			ClassicAssert.AreEqual(0, version); ClassicAssert.IsNull(rows);
		}

		[Test]
		public void SnapshotCapacityIsExactAndNeverSilentlyTruncated()
		{
			var rows = new List<KeyValuePair<string, int>>();
			for (int i = 0; i < KingdomStandingRules.MaxRelationships; i++)
				rows.Add(new KeyValuePair<string, int>("Faction" + i.ToString("D4"), i));
			ClassicAssert.IsTrue(KingdomFoundingRegardRules.TryEncode(2, rows, out var encoded));
			ClassicAssert.IsTrue(KingdomFoundingRegardRules.TryDecode(encoded, out _, out var decoded));
			ClassicAssert.AreEqual(rows.Count, decoded.Count);
			rows.Add(new KeyValuePair<string, int>("Overflow", 0));
			ClassicAssert.IsFalse(KingdomFoundingRegardRules.TryEncode(2, rows, out _));
		}
	}
}
#endif
