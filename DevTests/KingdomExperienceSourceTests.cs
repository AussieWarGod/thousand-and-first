#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomExperienceSourceTests
	{
		private static string Read(string relative)
		{
			return TestMain.ReadRepositoryText(relative);
		}

		[Test]
		public void CompositeUsesBoundedExplicitCodecBeforeAllocatingRows()
		{
			string state = Read(Path.Combine("Experience", "KingdomExperienceState.cs"));
			StringAssert.Contains(": IComposite", state);
			StringAssert.Contains("KingdomExperienceCodec.MaxEnvelopeBytes", state);
			StringAssert.Contains("KingdomExperienceCodec.DecodeEnvelopeRaw(envelope)", state);
			StringAssert.Contains("ReadBytesDirect(length)", state);
			string codec = Read(Path.Combine("Experience", "KingdomExperienceCodec.cs"));
			StringAssert.Contains("MaxEnvelopeBytes = 24 * 1024", codec);
			StringAssert.Contains("OpaqueFuturePayload", codec);
			StringAssert.Contains("SchemaState = KingdomExperienceSchemaState.Unknown", codec);
		}

		[Test]
		public void RuntimeIsNarrowAndNeverLoadsRemoteGroundOrPaysRewards()
		{
			string runtime = Read(Path.Combine("Experience", "KingdomExperienceRuntime.cs"));
			string capacity = Read(Path.Combine("Experience",
				"KingdomExperienceRuntime.FoundationCapacity.cs"));
			string combined = runtime + capacity;
			StringAssert.Contains("System.Bindings.TryRead", capacity);
			StringAssert.Contains("System.Jobs.TryRead", capacity);
			StringAssert.Contains("row.Kind != KingdomJobKind.Delivery", capacity);
			StringAssert.Contains("row.DeliveryTripId > 0", capacity);
			StringAssert.DoesNotContain("KingdomJobKind.Expedition", capacity);
			StringAssert.DoesNotContain("GetZone", combined);
			StringAssert.DoesNotContain("ZoneManager", combined);
			StringAssert.DoesNotContain("GameObjectFactory", combined);
			StringAssert.DoesNotContain("AddXP", combined);
			StringAssert.DoesNotContain("Award", combined);
			StringAssert.DoesNotContain("Reputation", combined);
			StringAssert.DoesNotContain("JournalAPI", combined);
			StringAssert.DoesNotContain("Random", combined);
			StringAssert.Contains("System.TryFindSettlement(SettlementId", runtime);
			StringAssert.Contains("string.Equals(RealmId, System.RealmId", runtime);
		}

		[Test]
		public void ReservationRuntimeChecksMasterBeforeAuthorityOrTopology()
		{
			string runtime = Read(Path.Combine("Experience", "KingdomExperienceRuntime.cs"));
			int prepare = runtime.IndexOf("private static bool PrepareReservation(",
				StringComparison.Ordinal);
			int release = runtime.IndexOf("public static bool TryReleaseAudience(",
				StringComparison.Ordinal);
			Assert.Greater(prepare, 0); Assert.Greater(release, prepare);
			string slice = runtime.Substring(prepare, release - prepare);
			Assert.Less(slice.IndexOf("KingdomMaster.NewWorkAllowed(System)",
				StringComparison.Ordinal), slice.IndexOf("System.TryFindSettlement(SettlementId",
				StringComparison.Ordinal));
			Assert.Less(slice.IndexOf("System.TryFindSettlement(SettlementId",
				StringComparison.Ordinal), slice.IndexOf("TryObserveConfiguredOptions(System, Tick",
				StringComparison.Ordinal));
		}

		[Test]
		public void MasterAndTelemetryOptionsPrecedeEverySessionAllocationOrWrite()
		{
			string runtime = Read(Path.Combine("Experience", "KingdomExperienceRuntime.cs"));
			int record = runtime.IndexOf("public static bool TryRecord", StringComparison.Ordinal);
			int export = runtime.IndexOf("public static bool TryExport", StringComparison.Ordinal);
			Assert.Greater(record, 0); Assert.Greater(export, record);
			string recordSlice = runtime.Substring(record, export - record);
			Assert.Less(recordSlice.IndexOf("KingdomMaster.NewWorkAllowed", StringComparison.Ordinal),
				recordSlice.IndexOf("Options.GetOption(TelemetryOptionId", StringComparison.Ordinal));
			Assert.Less(recordSlice.IndexOf("Options.GetOption(TelemetryOptionId", StringComparison.Ordinal),
				recordSlice.IndexOf("new KingdomExperienceTelemetryBuffer", StringComparison.Ordinal));
			string exportSlice = runtime.Substring(export);
			Assert.Less(exportSlice.IndexOf("Options.GetOption(TelemetryOptionId", StringComparison.Ordinal),
				exportSlice.IndexOf("File.WriteAllText", StringComparison.Ordinal));
			StringAssert.Contains("private const string ExportFile = \"experience-session.tsv\"",
				runtime);
		}

		[Test]
		public void OptionsAreExplicitAndTelemetryDefaultsOff()
		{
			XDocument options = XDocument.Parse(Read(Path.Combine("RuntimeData", "Options.xml")));
			Dictionary<string, XElement> rows = options.Root.Elements("option")
				.ToDictionary(x => (string)x.Attribute("ID"), StringComparer.Ordinal);
			Assert.AreEqual("Yes", (string)rows[KingdomExperienceOptions.StoryOptionId]
				.Attribute("Default"));
			Assert.AreEqual("Yes", (string)rows[KingdomExperienceOptions.KnowledgeOptionId]
				.Attribute("Default"));
			Assert.AreEqual("Yes", (string)rows[KingdomExperienceOptions.AmbientOptionId]
				.Attribute("Default"));
			Assert.AreEqual("No", (string)rows[KingdomExperienceOptions.TelemetryOptionId]
				.Attribute("Default"));
			StringAssert.Contains("no catch-up backlog",
				(string)rows[KingdomExperienceOptions.StoryOptionId].Attribute("DisplayText"));
		}

		[Test]
		public void LoaderAndSystemNormalizationOwnOneW0ReconciliationSeam()
		{
			string loader = Read(Path.Combine("Core", "KingdomLoader.cs"));
			StringAssert.Contains("KingdomExperienceRuntime.TryObserveConfiguredOptions", loader);
			int gate = loader.IndexOf("KingdomLoadReconciliationRules.Select(",
				StringComparison.Ordinal);
			StringAssert.Contains("founded && KingdomMaster.NewWorkAllowed(kingdomSystem)",
				loader);
			StringAssert.Contains("loadMode == KingdomLoadReconciliationMode.Full", loader);
			int foundation = loader.IndexOf("KingdomPolityRuntime.TryEnsureFoundation",
				StringComparison.Ordinal);
			int experience = loader.IndexOf(
				"KingdomExperienceRuntime.TryObserveConfiguredOptions", StringComparison.Ordinal);
			int committed = loader.IndexOf(
				"KingdomPolityActiveRuntime.TryReconcileCommittedCapacity",
				StringComparison.Ordinal);
			Assert.Greater(gate, 0); Assert.Greater(foundation, gate);
			Assert.Greater(experience, foundation); Assert.Greater(committed, experience);
			string normalization = Read(Path.Combine("Core",
				"KingdomSystem.z24.Normalization.Collections.cs"));
			StringAssert.Contains("KingdomExperienceRules.Normalize(Experience)", normalization);
			StringAssert.Contains("TryRebindEmptyIdentity(Experience, RealmId", normalization);
			string state = Read(Path.Combine("Core", "KingdomSystem.z02d.State.Experience.cs"));
			StringAssert.Contains("public KingdomExperienceLedger Experience", state);
		}

		[Test]
		public void CapacityConstantsStayPinnedToFoundationAndLivingCitySources()
		{
			string rules = Read(Path.Combine("Experience", "KingdomExperienceRules.Validation.cs"));
			StringAssert.Contains("MaxSettlements = 3", rules);
			StringAssert.Contains("MaxTransientBodySlots = 16", rules);
			StringAssert.Contains("MaxBodiesPerReservation = 7", rules);
			StringAssert.Contains("MaxAudienceReceipts = MaxSettlements", rules);
			StringAssert.Contains("AudienceRowByteBudget = 444", rules);
			StringAssert.Contains("BodyReservationRowByteBudget = 444", rules);
			StringAssert.Contains("VoiceRowByteBudget = 960", rules);
			StringAssert.Contains("MaxTransientBindings = KingdomCityMemoryRules.MaxOpenJobs",
				Read(Path.Combine("Simulation", "City", "KingdomBindingRegistry.Table.cs")));
			StringAssert.Contains("MaxOpenJobs = 16",
				Read(Path.Combine("Simulation", "City", "KingdomCityMemoryRules.cs")));
			StringAssert.Contains("MaxCohortMembers = 7",
				Read(Path.Combine("Polity", "KingdomPolityRules.ValidationCore.cs")));
		}

		[Test]
		public void EmbodiedPresentationUsesOneAtomicAudienceAndBodyCommit()
		{
			string rules = Read(Path.Combine("Experience",
				"KingdomExperienceRules.Presentation.cs"));
			StringAssert.Contains("TryReservePresentation", rules);
			StringAssert.Contains("SamePresentation(Audience, Bodies)", rules);
			StringAssert.Contains("AudienceCapacityAvailable", rules);
			StringAssert.Contains("BodyCapacityAvailable", rules);
			StringAssert.Contains("KingdomExperienceLedger candidate = Clone(Ledger)", rules);
			int release = rules.IndexOf("public static bool TryReleasePresentation",
				StringComparison.Ordinal);
			Assert.Greater(release, 0);
			Assert.AreEqual(1, Count(rules.Substring(0, release),
				"Ledger.CopyFrom(candidate)"));
			string runtime = Read(Path.Combine("Experience", "KingdomExperienceRuntime.cs"));
			StringAssert.Contains("public static bool TryReservePresentation", runtime);
			StringAssert.Contains("KingdomExperienceRules.TryReservePresentation", runtime);
			StringAssert.Contains("TryCountProtectedFoundationBodies", runtime);
		}

		[Test]
		public void OptionObservationCannotEraseFrozenBodyAccounting()
		{
			string options = Read(Path.Combine("Experience",
				"KingdomExperienceRules.Options.cs"));
			StringAssert.DoesNotContain("DropDisabledLeases", options);
			StringAssert.DoesNotContain("BodyReservations.Remove", options);
			StringAssert.DoesNotContain("Audiences.Remove", options);
			string lifecycle = Read(Path.Combine("Experience",
				"KingdomExperienceRules.LeaseLifecycle.cs"));
			StringAssert.Contains("KingdomExperienceLeaseState.Retirement", lifecycle);
			StringAssert.Contains("Receipt = Copy", lifecycle);
			string retirement = Read(Path.Combine("Experience",
				"KingdomExperienceRules.Retirement.cs"));
			string recovery = Read(Path.Combine("Experience",
				"KingdomExperienceRules.RecoveryCore.cs"));
			StringAssert.Contains("TryRecoverRetirementBodies", retirement);
			StringAssert.Contains("TryRecoverRetirementPresentation", retirement);
			StringAssert.Contains("TryRecoverDurableBodies", retirement);
			StringAssert.Contains("TryRecoverDurablePresentation", retirement);
			StringAssert.Contains("BodyCapacityAvailable", recovery);
			StringAssert.Contains("AudienceCapacityAvailable", recovery);
			StringAssert.DoesNotContain("GetZone", retirement + recovery);
			StringAssert.DoesNotContain("GameObject", retirement + recovery);
			string runtime = Read(Path.Combine("Experience", "KingdomExperienceRuntime.cs"));
			StringAssert.Contains("TryRecoverRetirementBodies", runtime);
			StringAssert.Contains("TryRecoverRetirementPresentation", runtime);
			StringAssert.Contains("TryRecoverDurableBodies", runtime);
			StringAssert.Contains("TryRecoverDurablePresentation", runtime);
		}

		[Test]
		public void DurableRecoveryBypassesNewWorkTopologyAndOptionObservation()
		{
			string runtime = Read(Path.Combine("Experience", "KingdomExperienceRuntime.cs"));
			int prepare = runtime.IndexOf("private static bool PrepareRecovery(",
				StringComparison.Ordinal);
			int reservation = runtime.IndexOf("private static bool PrepareReservation(",
				StringComparison.Ordinal);
			Assert.Greater(prepare, 0); Assert.Greater(reservation, prepare);
			string slice = runtime.Substring(prepare, reservation - prepare);
			StringAssert.DoesNotContain("KingdomMaster.NewWorkAllowed", slice);
			StringAssert.DoesNotContain("TryFindSettlement", slice);
			StringAssert.DoesNotContain("TryObserveConfiguredOptions", slice);
			StringAssert.DoesNotContain("GetZone", slice);
			StringAssert.DoesNotContain("GameObject", slice);
		}

		[Test]
		public void EveryDeliveryOpenReservesSharedCapacityBeforePublication()
		{
			string city = Path.Combine(TestMain.RepositoryRoot, "Simulation", "City");
			string[] files = Directory.GetFiles(city, "KingdomCentralLogistics.*.cs")
				.Concat(Directory.GetFiles(city, "KingdomPorters.*.cs")).ToArray();
			int opens = 0, fences = 0;
			for (int i = 0; i < files.Length; i++)
			{
				string source = File.ReadAllText(files[i]);
				int start = 0;
				while ((start = source.IndexOf(".TryOpen(", start,
					StringComparison.Ordinal)) >= 0)
				{
					opens++;
					Assert.Greater(source.LastIndexOf(
						"TryAdmitNewFoundationTransientClaims", start,
						StringComparison.Ordinal), -1, Path.GetFileName(files[i]));
					start += 9;
				}
				fences += Count(source, "TryAdmitNewFoundationTransientClaims");
			}
			Assert.AreEqual(4, opens, "new delivery opening seam needs source-gate review");
			Assert.AreEqual(opens, fences, "every delivery opening needs exactly one preflight");
		}

		[Test]
		public void ReverseAdmissionFenceAndMintRollbackPreserveOneSharedUnion()
		{
			string inspect = Read(Path.Combine("Simulation", "City",
				"KingdomResidents.01.BindingInspection.cs"));
			string mutation = Read(Path.Combine("Simulation", "City",
				"KingdomResidents.02.BindingMutation.cs"));
			string mint = Read(Path.Combine("Simulation", "City",
				"KingdomPorters.02.CarrierRendering.cs"));
			StringAssert.Contains("FoundationOwnsCarrierClaim", inspect);
			StringAssert.Contains("TryAdmitFoundationTransientClaim", inspect);
			StringAssert.Contains("FoundationOwnsCarrierClaim", mutation);
			StringAssert.Contains("TryAdmitFoundationTransientClaim", mutation);
			int bind = mint.IndexOf("if (!KingdomResidents.Bind", StringComparison.Ordinal);
			int obliterate = mint.IndexOf("body.Obliterate", StringComparison.Ordinal);
			int refused = mint.IndexOf("return null", bind, StringComparison.Ordinal);
			Assert.Greater(bind, 0); Assert.Greater(obliterate, bind);
			Assert.Greater(refused, obliterate);
		}

		[Test]
		public void MasterResumeUsesExactEnvelopeCasAndPublishesBeforeLatch()
		{
			string rules = Read(Path.Combine("Experience",
				"KingdomExperienceRules.MasterResume.cs"));
			StringAssert.Contains("SourceEnvelope", rules);
			StringAssert.Contains("Exact(Ledger, Plan.SourceEnvelope)", rules);
			StringAssert.Contains("RowsPredatePause", rules);
			StringAssert.Contains("ReservedTick > DisabledAt", rules);
			StringAssert.Contains("Equal-tick authority may have committed", rules);
			StringAssert.Contains("CanPublishMasterResume", rules);
			StringAssert.Contains("PublishMasterResumePrevalidated", rules);
			string master = Read(Path.Combine("Core", "KingdomMaster.cs"));
			string atomic = Read(Path.Combine("Core", "KingdomMaster.ResumeAtomicity.cs"));
			StringAssert.Contains("KingdomJobTable.Exact(current, Jobs)", atomic);
			StringAssert.Contains("KingdomArchivedSettlementCodec.TryEncode", atomic);
			StringAssert.Contains("Trade.MatchesSource", atomic);
			StringAssert.Contains("Construction.CanPublish", atomic);
			StringAssert.Contains("Sources.ExperienceMatches", atomic);
			StringAssert.Contains("KingdomPolityRules.CanPublishMasterResume", atomic);
			StringAssert.Contains("KingdomMasterPublicationGate.TryOpen(matches, count, -1",
				atomic);
			int preflight = master.IndexOf("if (!Preflight(out _)) return false;",
				StringComparison.Ordinal);
			int jobs = atomic.IndexOf("System.Jobs.PublishPrevalidated(ConstructionRoutes)",
				StringComparison.Ordinal);
			int experience = atomic.IndexOf(
				"KingdomExperienceRules.PublishMasterResumePrevalidated", StringComparison.Ordinal);
			int polity = atomic.IndexOf("KingdomPolityRules.PublishMasterResumePrevalidated",
				StringComparison.Ordinal);
			Assert.Greater(preflight, 0); Assert.Greater(jobs, 0);
			Assert.Greater(experience, jobs); Assert.Greater(polity, experience);
			StringAssert.DoesNotContain("TryPublishMasterResume(", atomic);
			int planPublish = master.IndexOf("if (!plan.Publish()) return false;",
				StringComparison.Ordinal);
			int latch = master.IndexOf("KingdomMasterRules.ApplyResume(decision)",
				StringComparison.Ordinal);
			Assert.Greater(planPublish, 0); Assert.Greater(latch, 0);
			Assert.Less(latch, planPublish,
				"applied latch decision must be staged before infallible owner publication");
		}

		[Test]
		public void ExportExistsOnlyBehindExplicitDiagnosticCommand()
		{
			string wish = Read(Path.Combine("Debug", "KingdomExperienceWishes.cs"));
			StringAssert.Contains("[WishCommand(\"kingdom:experience-export\")]", wish);
			StringAssert.Contains("KingdomExperienceRuntime.TryExport", wish);
			string loader = Read(Path.Combine("Core", "KingdomLoader.cs"));
			StringAssert.DoesNotContain("TryExport", loader);
			string events = Read(Path.Combine("Core", "KingdomSystem.z20.Events.cs"));
			StringAssert.DoesNotContain("TryExport", events);
		}

		[Test]
		public void W0ProductionFilesStayBelowThreeHundredLines()
		{
			string[] files = new string[]
			{
				"KingdomExperienceEnums.cs", "KingdomExperienceOptions.cs",
				"KingdomExperienceState.cs",
				"KingdomExperienceRules.Validation.cs",
				"KingdomExperienceRules.IdentityAndClone.cs",
				"KingdomExperienceRules.Options.cs", "KingdomExperienceRules.Capacity.cs",
				"KingdomExperienceRules.CapacityHelpers.cs",
				"KingdomExperienceRules.Presentation.cs",
				"KingdomExperienceRules.LeaseLifecycle.cs",
				"KingdomExperienceRules.Retirement.cs",
				"KingdomExperienceRules.RecoveryCore.cs",
				"KingdomExperienceRules.MasterResume.cs",
				"KingdomSharedBodyCapacityRules.cs", "KingdomExperienceCodec.cs",
				"KingdomExperienceCodec.Primitives.cs", "KingdomExperienceCodec.Payload.cs",
				"KingdomExperienceTelemetry.cs", "KingdomExperienceTelemetry.Export.cs",
				"KingdomExperienceRuntime.cs",
				"KingdomExperienceRuntime.FoundationCapacity.cs"
			};
			for (int i = 0; i < files.Length; i++)
			{
				string text = Read(Path.Combine("Experience", files[i]));
				int lines = text.Split(new char[] { '\n' }).Length;
				Assert.Less(lines, 300, files[i] + " has " + lines + " lines");
			}
			string loadRules = Read(Path.Combine("Core",
				"KingdomLoadReconciliationRules.cs"));
			Assert.Less(loadRules.Split(new char[] { '\n' }).Length, 300);
			string[] shared = new string[]
			{
				Path.Combine("Core", "KingdomMaster.cs"),
				Path.Combine("Core", "KingdomMaster.ResumeAtomicity.cs"),
				Path.Combine("Core", "KingdomMasterRecoveryPlans.cs"),
				Path.Combine("Core", "KingdomMasterPublicationGate.cs"),
				Path.Combine("Growth", "KingdomConstruction.MasterPause.cs"),
				Path.Combine("Simulation", "City", "KingdomJobRegistry.z12.RegistryPersistence.cs"),
				Path.Combine("Simulation", "City", "KingdomJobRegistry.z15.TableExact.cs")
			};
			for (int i = 0; i < shared.Length; i++)
			{
				string text = Read(shared[i]);
				Assert.Less(text.Split(new char[] { '\n' }).Length, 300,
					shared[i] + " exceeds source-size boundary");
			}
		}

		[Test]
		public void W0DoesNotContainFeatureDescendantsOrRejectedMachinery()
		{
			string combined = Read(Path.Combine("Experience", "KingdomExperienceState.cs"))
				+ Read(Path.Combine("Experience", "KingdomExperienceRules.Capacity.cs"))
				+ Read(Path.Combine("Experience", "KingdomExperienceRuntime.cs"));
			string[] banned = new string[]
			{
				"FirstFeastReceipt", "FirstGuestReceipt", "CuratorReceipt", "DecisionTag",
				"StoryDirector", "Mood", "Favorite", "RemoteActor", "Conquest", "PassiveYield"
			};
			for (int i = 0; i < banned.Length; i++)
				StringAssert.DoesNotContain(banned[i], combined, banned[i]);
		}

		private static int Count(string Text, string Needle)
		{
			int count = 0, start = 0;
			while ((start = Text.IndexOf(Needle, start, StringComparison.Ordinal)) >= 0)
			{
				count++; start += Needle.Length;
			}
			return count;
		}
	}
}
#endif
