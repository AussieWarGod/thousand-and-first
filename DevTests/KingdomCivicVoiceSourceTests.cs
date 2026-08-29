#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomCivicVoiceSourceTests
	{
		private static string Read(string path) { return TestMain.ReadRepositoryText(path); }

		[Test]
		public void ThreeOwnersShowTheirExactPreviewBeforeOutcomeThenPublish()
		{
			string creed = Read("Core/KingdomCharterPart.ReleaseAndCreed.cs");
			Ordered(creed, "KingdomCreedRules.DeclarationPreview(",
				"TryPrepareCivicVoice(System", "Popup.ShowYesNo(rendering)",
				"KingdomCreed.Declare(System", "TryPublishCivicVoice(System, voice)");
			string covenant = Read("Founding/FounderBasin.Rite.cs");
			Ordered(covenant, "KingdomFoundingTransaction.VillageCharterPreview(",
				"TryPrepareCivicVoice(System", "Popup.ShowYesNo(rendering)",
				"BeginVillageCharter(", "TryPublishCivicVoice(System, voice)");
			string moot = Read("Growth/KingdomAssentingMoot.UI.cs");
			Ordered(moot, "KingdomAssentingMootRules.MembershipPreview(receipt",
				"TryPrepareCivicVoice(system", "Popup.ShowYesNo(rendering)",
				"TryChangeMember(context", "TryPublishCivicVoice(system, voice)");
			StringAssert.Contains("KingdomCivicVoiceFixture.CreedDeclaration", creed);
			StringAssert.Contains("KingdomCivicVoiceFixture.VillageCovenant", covenant);
			StringAssert.Contains("KingdomCivicVoiceFixture.AssentingMoot", moot);
		}

		[Test]
		public void DecisionTagHasExactlyTwoReadOnlyAuthoredConsumers()
		{
			string creed = Read("Core/KingdomCharterPart.ReleaseAndCreed.cs");
			string covenant = Read("Founding/FounderBasin.Rite.cs");
			StringAssert.Contains("KingdomDecisionTagRules.CreedScene(System.City?.AssentingMoot)",
				creed);
			StringAssert.Contains("KingdomDecisionTagRules.CovenantScene(System.City?.AssentingMoot)",
				covenant);
			StringAssert.Contains("if (!string.IsNullOrEmpty(precedent)) rendering +=", creed);
			StringAssert.Contains("if (!string.IsNullOrEmpty(precedent)) rendering +=", covenant);
			string tag = Read("Core/KingdomDecisionTagRules.cs");
			StringAssert.Contains("KingdomAssentingMootReceipt copy = Receipt.Copy()", tag);
			StringAssert.Contains("SourceVersion = KingdomAssentingMootRules.CurrentReceiptVersion",
				tag);
			StringAssert.Contains("They do not decide this declaration", tag);
			StringAssert.Contains("They do not decide this covenant", tag);
			StringAssert.DoesNotContain("score", tag.ToLowerInvariant());
			StringAssert.DoesNotContain("SetStanding", tag);
			StringAssert.DoesNotContain("TryChangeMember", tag);
		}

		[Test]
		public void RuntimeNeverLoadsCreatesChoosesOrOwnsMechanicalOutcome()
		{
			string runtime = Read("Experience/KingdomExperienceRuntime.CivicVoices.cs");
			StringAssert.Contains("KingdomMaster.NewWorkAllowed(System)", runtime);
			StringAssert.Contains("TryVoiceSettlement(System, SettlementId", runtime);
			StringAssert.Contains("List<KingdomResidentRow> rows = VoiceRows(book)", runtime);
			StringAssert.Contains("System.SettlementIdForOwnedZone(zoneId)", runtime);
			StringAssert.Contains("TryResolveBoundBody(System, ResidentId, false", runtime);
			StringAssert.Contains("Rendering = Facts ?? \"\"", runtime);
			StringAssert.Contains("TryRecord(System, KingdomExperienceExperiment.CivicVoices", runtime);
			string[] forbidden = { "GetZone(", "ZoneManager", "GameObject.Create", "Random",
				"AdjustStanding", "SetStanding", "Dissent =", "Drain(", "JournalAPI", "AddXP",
				"Popup.", "Timer", "Backlog" };
			for (int i = 0; i < forbidden.Length; i++)
				StringAssert.DoesNotContain(forbidden[i], runtime, forbidden[i]);
			StringAssert.DoesNotContain("System?.City", runtime);
			Assert.Less(runtime.IndexOf("KingdomMaster.NewWorkAllowed(System)",
				StringComparison.Ordinal), runtime.IndexOf("TryVoiceSettlement(System, SettlementId",
					StringComparison.Ordinal));
		}

		[Test]
		public void PrepareAndRecallResolveTheReceiptSettlementInsteadOfTheSeatCursor()
		{
			string runtime = Read("Experience/KingdomExperienceRuntime.CivicVoices.cs");
			StringAssert.Contains("System.TryFindSettlement(SettlementId", runtime);
			StringAssert.Contains("Book = seated ? System.City : settlement?.City", runtime);
			StringAssert.Contains("CivicWitnessAvailable(System, row.SettlementId", runtime);
			StringAssert.DoesNotContain("KingdomResidents.RollRows(System)", runtime);
			StringAssert.DoesNotContain("System.City.SettlementId", runtime);
		}

		[Test]
		public void ReceiptAndCompactCodecKeepOneFactsStringAndNoRealmDuplication()
		{
			string model = Read("Experience/KingdomCivicVoiceModels.cs");
			string preview = Slice(model, "public sealed class KingdomCivicDecisionPreview",
				"public readonly struct KingdomCivicVoiceCandidate");
			string receipt = Slice(model, "public sealed class KingdomCivicVoiceReceipt",
				"public KingdomCivicVoiceReceipt Copy()");
			Assert.AreEqual(1, Count(preview, "public string Facts;"));
			Assert.AreEqual(1, Count(receipt, "public string Facts;"));
			Assert.AreEqual(2, Count(model, "public string Facts;"));
			StringAssert.DoesNotContain("Origin", model);
			StringAssert.DoesNotContain("Mood", model);
			StringAssert.DoesNotContain("Relationship", model);
			StringAssert.DoesNotContain("BodyObject", model);
			string payload = Read("Experience/KingdomExperienceCodec.Payload.cs");
			StringAssert.Contains("WriteAudienceCompact", payload);
			StringAssert.Contains("ReadAudienceCompact(R, realm)", payload);
			string compact = Slice(payload, "private static void WriteAudienceCompact",
				"private static KingdomExperienceAudienceReceipt ReadAudienceCompact");
			StringAssert.DoesNotContain("R.RealmId", compact);
			compact = Slice(payload, "private static void WriteBodyCompact",
				"private static KingdomExperienceBodyReservation ReadBodyCompact");
			StringAssert.DoesNotContain("R.RealmId", compact);
			StringAssert.Contains("EncodeLegacyV2Fixture", payload);
			string codec = Read("Experience/KingdomExperienceCodec.Civic.cs");
			string write = Slice(codec, "private static void WriteVoice",
				"private static KingdomCivicVoiceReceipt ReadVoice");
			Assert.AreEqual(1, Count(write, "WriteVoiceText(W, R.Facts)"));
			Assert.AreEqual(1, Count(codec, "Facts = ReadVoiceText(R)"));
		}

		[Test]
		public void CallbackIsExplicitBoundedAndNeverAutomatic()
		{
			string ui = Read("Growth/KingdomAssentingMoot.UI.cs");
			StringAssert.Contains("hear one recorded civic exchange", ui);
			StringAssert.Contains("TryRecallCivicVoice(system, Now()", ui);
			string rules = Read("Experience/KingdomCivicVoiceRules.cs");
			StringAssert.Contains("row.CallbackConsumed", rules);
			StringAssert.Contains("CallbackConsumed = true", rules);
			StringAssert.DoesNotContain("Schedule", rules);
			StringAssert.DoesNotContain("Random", rules);
		}

		private static void Ordered(string text, params string[] needles)
		{
			int prior = -1;
			for (int i = 0; i < needles.Length; i++)
			{
				int at = text.IndexOf(needles[i], prior + 1, StringComparison.Ordinal);
				Assert.Greater(at, prior, needles[i]); prior = at;
			}
		}

		private static int Count(string text, string needle)
		{
			int count = 0, at = 0;
			while ((at = text.IndexOf(needle, at, StringComparison.Ordinal)) >= 0)
			{ count++; at += needle.Length; }
			return count;
		}

		private static string Slice(string text, string start, string end)
		{
			int a = text.IndexOf(start, StringComparison.Ordinal);
			int b = text.IndexOf(end, a + 1, StringComparison.Ordinal);
			Assert.GreaterOrEqual(a, 0); Assert.Greater(b, a); return text.Substring(a, b - a);
		}
	}
}
#endif
