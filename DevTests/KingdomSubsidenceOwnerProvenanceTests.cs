#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	public class KingdomSubsidenceOwnerProvenanceTests
	{
		[Test]
		public void NewlyConstructedCityKeepsFreshAuthorityThroughNormalizationAndSeatTransfer()
		{
			KingdomSettlement source = new KingdomSettlement();
			KingdomCityBook city = source.City;
			source.Normalize();
			KingdomSettlement captured = new KingdomSettlement();
			captured.ReadFrom(source);
			KingdomSettlement restored = new KingdomSettlement();
			captured.WriteTo(restored);
			Assert.AreSame(city, restored.City);
			Assert.AreEqual(KingdomSubsidenceStepCodec.FreshWire, restored.City.SubsidenceModel);
			Assert.IsTrue(restored.City.HasValidSubsidenceStorage());
		}

		[TestCase(long.MinValue)]
		[TestCase(-1L)]
		[TestCase(0L)]
		[TestCase(1234L)]
		public void NormalizationPreservesRawSubsidenceClockAndExistingOtherRepairs(long tick)
		{
			KingdomSettlement source = new KingdomSettlement
			{
				LastSubsidenceTick = tick, LastFoodWorkTick = -7L, SupportedLevel = -1
			};
			source.Normalize();
			Assert.AreEqual(tick, source.LastSubsidenceTick);
			Assert.AreEqual(0L, source.LastFoodWorkTick);
			Assert.AreEqual(0, source.SupportedLevel);
		}

		[Test]
		public void MissingCityGetsRefusedStorageAndCannotBecomeFreshOnRepeatedNormalization()
		{
			KingdomSettlement source = new KingdomSettlement { City = null, LastSubsidenceTick = 321L };
			source.Normalize();
			KingdomCityBook repair = source.City;
			Assert.IsNotNull(repair);
			Assert.IsNull(repair.SubsidenceModel);
			Assert.IsFalse(repair.HasValidSubsidenceStorage());
			source.Normalize();
			Assert.AreSame(repair, source.City);
			Assert.IsNull(source.City.SubsidenceModel);
			Assert.AreEqual(321L, source.LastSubsidenceTick);
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("ss1:malformed")]
		public void MalformedExistingCityIsPreservedThroughNormalizationAndSeatTransfer(string wire)
		{
			KingdomSettlement source = new KingdomSettlement { LastSubsidenceTick = -123L };
			KingdomCityBook exact = source.City;
			exact.SubsidenceModel = wire;
			source.Normalize();
			KingdomSettlement captured = new KingdomSettlement();
			captured.ReadFrom(source);
			KingdomSettlement restored = new KingdomSettlement();
			captured.WriteTo(restored);
			Assert.AreSame(exact, restored.City);
			Assert.AreEqual(wire, restored.City.SubsidenceModel);
			Assert.IsFalse(restored.City.HasValidSubsidenceStorage());
			Assert.AreEqual(-123L, restored.LastSubsidenceTick);
		}

		[Test]
		public void SeatCaptureRepairsOnlyItsMissingContainerAndPreservesNegativeCheckpoint()
		{
			KingdomSettlement source = new KingdomSettlement { City = null, LastSubsidenceTick = -42L };
			KingdomSettlement captured = new KingdomSettlement();
			captured.ReadFrom(source);
			Assert.IsNull(source.City);
			Assert.IsNotNull(captured.City);
			Assert.IsNull(captured.City.SubsidenceModel);
			Assert.AreEqual(-42L, captured.LastSubsidenceTick);
			KingdomSettlement restored = new KingdomSettlement();
			captured.WriteTo(restored);
			Assert.AreSame(captured.City, restored.City);
			Assert.IsFalse(restored.City.HasValidSubsidenceStorage());
			Assert.AreEqual(-42L, restored.LastSubsidenceTick);
		}

		[TestCase("Core/KingdomSettlement.Normalize.cs", "public void Read(SerializationReader Reader)", "KingdomSettlement")]
		[TestCase("Core/KingdomSystem.z19a.Serialization.cs", "public override void Read(SerializationReader Reader)", "KingdomSystem")]
		public void NativeParentReadSourceClearsConstructorCityBeforeNamedDecode(string path, string signature, string owner)
		{
			string method = Method(TestMain.ReadRepositoryText(path), signature);
			int clear = method.IndexOf("City = null;", StringComparison.Ordinal);
			int read = method.IndexOf("Reader.ReadNamedFields(this, typeof(" + owner + "))", StringComparison.Ordinal);
			Assert.That(clear, Is.GreaterThanOrEqualTo(0));
			Assert.That(read, Is.GreaterThan(clear));
			StringAssert.DoesNotContain("new Simulation.City.KingdomCityBook", method.Substring(clear, read - clear));
		}

		[TestCase("Core/KingdomSettlement.Normalize.cs", 1)]
		[TestCase("Core/KingdomSystem.z23.Normalization.cs", 1)]
		[TestCase("Core/KingdomSystem.z25.IdentityNormalization.cs", 2)]
		public void RepairConstructorSourceNeverGrantsFreshSubsidenceAuthority(string path, int expected)
		{
			string source = TestMain.ReadRepositoryText(path);
			Assert.AreEqual(expected, Count(source, "new Simulation.City.KingdomCityBook"));
			Assert.AreEqual(expected, Count(source, "new Simulation.City.KingdomCityBook { SubsidenceModel = null }"));
		}

		[TestCase("Core/KingdomSettlement.Normalize.cs")]
		[TestCase("Core/KingdomSystem.z23.Normalization.cs")]
		public void OwnerNormalizationSourceNeverResetsRawSubsidenceClock(string path)
		{
			string source = TestMain.ReadRepositoryText(path);
			StringAssert.DoesNotContain("LastSubsidenceTick =", source);
		}

		[Test]
		public void TrueFoundingSourceRetainsNewCityConstructors()
		{
			string source = TestMain.ReadRepositoryText("Core/KingdomSystem.z05.Identity.Founding.cs");
			Assert.AreEqual(2, Count(source, "new Simulation.City.KingdomCityBook()"));
			StringAssert.DoesNotContain("SubsidenceModel = null", source);
		}

		[Test]
		public void NativeParentSourceRejectsSkippedNestedBlocksBeforeNormalizationAndSuccess()
		{
			string method = Method(TestMain.ReadRepositoryText("Core/KingdomSystem.z19a.Serialization.cs"),
				"public override void Read(SerializationReader Reader)");
			string[] ordered =
			{
				"City = null;", "int nestedErrorsBefore = Reader.Errors;",
				"Reader.ReadNamedFields(this, typeof(KingdomSystem));",
				"if (Reader.Errors != nestedErrorsBefore)",
				"throw new InvalidOperationException(\"ThousandAndFirst nested kingdom records could not be read.\");",
				"RequireReadableSubsidenceStorage();", "NormalizeState(AllowLegacyIdentityMigration: false);",
				"LoadFailed = false;", "CustomReadCompleted = true;", "catch", "LoadFailed = true;", "throw;"
			};
			int previous = -1;
			foreach (string item in ordered)
			{
				int current = method.IndexOf(item, previous + 1, StringComparison.Ordinal);
				Assert.That(current, Is.GreaterThan(previous), item);
				previous = current;
			}
		}

		[Test]
		public void NativeParentSourceChecksEveryRetainedCityCarrierWithoutReplacingIt()
		{
			string source = TestMain.ReadRepositoryText("Core/KingdomSystem.z19a.Serialization.cs");
			string guard = Method(source, "private void RequireReadableSubsidenceStorage()");
			StringAssert.Contains("City == null || !City.HasValidSubsidenceStorage()", guard);
			foreach (string carrier in new[] { "ExiledSeat", "Seceded", "Away", "ExiledAway",
				"SettlementTopology.Get(i)", "ExiledSettlementTopology.Get(i)" })
				StringAssert.Contains("RequireReadableSubsidenceSettlement(" + carrier + ");", guard);
			StringAssert.Contains("i < SettlementTopology.Count", guard);
			StringAssert.Contains("i < ExiledSettlementTopology.Count", guard);
			StringAssert.Contains("#pragma warning disable 618", guard);
			StringAssert.Contains("#pragma warning restore 618", guard);
			StringAssert.Contains("if (Exiled && ExiledSeat == null && ExiledAway == null)", guard);
			StringAssert.Contains("throw new InvalidOperationException(\"ThousandAndFirst exiled settlement carrier is missing.\");", guard);
			StringAssert.DoesNotContain("new Simulation.City.KingdomCityBook", guard);
			string helper = Method(source, "private static void RequireReadableSubsidenceSettlement(KingdomSettlement settlement)");
			StringAssert.Contains("settlement != null", helper);
			StringAssert.Contains("settlement.City == null || !settlement.City.HasValidSubsidenceStorage()", helper);
			StringAssert.Contains("throw new InvalidOperationException", helper);
		}

		private static int Count(string source, string needle)
		{
			int count = 0, cursor = 0;
			while ((cursor = source.IndexOf(needle, cursor, StringComparison.Ordinal)) >= 0)
			{
				count++;
				cursor += needle.Length;
			}
			return count;
		}

		private static string Method(string source, string signature)
		{
			int start = source.IndexOf(signature, StringComparison.Ordinal);
			Assert.That(start, Is.GreaterThanOrEqualTo(0), signature);
			int open = source.IndexOf('{', start), depth = 0;
			for (int i = open; i < source.Length; i++)
			{
				if (source[i] == '{') depth++;
				else if (source[i] == '}' && --depth == 0) return source.Substring(start, i - start + 1);
			}
			Assert.Fail("Unclosed method: " + signature);
			return null;
		}
	}
}
#endif
