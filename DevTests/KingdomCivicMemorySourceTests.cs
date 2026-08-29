#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Source-contract coverage for the parts of civic memory that only exist against the engine,
	/// and for the one law that has to hold everywhere rather than in one file.
	/// <para>
	/// The save surface and the real family wiring are compiled out of the pure test projects, so
	/// their obligations are checked against the source: field reflection off, <c>BeforeSave</c>
	/// throwing and never assigning, <c>Read</c> quarantining the bytes it actually recovered with
	/// the true cause, all nine sections wired to their real codecs, and every engine claim in the
	/// docstrings still naming the line it was taken from.
	/// </para>
	/// </summary>
	[TestFixture]
	public class KingdomCivicMemorySourceTests
	{
		private static string Source(params string[] Parts)
		{
			return TestMain.ReadRepositoryText(Path.Combine(Parts));
		}

		private static string Save()
		{
			return Source("Core", "KingdomCivicMemorySystem.Save.cs");
		}

		[Test]
		public void TheSaveBlockIsCustomRatherThanReflected()
		{
			string save = Save();
			StringAssert.Contains("public override bool WantFieldReflection => false;", save);
			StringAssert.Contains("XRL/IGameSystem.cs:40", save);
			StringAssert.Contains("ICompositeType", save);
			StringAssert.Contains("XRL/World/SerializationWriter.cs:2756-2770", save);
			StringAssert.Contains("XRL/World/SerializationReader.cs:1329-1333", save);

			int write = save.IndexOf("public override void Write(SerializationWriter Writer)",
				StringComparison.Ordinal);
			Assert.Greater(write, -1);
			int magic = save.IndexOf("Writer.Write(BlockMagic);", write, StringComparison.Ordinal);
			int version = save.IndexOf("Writer.Write(CurrentBlockVersion);", magic,
				StringComparison.Ordinal);
			int length = save.IndexOf("Writer.Write(envelope.Length);", version,
				StringComparison.Ordinal);
			int payload = save.IndexOf("Writer.WriteBytesDirect(envelope);", length,
				StringComparison.Ordinal);
			Assert.Greater(magic, write);
			Assert.Greater(version, magic);
			Assert.Greater(length, version);
			Assert.Greater(payload, length);
		}

		[Test]
		public void BeforeSaveVetoesOnTheLatchAndNeverAssignsIt()
		{
			string save = Save();
			int method = save.IndexOf("public override void BeforeSave()", StringComparison.Ordinal);
			Assert.Greater(method, -1, "civic memory must have a BeforeSave veto");
			string body = save.Substring(method);

			int check = body.IndexOf("if (Records.Latch.Tripped)", StringComparison.Ordinal);
			int thrown = body.IndexOf("throw new InvalidOperationException", check,
				StringComparison.Ordinal);
			Assert.Greater(check, -1, "BeforeSave must read the latch");
			Assert.Greater(thrown, check, "reading the latch must be followed by a refusal");

			Assert.IsFalse(body.Contains("Latch.Trip("),
				"BeforeSave observes the latch; it must not be able to set one either");
			Assert.IsFalse(body.Contains("AdoptAbsent"),
				"the veto must never reach for the empty-save path");

			StringAssert.Contains("XRL/XRLGame.cs:1580-1590", save);
			StringAssert.Contains("FinalizeWrite", save);
			StringAssert.Contains("RestoreBackup = false", save);
			StringAssert.Contains(":2383-2387", save);
		}

		/// <summary>
		/// Replaces an earlier test that checked only that <c>Trip(</c> appeared before
		/// <c>throw;</c>. That ordering was true of the broken version too: it adopted an empty
		/// stand-in first, so the latch took its cause from decoding <i>that</i>, and the real
		/// bytes were discarded. Call order was never the property worth asserting; what reaches
		/// quarantine is. This binds the argument.
		/// </summary>
		[Test]
		public void ReadQuarantinesTheBytesItActuallyRecoveredWithTheTrueFramingCause()
		{
			string save = Save();
			int read = save.IndexOf("public override void Read(SerializationReader Reader)",
				StringComparison.Ordinal);
			Assert.Greater(read, -1);
			string body = save.Substring(read,
				save.IndexOf("private static int Word(", read, StringComparison.Ordinal) - read);

			// The real bytes are accumulated as they are read, and that accumulator -- not a
			// substitute -- is what is handed to quarantine.
			Assert.IsTrue(Regex.IsMatch(body,
				@"Records\.AdoptUnreadableFraming\(\s*recovered\.ToArray\(\)\s*,"),
				"the framing path must quarantine the bytes it actually recovered");
			Assert.IsTrue(Regex.IsMatch(body, @"Word\(Reader,\s*recovered\)"),
				"every framing word read must be added to that accumulator");

			// The specific defect this replaces: adopting a stand-in payload, whose decode would
			// latch first with a synthetic cause and lose the real one.
			Assert.IsFalse(Regex.IsMatch(body, @"AdoptSaved\(\s*new byte\[0\]\s*\)"),
				"the framing path must never adopt an empty stand-in; the latch is one-way and "
				+ "would keep that decode's synthetic cause instead of the true one");
			Assert.IsFalse(body.Contains("Records.Latch.Trip("),
				"the framing path must set its cause through AdoptUnreadableFraming, which trips "
				+ "the latch before anything else can");

			int quarantine = body.IndexOf("Records.AdoptUnreadableFraming(", StringComparison.Ordinal);
			int rethrow = body.IndexOf("throw;", quarantine, StringComparison.Ordinal);
			Assert.Greater(rethrow, quarantine, "it must record before it rethrows, not after");

			// And AdoptUnreadableFraming really does put the cause on the latch before the state.
			string authority = Source("Core", "KingdomCivicMemoryAuthority.cs");
			int method = authority.IndexOf("public void AdoptUnreadableFraming(",
				StringComparison.Ordinal);
			string adopt = authority.Substring(method,
				authority.IndexOf("public void AdoptAbsent(", method, StringComparison.Ordinal)
					- method);
			int trip = adopt.IndexOf("Latch.Trip(cause);", StringComparison.Ordinal);
			int keep = adopt.IndexOf("KingdomCivicMemoryState.Quarantine(Evidence", trip,
				StringComparison.Ordinal);
			Assert.Greater(trip, -1, "the true cause must reach the latch");
			Assert.Greater(keep, trip, "and the evidence must be kept, unmodified");
			Assert.IsFalse(adopt.Contains("Decode("),
				"this path must never decode anything; that is how a synthetic cause gets in");

			StringAssert.Contains("XRL/World/SerializationReader.cs:1320-1340", save);
			StringAssert.Contains(":2186-2193", save);
			StringAssert.Contains("SkipBlock", save);
			StringAssert.Contains("only lawful empty", save);
		}

		[Test]
		public void AShortOrThrowingPayloadReadLatchesBeforeTheEngineCanReturnTheInstance()
		{
			string save = Save();
			int read = save.IndexOf("public override void Read(SerializationReader Reader)",
				StringComparison.Ordinal);
			string body = save.Substring(read,
				save.IndexOf("private static int Word(", read, StringComparison.Ordinal) - read);
			StringAssert.Contains("payload.Length != length", body);
			int catchAt = body.IndexOf("catch (Exception e)",
				body.IndexOf("byte[] payload", StringComparison.Ordinal), StringComparison.Ordinal);
			int latch = body.IndexOf("Records.AdoptUnreadableFraming(", catchAt,
				StringComparison.Ordinal);
			int rethrow = body.IndexOf("throw;", latch, StringComparison.Ordinal);
			Assert.Greater(catchAt, -1);
			Assert.Greater(latch, catchAt);
			Assert.Greater(rethrow, latch);
		}

		[Test]
		public void AConstructedSystemWhoseCustomReadNeverCompletedIsLatched()
		{
			string save = Save();
			int read = save.IndexOf("public override void Read(SerializationReader Reader)",
				StringComparison.Ordinal);
			int clear = save.IndexOf("CustomReadCompleted = false;", read,
				StringComparison.Ordinal);
			int adopt = save.IndexOf("Records.AdoptSaved(payload);", clear,
				StringComparison.Ordinal);
			int complete = save.IndexOf("CustomReadCompleted = true;", adopt,
				StringComparison.Ordinal);
			Assert.Greater(clear, read);
			Assert.Greater(adopt, clear);
			Assert.Greater(complete, adopt);

			string guard = Source("Core", "KingdomCivicMemorySystem.LoadGuard.cs");
			StringAssert.Contains("public override void AfterLoad(XRLGame Game)", guard);
			StringAssert.Contains("if (!CustomReadCompleted)", guard);
			StringAssert.Contains("RefuseRosterLoss(", guard);
			StringAssert.Contains("Records.AdoptUnreadableFraming(new byte[0]", guard);
		}

		[Test]
		public void TheLatchTypeOffersNoWayBack()
		{
			string latch = Source("Core", "KingdomCivicMemoryLatch.cs");

			Assert.IsTrue(Regex.IsMatch(latch, @"public\s+bool\s+Tripped\s*=>\s*Thrown\s*;"),
				"Tripped must be an expression-bodied read, never a settable property");
			Assert.IsTrue(Regex.IsMatch(latch, @"private\s+bool\s+Thrown\s*;"),
				"the latch's state must be private");
			Assert.IsFalse(Regex.IsMatch(latch, @"\bThrown\s*=\s*false"),
				"the latch must have no off switch");

			foreach (string name in new[] { "Clear", "Reset", "Dismiss", "Acknowledge",
				"Untrip", "Unlatch", "Repair", "Forgive" })
				Assert.IsFalse(Regex.IsMatch(latch,
					@"(public|internal|protected|private)\s[^\n]*\b" + name + @"\s*\("),
					"KingdomCivicMemoryLatch must declare no " + name + " member");

			StringAssert.Contains("public readonly KingdomCivicMemoryLatch Latch",
				Source("Core", "KingdomCivicMemoryAuthority.cs"));
		}

		/// <summary>
		/// The C17 lesson, enforced where it was actually broken. That veto did not fail because
		/// the flag was wrong; it failed because a method in a completely different file assigned
		/// it. Checking the latch's own file would have caught nothing. So this sweeps the tree.
		/// </summary>
		[Test]
		public void NoFileAnywhereInTheTreeCanRetireTheLatch()
		{
			string root = TestMain.RepositoryRoot;
			string latchFile = Path.Combine(root, "Core", "KingdomCivicMemoryLatch.cs");
			List<string> offenders = new List<string>();
			List<string> setters = new List<string>();

			foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
			{
				string text = File.ReadAllText(path);
				string shown = path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar);

				if (Regex.IsMatch(text, @"\bTripped\s*=(?!=|>)")) offenders.Add(shown + " (Tripped)");

				foreach (Match match in Regex.Matches(text, @"\bThrown\s*=(?!=|>)\s*(\w+)"))
				{
					if (!string.Equals(path, latchFile, StringComparison.Ordinal))
						offenders.Add(shown + " (Thrown)");
					else if (match.Groups[1].Value != "true")
						offenders.Add(shown + " (Thrown = " + match.Groups[1].Value + ")");
				}

				if (Regex.IsMatch(text, @"\.Latch\s*=(?!=|>)")) setters.Add(shown);
			}

			CollectionAssert.IsEmpty(offenders,
				"the civic memory latch must be unassignable outside its own one-way setter; "
				+ "C17's veto died exactly this way");
			CollectionAssert.IsEmpty(setters,
				"an authority's latch must never be replaced with a fresh, untripped one");
		}

		/// <summary>
		/// The pure side proves what the authority does with a verdict; only the source can prove
		/// the verdicts come from the real codecs, because two of those families reach the engine.
		/// </summary>
		[Test]
		public void EveryKnownSectionIsWiredToItsRealFrozenCodec()
		{
			string bindings = Source("Core", "KingdomCivicMemoryFamilyBindings.cs");
			string[,] wiring =
			{
				{ "SectionCivicArtifacts", "KingdomCivicArtifactsCodec.Decode" },
				{ "SectionCivicPractice", "KingdomCivicPracticeCodec.Decode" },
				{ "SectionBodyHistory", "KingdomBodyHistoryCodec.Decode" },
				{ "SectionCuriosity", "KingdomCuriosityLeadCodec.DecodeCuriosity" },
				{ "SectionCivicLeads", "KingdomCuriosityLeadCodec.DecodeLeads" },
				{ "SectionTreaty", "KingdomTreatyCodec.Decode" },
				{ "SectionCommunalRite", "KingdomCommunalRiteCodec.DecodeEnvelope" },
				{ "SectionGuestFeast", "KingdomGuestFeastCodec.DecodeEnvelope" }
			};
			for (int i = 0; i < wiring.GetLength(0); i++)
			{
				StringAssert.Contains("KingdomCivicMemoryLimits." + wiring[i, 0], bindings);
				StringAssert.Contains(wiring[i, 1], bindings);
			}

			// The table refuses a second claim on an id, so a permissive reader cannot be
			// installed over a strict one.
			StringAssert.Contains("already has a family reader",
				Source("Core", "KingdomCivicMemoryFamilies.cs"));

			// Treaty distinguishes future from malformed by StoreState, not Quarantined.
			StringAssert.Contains("KingdomTreatyStoreState.FutureOpaque", bindings);

			// O6/D7 now make the same distinction explicitly; lawful later books are carried and
			// cannot become replaceable current sections.
			StringAssert.Contains("KingdomCuriosityBookState.FutureOpaque", bindings);
			StringAssert.Contains("KingdomCuriosityBookState.Compatible", bindings);
			StringAssert.DoesNotContain("cannot tell a future book from a broken one", bindings);
			int mapStart = bindings.IndexOf(
				"private static KingdomCivicMemoryNested CuriosityBook(", StringComparison.Ordinal);
			int mapEnd = bindings.IndexOf(
				"private static KingdomCivicMemoryNested EnvelopeState(", mapStart,
				StringComparison.Ordinal);
			Assert.Greater(mapStart, -1);
			Assert.Greater(mapEnd, mapStart);
			string map = bindings.Substring(mapStart, mapEnd - mapStart);
			int futureState = map.IndexOf("State == KingdomCuriosityBookState.FutureOpaque",
				StringComparison.Ordinal);
			int futureVerdict = map.IndexOf("return KingdomCivicMemoryNested.Future", futureState,
				StringComparison.Ordinal);
			int currentState = map.IndexOf("State == KingdomCuriosityBookState.Compatible",
				futureVerdict, StringComparison.Ordinal);
			int currentVerdict = map.IndexOf("return KingdomCivicMemoryNested.Current", currentState,
				StringComparison.Ordinal);
			int invalidState = map.IndexOf("State != KingdomCuriosityBookState.Quarantined",
				currentVerdict, StringComparison.Ordinal);
			int malformedVerdict = map.IndexOf("return KingdomCivicMemoryNested.Malformed",
				invalidState, StringComparison.Ordinal);
			Assert.Greater(futureVerdict, futureState);
			Assert.Greater(currentState, futureVerdict);
			Assert.Greater(currentVerdict, currentState);
			Assert.Greater(invalidState, currentVerdict);
			Assert.Greater(malformedVerdict, invalidState,
				"undefined O6/D7 book states must fail closed after the two supported states");
		}

		[Test]
		public void IdentityOwningFamiliesValidateDecodedAuthorityBeforeReturningCurrent()
		{
			string bindings = Source("Core", "KingdomCivicMemoryFamilyBindings.cs");
			string[,] families =
			{
				{ "Artifacts", "KingdomCivicArtifactsCodec.Decode(Payload)",
					"KingdomCivicArtifactsStore.TryValidateIdentity" },
				{ "Practice", "KingdomCivicPracticeCodec.Decode(Payload)",
					"KingdomCivicPracticeStore.TryValidateIdentity" },
				{ "BodyHistory", "KingdomBodyHistoryCodec.Decode(Payload)",
					"KingdomBodyHistoryStore.TryValidateIdentity" }
			};
			for (int i = 0; i < families.GetLength(0); i++)
			{
				int method = bindings.IndexOf("private static KingdomCivicMemoryNested "
					+ families[i, 0] + "(", StringComparison.Ordinal);
				int decode = bindings.IndexOf(families[i, 1], method, StringComparison.Ordinal);
				int disposition = bindings.IndexOf("EnvelopeState(", decode, StringComparison.Ordinal);
				int currentOnly = bindings.IndexOf(
					"if (framing != KingdomCivicMemoryNested.Current) return framing;", disposition,
					StringComparison.Ordinal);
				int identity = bindings.IndexOf(families[i, 2], currentOnly,
					StringComparison.Ordinal);
				int accepted = bindings.IndexOf("return KingdomCivicMemoryNested.Current;", identity,
					StringComparison.Ordinal);
				int refused = bindings.IndexOf("return InvalidIdentity(", accepted,
					StringComparison.Ordinal);

				Assert.Greater(method, -1, families[i, 0]);
				Assert.Greater(decode, method, families[i, 0]);
				Assert.Greater(disposition, decode, families[i, 0]);
				Assert.Greater(currentOnly, disposition,
					families[i, 0] + " must preserve future/quarantine before identity validation");
				Assert.Greater(identity, currentOnly,
					families[i, 0] + " must ask its Store about decoded authority identity");
				Assert.Greater(accepted, identity, families[i, 0]);
				Assert.Greater(refused, accepted,
					families[i, 0] + " must map invalid populated-unbound v1 to malformed");
			}

			string[,] stores =
			{
				{ "KingdomCivicArtifactsStore.cs", "!Value.IdentityBound",
					"Value.RealmId == null && IsAuthorityEmpty(Value)",
					"unbound civic artifacts carry authority" },
				{ "KingdomCivicPracticeStore.cs", "!value.IdentityBound",
					"value.RealmId == null && IsAuthorityEmpty(value)",
					"unbound civic practice carries authority" },
				{ "KingdomBodyHistoryStore.cs", "!Value.IdentityBound",
					"Value.RealmId == null && IsAuthorityEmpty(Value)",
					"unbound body history carries authority" }
			};
			for (int i = 0; i < stores.GetLength(0); i++)
			{
				string source = Source("Core", stores[i, 0]);
				StringAssert.Contains(stores[i, 1], source);
				StringAssert.Contains(stores[i, 2], source,
					stores[i, 0] + " may accept unbound identity only for exactly empty authority");
				StringAssert.Contains(stores[i, 3], source);
			}
		}

		[Test]
		public void TransactionSurfaceDelegatesLeasesWithoutRebuildingTheirAuthority()
		{
			string transactions = Source("Core", "KingdomCivicMemorySystem.Transactions.cs");
			int read = transactions.IndexOf("public bool TryReadSection(", StringComparison.Ordinal);
			int readDelegate = transactions.IndexOf("return Records.TryReadSection(SectionId, out Lease, "
				+ "out Failure);", read, StringComparison.Ordinal);
			int commit = transactions.IndexOf("public bool TryCommitSection(", readDelegate,
				StringComparison.Ordinal);
			int commitDelegate = transactions.IndexOf("return Records.TryCommitSection(Lease, Payload, "
				+ "out Failure);", commit, StringComparison.Ordinal);

			Assert.Greater(read, -1);
			Assert.Greater(readDelegate, read,
				"the engine surface must return the authority's origin-bound lease unchanged");
			Assert.Greater(commit, readDelegate);
			Assert.Greater(commitDelegate, commit,
				"the engine surface must offer the same lease back to the same authority");
			StringAssert.Contains("KingdomCivicMemorySectionLease", transactions);
		}

		[Test]
		public void SectionReadKeepsTheGuardThroughInspectionAndItsFinalCasCheck()
		{
			string lease = Source("Core", "KingdomCivicMemorySectionLease.cs");
			int enter = lease.IndexOf("EnterMutation(\"read section \" + SectionId)",
				StringComparison.Ordinal);
			int inspect = lease.IndexOf("InspectStable(SectionId, payload, out nested)", enter,
				StringComparison.Ordinal);
			int reread = lease.IndexOf("KingdomCivicMemoryState afterInspection = Current", inspect,
				StringComparison.Ordinal);
			int identity = lease.IndexOf("ReferenceEquals(afterInspection, snapshot)", reread,
				StringComparison.Ordinal);
			int revision = lease.IndexOf("afterInspection.Revision != snapshot.Revision", identity,
				StringComparison.Ordinal);
			int issue = lease.IndexOf("Lease = new KingdomCivicMemorySectionLease", revision,
				StringComparison.Ordinal);
			int release = lease.IndexOf("finally { MutationInProgress = false; }", issue,
				StringComparison.Ordinal);

			Assert.Greater(enter, -1);
			Assert.Greater(inspect, enter);
			Assert.Greater(reread, inspect);
			Assert.Greater(identity, reread);
			Assert.Greater(revision, identity);
			Assert.Greater(issue, revision,
				"no lease may escape before both snapshot identity and revision are rechecked");
			Assert.Greater(release, issue,
				"every return and family exception must release only the guard this read acquired");
		}

		[Test]
		public void EveryMirroredCapIsBoundBackToItsFrozenConstant()
		{
			string derivation = Source("Core", "KingdomCivicMemoryDerivation.cs");
			string[] bindings =
			{
				"KingdomCivicArtifactsCodec.MaxEnvelopeBytes",
				"KingdomCivicPracticeCodec.MaxEnvelopeBytes",
				"KingdomBodyHistoryCodec.MaxEnvelopeBytes",
				"KingdomCuriosityLeadCodec.MaxCuriosityBookBytes",
				"KingdomCuriosityLeadCodec.MaxLeadBookBytes",
				"Treaty.KingdomTreatyCodec.MaxEnvelopeBytes",
				"KingdomCommunalRiteCodec.MaxEnvelopeBytes",
				"KingdomGuestFeastCodec.MaxEnvelopeBytes"
			};
			foreach (string binding in bindings)
				StringAssert.Contains(binding, derivation,
					"every mirrored cap must be checked against the real constant at runtime");

			string save = Save();
			int verify = save.IndexOf("KingdomCivicMemoryDerivation.Verify(out derivation)",
				StringComparison.Ordinal);
			int latch = save.IndexOf("if (Records.Latch.Tripped)", StringComparison.Ordinal);
			Assert.Greater(verify, -1, "BeforeSave must verify the derivation");
			Assert.Greater(latch, verify, "the derivation is checked before the latch");
		}

		/// <summary>The revision counter must refuse to wrap rather than turn over.</summary>
		[Test]
		public void TheCommitPathRefusesTheLastRevisionRatherThanWrappingIt()
		{
			string commit = Source("Core", "KingdomCivicMemoryAuthority.Commit.cs");
			int guard = commit.IndexOf("Current.Revision == long.MaxValue", StringComparison.Ordinal);
			int increment = commit.IndexOf("Current.Revision + 1", StringComparison.Ordinal);
			Assert.Greater(guard, -1, "the commit path must name the last expressible revision");
			Assert.Greater(increment, guard, "and must refuse it before incrementing");
		}

		[Test]
		public void DecodeSnapshotsCallerOwnedBytesBeforeCheckingOrParsingThem()
		{
			string decode = Source("Core", "KingdomCivicMemoryCodec.Decode.cs");
			int cap = decode.IndexOf("if (Bytes.Length > KingdomCivicMemoryLimits.MaxEnvelopeBytes)",
				StringComparison.Ordinal);
			int clone = decode.IndexOf("byte[] snapshot = (byte[])Bytes.Clone();",
				StringComparison.Ordinal);
			int length = decode.IndexOf("snapshot.Length", clone, StringComparison.Ordinal);
			int integrity = decode.IndexOf("VerifyIntegrity(snapshot);", length,
				StringComparison.Ordinal);
			int parse = decode.IndexOf("ReadSections(reader, stream", integrity,
				StringComparison.Ordinal);

			Assert.Greater(cap, -1);
			Assert.Greater(clone, cap, "oversized input must be refused before a second allocation");
			Assert.Greater(length, clone);
			Assert.Greater(integrity, length);
			Assert.Greater(parse, integrity);
			Assert.IsFalse(decode.Contains("VerifyIntegrity(Bytes)"));
			Assert.IsFalse(decode.Contains("new MemoryStream(Bytes"));
		}

		[Test]
		public void TheLoaderReadsTheProvedSystemBesideTheRealmAndSealWithoutResurrection()
		{
			string loader = Source("Core", "KingdomLoader.cs");
			int realm = loader.IndexOf("GetSystem<KingdomSystem>()", StringComparison.Ordinal);
			int seal = loader.IndexOf("GetSystem<KingdomSeal>()", realm,
				StringComparison.Ordinal);
			int civic = loader.IndexOf("GetSystem<KingdomCivicMemorySystem>()",
				StringComparison.Ordinal);
			int absent = loader.IndexOf(
				"if (kingdomSystem == null || seal == null || memory == null) return;",
				civic, StringComparison.Ordinal);
			Assert.Greater(realm, -1);
			Assert.Greater(seal, realm);
			Assert.Greater(civic, seal,
				"civic memory must be read alongside the already-proved realm and seal systems");
			Assert.Greater(absent, civic,
				"a prepared-removal roster must return without recreating any carrier");
			StringAssert.DoesNotContain("RequireSystem<KingdomSystem>()", loader);
			StringAssert.DoesNotContain("RequireSystem<KingdomSeal>()", loader);
			StringAssert.DoesNotContain("RequireSystem<KingdomCivicMemorySystem>()", loader);
		}

		[Test]
		public void EveryProductionFileStaysUnderThreeHundredLines()
		{
			foreach (string path in Directory.GetFiles(
				Path.Combine(TestMain.RepositoryRoot, "Core"), "KingdomCivicMemory*.cs"))
				Assert.Less(File.ReadAllLines(path).Length, 300,
					Path.GetFileName(path) + " must stay under 300 physical lines");
		}
	}
}
#endif
