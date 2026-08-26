#if TAF_TESTS
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomPhysicalHappeningSourceTests
	{
		private static string Source(string relative)
		{
			return TestMain.ReadRepositoryText(relative);
		}

		[Test]
		public void RuntimeStagesExactNamedBodiesByVanillaPathingAndNeverMakesProxies()
		{
			string source = Source(Path.Combine("Simulation", "City",
				"KingdomPhysicalHappenings.cs"));
			StringAssert.Contains("KingdomResidents.TryResolveBoundBody", source);
			StringAssert.Contains("ReferenceEquals(exact, candidate)", source);
			StringAssert.Contains("body.Brain.Stay(target)", source);
			StringAssert.Contains("KingdomHappeningMoveTo : MoveTo", source);
			StringAssert.Contains("new KingdomHappeningMoveTo(operation.EventId, target)", source);
			StringAssert.Contains("new FindPath", source);
			StringAssert.DoesNotContain("GameObject.Create", source);
			StringAssert.DoesNotContain("AddObject(", source);
			StringAssert.DoesNotContain("Teleport", source);
			StringAssert.DoesNotContain("body.CurrentCell.AddObject", source);
		}

		[Test]
		public void FixturesPostsUseAndRestoreAreExactDurableReceipts()
		{
			string source = Source(Path.Combine("Simulation", "City",
				"KingdomPhysicalHappenings.cs"));
			StringAssert.Contains("PostReceiptProperty", source);
			StringAssert.Contains("AnchorReceiptProperty", source);
			StringAssert.Contains("HomeReceiptProperty", source);
			StringAssert.Contains("FixtureUseProperty", source);
			StringAssert.Contains("ExactBodyReceipt", source);
			StringAssert.Contains("KingdomStations.Post(body, row.PostWorkId", source);
			StringAssert.Contains("body.Brain.StartingCell = new GlobalLocation(row.Anchor)", source);
			StringAssert.Contains("fixture.GetPart<Chair>()", source);
			StringAssert.Contains("fixture.HasPart(\"Shrine\")", source);
			StringAssert.Contains("fixture.HasPart(\"Campfire\")", source);
			StringAssert.Contains("fixture.HasPart(\"LiquidVolume\")", source);
			StringAssert.Contains("chair.SitDown(actor)", source);
			StringAssert.Contains("shrine.PrayAtShrine(actor, Silent: true)", source);
			StringAssert.Contains("RadiatesHeatEvent.Check(evidence.Fixture)", source);
			StringAssert.Contains("GetStorableDramsEvent.GetFor", source);
			StringAssert.Contains("TryMarkRestored", source);
			StringAssert.Contains("RestorationSettled", source);
			StringAssert.Contains("RemoveOwnedGoal(body, operation.EventId)", source);
			StringAssert.Contains("ParticipantGone(system, book, row)", source);
			StringAssert.Contains("body.CurrentCell != original", source);
			StringAssert.DoesNotContain("body.CurrentCell != original && !timedOut", source);
			StringAssert.Contains("token.StartsWith(prefix, StringComparison.Ordinal)", source);

			string blueprints = Source("ObjectBlueprints.xml");
			int basin = blueprints.IndexOf("<object Name=\"r_KingdomFirstBasin\"",
				System.StringComparison.Ordinal);
			int end = blueprints.IndexOf("</object>", basin, System.StringComparison.Ordinal);
			Assert.GreaterOrEqual(basin, 0);
			StringAssert.Contains("<part Name=\"LiquidVolume\"", blueprints.Substring(basin,
				end - basin));
		}

		[Test]
		public void AllFourProductionOwnersDogfoodLifecycleAndAbsenceIsReportOnly()
		{
			string happenings = Source(Path.Combine("Simulation", "City", "KingdomHappenings.cs"));
			StringAssert.Contains("KingdomPhysicalHappeningKind.Wedding", happenings);
			StringAssert.Contains("KingdomPhysicalHappeningKind.Funeral", happenings);
			StringAssert.Contains("KingdomPhysicalHappeningKind.Feast", happenings);
			StringAssert.Contains("DatedReport", happenings);
			StringAssert.Contains("OwnDeathTelling", happenings);
			StringAssert.Contains("Drawn(book.SettlementId", happenings);
			StringAssert.Contains("System.DeadNames.Contains(row.Name)", happenings);

			string ceremony = Source(Path.Combine("Experience", "KingdomCeremony.cs"));
			StringAssert.Contains("KingdomPhysicalHappenings.QueueRaising", ceremony);
			StringAssert.Contains("KingdomPhysicalHappenings.TryReadyRaising", ceremony);
			StringAssert.Contains("KingdomPhysicalHappenings.AcknowledgeRaising", ceremony);
			StringAssert.Contains("mode = 4", ceremony);

			string offices = Source(Path.Combine("Experience", "KingdomOffices.cs"));
			StringAssert.Contains("KingdomHappenings.OwnDeathTelling", offices);
			StringAssert.DoesNotContain("KingdomHappenings.FuneralClause(system", offices);

			string physical = Source(Path.Combine("Simulation", "City",
				"KingdomPhysicalHappenings.cs"));
			StringAssert.Contains("OpenReport", physical);
			StringAssert.Contains("false, false, chronicleAttended, chronicleUnattended", physical);
			StringAssert.Contains("KingdomChronicle.RecordOnce", physical);
			StringAssert.Contains("KingdomHappeningSinkState.Pending, nowTick", physical);
			StringAssert.Contains("PublishTold", physical);
			StringAssert.Contains("string.IsNullOrWhiteSpace(ledger)", physical);
			StringAssert.Contains("string.IsNullOrWhiteSpace(message)", physical);
			StringAssert.Contains("KingdomHappeningSinkState.Skipped", physical);
			StringAssert.Contains("book.SettlementId", physical);
			StringAssert.Contains("KingdomPhysicalQueueResult.Busy", physical);
			StringAssert.Contains("AlreadyCompleted", physical);
			StringAssert.Contains("PhaseTransition", Source(Path.Combine("Simulation", "City",
				"KingdomHappeningLifecycleRules.cs")));
			StringAssert.Contains("SinkTransition", Source(Path.Combine("Simulation", "City",
				"KingdomHappeningLifecycleRules.cs")));
			StringAssert.Contains("KingdomPhysicalHappeningKind.Raising", Source(Path.Combine(
				"Simulation", "City", "KingdomHappeningLifecycleRules.cs")));
			StringAssert.Contains("ActivityCell", physical);
			StringAssert.Contains("item.Physics.Solid", physical);
		}

		[Test]
		public void LifecycleRunsWithoutPushBudgetAndOtherSchedulersRespectItsLease()
		{
			string heartbeat = Source(Path.Combine("Simulation", "City", "KingdomHeartbeat.cs"));
			StringAssert.Contains("budget > 0 ? budget : 0", heartbeat);
			StringAssert.Contains("KingdomHappenings.Reckon", heartbeat);

			string stations = Source(Path.Combine("Simulation", "City", "KingdomStations.cs"));
			StringAssert.Contains("KingdomPhysicalHappenings.IsStaged", stations);
			string growth = Source(Path.Combine("Growth", "KingdomGrowth.cs"));
			StringAssert.Contains("KingdomPhysicalHappenings.IsStaged", growth);
			string construction = Source(Path.Combine("Growth",
				"KingdomConstructionPresence.cs"));
			StringAssert.Contains("KingdomPhysicalHappenings.IsStaged", construction);
			string expeditions = Source(Path.Combine("Experience", "KingdomExpeditions.cs"));
			StringAssert.Contains("KingdomPhysicalHappenings.IsStaged(Body)", expeditions);
			StringAssert.Contains("KingdomPhysicalHappenings.IsStaged(body)", expeditions);
			StringAssert.Contains("KingdomPhysicalHappenings.IsStaged(item)", growth);
		}

		[Test]
		public void ResumeFindsPersistedFixturesByExactIdWithoutClassifyingRemoteZones()
		{
			string source = Source(Path.Combine("Simulation", "City",
				"KingdomPhysicalHappenings.cs"));
			int find = source.IndexOf("private static GameObject FindById(",
				System.StringComparison.Ordinal);
			int loaded = source.IndexOf("private static Zone ExactLoadedZone(", find,
				System.StringComparison.Ordinal);
			Assert.GreaterOrEqual(find, 0);
			Assert.Greater(loaded, find);
			string exactLookup = source.Substring(find, loaded - find);
			Assert.GreaterOrEqual(Occurrences(exactLookup, "GameObject.FindByID(objectId)"), 2);
			StringAssert.DoesNotContain("KingdomSurvey.ObjectsFor", exactLookup);
			StringAssert.DoesNotContain("GetObjects()", exactLookup);
			StringAssert.Contains("ReferenceEquals(exact.CurrentZone, zone)", exactLookup);
		}

		[Test]
		public void ArchiveV12RetainsBoundedV8HappeningAuthorityAndOlderWritersStayExplicit()
		{
			string codec = Source(Path.Combine("Core", "KingdomArchivedSettlementCodec.cs"));
			StringAssert.Contains("public const int BehaviourVersion = 7;", codec);
			StringAssert.Contains("public const int PhysicalHappeningVersion = 8;", codec);
			StringAssert.Contains("public const int HappeningCursorVersion = 12;", codec);
			StringAssert.Contains("public const int CurrentVersion = HappeningCursorVersion;", codec);
			StringAssert.Contains("TryEncodeBehaviourV7ForTests", codec);
			StringAssert.Contains("HappeningModel", codec);
			string book = Source(Path.Combine("Simulation", "City", "KingdomCityBook.cs"));
			StringAssert.Contains("MaxHappeningModelChars", book);
			StringAssert.Contains("public string HappeningModel", book);
			StringAssert.Contains("public string ExtensionHappeningCursors", book);
			string extensionHost = Source(Path.Combine("Api", "KingdomExtensions.cs"));
			StringAssert.Contains("binding.AssemblyName, binding.TypeName", extensionHost);
			StringAssert.Contains("KingdomHappeningCursorRules.TrySeedLegacy(", extensionHost);
			StringAssert.Contains("notice.Tick > nowTick || notice.Tick <= sinceTick", extensionHost);
			string happenings = Source(Path.Combine("Simulation", "City", "KingdomHappenings.cs"));
			int legacyCapture = happenings.IndexOf("long legacySinceTick = book.LastExtensionTick;",
				System.StringComparison.Ordinal);
			Assert.GreaterOrEqual(legacyCapture, 0);
			Assert.Greater(happenings.IndexOf("book.LastExtensionTick = nowTick;",
				System.StringComparison.Ordinal), legacyCapture);

			string sourceKey;
			Assert.IsTrue(Api.KingdomHappeningCursorRules.TrySourceKey("fixture-owner",
				"Fixture.Assembly", "Fixture.Source", out sourceKey));
			string cursor;
			long since;
			Assert.IsTrue(Api.KingdomHappeningCursorRules.TryAdvance("", sourceKey, 100L,
				out since, out cursor));
			Assert.AreEqual(0L, since);
			string resumed;
			Assert.IsTrue(Api.KingdomHappeningCursorRules.TryRebaseAfterPause(cursor, 100L,
				1300L, out resumed));
			Assert.IsTrue(Api.KingdomHappeningCursorRules.TryAdvance(resumed, sourceKey, 1400L,
				out since, out cursor));
			Assert.AreEqual(1300L, since,
				"a source must never receive the master-paused happening window");
			Assert.IsFalse(Api.KingdomHappeningCursorRules.TryRebaseAfterPause(resumed, 1200L,
				1400L, out cursor), "a post-disable receipt must fail the atomic resume plan");
		}

		private static int Occurrences(string text, string token)
		{
			int count = 0;
			for (int at = 0; (at = text.IndexOf(token, at,
				System.StringComparison.Ordinal)) >= 0; at += token.Length) count++;
			return count;
		}
	}
}
#endif
