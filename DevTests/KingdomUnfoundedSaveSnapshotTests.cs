#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>#272/#271 cold-load witness codec and route wiring. The native unfounded save
	/// and cold load are owed separately; these pins prove only the engine-free contract.</summary>
	public class KingdomUnfoundedSaveSnapshotTests
	{
		private const string Game = "01234567-89ab-cdef-0123-456789abcdef";
		private const string Zone = "JoppaWorld.8.22.1.1.10";
		private static readonly string Life = new string('a', 64);
		private static readonly string Carry = new string('b', 64);

		private static KingdomUnfoundedSaveSnapshot Sample(string game = Game, string zone = Zone,
			int frame = 5, string life = null, string carry = null, long ticks = 1234L)
		{
			return new KingdomUnfoundedSaveSnapshot(game, zone, frame, life ?? Life, carry ?? Carry, ticks);
		}

		[Test]
		public void EncodesOneCanonicalLineAndDecodesItExactly()
		{
			ClassicAssert.IsTrue(KingdomUnfoundedSaveSnapshot.TryEncode(Sample(), out string wire));
			ClassicAssert.AreEqual("taf-unfounded-save-v1:" + Game + ";" + Zone + ";5;" + Life + ";" + Carry
				+ ";1234", wire);
			ClassicAssert.IsTrue(KingdomUnfoundedSaveSnapshot.TryDecode(wire, out KingdomUnfoundedSaveSnapshot value));
			ClassicAssert.AreEqual(Game, value.GameId);
			ClassicAssert.AreEqual(Zone, value.ZoneId);
			ClassicAssert.AreEqual(5, value.LifecycleFrame);
			ClassicAssert.AreEqual(Life, value.LifecycleSha256);
			ClassicAssert.AreEqual(Carry, value.CarrySha256);
			ClassicAssert.AreEqual(1234L, value.TimeTicks);
		}

		[TestCase("01234567-89AB-cdef-0123-456789abcdef", Zone, 5, 1L)]
		[TestCase("0123456789abcdef0123456789abcdef", Zone, 5, 1L)]
		[TestCase(Game, "", 5, 1L)]
		[TestCase(Game, "Joppa World", 5, 1L)]
		[TestCase(Game, "Joppa;World", 5, 1L)]
		[TestCase(Game, Zone, 0, 1L)]
		[TestCase(Game, Zone, 100, 1L)]
		[TestCase(Game, Zone, 5, -1L)]
		public void EncodeRefusesMalformedFields(string game, string zone, int frame, long ticks)
		{
			ClassicAssert.IsFalse(KingdomUnfoundedSaveSnapshot.TryEncode(Sample(game, zone, frame, ticks: ticks),
				out string wire));
			ClassicAssert.IsNull(wire);
		}

		[Test]
		public void EncodeRefusesMalformedDigestsAndOverlongZones()
		{
			foreach (KingdomUnfoundedSaveSnapshot value in new[] { Sample(life: new string('A', 64)),
				Sample(carry: new string('a', 63)), Sample(life: new string('g', 64)),
				Sample(zone: new string('z', 129)), null })
				ClassicAssert.IsFalse(KingdomUnfoundedSaveSnapshot.TryEncode(value, out _));
		}

		[Test]
		public void DecodeAdmitsOnlyTheCanonicalSpelling()
		{
			ClassicAssert.IsTrue(KingdomUnfoundedSaveSnapshot.TryEncode(Sample(), out string wire));
			string[] refused =
			{
				null, "", wire.Replace("taf-unfounded-save-v1:", "taf-unfounded-save-v2:"),
				wire + ";", wire.Replace(";5;", ";05;"), wire.Replace(";1234", ";01234"),
				wire.Replace(";1234", ";+1234"), wire.Replace(";1234", "; 1234"), wire.Replace(";1234", ";-1"),
				wire.Replace(";5;", ";x;"), wire + new string('0', 512), " " + wire, wire.Replace(";", ",")
			};
			foreach (string bad in refused)
			{
				ClassicAssert.IsFalse(KingdomUnfoundedSaveSnapshot.TryDecode(bad, out KingdomUnfoundedSaveSnapshot value),
					bad ?? "null");
				ClassicAssert.IsNull(value);
			}
		}

		[Test]
		public void LoadEntryBindsTheUnfoundedSnapshotBeforeArmingAndRoutesItsOwnWitness()
		{
			string entry = TestMain.ReadRepositoryText("Harness/KingdomScenarioLoadEntry.cs");
			int bind = entry.IndexOf("KingdomUnfoundedSaveSnapshot.TryDecode(SnapshotWire, out UnfoundedSnapshot)",
				StringComparison.Ordinal);
			int rung = entry.IndexOf("else if (KingdomSubsidenceRungSaveSnapshotCodec.MatchesPrefix(SnapshotWire))",
				StringComparison.Ordinal);
			int armed = entry.IndexOf("Armed = true;", StringComparison.Ordinal);
			int verify = entry.IndexOf("KingdomUnfoundedLoad.VerifyLoaded(loaded, UnfoundedSnapshot, Request.PrimarySha256)",
				StringComparison.Ordinal);
			int generic = entry.IndexOf("string route = RungSnapshot == null", StringComparison.Ordinal);
			ClassicAssert.Greater(bind, -1);
			ClassicAssert.Greater(rung, bind, "the unfounded prefix is routed before the rung and generic snapshots");
			ClassicAssert.Greater(armed, rung);
			ClassicAssert.Greater(verify, armed);
			ClassicAssert.Greater(generic, verify, "the unfounded load never reaches the generic witness route");
			StringAssert.Contains("UnfoundedSnapshot.GameId == Request.GameId", entry);
			string witness = TestMain.ReadRepositoryText("Harness/KingdomScenarioLoadWitness.cs");
			int routed = witness.IndexOf("KingdomScenarioLoadEntry.UnfoundedSnapshot != null", StringComparison.Ordinal);
			int fallback = witness.IndexOf("no generic snapshot was decoded", StringComparison.Ordinal);
			ClassicAssert.Greater(routed, -1);
			ClassicAssert.Greater(fallback, routed, "the unfounded load has its own pre-activation witness");
			StringAssert.Contains("KingdomUnfoundedLoad.BeforeActivation()", witness);
		}

		[Test]
		public void TheSharedSaveErrorWitnessObservesBothSaveOwners()
		{
			string save = TestMain.ReadRepositoryText("Harness/KingdomQuickstartSaveTest.cs");
			int patch = save.IndexOf("[HarmonyPatch(typeof(XRLGame), \"SaveGameError\")]", StringComparison.Ordinal);
			ClassicAssert.Greater(patch, -1);
			string witness = save.Substring(patch);
			StringAssert.Contains("KingdomQuickstartSaveTest.NoteSaveError(__instance);", witness);
			StringAssert.Contains("KingdomUnfoundedSave.NoteSaveError(__instance);", witness);
			string unfounded = TestMain.ReadRepositoryText("Harness/KingdomUnfoundedSave.cs");
			int dormant = unfounded.IndexOf("RequireDormant(system, \"before the save\");", StringComparison.Ordinal);
			int engine = unfounded.IndexOf("string primaryHash = SavePrimary(Game, directory);", StringComparison.Ordinal);
			int frame = unfounded.IndexOf("byte[] lifecycle = Lifecycle(system.LifecycleBook);", StringComparison.Ordinal);
			ClassicAssert.Greater(dormant, -1);
			ClassicAssert.Greater(engine, dormant);
			ClassicAssert.Greater(frame, engine,
				"no lifecycle write may precede the engine save, so an unfixed writer fails in the engine save");
			StringAssert.Contains("Check(!SaveError, \"actual SaveGameError observed\");", unfounded);
		}
	}
}
#endif
