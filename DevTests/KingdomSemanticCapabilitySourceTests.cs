#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomSemanticCapabilitySourceTests
	{
		private static string Read(string relative)
		{
			return TestMain.ReadRepositoryText(relative);
		}

		[Test]
		public void NativeAdaptersExposeEmbodiedCapabilitiesWithoutInventingInquiry()
		{
			string source = Read("Growth/KingdomBenefitIndex.Native.cs");
			StringAssert.Contains("KingdomBenefitCapabilities.Cooking", source);
			StringAssert.Contains("KingdomBenefitCapabilities.Shrine", source);
			StringAssert.Contains("KingdomBenefitCapabilities.Education", source);
			StringAssert.DoesNotContain("KingdomBenefitCapabilities.Inquiry", source);
			StringAssert.Contains("KingdomBenefitScope.Plot, KingdomBenefitOperation.Present", source);
			StringAssert.Contains("KingdomBenefitScope.Interior, KingdomBenefitOperation.Staffed", source);
		}

		[Test]
		public void SemanticConsumersReadLiveCapabilityEvidence()
		{
			foreach (string path in new[] {
				"Core/KingdomReports.cs",
				"Experience/KingdomFaith.z01.ShrinePass.cs",
				"Experience/KingdomFaith.z03.EducationAndConsecration.cs",
				"Growth/KingdomLarder.cs",
				"Growth/KingdomLab.CivicSelection.cs",
				"Growth/KingdomReach.GroundCharacter.cs",
				"Growth/KingdomResearch.Advance.cs",
				"Quests/KingdomPetitionLifecycle.Projection.cs"
			})
			{
				string source = Read(path);
				StringAssert.Contains("KingdomBenefitCapabilities.", source, path);
				StringAssert.DoesNotContain("Survey.Kitchens", source, path);
				StringAssert.DoesNotContain("survey.Kitchens", source, path);
				StringAssert.DoesNotContain("Survey.Shrines", source, path);
				StringAssert.DoesNotContain("survey.Shrines", source, path);
			}
		}

		[Test]
		public void FaithRuntimeCannotFallBackToCategoryOrWorkPresence()
		{
			string source = Read("Experience/KingdomFaith.z01.ShrinePass.cs")
				+ Read("Experience/KingdomFaith.z02.ShrinePressureAndEducation.cs")
				+ Read("Experience/KingdomFaith.z03.EducationAndConsecration.cs");
			StringAssert.Contains("KingdomCapabilityRuntime", source);
			StringAssert.DoesNotContain("Survey.Works", source);
			StringAssert.DoesNotContain("survey.Works", source);
			StringAssert.DoesNotContain("CanConsecrate", source);
			StringAssert.DoesNotContain("IsEducationCategory", source);
		}

		[Test]
		public void SurveyDoesNotMaintainSemanticObjectShortcuts()
		{
			string source = Read("Growth/KingdomSurvey.00.Declarations.cs")
				+ Read("Growth/KingdomSurvey.01.Capture.cs")
				+ Read("Growth/KingdomSurvey.02.IndexMaintenance.cs");
			StringAssert.DoesNotContain("HasPart(\"Campfire\")", source);
			StringAssert.DoesNotContain("HasPart(\"Shrine\")", source);
			StringAssert.DoesNotContain("Kitchens", source);
			StringAssert.DoesNotContain("Shrines", source);
		}

		[Test]
		public void InquiryPartAndInteractionReproveTheExactLiveBench()
		{
			string research = Read("Growth/KingdomResearch.Advance.cs");
			string bench = Read("Growth/KingdomResearch.Bench.cs");
			string inquiry = Read("Growth/KingdomInquiry.cs");
			StringAssert.Contains("KingdomCapabilityRuntime.HasRoot", research);
			StringAssert.Contains("!LiveBench(Bench)", bench);
			StringAssert.Contains("!KingdomResearch.LiveBench(ParentObject)", inquiry);
		}

		[Test]
		public void RivalShrineRequiresLiveCapabilityAndExactReach()
		{
			string source = Read("Experience/KingdomWaterRite.z03.OfferAndGates.cs");
			StringAssert.Contains("KingdomBenefitCapabilities.Shrine", source);
			StringAssert.Contains("KingdomCapabilityRuntime.TryIndex", source);
			StringAssert.Contains("KingdomReach.ReachesCell", source);
			StringAssert.DoesNotContain("KingdomSurvey.ObjectsFor(Z))", BetweenRival(source));
			StringAssert.DoesNotContain("KingdomWaterRiteRules.WithinQuarter", BetweenRival(source));
		}

		private static string BetweenRival(string Source)
		{
			int start = Source.IndexOf("private static string RivalShrineNear",
				StringComparison.Ordinal);
			int end = Source.IndexOf("private static Cell DoorOf", start,
				StringComparison.Ordinal);
			return start >= 0 && end > start ? Source.Substring(start, end - start) : Source;
		}
	}
}
#endif
