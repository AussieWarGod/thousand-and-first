#if TAF_TESTS
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The bit mint's loop, executed (#264 review). A fake world stands in for the store: it
	/// counts what it is told to count, so each case shows the loop reading production's tally,
	/// not its own bookkeeping. The reviewed defect is the first case: on the arcology's "00346"
	/// the body bought for tier three already covers tier four, and the untakeable candidate the
	/// old mint reached for next is never offered.
	/// </summary>
	public class KingdomCampHeartHighCraftFillTests
	{
		private sealed class World
		{
			internal readonly KingdomBitTally Held = new KingdomBitTally();
			internal readonly Dictionary<int, List<string>> Factory = new Dictionary<int, List<string>>();
			internal readonly Dictionary<string, string> Worth = new Dictionary<string, string>();
			internal readonly Dictionary<string, string> Refuses = new Dictionary<string, string>();
			internal readonly HashSet<string> Uncounted = new HashSet<string>();
			internal readonly List<string> Offered = new List<string>(), Stored = new List<string>();
			internal int Retracted;
			internal bool Unreadable;

			internal World Candidate(int Tier, string Key, string Bits)
			{
				if (!Factory.TryGetValue(Tier, out var keys)) Factory[Tier] = keys = new List<string>();
				keys.Add(Key);
				Worth[Key] = Bits;
				return this;
			}

			internal string Fill(string Wanted, KingdomCampHeartHighCraftRules.Ledger Book)
			{
				return KingdomCampHeartHighCraftRules.Fill(Tally(Wanted),
					() => Unreadable ? null : Held.Copy(),
					(tier, skipped) => Factory.TryGetValue(tier, out var keys)
						? keys.FirstOrDefault(k => !skipped.Contains(k)) : null,
					Offer, () => { Retracted++; Stored.RemoveAt(Stored.Count - 1); }, Book);
			}

			private string Offer(string Key, int Tier, out KingdomBitTally Unit)
			{
				Offered.Add(Key);
				Unit = null;
				if (Refuses.TryGetValue(Key, out string reason)) return reason;
				Unit = Tally(Worth[Key]);
				Stored.Add(Key);
				if (!Uncounted.Contains(Key)) Held.AddAll(Unit);
				return null;
			}
		}

		private static KingdomBitTally Tally(string Text)
		{
			Assert.That(KingdomMaterialRules.TryParseBitCost(Text, out KingdomBitTally bits, out string error),
				Is.True, error);
			return bits;
		}

		[Test]
		public void ATierAnEarlierBodyCoversIsNeverMintedFor()
		{
			// The reviewer's walk of the old mint: two lasers, then the maghammer, then the cist.
			var world = new World().Candidate(0, "Laser", "0256").Candidate(3, "Maghammer", "0345")
				.Candidate(4, "Cist", "00145").Candidate(6, "Rail", "0006");
			world.Refuses["Cist"] = "untakeable";
			var book = new KingdomCampHeartHighCraftRules.Ledger();
			Assert.That(world.Fill("00346", book), Is.Null);
			Assert.That(world.Offered, Is.EqualTo(new[] { "Laser", "Laser", "Maghammer" }));
			Assert.That(book.Minted, Is.EqualTo(new[] { "0:Laser", "0:Laser", "3:Maghammer" }));
			Assert.That(book.Skipped, Is.Empty);
			Assert.That(KingdomMaterialRules.CoversBits(world.Held, Tally("00346")), Is.True);
		}

		[Test]
		public void ARefusedBodySkipsItsBlueprintAndTheWalkTakesTheNext()
		{
			var world = new World().Candidate(0, "Corroded", "00").Candidate(4, "Cist", "00145")
				.Candidate(4, "Cheek", "3478");
			world.Refuses["Cist"] = "untakeable";
			var book = new KingdomCampHeartHighCraftRules.Ledger();
			Assert.That(world.Fill("004", book), Is.Null);
			Assert.That(world.Offered, Is.EqualTo(new[] { "Corroded", "Cist", "Cheek" }));
			Assert.That(book.Skipped, Is.EqualTo(new[] { "Cist" }));
			Assert.That(book.Notes, Is.EqualTo(new[] { "Cist(untakeable)" }));
			Assert.That(world.Stored, Is.EqualTo(new[] { "Corroded", "Cheek" }), "a refused body is never stored");
		}

		[Test]
		public void AStoredBodyProductionDoesNotCountIsRetracted()
		{
			var world = new World().Candidate(3, "Socketed", "0345").Candidate(3, "Mirrorshades", "03");
			world.Uncounted.Add("Socketed");
			var book = new KingdomCampHeartHighCraftRules.Ledger();
			Assert.That(world.Fill("3", book), Is.Null);
			Assert.That(world.Retracted, Is.EqualTo(1));
			Assert.That(world.Stored, Is.EqualTo(new[] { "Mirrorshades" }));
			Assert.That(book.Notes, Is.EqualTo(new[] { "Socketed(uncounted)" }));
			Assert.That(book.Minted, Is.EqualTo(new[] { "3:Mirrorshades" }));
		}

		[Test]
		public void AnAlreadyCoveredBillMintsNothing()
		{
			var world = new World().Candidate(0, "Corroded", "00");
			world.Held.AddAll(Tally("00346"));
			var book = new KingdomCampHeartHighCraftRules.Ledger();
			Assert.That(world.Fill("00346", book), Is.Null);
			Assert.That(world.Offered, Is.Empty);
		}

		[Test]
		public void AnUnreadableStockOrAnUnsourcedTierRefusesByName()
		{
			var world = new World().Candidate(0, "Corroded", "00");
			world.Unreadable = true;
			StringAssert.StartsWith("taf-camp-rung5-bits-unreadable",
				world.Fill("00", new KingdomCampHeartHighCraftRules.Ledger()));
			world.Unreadable = false;
			StringAssert.StartsWith("taf-camp-rung5-bit-tier-unsourced: no scanned blueprint is worth a tier-6 bit",
				world.Fill("006", new KingdomCampHeartHighCraftRules.Ledger()));
			Assert.That(KingdomCampHeartHighCraftRules.Fill(null, null, null, null, null, null),
				Is.EqualTo("taf-camp-rung5-bits-unwired"));
		}

		[Test]
		public void TheMintCapEndsALoopThatNeverCovers()
		{
			var world = new World().Candidate(0, "Corroded", "0");
			var book = new KingdomCampHeartHighCraftRules.Ledger();
			string wanted = new string('0', KingdomCampHeartHighCraftRules.MintCap + 1);
			StringAssert.StartsWith("taf-camp-rung5-bits-unbounded", world.Fill(wanted, book));
			Assert.That(book.Minted.Count, Is.EqualTo(KingdomCampHeartHighCraftRules.MintCap));
		}

		[Test]
		public void TheSkipCapEndsALoopWhoseEveryBodyIsRefused()
		{
			var world = new World();
			for (int i = 0; i <= KingdomCampHeartHighCraftRules.SkipCap; i++)
			{
				world.Candidate(0, "Holder" + i, "0");
				world.Refuses["Holder" + i] = "holds";
			}
			var book = new KingdomCampHeartHighCraftRules.Ledger();
			StringAssert.StartsWith("taf-camp-rung5-bits-unsourced", world.Fill("0", book));
			Assert.That(book.Skipped.Count, Is.EqualTo(KingdomCampHeartHighCraftRules.SkipCap));
			Assert.That(world.Offered.Count, Is.EqualTo(KingdomCampHeartHighCraftRules.SkipCap + 1));
			Assert.That(world.Offered.Distinct().Count(), Is.EqualTo(world.Offered.Count),
				"a skipped blueprint is never offered twice");
		}
	}
}
#endif
