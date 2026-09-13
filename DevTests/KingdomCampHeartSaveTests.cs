#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ThousandAndFirst.Harness;
using Codec = ThousandAndFirst.Harness.KingdomCampHeartSaveSnapshotCodec;
using Unit = ThousandAndFirst.Harness.KingdomCampHeartNativeCensus.Unit;

namespace ThousandAndFirst.Tests
{
	public class KingdomCampHeartSaveTests
	{
		[Test]
		public void CampLoadHasOwnPreactivationAndPopupLifetime()
		{
			string routes = TestMain.ReadRepositoryText("Harness/KingdomScenarioLoadWitness.cs");
			int camp = routes.IndexOf("KingdomScenarioLoadEntry.CampSnapshot != null", StringComparison.Ordinal);
			int generic = routes.IndexOf("KingdomScenarioSaveSnapshot snapshot", StringComparison.Ordinal);
			Assert.That(camp, Is.GreaterThan(-1));
			Assert.That(camp, Is.LessThan(generic));
			Assert.That(routes.Substring(camp, generic - camp), Does.Contain("KingdomCampHeartLoad.BeforeActivation()"));
			string load = TestMain.ReadRepositoryText("Harness/KingdomCampHeartLoad.cs");
			Assert.That(load, Does.Contain("ReferenceEquals(WitnessedGame, Game)"));
			Assert.That(load, Does.Contain("OwnsPopups = true"));
			Assert.That(load, Does.Contain("Popup.Suppress = PriorPopup"));
			Assert.That(TestMain.ReadRepositoryText("Harness/KingdomScenarioLoadEntry.cs"),
				Does.Contain("!KingdomCampHeartLoad.OwnsPopups"));
		}

		private static KingdomCampHeartSaveSnapshot Sample(string game = "01234567-89ab-cdef-0123-456789abcdef",
			string heart = "heart", string store = "store", string digest = null, int x = 40, int water = 300, long turns = 6002, string tentJob = "tent-job", long ticks = 265527)
			=> new KingdomCampHeartSaveSnapshot(game, "realm", "city", "JoppaWorld.8.22.1.1.10",
				heart, "upgrade", store, "fire", "timber", digest ?? new string('a', 64), new string('b', 64), tentJob,
				x, 12, 43, 13, 39, 13, water, turns, ticks);
		private static string Encode(KingdomCampHeartSaveSnapshot value)
		{
			Assert.That(Codec.TryEncode(value, out string wire), Is.True);
			return wire;
		}
		private static string Wrap(byte[] bytes) => Codec.Prefix + Convert.ToBase64String(bytes);
		private static byte[] Bytes() => Convert.FromBase64String(Encode(Sample()).Substring(Codec.Prefix.Length));
		private static void Refuse(string wire)
		{
			Assert.That(Codec.TryDecode(wire, out var value), Is.False);
			Assert.That(value, Is.Null);
		}

		[Test]
		public void SnapshotRoundTripPreservesAllIdentityCustodyAndClockFields()
		{
			var want = Sample(heart: "heart|:é", turns: long.MaxValue);
			string wire = Encode(want);
			Assert.That(Codec.TryDecode(wire, out var got), Is.True);
			Assert.That(new[] { got.GameId, got.RealmId, got.CityId, got.ZoneId, got.HeartId, got.UpgradeJobId,
				got.StoreId, got.FireId, got.TimberId, got.ContentsDigest, got.BrushDigest, got.TentJobId },
				Is.EqualTo(new[] { want.GameId, want.RealmId, want.CityId, want.ZoneId, want.HeartId, want.UpgradeJobId,
					want.StoreId, want.FireId, want.TimberId, want.ContentsDigest, want.BrushDigest, want.TentJobId }));
			Assert.That(new[] { got.HeartX, got.HeartY, got.StoreX, got.StoreY, got.FireX, got.FireY, got.Water },
				Is.EqualTo(new[] { want.HeartX, want.HeartY, want.StoreX, want.StoreY, want.FireX, want.FireY, want.Water }));
			Assert.That(got.Turns, Is.EqualTo(want.Turns));
			Assert.That(got.TimeTicks, Is.EqualTo(want.TimeTicks));
			Assert.That(Encode(got), Is.EqualTo(wire));
		}

		[Test]
		public void EveryByteTruncationAndTrailingDataRefuseWithoutPartialResult()
		{
			byte[] bytes = Bytes();
			for (int i = 0; i < bytes.Length; i++) Refuse(Wrap(bytes.Take(i).ToArray()));
			Refuse(Wrap(bytes.Concat(new byte[] { 0 }).ToArray()));
		}

		[TestCase(0, 0)]
		[TestCase(4, 1)]
		[TestCase(4, 3)]
		[TestCase(8, 0)]
		[TestCase(8, -1)]
		[TestCase(8, 4097)]
		[TestCase(8, 2147483647)]
		public void MagicVersionAndFramedLengthCorruptionRefuse(int offset, int value)
		{
			byte[] bytes = Bytes();
			Array.Copy(BitConverter.GetBytes(value), 0, bytes, offset, 4);
			Refuse(Wrap(bytes));
		}

		[Test]
		public void InvalidUtf8AndBase64AliasesRefuse()
		{
			byte[] bytes = Bytes();
			bytes[12] = 0xff;
			Refuse(Wrap(bytes));
			string wire = Encode(Sample());
			Refuse(wire.Insert(Codec.Prefix.Length + 4, " "));
			Refuse(wire + "\n");
			Refuse(Codec.Prefix + "!");
			Refuse("taf-camp-heart-save-v3:" + wire.Substring(Codec.Prefix.Length));
			Refuse(null);
			Refuse(new string('a', Codec.MaxWireChars + 1));
		}

		[TestCase("empty-game")]
		[TestCase("uppercase-guid")]
		[TestCase("duplicate-object")]
		[TestCase("duplicate-job")]
		[TestCase("negative-coordinate")]
		[TestCase("outside-coordinate")]
		[TestCase("negative-water")]
		[TestCase("negative-turns")]
		[TestCase("negative-ticks")]
		[TestCase("uppercase-digest")]
		[TestCase("short-digest")]
		[TestCase("control")]
		[TestCase("surrogate")]
		[TestCase("unbounded")]
		public void InvalidSnapshotsCannotPublish(string fault)
		{
			var value = fault == "empty-game" ? Sample(game: "")
				: fault == "uppercase-guid" ? Sample(game: "01234567-89AB-CDEF-0123-456789ABCDEF")
				: fault == "duplicate-object" ? Sample(store: "heart")
				: fault == "duplicate-job" ? Sample(tentJob: "upgrade")
				: fault == "negative-coordinate" ? Sample(x: -1)
				: fault == "outside-coordinate" ? Sample(x: 4096)
				: fault == "negative-water" ? Sample(water: -1)
				: fault == "negative-turns" ? Sample(turns: -1)
				: fault == "negative-ticks" ? Sample(ticks: -1)
				: fault == "uppercase-digest" ? Sample(digest: new string('A', 64))
				: fault == "short-digest" ? Sample(digest: "abc")
				: fault == "control" ? Sample(heart: "heart\n")
				: fault == "surrogate" ? Sample(heart: "heart\ud800") : Sample(heart: new string('a', 1025));
			Assert.That(Codec.TryEncode(value, out var wire), Is.False);
			Assert.That(wire, Is.Null);
		}

		[Test]
		public void CustodyDigestIgnoresOrderButDetectsEveryChangedFact()
		{
			var a = new Unit("one", "brush", "inv:store", 1);
			var b = new Unit("two", "timber", "inv:store", 1);
			string original = Codec.CustodyDigest(new[] { a, b });
			Assert.That(Codec.CustodyDigest(new[] { b, a }), Is.EqualTo(original));
			foreach (var replacement in new[] { new Unit("other", "brush", "inv:store", 1),
				new Unit("one", "timber", "inv:store", 1), new Unit("one", "brush", "inv:other", 1),
				new Unit("one", "brush", "inv:store", 2) })
				Assert.That(Codec.CustodyDigest(new[] { replacement, b }), Is.Not.EqualTo(original));
			Assert.That(Codec.CustodyDigest(new[] { new Unit("a|b", "c", "d", 1) }),
				Is.Not.EqualTo(Codec.CustodyDigest(new[] { new Unit("a", "b|c", "d", 1) })));
		}

		[Test]
		public void MalformedOrUnboundedCustodyCannotProduceEvidence()
		{
			var valid = new Unit("one", "brush", "inv:store", 1);
			var cases = new List<IReadOnlyList<Unit>> { null, new Unit[] { null }, new[] { valid, valid },
				Enumerable.Range(0, 49).Select(i => new Unit("id" + i, "brush", "store", 1)).ToArray() };
			foreach (var bad in new[] { new Unit("", "brush", "store", 1), new Unit("one", "", "store", 1),
				new Unit("one", "brush", "", 1), new Unit("one", "brush", "store", 0),
				new Unit("one", "brush", "store", -1), new Unit("one\n", "brush", "store", 1),
				new Unit("one", "brush\ud800", "store", 1), new Unit("one", "brush", new string('s', 1025), 1) })
				cases.Add(new[] { bad });
			foreach (var rows in cases) Assert.Throws<System.IO.InvalidDataException>(() => Codec.CustodyDigest(rows));
		}

		[Test]
		public void SavePersonaUsesExactOriginalStepsAndOneFinalSave()
		{
			string[] source = Script("camp-heart-native-checks"), saved = Script("camp-heart-save");
			Assert.That(KingdomCampHeartScript.Matches(source), Is.True);
			Assert.That(KingdomCampHeartScript.Matches(source, true), Is.False);
			Assert.That(KingdomCampHeartScript.Matches(saved, true), Is.True);
			Assert.That(saved, Is.EqualTo(source.Concat(new[] { "camp-heart-save" }).ToArray()));
			Assert.That(KingdomCampHeartScript.Matches(null), Is.False);
			for (int i = 0; i < saved.Length; i++)
			{
				var changed = (string[])saved.Clone();
				changed[i] += " ";
				Assert.That(KingdomCampHeartScript.Matches(changed, true), Is.False);
				Assert.That(KingdomCampHeartScript.Matches(saved.Where((_, at) => at != i).ToArray(), true), Is.False);
			}
			Assert.That(KingdomCampHeartScript.Matches(saved.Concat(new[] { "camp-heart-save" }).ToArray()), Is.False);
		}
		[Test]
		public void PaidChainRequiresEveryOrdinaryWaitAndCannotClaimSaveCoverage()
		{
			var script = Script("camp-heart-chain");
			Assert.That(KingdomCampHeartChainScript.Matches(script), Is.True);
			Assert.That(KingdomCampHeartScript.Matches(script), Is.True);
			Assert.That(KingdomCampHeartScript.Matches(script, true), Is.False);
			Assert.That(script.Take(9), Is.EqualTo(Script("camp-heart-native-checks")));
			Assert.That(script.Skip(9).Take(5), Is.EqualTo(new[] {
				"camp-heart-chain-setup", "advance 1200", "camp-heart-chain-supply",
				"advance 1200", "camp-heart-chain-check" }));
			Assert.That(KingdomCampHeartChainScript.Matches(null), Is.False);
			for (int i = 0; i < script.Length; i++)
			{
				var changed = (string[])script.Clone(); changed[i] += " ";
				Assert.That(KingdomCampHeartChainScript.Matches(changed), Is.False);
				Assert.That(KingdomCampHeartChainScript.Matches(script.Where((_, at) => at != i).ToArray()), Is.False);
			}
			Assert.That(KingdomCampHeartChainScript.Matches(script.Concat(new[] { "camp-heart-save" }).ToArray()), Is.False);
			int turns = script.Where(x => x.StartsWith("advance ")).Sum(x => int.Parse(x.Substring(8)));
			Assert.That(turns, Is.EqualTo(31200));
			Assert.That(script.Where(x => x.StartsWith("advance ")).All(x => int.Parse(x.Substring(8)) <= 10000), Is.True);
		}

		[Test]
		public void ChainHousingGridFitsEighteenLotsBesideTentAndFutureHeart()
		{
			Assert.That(KingdomPlotRules.TryInterior(80, 25, out var usable), Is.True);
			var occupied = new System.Collections.Generic.List<KingdomPlotRules.PlotRect> {
				new KingdomPlotRules.PlotRect(31, 4, 50, 21),
				new KingdomPlotRules.PlotRect(24, 7, 29, 10) };
			int accepted = 0;
			foreach (var rect in KingdomCampHeartChainGrid.Candidates())
			{
				if (!KingdomPlotRules.Fits(rect, usable)
					|| !KingdomCampHeartChainGrid.ClearsPaidApproach(rect, occupied[0])
					|| !KingdomCampHeartChainGrid.ClearsPaidApproach(rect, occupied[1])
					|| KingdomPlotRules.CrowdsExisting(rect, occupied)) continue;
				occupied.Add(rect); accepted++;
			}
			Assert.That(accepted, Is.GreaterThanOrEqualTo(18),
				"leave complete paid entrance approaches and every existing plot's reserved lane intact");
			Assert.That(KingdomCampHeartChainGrid.Candidates().All(rect =>
				KingdomPlotRules.Fits(rect, usable)), Is.True, "all candidates fit the production interior");
		}

		[Test]
		public void ChainHousingProtectsPaidLaneBeyondReservedMargin()
		{
			var tent = new KingdomPlotRules.PlotRect(24, 7, 29, 10);
			var blocker = new KingdomPlotRules.PlotRect(23, 2, 28, 5);
			Assert.That(KingdomPlotRules.CrowdsExisting(blocker, new[] { tent }), Is.False);
			Assert.That(blocker.Contains(26, 5), Is.True, "recorded north-facing tent lane endpoint");
			Assert.That(KingdomCampHeartChainGrid.ClearsPaidApproach(blocker, tent), Is.False);
			Assert.That(KingdomCampHeartChainGrid.ClearsPaidApproach(
				new KingdomPlotRules.PlotRect(16, 2, 21, 5), tent), Is.True);
		}

		[TestCase(true, true, "CommandSurvivalCamp", true)]
		[TestCase(false, true, "CommandSurvivalCamp", false)]
		[TestCase(true, false, "CommandSurvivalCamp", false)]
		[TestCase(true, true, "CommandWait", false)]
		[TestCase(true, true, null, false)]
		public void ChainCampCommandGuardOnlyAppliesDuringItsPlayerWait(bool advancing,
			bool player, string command, bool expected)
		{
			Assert.That(KingdomCampHeartChainScript.UnexpectedCampCommand(Script("camp-heart-chain"),
				advancing, player, command), Is.EqualTo(expected));
			Assert.That(KingdomCampHeartChainScript.UnexpectedCampCommand(Script("camp-heart-save"),
				advancing, player, command), Is.False);
			Assert.That(KingdomCampHeartChainScript.UnexpectedCampCommand(null,
				advancing, player, command), Is.False);
		}

		private static string[] Script(string name) => TestMain.ReadRepositoryText("Tools/personas/" + name + ".persona")
			.Split('\n').Single(line => line.StartsWith("SCRIPT=", StringComparison.Ordinal)).Substring(7).Split(';');
	}
}
#endif
