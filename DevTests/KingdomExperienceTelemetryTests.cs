#if TAF_TESTS
using System;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomExperienceTelemetryTests
	{
		[Test]
		public void ExperimentArmFixtureAndObservationIdsAreFrozen()
		{
			ClassicAssert.AreEqual("0,1,2,3", Values(typeof(KingdomExperienceOptionKind)));
			ClassicAssert.AreEqual("0,1,2", Values(typeof(KingdomExperienceOptionState)));
			ClassicAssert.AreEqual("0,1,2,3,4,5,6,7,8,9,10,11,12,13",
				Values(typeof(KingdomExperienceLane)));
			ClassicAssert.AreEqual("0,1,2,3,4,5,6,7,8,9,10,11,12",
				Values(typeof(KingdomExperienceCapacityFault)));
			ClassicAssert.AreEqual("0,1,2", Values(typeof(KingdomExperienceLeaseState)));
			ClassicAssert.AreEqual("0,1,2,3,4,5,6,7", Values(typeof(KingdomExperienceExperiment)));
			ClassicAssert.AreEqual("0,1,2,3,4", Values(typeof(KingdomExperienceTrialArm)));
			ClassicAssert.AreEqual("0,1,2,3,4,5,6,7", Values(typeof(KingdomExperienceFixture)));
			ClassicAssert.AreEqual("0,1,2,3,4,5,6,7,8",
				Values(typeof(KingdomExperienceObservationKind)));
		}

		[Test]
		public void VocabularyRejectsFreeCombinationsAndUnboundedMeasures()
		{
			ClassicAssert.IsTrue(KingdomExperienceTelemetryRules.Valid(
				KingdomExperienceExperiment.CivicVoices, KingdomExperienceTrialArm.FactsOnly,
				KingdomExperienceFixture.Choice, KingdomExperienceObservationKind.Exposed, 0));
			ClassicAssert.IsFalse(KingdomExperienceTelemetryRules.Valid(
				KingdomExperienceExperiment.CivicVoices, KingdomExperienceTrialArm.Projected,
				KingdomExperienceFixture.Choice, KingdomExperienceObservationKind.Exposed, 0));
			ClassicAssert.IsFalse(KingdomExperienceTelemetryRules.Valid(
				KingdomExperienceExperiment.CivicVoices, KingdomExperienceTrialArm.FactsOnly,
				KingdomExperienceFixture.WholeArc, KingdomExperienceObservationKind.Exposed, 0));
			ClassicAssert.IsFalse(KingdomExperienceTelemetryRules.Valid(
				KingdomExperienceExperiment.CivicVoices, KingdomExperienceTrialArm.FactsOnly,
				KingdomExperienceFixture.Choice, KingdomExperienceObservationKind.Exposed,
				KingdomExperienceTelemetryBuffer.MaxMeasure + 1));
		}

		[Test]
		public void RingIsBoundedAndReportsEveryOverwrite()
		{
			KingdomExperienceTelemetryBuffer b = new KingdomExperienceTelemetryBuffer();
			for (int i = 0; i < KingdomExperienceTelemetryBuffer.Capacity + 9; i++)
				ClassicAssert.IsTrue(b.TryRecord(KingdomExperienceExperiment.CivicVoices,
					KingdomExperienceTrialArm.FactsOnly, KingdomExperienceFixture.Choice,
					KingdomExperienceObservationKind.Exposed, i));
			ClassicAssert.AreEqual(KingdomExperienceTelemetryBuffer.Capacity, b.Count);
			ClassicAssert.AreEqual(9L, b.Dropped);
			ClassicAssert.IsTrue(b.TryGet(0, out KingdomExperienceTelemetryReceipt oldest));
			ClassicAssert.AreEqual(10L, oldest.Sequence);
		}

		[Test]
		public void ExportIsDeterministicBoundedAndCarriesNoGameplayIdentity()
		{
			KingdomExperienceTelemetryBuffer b = new KingdomExperienceTelemetryBuffer();
			ClassicAssert.IsTrue(b.TryRecord(KingdomExperienceExperiment.Curator,
				KingdomExperienceTrialArm.SemanticOnly,
				KingdomExperienceFixture.KnownDestination,
				KingdomExperienceObservationKind.DestinationVisited, 1));
			ClassicAssert.IsTrue(KingdomExperienceTelemetryExport.TryCompose(b, out string first));
			ClassicAssert.IsTrue(KingdomExperienceTelemetryExport.TryCompose(b, out string second));
			ClassicAssert.AreEqual(first, second);
			StringAssert.StartsWith("taf-experience-v1\n", first);
			ClassicAssert.IsFalse(first.Contains("taf:"));
			ClassicAssert.IsFalse(first.Contains("player", StringComparison.OrdinalIgnoreCase));
			ClassicAssert.LessOrEqual(new UTF8Encoding(false, true).GetByteCount(first),
				KingdomExperienceTelemetryExport.MaxExportBytes);
		}

		[Test]
		public void ReceiptShapeCannotCarryStringsReferencesOrWallClock()
		{
			FieldInfo[] fields = typeof(KingdomExperienceTelemetryReceipt).GetFields(
				BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			ClassicAssert.AreEqual(6, fields.Length);
			for (int i = 0; i < fields.Length; i++)
			{
				Type t = fields[i].FieldType;
				ClassicAssert.IsTrue(t == typeof(long) || t == typeof(int) || t.IsEnum,
					fields[i].Name + " can carry identifying or unbounded data");
			}
		}

		[Test]
		public void EmptySessionExportsAnExplicitZeroCount()
		{
			ClassicAssert.IsFalse(KingdomExperienceTelemetryExport.TryCompose(null, out string _));
			ClassicAssert.IsTrue(KingdomExperienceTelemetryExport.TryCompose(
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
