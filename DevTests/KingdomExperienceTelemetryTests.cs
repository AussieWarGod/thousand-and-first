#if TAF_TESTS
using System;
using System.Reflection;
using System.Text;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomExperienceTelemetryTests
	{
		[Test]
		public void ExperimentArmFixtureAndObservationIdsAreFrozen()
		{
			Assert.AreEqual("0,1,2,3", Values(typeof(KingdomExperienceOptionKind)));
			Assert.AreEqual("0,1,2", Values(typeof(KingdomExperienceOptionState)));
			Assert.AreEqual("0,1,2,3,4,5,6,7,8,9,10,11,12,13",
				Values(typeof(KingdomExperienceLane)));
			Assert.AreEqual("0,1,2,3,4,5,6,7,8,9,10,11,12",
				Values(typeof(KingdomExperienceCapacityFault)));
			Assert.AreEqual("0,1,2", Values(typeof(KingdomExperienceLeaseState)));
			Assert.AreEqual("0,1,2,3,4,5,6,7", Values(typeof(KingdomExperienceExperiment)));
			Assert.AreEqual("0,1,2,3,4", Values(typeof(KingdomExperienceTrialArm)));
			Assert.AreEqual("0,1,2,3,4,5,6,7", Values(typeof(KingdomExperienceFixture)));
			Assert.AreEqual("0,1,2,3,4,5,6,7,8",
				Values(typeof(KingdomExperienceObservationKind)));
		}

		[Test]
		public void VocabularyRejectsFreeCombinationsAndUnboundedMeasures()
		{
			Assert.IsTrue(KingdomExperienceTelemetryRules.Valid(
				KingdomExperienceExperiment.CivicVoices, KingdomExperienceTrialArm.FactsOnly,
				KingdomExperienceFixture.Choice, KingdomExperienceObservationKind.Exposed, 0));
			Assert.IsFalse(KingdomExperienceTelemetryRules.Valid(
				KingdomExperienceExperiment.CivicVoices, KingdomExperienceTrialArm.Projected,
				KingdomExperienceFixture.Choice, KingdomExperienceObservationKind.Exposed, 0));
			Assert.IsFalse(KingdomExperienceTelemetryRules.Valid(
				KingdomExperienceExperiment.CivicVoices, KingdomExperienceTrialArm.FactsOnly,
				KingdomExperienceFixture.WholeArc, KingdomExperienceObservationKind.Exposed, 0));
			Assert.IsFalse(KingdomExperienceTelemetryRules.Valid(
				KingdomExperienceExperiment.CivicVoices, KingdomExperienceTrialArm.FactsOnly,
				KingdomExperienceFixture.Choice, KingdomExperienceObservationKind.Exposed,
				KingdomExperienceTelemetryBuffer.MaxMeasure + 1));
		}

		[Test]
		public void RingIsBoundedAndReportsEveryOverwrite()
		{
			KingdomExperienceTelemetryBuffer b = new KingdomExperienceTelemetryBuffer();
			for (int i = 0; i < KingdomExperienceTelemetryBuffer.Capacity + 9; i++)
				Assert.IsTrue(b.TryRecord(KingdomExperienceExperiment.CivicVoices,
					KingdomExperienceTrialArm.FactsOnly, KingdomExperienceFixture.Choice,
					KingdomExperienceObservationKind.Exposed, i));
			Assert.AreEqual(KingdomExperienceTelemetryBuffer.Capacity, b.Count);
			Assert.AreEqual(9L, b.Dropped);
			Assert.IsTrue(b.TryGet(0, out KingdomExperienceTelemetryReceipt oldest));
			Assert.AreEqual(10L, oldest.Sequence);
		}

		[Test]
		public void ExportIsDeterministicBoundedAndCarriesNoGameplayIdentity()
		{
			KingdomExperienceTelemetryBuffer b = new KingdomExperienceTelemetryBuffer();
			Assert.IsTrue(b.TryRecord(KingdomExperienceExperiment.Curator,
				KingdomExperienceTrialArm.SemanticOnly,
				KingdomExperienceFixture.KnownDestination,
				KingdomExperienceObservationKind.DestinationVisited, 1));
			Assert.IsTrue(KingdomExperienceTelemetryExport.TryCompose(b, out string first));
			Assert.IsTrue(KingdomExperienceTelemetryExport.TryCompose(b, out string second));
			Assert.AreEqual(first, second);
			StringAssert.StartsWith("taf-experience-v1\n", first);
			Assert.IsFalse(first.Contains("taf:"));
			Assert.IsFalse(first.Contains("player", StringComparison.OrdinalIgnoreCase));
			Assert.LessOrEqual(new UTF8Encoding(false, true).GetByteCount(first),
				KingdomExperienceTelemetryExport.MaxExportBytes);
		}

		[Test]
		public void ReceiptShapeCannotCarryStringsReferencesOrWallClock()
		{
			FieldInfo[] fields = typeof(KingdomExperienceTelemetryReceipt).GetFields(
				BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			Assert.AreEqual(6, fields.Length);
			for (int i = 0; i < fields.Length; i++)
			{
				Type t = fields[i].FieldType;
				Assert.IsTrue(t == typeof(long) || t == typeof(int) || t.IsEnum,
					fields[i].Name + " can carry identifying or unbounded data");
			}
		}

		[Test]
		public void EmptySessionExportsAnExplicitZeroCount()
		{
			Assert.IsFalse(KingdomExperienceTelemetryExport.TryCompose(null, out string _));
			Assert.IsTrue(KingdomExperienceTelemetryExport.TryCompose(
				new KingdomExperienceTelemetryBuffer(), out string text));
			StringAssert.Contains("\ncount\t0\n", text);
			StringAssert.Contains("\ndropped\t0\n", text);
		}

		private static string Values(Type Type)
		{
			Array values = Enum.GetValues(Type); string[] rows = new string[values.Length];
			for (int i = 0; i < values.Length; i++)
				rows[i] = Convert.ToInt32(values.GetValue(i)).ToString();
			return string.Join(",", rows);
		}
	}
}
#endif
