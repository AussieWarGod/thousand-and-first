#if TAF_TESTS
using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>Source contracts only: these inspect bridge boundaries, not engine execution,
	/// physical carrier recovery, native serialization, or save/load correctness.</summary>
	[TestFixture]
	public sealed class KingdomSubsidenceDepartureRuntimeSourceTests
	{
		private const string Growth = "Growth/KingdomGrowth.z16.Emigration.cs";
		private const string Begin = "Growth/KingdomResidentDepartureRuntime.Begin.cs";
		private const string Recovery = "Growth/KingdomResidentDepartureRuntime.Recovery.cs";
		private const string Effects = "Growth/KingdomResidentDepartureRuntime.Effects.cs";
		private const string Rollback = "Growth/KingdomResidentDepartureRuntime.Rollback.cs";
		private const string Bridge = "Growth/KingdomSubsidenceStepRuntime.Departures.cs";
		private const string Storage = "Growth/KingdomSubsidenceStepRuntime.Storage.cs";
		private const string Residents = "Simulation/City/KingdomResidents.07.DepartureRecovery.cs";

		[Test]
		public void SourceContractSubsidenceEntryRequiresExplicitStepIdWithoutCauseInference()
		{
			string entry = Method(Growth, "internal static bool EmigrateForSubsidence(");
			Has(entry, "KingdomSurvey Survey, string StepId, string Cause, bool Chronicled",
				"return !string.IsNullOrEmpty(StepId) && EmigrateCore(System, Zone, Survey,",
				"default(Simulation.City.KingdomResidentDestructionAuthorization), StepId);");
			Has(Method(Growth, "private static bool EmigrateCore("),
				"string SubsidenceStepId = null", "out string failure, SubsidenceStepId)");
			string association = Method(Bridge, "internal static bool TryAssociate(");
			Has(association, "string stepId", "if (stepId == null)", "if (associated) return false;",
				"owner.Step.Active?.Id != stepId", "KingdomSubsidenceStepRules.TryAssociate(owner.Step, departure,");
			StringAssert.DoesNotContain("Cause", TestMain.ReadRepositoryText(Bridge));
		}

		[Test]
		public void SourceContractCapturedDepartureAssociatesBeforeJournalAndBodyMarker()
		{
			string body = Method(Begin, "internal static bool TryBegin(");
			Ordered(body, "KingdomSubsidenceStepRuntime.TryRecoverOrphan(System, Body.CurrentZone, out Failure)",
				"!TryCapture(System, Body, residentId, Cause, Chronicled, Note,",
				"if (!KingdomSubsidenceStepRuntime.TryAssociate(System, SubsidenceStepId, operation, out Failure)) return false;",
				"System.ResidentDeparture = operation;", "new r_KingdomResidentDeparture", "Body.AddPart(marker);");
		}

		[Test]
		public void SourceContractFalseOrThrowingCarrierAttemptCreditsBeforeDiagnosticsAndAdvance()
		{
			string body = Method(Recovery, "private static bool TryContinue(");
			Ordered(body, "try { KingdomResidents.TryCompleteDepartureCarriers(System, Body, operation,",
				"catch (Exception ex)", "Failure = \"departure carrier completion threw \" + ex.GetType().Name;",
				"bool credited = KingdomSubsidenceStepRuntime.TryCredit(System, Body, operation, out string creditFailure);",
				"if (Failure != null) try { KingdomLog.LogError(Failure); } catch { }",
				"if (!credited || !ExactRemovedCitizenship(System, Body, operation)",
				"!KingdomResidents.DepartureCarriersAbsent(System, operation)",
				"!KingdomResidentTransitionAuthority.CanContinueJournaledCarrierRemoval(",
				"KingdomResidentDeparturePhase.CitizenshipRemoved, KingdomResidentDeparturePhase.CarriersRemoved)");
			string attempt = body.Substring(body.IndexOf("try { KingdomResidents.TryCompleteDepartureCarriers", StringComparison.Ordinal));
			attempt = attempt.Substring(0, attempt.IndexOf("bool credited =", StringComparison.Ordinal));
			ClassicAssert.IsFalse(Regex.IsMatch(attempt, @"\breturn\b"), "Carrier attempt must reach exact post-state credit.");
			Has(body, "if (!KingdomSubsidenceStepRuntime.TryCredit(System, Body, operation, out Failure)) return false;");
		}

		[Test]
		public void SourceContractCreditRequiresExactRemovedCarriersBeforeFrozenStageMeasurement()
		{
			string body = Method(Bridge, "internal static bool TryCredit(");
			Has(body, "!ReferenceEquals(system.ResidentDeparture, departure) || !GameObject.Validate(body)",
				"body.IDIfAssigned != departure.BodyObjectId || body.CurrentZone?.ZoneID != departure.ZoneId",
				"body.GetIntProperty(KingdomResidents.ResidentIdProperty) != departure.ResidentId",
				"body.GetPart<r_KingdomResidentDeparture>()?.Matches(departure, body) != true");
			Ordered(body, "!KingdomResidentDepartureRuntime.ExactRemovedCitizenship(system, body, departure)",
				"!KingdomResidents.DepartureCarriersAbsent(system, owner.City, departure.ResidentId)",
				"!owner.City.TryReadExact(", "!KingdomResidentRules.TryProject(state,", "if (!active.PendingCredited)",
				"active.FromStage, roll.Population, active.StorageCapacity)",
				"KingdomSubsidenceStepRules.TryCredit(owner.Step, departure.OperationId, reached,",
				"!Publish(system, owner, next)");
		}

		[Test]
		public void SourceContractRemovedCitizenshipProvesExactReceiptAndRestoredAllegiance()
		{
			string body = Method("Growth/KingdomResidentDepartureRuntime.Authority.cs",
				"internal static bool ExactRemovedCitizenship(");
			Has(body, "receipt == null || Body.Brain == null",
				"receipt.ReceiptVersion != KingdomCitizenshipRules.CurrentReceiptVersion",
				"receipt.Phase != KingdomCitizenshipPhase.Removed",
				"receipt.RemovalReason != (int)KingdomCitizenshipRemovalReason.Emigration",
				"!KingdomCitizenshipRules.ValidReceiptShape(", "receipt.BodyObjectId != Operation.BodyObjectId",
				"receipt.OwnerRealmId != Operation.RealmId", "receipt.OwnerSettlementId != Operation.SettlementId",
				"receipt.FactionId != System.KingdomFactionName", "Body.GetIntProperty(\"KingdomCitizen\") == 1",
				"allegiance.TryGetValue(receipt.FactionId, out value)",
				"KingdomCitizenshipRules.MatchesRemovalPost( receipt.PriorKind, receipt.PriorValue, present, value)");
		}

		[Test]
		public void SourceContractBodyDestructionRequiresParentAckBeforeAndAfterObliteration()
		{
			Ordered(Method(Effects, "private static bool TryDestroyBody("),
				"marker?.Matches(Operation, leaver) != true",
				"!KingdomSubsidenceStepRuntime.CanRetire(System, Operation, false)", "leaver.Obliterate();",
				"if (GameObject.Validate(leaver))",
				"if (!KingdomSubsidenceStepRuntime.TryRetireJournal(System, Operation, false, out Failure)) return false;");
			StringAssert.DoesNotContain("ResidentDeparture =", TestMain.ReadRepositoryText(Effects));
		}

		[Test]
		public void SourceContractZeroBodyZeroMarkerRecoveryClearsOnlyThroughParentAck()
		{
			string body = Method(Recovery, "internal static bool TryRecoverPending(");
			Ordered(body, "KingdomSubsidenceStepRuntime.TryRecoverOrphan(System, Zone, out Failure)",
				"!KingdomMarketHandoffGlobalIndex.TryLoaded(", "bool live = GameObject.Validate(item);",
				"if (bodies == 0 && markers == 0 && operation.Phase == (int)KingdomResidentDeparturePhase.EffectsPublished)",
				"return KingdomSubsidenceStepRuntime.TryRetireJournal(System, operation, false, out Failure);");
			Has(body, "marker.OperationId == operation.OperationId || marker.RealmId == operation.RealmId",
				"if (!live || item.IDIfAssigned != operation.BodyObjectId) continue;");
			StringAssert.DoesNotContain("KingdomResidentDepartureRules.Empty()", body);
		}

		[Test]
		public void SourceContractRollbackRequiresRestoredAuthorityAndExactMarkerAbsenceBeforeAck()
		{
			string body = Method(Rollback, "private static bool TryRollbackPrepared(");
			Ordered(body, "!KingdomCitizenship.BelongsTo(System, Body)", "bool polity = RollbackPolity(",
				"bool office = RollbackOffice(", "bool cook = RollbackCook(", "if (!polity || !office || !cook)",
				"CanPrepareJournaledRoles(", "CanPrepareResidentBodyDestruction(", "if (!authority)",
				"if (marker == null && !RequireMarker)",
				"return KingdomSubsidenceStepRuntime.TryRetireJournal(System, Operation, true, out Failure);",
				"if (marker == null || !marker.Matches(Operation, Body))", "Body.RemovePart(marker);",
				"if (Body.GetPart<r_KingdomResidentDeparture>() != null)",
				"if (!ExactRestoredAfterRemoval(System, Body, Operation))",
				"return KingdomSubsidenceStepRuntime.TryRetireJournal(System, Operation, true, out Failure);");
			StringAssert.DoesNotContain("ResidentDeparture =", body);
			Has(Method(Rollback, "private static bool ExactRestoredAfterRemoval("),
				"Body.IDIfAssigned == Operation.BodyObjectId", "Body.CurrentZone?.ZoneID == Operation.ZoneId",
				"KingdomCitizenship.BelongsTo(System, Body)", "CanPrepareResidentBodyDestruction(System,",
				"TryCaptureRoles(System, Body,", "SameCook(Operation.PriorCook, cook)",
				"SameOffice(Operation.PriorOffice, office)", "SamePolity(Operation.PriorPolity, polity)");
		}

		[Test]
		public void SourceContractExactJournalAckPrecedesClearAndDurableParentRelease()
		{
			Ordered(Method(Bridge, "internal static bool TryRetireJournal("),
				"!ReferenceEquals(system?.ResidentDeparture, departure)", "!TryMatch(system, departure,",
				"associated && owner.Step.Active.PendingCredited == rolledBack",
				"!rolledBack && !KingdomResidentDepartureRuntime.ExactTerminalAbsence(system, departure)",
				"system.ResidentDeparture = KingdomResidentDepartureRules.Empty();",
				"KingdomSubsidenceStepRules.TryReleaseRolledBack(owner.Step, departure.OperationId, out next)",
				"KingdomSubsidenceStepRules.TryReleaseRetired(owner.Step, departure.OperationId, out next)",
				"if (!released || !Publish(system, owner, next)) return false;");
		}

		[Test]
		public void SourceContractCreditedOrphanNeedsGlobalNoLiveBodyAndNoRealmMarkerProof()
		{
			Ordered(Method(Bridge, "internal static bool TryRecoverOrphan("),
				"!KingdomResidentDepartureRules.IsEmpty(system?.ResidentDeparture)", "!TryReadOwned(system,",
				"!KingdomMarketHandoffGlobalIndex.TryLoaded(out IList<GameObject> objects) || objects == null",
				"if (!GameObject.Validate(item)) continue;",
				"if (marker != null && (marker.RealmId == owner.Step.RealmId || marker.OperationId == active.PendingDepartureId)) return false;",
				"if (item.IDIfAssigned != active.PendingIdentity.BodyObjectId) continue;", "body = item; matches++;",
				"if (active.PendingCredited)", "if (matches != 0 || !KingdomResidents.DepartureCarriersAbsent(system, owner.City,",
				"active.PendingIdentity.ResidentId) || !KingdomSubsidenceStepRules.TryReleaseRetired(owner.Step,",
				"if (!Publish(system, owner, next)) return false;");
		}

		[Test]
		public void SourceContractUncreditedOrphanRequiresExactRestoredResidentBeforeRelease()
		{
			Ordered(Method(Bridge, "internal static bool TryRecoverOrphan("),
				"KingdomSubsidenceDepartureIdentity held = active.PendingIdentity;", "zone?.ZoneID != held.ZoneId",
				"system.SettlementIdForOwnedZone(held.ZoneId) != owner.City.SettlementId",
				"matches != 1 || body.CurrentZone?.ZoneID != held.ZoneId",
				"body.GetPart<r_KingdomResidentDeparture>() != null", "!KingdomCitizenship.BelongsTo(system, body)",
				"!KingdomResidentTransitionAuthority.CanPrepareResidentBodyDestruction(",
				"system, body, held.ResidentId, default(KingdomResidentDestructionAuthorization)",
				"!KingdomSubsidenceStepRules.TryReleaseRolledBack(owner.Step,", "!Publish(system, owner, next)");
		}

		[Test]
		public void SourceContractAllOwnedStepBooksAreUniqueReadableAndPublishedWithExactWireCas()
		{
			Has(Method(Storage, "private static bool TryReadOwned("),
				"books.Count != 1 + system.NonSeatSettlementCount", "books.Count > KingdomIdentityRules.MaxSettlements",
				"city == null || !references.Add(city) || !identities.Add(city.SettlementId)",
				"!KingdomIdentityRules.IsSettlementId(city.SettlementId)", "!city.HasValidSubsidenceStorage()",
				"!KingdomSubsidenceStepCodec.TryDecode(wire,", "step.RealmId != realm || step.SettlementId != city.SettlementId",
				"step.Active.PendingDepartureId != \"\" && ++pending > 1");
			Has(Method(Storage, "private static bool TryMatch("), "!TryReadOwned(system,",
				"item.City.SettlementId != departure.SettlementId", "active.PendingDepartureId != departure.OperationId",
				"!active.PendingIdentity.Matches(departure)", "active.Phase == KingdomSubsidenceStepPhase.Quarantined");
			Ordered(Method(Storage, "private static bool Publish("), "!TryReadOwned(system,",
				"if (!ReferenceEquals(item.City, prior.City)) continue;", "if (item.Wire != prior.Wire || !KingdomResidentDeathRuntime.CanProceed(system, out _)) return false;",
				"item.City.SubsidenceModel = wire;", "return item.City.SubsidenceModel == wire;");
		}

		[Test]
		public void SourceContractGlobalCarrierAbsenceAuditsBindingAndEveryUniqueReadableOwnedBook()
		{
			string body = Method(Residents, "internal static bool DepartureCarriersAbsent(KingdomSystem System,\n\t\t\tKingdomCityBook Book,");
			Has(body, "System != null && KingdomIdentityRules.IsRealmId(System.CurrentRealmId)",
				"KingdomResidentCarrierAbsenceRules.ProvesAbsent(Book, ResidentId, System.Bindings,",
				"System.OwnedCityBooks(), 1 + System.NonSeatSettlementCount)");
			StringAssert.DoesNotContain("TryRead(", TestMain.ReadRepositoryText(Residents));
			StringAssert.DoesNotContain("Normalize(", TestMain.ReadRepositoryText(Residents));
		}

		[Test]
		public void SourceContractDepartureProjectionPropagatesExactReadWithoutChangingLegacyDefaults()
		{
			Ordered(Method(Residents, "internal static bool TryCompleteDepartureCarriers("),
				"ProjectCompatibility(System, Exact: true);",
				"return DepartureCarriersAbsent(System, intended, Operation.ResidentId);");
			Has(Method("Simulation/City/KingdomResidents.04.ResidentTransitionsAndAccession.cs",
				"internal static bool TryDepart("), "ProjectCompatibility(System, Exact: true);");
			const string projection = "Simulation/City/KingdomResidents.00.IdentityAndRoll.cs";
			Has(Method(projection, "internal static bool ProjectCompatibility(KingdomSystem System,"),
				"bool Exact = false", "ProjectCompatibility(System.City, out seatRoll, Exact)",
				"ProjectCompatibility(row, Exact)");
			Has(Method(projection, "internal static bool ProjectCompatibility(KingdomSettlement Settlement,"),
				"bool Exact = false", "ProjectCompatibility(Settlement.City, out KingdomResidentRollProjection roll, Exact)");
			Has(Method(projection, "internal static bool ProjectCompatibility(KingdomCityBook Book,"),
				"bool Exact = false", "Exact ? Book.TryReadExact(out state, out fault) : Book.TryRead(out state, out fault)",
				"KingdomResidentRules.TryProject(state, out Roll)");
		}

		[Test]
		public void SourceContractExistingPositionalDepartureFieldShapeRemainsUnextended()
		{
			string source = TestMain.ReadRepositoryText("Growth/KingdomResidentDepartureOperation.cs");
			string[] fields = Regex.Matches(source,
				@"(?m)^\s*public\s+([A-Za-z_]\w*)\s+([A-Za-z_]\w*)\s*(?:=(?!>)[^;\r\n]*)?;")
				.Cast<Match>().Select(m => m.Groups[1].Value + " " + m.Groups[2].Value).ToArray();
			CollectionAssert.AreEqual(new[] {
				"int Version", "int Phase", "long Revision", "string OperationId", "string RealmId",
				"string SettlementId", "int ResidentId", "string BodyObjectId", "string ZoneId",
				"string ResidentName", "string Origin", "long PreparedTick", "int DeparturesBefore",
				"bool Chronicled", "string ChronicleLine", "string LedgerLine", "string Cause",
				"KingdomNamedCookReceipt PriorCook", "KingdomCivicOfficeReceipt PriorOffice",
				"KingdomPolityNamedFigureRecord PriorPolity", "string PolityConclusionRef", "int AuthorizationKind",
				"string AuthorizationEventId", "string AuthorizationOwnerObjectId", "string AuthorizationCauseDigest"
			}, fields);
			StringAssert.Contains("public const int CurrentVersion = 1;", source);
		}

		private static string Flat(string source) => Regex.Replace(source, @"\s+", " ");
		private static void Has(string source, params string[] needles)
		{ foreach (string needle in needles) StringAssert.Contains(needle, source); }
		private static void Ordered(string source, params string[] needles)
		{
			int previous = -1;
			foreach (string needle in needles)
			{
				int current = source.IndexOf(needle, previous + 1, StringComparison.Ordinal);
				Assert.That(current, Is.GreaterThan(previous), needle);
				previous = current;
			}
		}
		private static string Method(string path, string signature)
		{
			string source = Flat(TestMain.ReadRepositoryText(path));
			int start = source.IndexOf(Flat(signature), StringComparison.Ordinal);
			Assert.That(start, Is.GreaterThanOrEqualTo(0), path + ": " + signature);
			int open = source.IndexOf('{', start), depth = 0;
			for (int i = open; i < source.Length; i++)
			{
				if (source[i] == '{') depth++;
				else if (source[i] == '}' && --depth == 0) return Flat(source.Substring(start, i - start + 1));
			}
			Assert.Fail("Unclosed source method: " + path + ": " + signature);
			return null;
		}
	}
}
#endif
