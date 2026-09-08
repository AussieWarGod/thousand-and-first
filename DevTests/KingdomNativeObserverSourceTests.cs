#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Source wiring for the three unattended observers that replace attended steps. Behavioural
	/// evidence belongs to the sealed real-engine personas; what executes here is the claim each
	/// observer makes about itself - that it OBSERVES. An observer that lit the zone, rebuilt the
	/// guide's graph, or wrote the message it counts would be marking its own homework, so the
	/// mutating entry points are asserted absent rather than merely undocumented.
	/// </summary>
	[TestFixture]
	public sealed class KingdomNativeObserverSourceTests
	{
		private const string LightProvider = "Harness/KingdomClaimedLightNativeProvider.cs";
		private const string LightChecks = "Harness/KingdomClaimedLightNativeChecks.cs";
		private const string GuideChecks = "Harness/KingdomQuickstartBootstrap.NativeGuideTopics.cs";
		private const string GuideProvider = "Harness/KingdomGuideTopicsNativeProvider.cs";
		private const string GuestProvider = "Harness/KingdomFirstGuestNativeProvider.cs";
		private const string GuestChecks = "Harness/KingdomFirstGuestNativeChecks.cs";
		private const string Founding = "Harness/KingdomNativeCampFounding.cs";

		private static string Read(string path)
		{
			return TestMain.ReadRepositoryText(path);
		}

		/// <summary>The claim is made by a real founding and a real engine activation only.</summary>
		[Test]
		public void ClaimedLightObserverNeverLightsExploresOrReconcilesTheZoneItself()
		{
			string checks = Read(LightChecks);
			foreach (string token in new[] { "Zone.Activated();", "Zone.GetLight(x, y)",
				"Zone.GetCell(x, y).IsLit()", "(int)LightLevel.Light",
				"KingdomNativeCampFounding.Found(Game, Zone, Require)",
				"System.SettlementIdForOwnedZone(ZoneId)", "Attached.Version == 1",
				"The.ZoneManager", "manager.CachedZones" }) StringAssert.Contains(token, checks);
			foreach (string token in new[] { "AddLight(", "LightAll(", "ExploreAll(", "VisAll(",
				"SetLight(", "MixLight(", "ReconcileZone(", "RemoveZone(", "AddPart(",
				"RemovePart(", "GetZone(", "SetActiveZone(" })
				StringAssert.DoesNotContain(token, checks);
		}

		/// <summary>The render bracket is a void prefix/postfix pair, never a replacement.</summary>
		[Test]
		public void ClaimedLightRenderObserverIsAVoidBracketAroundTheProductionDispatch()
		{
			string provider = Read(LightProvider);
			foreach (string token in new[] { "[HarmonyPrefix]", "[HarmonyPostfix]",
				"internal static void Prefix(", "internal static void Postfix(",
				"typeof(BeforeRenderEvent)", "KingdomClaimedGround.Enabled" })
				StringAssert.Contains(token, provider);
			foreach (string token in new[] { "__result", "return false;", "[HarmonyTranspiler]",
				"__state" }) StringAssert.DoesNotContain(token, provider);
			StringAssert.Contains("\"advance 20\"", provider);
		}

		/// <summary>The guide is built by the production creator and afterwards only read.</summary>
		[Test]
		public void GuideTopicObserverReadsTheProductionGraphAndBuildsNoConversationOfItsOwn()
		{
			string checks = Read(GuideChecks);
			foreach (string token in new[] { "CreateAdvisor(Context.Game, Context.Zone, profile,",
				"VerifyAdvisor(Context.Zone, advisor, receipt, out failure)",
				"GetPart<ConversationScript>()?.Blueprint",
				"KingdomQuickstartGuideRules.Topics()", "KingdomQuickstartGuideRules.Start",
				"KingdomQuickstartGuideRules.Goodbye", "KingdomQuickstartGuideRules.TopicCount",
				"\"I have more to ask.\"", "choices[0][\"Target\"], \"End\"",
				"back[0][\"Target\"], \"Start\"" }) StringAssert.Contains(token, checks);
			foreach (string token in new[] { "ConversationsAPI.", "AddChoice(", "AddNode(",
				"AddText(", "AddChild(", "Blueprint =", "Popup.", "Conversation.Load" })
				StringAssert.DoesNotContain(token, checks);
			StringAssert.Contains("KingdomQuickstartBootstrap.NativeGuideTopicChecks(context)",
				Read(GuideProvider));
			StringAssert.Contains("ExpectedCases = 3", Read(GuideProvider));
		}

		/// <summary>Exactly-once is counted at the real write, and nothing writes the line here.</summary>
		[Test]
		public void FirstGuestObserverCountsTheProductionMessageWithoutWritingOrSuppressingIt()
		{
			string checks = Read(GuestChecks);
			foreach (string token in new[] { "Emissions == 1", "Logged() == 1",
				"GuestNotes() == 0", "KingdomFirstGuestRuntime.IsAwaitingAnswer(",
				"KingdomReports.NextNeed(System, Zone)", "need.StartsWith(GuestNeed,",
				"Contains(\"No roof stands.\")", "Emissions == PassOne" })
				StringAssert.Contains(token, checks);
			foreach (string token in new[] { "AddPlayerMessage(", "Messages.Add(",
				"Messages.Clear(", "MessageQueue.Suppress", "Popup.", "TimeTicks =",
				"NextArrivalTick =", "ArrivalCandidate =" }) StringAssert.DoesNotContain(token, checks);
			string provider = Read(GuestProvider);
			foreach (string token in new[] { "[HarmonyPrefix]", "typeof(MessageQueue), \"Add\"",
				"\"advance 4200\"", "\"advance 2400\"" }) StringAssert.Contains(token, provider);
			foreach (string token in new[] { "__result", "return false;", "[HarmonyTranspiler]" })
				StringAssert.DoesNotContain(token, provider);
		}

		/// <summary>The shared fixture founds and dedicates through production APIs only.</summary>
		[Test]
		public void SharedCampFixtureUsesTheProductionFoundingAndCheckInAndNothingElse()
		{
			string founding = Read(Founding);
			foreach (string token in new[] { "KingdomScenarioFoundingStep.TryProvePreconditions(",
				"KingdomScenarioTransactionMarker.TryBegin(",
				"KingdomScenarioFoundingStep.TryFound(",
				"KingdomScenarioTransactionMarker.TryCommit(", "KingdomSurvey.Take(Zone, System)",
				"survey.BindPass()", "KingdomCity.CheckIn(System, Zone, survey, tick)",
				"KingdomCity.DedicationOrderProperty" }) StringAssert.Contains(token, founding);
			foreach (string token in new[] { "Options.SetOption(", "SetIntGameState(",
				"SetStringGameState(", "TryEnroll(", "TryEnsureRow(", "Founded =",
				"Population =" }) StringAssert.DoesNotContain(token, founding);
		}
	}
}
#endif
