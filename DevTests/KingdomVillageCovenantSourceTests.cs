#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The claims about D9 that only the source can settle.
	/// <para>
	/// Three of them cannot be reached from a pure test at all. The order of the founding cut lives
	/// inside a method that needs a running game to call; the promise that one lease spans a whole
	/// recording is about which call appears where rather than about any value; and a sweep for
	/// things this family must never touch is only meaningful over every line of it, including the
	/// lines no test happens to exercise. So they are read.
	/// </para>
	/// </summary>
	[TestFixture]
	public sealed class KingdomVillageCovenantSourceTests
	{
		private static string Source(string relative)
		{
			return TestMain.ReadRepositoryText(relative);
		}

		/// <summary>
		/// The file with its comments and string literals removed, so a sweep for a forbidden call
		/// cannot be tripped by prose that merely names it &mdash; including the prose in this very
		/// family explaining why the call is forbidden.
		/// </summary>
		private static string Code(string text)
		{
			StringBuilder code = new StringBuilder(text.Length);
			for (int i = 0; i < text.Length; i++)
			{
				if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '/')
				{
					while (i < text.Length && text[i] != '\n') i++;
					code.Append('\n');
					continue;
				}
				if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '*')
				{
					i += 2;
					while (i + 1 < text.Length && !(text[i] == '*' && text[i + 1] == '/')) i++;
					i++;
					continue;
				}
				if (text[i] == '"')
				{
					i++;
					while (i < text.Length && text[i] != '"')
					{
						if (text[i] == '\\') i++;
						i++;
					}
					code.Append("\"\"");
					continue;
				}
				code.Append(text[i]);
			}
			return code.ToString();
		}

		private static string Method(string text, string signature)
		{
			int start = text.IndexOf(signature, StringComparison.Ordinal);
			Assert.Greater(start, -1, "cannot find " + signature);
			int next = text.IndexOf("\n\t\t/// <summary>", start + signature.Length,
				StringComparison.Ordinal);
			int alternative = text.IndexOf("\n\t\tpublic ", start + signature.Length,
				StringComparison.Ordinal);
			if (next < 0 || (alternative >= 0 && alternative < next)) next = alternative;
			if (next < 0) next = text.Length;
			return text.Substring(start, next - start);
		}

		private static int At(string text, string needle)
		{
			int index = text.IndexOf(needle, StringComparison.Ordinal);
			Assert.Greater(index, -1, "cannot find " + needle);
			return index;
		}

		private static int Occurrences(string text, string needle)
		{
			int count = 0;
			for (int i = text.IndexOf(needle, StringComparison.Ordinal); i >= 0;
				i = text.IndexOf(needle, i + needle.Length, StringComparison.Ordinal)) count++;
			return count;
		}

		private static List<string> Owned()
		{
			List<string> files = new List<string>();
			foreach (string path in Directory.GetFiles(
				Path.Combine(TestMain.RepositoryRoot, "Core"), "KingdomVillageCovenant*.cs"))
				files.Add("Core/" + Path.GetFileName(path));
			files.Sort(StringComparer.Ordinal);
			return files;
		}

		// ---- the founding cut ------------------------------------------------------------

		/// <summary>
		/// The archive is asked before the water moves. A rite that seals a covenant and then finds
		/// it has nowhere to write it down has already spent the founder's drams on a record nobody
		/// can produce afterwards.
		/// </summary>
		[Test]
		public void TheArchiveIsConsultedBeforeAnyWaterIsDebited()
		{
			string begin = Source("Core/KingdomFoundingTransaction.10Begin.cs");
			int preflight = At(begin, "VillageCovenantPreflight(system, Kind, transaction,");
			int authority = At(begin, "string encodedAuthority =");
			int drain = At(begin, "KingdomLiquids.Drain(vessel,");
			int committed = At(begin, "Basin.PendingPhase = KingdomFoundingPhase.WaterCommitted;");
			Assert.Greater(preflight, authority,
				"the candidate cannot be shaped before its authority exists");
			Assert.Less(preflight, At(begin, "TryStageFoundingReceipt(Basin, Actor, Site, vessel,"),
				"nothing is staged until this covenant is known to be recordable");
			string cut = Source("Core/KingdomVillageCovenantFoundingCut.cs");
			StringAssert.Contains("KingdomVillageCovenantRuntime.TryPreflight(System,", cut);
			StringAssert.Contains("KingdomFoundingWaterDisposition.Untouched", cut);
			Assert.Less(preflight, committed,
				"the archive is consulted before any durable intent to spend water");
			Assert.Less(preflight, drain, "the archive is consulted before the drain");

		}

		/// <summary>
		/// The exact order of the cut, and every step of it is load-bearing.
		/// <para>
		/// The covenant's standing must already be exact and its chronicle entry must already be
		/// terminal, or the archive would record a rite that had not happened. The seal, the
		/// completion and the reservation cleanup must not have run, or a failure to write the
		/// record would arrive after the receipt that paid for it was cleared away.
		/// </para>
		/// </summary>
		[Test]
		public void TheCovenantIsArchivedAfterItsStandingAndChronicleAndBeforeItsSeal()
		{
			string effect = Source(
				"Core/KingdomFoundingTransaction.15aVillageStandingEffect.cs");
			string village = Source("Core/KingdomFoundingTransaction.15IdentityAndVillage.cs");
			string publish = Method(village,
				"private static void PublishVillageCharter(r_FounderBasin Basin, Zone Site,");
			int intent = At(effect, "basin.PendingVillageEffectBefore = before;");
			int prepared = At(effect, "VillageStandingEffectPrepared;");
			int standing = At(effect, "system.TrySetRegardForRealm(");
			int applied = At(effect, "VillageStandingEffectApplied;");
			int ensure = At(publish, "EnsureVillageStandingEffectApplied(Basin, Site, system);");
			int published = At(publish,
				"Basin.PendingPhase = KingdomFoundingPhase.PublicationCommitted;");
			int chronicle = At(publish, "RecordChronicleOnce(system, Basin.PendingChronicleEventID,");
			int readback = At(publish, "ChronicleAccomplishmentObserved(Basin.PendingChronicleEventID,");
			int archived = At(publish, "KingdomVillageCovenantRuntime.TryRecord(system,");
			int seal = At(publish, "KingdomSeal.TryStageSemanticSnapshot(\"village charter\"");

			Assert.Less(intent, prepared,
				"exact pair and digest are written before the Prepared state");
			Assert.Less(prepared, standing, "write-ahead intent precedes standing mutation");
			Assert.Less(standing, applied,
				"the after pair exists before its Applied marker");
			Assert.Less(ensure, published);
			Assert.Less(published, chronicle, "the durable redo barrier precedes the chronicle");
			Assert.Less(chronicle, readback);
			Assert.Less(readback, archived,
				"nothing is archived until the chronicle outbox has been read back terminal");
			Assert.Less(archived, seal, "the record is durable before the rite is sealed");
			StringAssert.Contains("was not durably archived", publish,
				"a failure to archive must throw, so the paid receipt is retained for recovery");
		}

		[Test]
		public void PreexistingStandingAndThePostWriteCutCannotMasqueradeAsPublication()
		{
			string effect = Source(
				"Core/KingdomFoundingTransaction.15aVillageStandingEffect.cs");
			string validation = Source(
				"Core/KingdomFoundingTransaction.17aVillageEffectValidation.cs");
			string rules = Source("Core/KingdomFoundingTransactionRules.AuthorityCodec.cs");
			StringAssert.Contains("Preexisting village regard cannot be attributed", effect);
			StringAssert.Contains("(long)Before * KingdomStandingRules.FractionScale + BeforeCarry >=",
				rules);
			StringAssert.Contains("VillageStandingEffectPrepared", validation);
			StringAssert.Contains("phase == KingdomFoundingPhase.PublicationCommitted", validation,
				"standing-after/marker-before cut must remain resumable");
			StringAssert.Contains("current == after && currentCarry == afterCarry", effect,
				"only the receipt's exact after pair can prove the cut");
			StringAssert.Contains(
				"VillageStandingEffectCut(\"village-standing:prepared\")", effect,
				"pre-write failure cut is explicit");
			StringAssert.Contains(
				"VillageStandingEffectCut(\"village-standing:standing\")", effect,
				"post-write/pre-marker recovery cut is explicit");
			string identity = Method(effect,
				"private static bool ExactVillagePublicationIdentity(r_FounderBasin basin,");
			StringAssert.Contains("village.DisplayName == basin.PendingVillageDisplayName", identity);
			StringAssert.Contains("site.GetZoneProperty(\"faction\", null) ==",
				identity);
			StringAssert.DoesNotContain("GetRegardForRealm", Code(effect));
		}

		/// <summary>
		/// Completion is what releases the reservation and clears the receipt. Requiring the
		/// archived row there is what stops that cleanup from being the act that erases the only
		/// record of the rite.
		/// </summary>
		[Test]
		public void CompletionRequiresTheArchivedRowBeforeAnyCleanupCanRun()
		{
			string completion = Source("Core/KingdomFoundingTransaction.18ReceiptCompletion.cs");
			int village = At(completion, "case KingdomFoundingKind.VillageCharter:");
			int archived = At(completion, "KingdomVillageCovenantRuntime.TryArchived(System,");
			int finish = At(completion, "private static bool FinishReceipt(");
			Assert.Greater(archived, village,
				"the archived row is required by the village branch of completion");
			Assert.Less(archived, finish);
			StringAssert.Contains("out int sealedStanding, out long reservationTick", completion,
				"the covenant is proved by the row's own frozen facts");
			StringAssert.Contains(
				"sealedStanding >= KingdomVillageCovenantRules.MinimumSealedStandingV1", completion);
			StringAssert.DoesNotContain("System.GetStanding(Basin.PendingVillageFaction)",
				Code(completion),
				"a covenant is never proved by today's standing, not even indirectly");

			string run = Source("Core/KingdomFoundingTransaction.11Run.cs");
			string code = Code(run);
			int observed = At(code, "CompletionObserved(Basin, Actor, Site, system)");
			int cleanup = At(code, "FinishReceipt(Basin, Site)");
			Assert.Less(observed, cleanup,
				"completion is observed before the reservation and receipt are cleared");
		}

		// ---- one lease means one lease ---------------------------------------------------

		/// <summary>
		/// Section nine is opened once for a recording, and the lease that opening produced is the
		/// object the commit is made under. If it were opened a second time, everything decided
		/// while the archive was being read would have been decided about a save that may since
		/// have moved &mdash; which is the exact window a lease exists to close.
		/// </summary>
		[Test]
		public void TheArchiveSectionIsOpenedOnceAndCommittedUnderThatSameLease()
		{
			string lease = Source("Core/KingdomVillageCovenantLease.cs");
			string code = Code(lease);
			Assert.AreEqual(1, Occurrences(code, "authority.TryReadSection("),
				"the covenant section may be opened in exactly one place");
			StringAssert.Contains("authority.TryReadSection(", Method(lease,
				"public static bool TryReadArchive("));

			string commit = Method(lease, "public static bool TryCommitAppended(");
			StringAssert.DoesNotContain("TryReadSection", commit,
				"the commit rides the lease it was handed and opens nothing");
			StringAssert.Contains("Held(lease)", commit,
				"the transition is made on the lease's own payload");
			StringAssert.Contains("return authority.TryCommitSection(lease, bytes, out failure);",
				commit, "the authority's answer is the method's answer");
			StringAssert.DoesNotContain("authority.TryCommit(", code,
				"the lease API is used whole, never its ingredients");
			StringAssert.DoesNotContain("authority.Read()", code,
				"the lease carries the bytes; nothing here reads the authority a second time");
			StringAssert.DoesNotContain("authority.Revision", code,
				"the lease carries the revision; nothing here asks for it separately");
		}

		/// <summary>
		/// The runtime keeps the lease across the whole recording and passes that very object into
		/// the commit. The confirmation afterwards is a separate reading on purpose: its job is to
		/// ask the save rather than to re-read what was offered to it.
		/// </summary>
		[Test]
		public void TheRuntimeCarriesOneLeaseFromTheReadIntoTheCommit()
		{
			string runtime = Source("Core/KingdomVillageCovenantRuntime.cs");
			string record = Method(runtime, "public static bool TryRecord(KingdomSystem System,");
			StringAssert.Contains("KingdomVillageCovenantRuntimeCut.TryRecord(authority, realmId,",
				record, "the engine shell must call the independently tested transaction cut");
			string cut = Method(Source("Core/KingdomVillageCovenantRuntimeCut.cs"),
				"public static bool TryRecord(IKingdomCivicMemoryAuthority Authority,");
			int read = At(cut, "KingdomVillageCovenantLease.TryReadArchive(Authority, RealmId,");
			int captured = At(cut, "out KingdomCivicMemorySectionLease lease, out _, out Failure");
			int commit = At(cut,
				"KingdomVillageCovenantLease.TryCommitAppended(Authority, lease, RealmId,");
			StringAssert.Contains("out Effective, out Failure", cut,
				"a recovery confirms the covenant the archive holds, not the one it just built");
			int confirm = At(cut,
				"KingdomVillageCovenantLease.TryConfirm(Authority, RealmId, Effective,");
			Assert.Less(read, captured);
			Assert.Less(captured, commit, "the commit receives the lease the read handed back");
			Assert.Less(commit, confirm, "the save is asked only after it has taken the record");
			StringAssert.DoesNotContain("TryReadSection", cut,
				"the runtime opens nothing itself");
			StringAssert.DoesNotContain("TryCommitSection", cut);
		}

		/// <summary>
		/// A realm has two true names, and the one the living game knows is checked where the
		/// living game is. The row's own rules never compare them, because a migrated save keeps a
		/// faction key that is no longer its realm id and would otherwise be unable to charter
		/// anything ever again.
		/// </summary>
		[Test]
		public void TheAuthoritysFactionKeyIsProvedAgainstTheLivingRealmRatherThanTheRow()
		{
			string runtime = Source("Core/KingdomVillageCovenantRuntime.cs");
			StringAssert.Contains("AuthorityIsThisRealms(System, FoundingAuthority, out Failure)",
				Method(runtime, "public static bool TryPreflight(KingdomSystem System,"));
			StringAssert.Contains("System.KingdomFactionName, receipt",
				Method(runtime, "public static bool TryRecord(KingdomSystem System,"),
				"the faction key the living game supplies must cross the tested runtime cut");
			string cut = Source("Core/KingdomVillageCovenantRuntimeCut.cs");
			StringAssert.Contains("Receipt.FoundingAuthority, CurrentFactionKey, out Failure", cut,
				"the cut judges the frozen authority against the separate live faction key");

			string row = Code(Method(Source("Core/KingdomVillageCovenantRules.Fields.cs"),
				"private static bool ValidAuthority(KingdomVillageCovenantReceipt row,"));
			StringAssert.DoesNotContain("parsed.RealmFaction", row,
				"the row freezes both identities and compares neither; a migrated realm must still "
				+ "be able to seal a covenant");
			StringAssert.Contains("public static bool AuthorityBelongsToRealm(",
				Source("Core/KingdomVillageCovenantRules.Fields.cs"),
				"the engine cut is a pure rule so a migrated realm is testable without a game");
		}

		// ---- what this family may never do -----------------------------------------------

		/// <summary>
		/// The covenant family reads. It does not move a standing, spend an action, wake an actor,
		/// write a property, or say anything to anybody. Every one of those would turn a record of
		/// what happened into a thing that makes something happen.
		/// </summary>
		[Test]
		public void NoCovenantFileMutatesStateActionsOrEnergy()
		{
			string[] forbidden =
			{
				"SetStanding(", "AdjustStanding(", "ModifyReputation(", "UseEnergy(",
				"SetZoneProperty(", "RemoveZoneProperty(", "SetStringProperty(",
				"SetIntProperty(", "RemoveStringProperty(", "RemoveIntProperty(",
				"AddPlayerMessage(", "Popup.", "JournalAPI.", "AwardXP(", "GiveReward(",
				"RecordDeed(", "AddAccomplishment(", "AddMapNote(", "RequirePart(",
				"KingdomChronicle.Record(", "Tradable = true"
			};
			List<string> owned = Owned();
			Assert.GreaterOrEqual(owned.Count, 10, "the covenant family is not that small");
			for (int f = 0; f < owned.Count; f++)
			{
				string code = Code(Source(owned[f]));
				for (int i = 0; i < forbidden.Length; i++)
					StringAssert.DoesNotContain(forbidden[i], code, owned[f] + " / " + forbidden[i]);
			}
		}

		/// <summary>
		/// Nothing here reaches a zone that is not already loaded, or an actor at all. A view that
		/// could thaw a distant zone to answer a question about a covenant would make reading the
		/// founder's history a thing with a cost and a side effect.
		/// </summary>
		[Test]
		public void NoCovenantFileTouchesARemoteZoneOrBorrowsAnActor()
		{
			string[] forbidden =
			{
				"ZoneManager", "GetZone(", "SetActiveZone", "Thaw", "CacheZone", "LoadZone",
				"GetZoneFromCache", "FindObject(", "FindByID(", "GetObjectByID(",
				"Factions.Get(", "The.Player", "GetPlayer(", "WorshipDefault", "GetReaction("
			};
			List<string> owned = Owned();
			for (int f = 0; f < owned.Count; f++)
			{
				string code = Code(Source(owned[f]));
				for (int i = 0; i < forbidden.Length; i++)
					StringAssert.DoesNotContain(forbidden[i], code, owned[f] + " / " + forbidden[i]);
			}
			StringAssert.Contains("Factions.GetIfExists(",
				Source("Core/KingdomVillageCovenantRuntime.JointView.cs"),
				"a faction from an uninstalled mod must refuse this owner, not crash it");
		}

		/// <summary>
		/// Standing reaches exactly one file and is used in exactly one way there. The decision
		/// about whether a covenant stands is taken in the pure view, which never sees a standing
		/// at all.
		/// </summary>
		[Test]
		public void StandingIsReadInOnePlaceAndNeverDecidesWhetherACovenantStands()
		{
			List<string> owned = Owned();
			int readers = 0;
			for (int f = 0; f < owned.Count; f++)
				if (Code(Source(owned[f])).Contains("GetRegardForRealm(")) readers++;
			Assert.AreEqual(1, readers, "only the joint-view runtime may read a standing");

			string jointView = Code(Source("Core/KingdomVillageCovenantRuntime.JointView.cs"));
			StringAssert.Contains(
				"CurrentStanding = System.GetRegardForRealm(row.VillageFactionId)",
				jointView, "standing is recorded as a projection beside the row");
			Assert.AreEqual(1, Occurrences(jointView, "GetRegardForRealm("));

			string decision = Method(Source("Core/KingdomVillageCovenantView.cs"),
				"private static KingdomJointCivicOwnerView Recorded(string realmId,");
			StringAssert.DoesNotContain("CurrentStanding", decision,
				"the verdict must not depend on today's standing");
		}

		/// <summary>
		/// The joint-view reading takes a copy of the whole state rather than leasing the section,
		/// because a lease is refused for a read-only session and for a payload from a later build,
		/// and both of those are answers this view has to be able to give.
		/// </summary>
		[Test]
		public void TheJointViewReadsAndNeverLeasesOrCommits()
		{
			string jointView = Code(Source("Core/KingdomVillageCovenantRuntime.JointView.cs"));
			StringAssert.Contains("authority.Read()", jointView);
			StringAssert.DoesNotContain("TryReadSection", jointView);
			StringAssert.DoesNotContain("TryCommit", jointView);
			StringAssert.Contains("state.Quarantined", jointView);
			StringAssert.Contains("state.IsFutureOuter", jointView);
			StringAssert.Contains("KingdomVillageCovenantEvidence.WrongRealm", jointView);
			StringAssert.Contains("KingdomVillageCovenantEvidence.ArchiveAbsent", jointView);
			StringAssert.Contains("KingdomVillageCovenantEvidence.NoneRecorded", jointView);

			string runtime = Source("Core/KingdomJointCivicViewRuntime.cs");
			StringAssert.Contains("KingdomVillageCovenantRuntime.ReadOwnerForJointView(System)",
				runtime, "the covenant owner is read rather than declared missing outright");
			StringAssert.DoesNotContain("KingdomJointCivicViewAdapters.CovenantMissing()",
				Code(runtime));
		}

		// ---- the wire's own promises ------------------------------------------------------

		/// <summary>
		/// SHA-256 here detects change and says nothing about who wrote the bytes. Calling it
		/// authentication or proof would be a claim this family cannot support and does not need.
		/// </summary>
		[Test]
		public void TheDigestIsDescribedAsIntegrityAndNeverAsAuthentication()
		{
			List<string> owned = Owned();
			for (int f = 0; f < owned.Count; f++)
			{
				string text = Source(owned[f]).ToLowerInvariant();
				StringAssert.DoesNotContain("authenticat", text, owned[f]);
				StringAssert.DoesNotContain("proves the producer", text, owned[f]);
			}
			string codec = Source("Core/KingdomVillageCovenantCodec.cs");
			StringAssert.Contains("Verify integrity, then interpret", codec);
			StringAssert.Contains("What the digest is not: a signature.", codec);
			StringAssert.Contains("says nothing whatever about", codec);
		}

		/// <summary>
		/// There is no earlier format, so there is no migration. A door built for a building that
		/// was never there admits only whatever walks up to it.
		/// </summary>
		[Test]
		public void NoLegacyMigrationExistsBecauseNoLegacyFormatDoes()
		{
			string codec = Source("Core/KingdomVillageCovenantCodec.cs");
			Assert.AreEqual(1, KingdomVillageCovenantCodec.FirstWireVersion);
			Assert.AreEqual(1, KingdomVillageCovenantCodec.CurrentWireVersion);
			string code = Code(codec + Source("Core/KingdomVillageCovenantCodec.Rows.cs"));
			StringAssert.DoesNotContain("LegacyWireVersion", code);
			StringAssert.DoesNotContain("Migrate", code);
			StringAssert.Contains("There is no earlier format", codec);
		}

		/// <summary>
		/// The digest is checked before the revision decides anything, so a lawful successor and a
		/// bad sector are told apart by evidence rather than by which branch ran first.
		/// </summary>
		[Test]
		public void IntegrityIsVerifiedBeforeAnyPayloadIsClassified()
		{
			string inspect = Method(Source("Core/KingdomVillageCovenantCodec.Frame.cs"),
				"internal static KingdomVillageCovenantFrame Inspect(byte[] bytes)");
			int digest = At(inspect, "if (!DigestStands(bytes))");
			int classify = At(inspect, "version > CurrentWireVersion");
			Assert.Less(digest, classify,
				"a payload is proved whole before it is called a future");

			string decode = Method(Source("Core/KingdomVillageCovenantCodec.cs"),
				"public static KingdomVillageCovenantArchive Decode(byte[] bytes)");
			int ingress = At(decode, "Ingress(bytes, MaxEnvelopeBytes,");
			int inspected = At(decode, "Inspect(snapshot)");
			Assert.Less(ingress, inspected,
				"one private copy is taken before anything about the bytes is judged");
			Assert.AreEqual(1, Occurrences(Code(decode), "Ingress("));
		}

		// ---- the envelope's wiring and the line law ---------------------------------------

		[Test]
		public void SectionNineIsWiredIntoTheEnvelopeAndBoundBackToItsOwnCap()
		{
			string limits = Source("Core/KingdomCivicMemoryLimits.cs");
			StringAssert.Contains("public const int SectionVillageCovenant = 9;", limits);
			StringAssert.Contains("case SectionVillageCovenant: return MaxVillageCovenantBytes;",
				limits);
			StringAssert.Contains("+ MaxCommunalRiteBytes + MaxGuestFeastBytes "
				+ "+ MaxVillageCovenantBytes;", limits);

			string bindings = Source("Core/KingdomCivicMemoryFamilyBindings.cs");
			StringAssert.Contains("KingdomCivicMemoryLimits.SectionVillageCovenant, VillageCovenant",
				bindings);
			StringAssert.Contains("KingdomVillageCovenantInspection.InspectGuarded(Payload, out Fault)",
				bindings);

			string derivation = Source("Core/KingdomCivicMemoryDerivation.cs");
			StringAssert.Contains("KingdomVillageCovenantCodec.MaxEnvelopeBytes", derivation);
			StringAssert.Contains("KingdomCivicMemoryLimits.MaxTreatyBytes", derivation);
			StringAssert.Contains("must never widen what a payload there may be", derivation);

			string inspection = Source("Core/KingdomVillageCovenantInspection.cs");
			StringAssert.Contains("KingdomVillageCovenantCodec.Decode(Payload)", inspection);
		}

		/// <summary>
		/// The joint view's enclave owner is re-proved against ground the realm holds now, through
		/// the topology's canonical uniqueness APIs. Matching the ids an authority was handed proves
		/// only that the authority agrees with itself.
		/// </summary>
		[Test]
		public void TheEnclaveOwnerIsReprovedAgainstCurrentlyOwnedGround()
		{
			string runtime = Source("Core/KingdomJointCivicViewRuntime.cs");
			string enclave = Method(runtime,
				"private static KingdomJointCivicOwnerView ReadEnclave(KingdomSystem System,");
			StringAssert.Contains("authority.RealmId, realmId", enclave);
			StringAssert.Contains("authority.ZoneId, LoadedZone.ZoneID", enclave,
				"a remote reserved owner must not masquerade as current loaded evidence");
			StringAssert.Contains("System.OwnedZone(authority.ZoneId)", enclave);
			StringAssert.Contains("System.SettlementIdForOwnedZone(authority.ZoneId)", enclave);
			StringAssert.Contains("authority.SettlementId, StringComparison.Ordinal", enclave);
			StringAssert.DoesNotContain("FindNonSeatSettlementByZone", enclave,
				"the canonical API owns overlap refusal; this view must not choose a claimant");
			string code = Code(runtime);
			foreach (string forbidden in new[] { "GetZone(", "Thaw", "ZoneManager", "CacheZone",
				"LoadZone", "SetActiveZone" })
				StringAssert.DoesNotContain(forbidden, code,
					"no zone is loaded to make an ownership answer come out true");
		}

		/// <summary>
		/// Completion compares the archived tick with the reservation marker while the marker is
		/// still there, and accepts its absence afterwards. Requiring a marker that completion
		/// itself released would turn a rite that had already succeeded into one that can never
		/// finish.
		/// </summary>
		[Test]
		public void CompletionMatchesTheArchivedTickAgainstTheReservationWhileItExists()
		{
			string completion = Source("Core/KingdomFoundingTransaction.18ReceiptCompletion.cs");
			StringAssert.Contains("ArchivedReservationTickStillMatches(Site, reservationTick)",
				completion);
			string cut = Source("Core/KingdomVillageCovenantFoundingCut.cs");
			string matcher = Method(cut,
				"private static bool ArchivedReservationTickStillMatches(Zone Site,");
			int cleared = At(matcher, "if (!HasSiteReservation(Site)) return true;");
			int compared = At(matcher, "marker == ArchivedTick");
			Assert.Less(cleared, compared,
				"an already-cleared reservation is accepted before any comparison is attempted");
			StringAssert.Contains("ArchivedTick < 0L) return false;", matcher);
		}

		/// <summary>
		/// Section nine is a full citizen of the civic-memory envelope: its own cap, its own
		/// binding, its own entry in the cumulative arithmetic, and the counts and framing the
		/// envelope quotes about itself all moved with it.
		/// </summary>
		[Test]
		public void TheEnvelopeCountsAndFramingMovedWithSectionNine()
		{
			Assert.AreEqual(9, KingdomCivicMemoryLimits.LastKnownSection);
			Assert.AreEqual(9, KingdomCivicMemoryLimits.KnownSectionCount);
			Assert.AreEqual(18, KingdomCivicMemoryLimits.MaxSections);
			Assert.IsTrue(KingdomCivicMemoryLimits.Known(
				KingdomCivicMemoryLimits.SectionVillageCovenant));
			Assert.IsFalse(KingdomCivicMemoryLimits.Known(
				KingdomCivicMemoryLimits.SectionVillageCovenant + 1));
			Assert.AreEqual(KingdomCivicMemoryLimits.MaxTreatyBytes,
				KingdomCivicMemoryLimits.SectionCap(
					KingdomCivicMemoryLimits.SectionVillageCovenant + 1),
				"the section after this one is still an unknown held to the widest known cap");

			string limits = Source("Core/KingdomCivicMemoryLimits.cs");
			StringAssert.Contains("eighteen sections cost 144 bytes of framing", limits);
			StringAssert.DoesNotContain("sixteen sections cost 128 bytes", limits);
			StringAssert.Contains("Room for the nine known sections", limits);
			StringAssert.Contains("= 102 + 3 * 3979 + 44 = 12083.", limits,
				"the guest-feast comment must quote the cap it actually mirrors");

			foreach (string family in new[] { "Core/KingdomCivicMemoryFamilies.cs",
				"Core/KingdomCivicMemoryFamilyBindings.cs", "Core/KingdomCivicMemorySection.cs",
				"Core/KingdomCivicMemorySystem.cs" })
				StringAssert.DoesNotContain("seven wire families", Source(family), family);
			StringAssert.DoesNotContain("Two of the seven families",
				Source("Core/KingdomCivicMemoryFamilies.cs"));
			StringAssert.DoesNotContain("Two of the seven families",
				Source("Core/KingdomCivicMemoryLimits.cs"));
			StringAssert.DoesNotContain("Two of the eight families",
				Source("Core/KingdomCivicMemoryFamilies.cs"));

			// A digest detects change and never proves a producer; the joint view's own model says
			// validated rather than authenticated for the same reason.
			StringAssert.Contains("One independently validated semantic owner",
				Source("Core/KingdomJointCivicView.cs"));
			StringAssert.DoesNotContain("authenticated semantic owner",
				Source("Core/KingdomJointCivicView.cs"));
		}

		/// <summary>Every production file in this family stays readable. The repository's rule is
		/// strictly fewer than three hundred physical lines.</summary>
		[Test]
		public void EveryCovenantProductionFileStaysUnderTheLineLaw()
		{
			List<string> owned = Owned();
			owned.Add("Core/KingdomCivicMemoryLimits.cs");
			owned.Add("Core/KingdomCivicMemoryFamilyBindings.cs");
			owned.Add("Core/KingdomCivicMemoryDerivation.cs");
			owned.Add("Core/KingdomJointCivicView.cs");
			owned.Add("Core/KingdomJointCivicViewAdapters.cs");
			owned.Add("Core/KingdomJointCivicViewRuntime.cs");
			owned.Add("Core/KingdomFoundingTransaction.10Begin.cs");
			owned.Add("Core/KingdomFoundingTransaction.15IdentityAndVillage.cs");
			owned.Add("Core/KingdomFoundingTransaction.15aVillageStandingEffect.cs");
			owned.Add("Core/KingdomFoundingTransaction.17aVillageEffectValidation.cs");
			owned.Add("Core/KingdomFoundingTransaction.18ReceiptCompletion.cs");
			for (int i = 0; i < owned.Count; i++)
			{
				int lines = Source(owned[i]).Split('\n').Length;
				Assert.Less(lines, 301, owned[i] + " is " + lines + " physical lines");
			}
		}

		/// <summary>
		/// No source in this family may carry a raw control or format character. A C1 control in a
		/// literal is eaten by the compiler before anyone reads the intent, and a format character
		/// is invisible to the next person who opens the file; both are spelled as escapes instead.
		/// </summary>
		[Test]
		public void NoCovenantSourceCarriesARawControlOrFormatCharacter()
		{
			List<string> owned = Owned();
			owned.Add("DevTests/KingdomVillageCovenantTests.cs");
			owned.Add("DevTests/KingdomVillageCovenantArchiveTests.cs");
			owned.Add("DevTests/KingdomVillageCovenantFutureTests.cs");
			owned.Add("DevTests/KingdomVillageCovenantLeaseTests.cs");
			owned.Add("DevTests/KingdomVillageCovenantViewTests.cs");
			owned.Add("DevTests/KingdomVillageCovenantSourceTests.cs");
			for (int f = 0; f < owned.Count; f++)
			{
				string text = Source(owned[f]);
				for (int i = 0; i < text.Length; i++)
				{
					char c = text[i];
					if (c == '\t' || c == '\n' || c == '\r') continue;
					bool control = c < ' ' || (c >= '\u0080' && c <= '\u009f');
					bool format = char.GetUnicodeCategory(c)
						== System.Globalization.UnicodeCategory.Format;
					Assert.IsFalse(control || format, owned[f] + " carries U+"
						+ ((int)c).ToString("X4") + " raw at offset " + i);
				}
			}
		}
	}
}
#endif
