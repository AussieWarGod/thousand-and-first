#if TAF_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>#272: a realm that has not founded its first city must be saveable. The dormant
	/// lifecycle book is written in the growth-free v5 frame; the growth envelope gate stays
	/// strict and never sees the unbound growth book.</summary>
	public class KingdomLifecycleDormantSaveTests
	{
		private const string Txn = "0123456789abcdef0123456789abcdef";
		private const string Refusal = "growth envelope is not bounded and writable";
		private const string OpaqueRefusal = "opaque growth envelope is malformed";
		private const string BindFault = "lifecycle authority quarantined by an unbound fault";

		// The exact 139-byte v5 image of new KingdomLifecycleBook(); every reader since v0.3.0
		// parses it into the same pristine, foundable book.
		private const string DormantHex =
			"32434C540500000000FFFFFFFF00FFFFFFFFFFFFFFFF00FFFFFFFF01000000000000000000000000"
			+ "00000001000000000000000000000000000000010000000000000000000000000000000100000000"
			+ "00000000000000000000000000000000000000000000000000000000000000000000000000000000"
			+ "00000000000000000000000000000000000000";

		// Reviewed against DormantLifecycleWireExact, PristineGrowthBook,
		// ConstructorDefaultRaidLedger and the v5 frame. A new serialized field fails T9 until
		// that review is repeated and the array updated.
		private static readonly string[] LifecycleFields = { "FormatVersion", "LegacyIdentity",
			"LegacyMigrationKey", "Quarantined", "Fault", "SettlementId", "IdentityBound",
			"IdentityProof", "PlainGuestNextSequence", "PlainGuestRetiredThrough",
			"NotableGuestNextSequence", "NotableGuestRetiredThrough", "RaidNextSequence",
			"RaidRetiredThrough", "PetitionNextSequence", "PetitionRetiredThrough", "LocusOption",
			"LocusOptionTick", "NotableOption", "NotableOptionTick", "RaidOption", "RaidOptionTick",
			"PetitionOption", "PetitionOptionTick", "PlainGuest", "NotableGuest", "Raid", "Petition",
			"Resources", "RecentProofs", "RaidLedger", "Growth" };
		private static readonly string[] GrowthFields = { "FormatVersion", "Quarantined", "Fault",
			"OpaqueWireVersion", "OpaquePayload", "SettlementId", "IdentityBound", "IdentityProof",
			"MigratedFromLifecycleVersion", "MigrationPending", "MigrationTick", "OptionState",
			"OptionTick", "HealthState", "HealthTick", "ScarcityOptionState", "ScarcityOptionTick",
			"WorkPaused", "WorkPauseStartedTick", "WorkPausedTicks", "EffectiveWorkTick",
			"LastHeartbeatTick", "NextArrivalTick", "ArrivalIntervalTicks", "ArrivalEventStreamId",
			"ArrivalRulesVersion", "ArrivalRateEpoch", "ArrivalRateEpochStartedTick",
			"ArrivalProcessedThroughTick", "ArrivalCadenceNextDueTick", "ArrivalRateCohort",
			"ArrivalOrdinalHighWater", "ArrivalOrdinalRetiredThrough", "ArrivalCadenceMigrationPending",
			"ArrivalCadenceResumePending", "ArrivalOpportunity", "ArrivalDebtRanges", "LastFetchTick",
			"LastMillTick", "LastSubsidenceTick", "LastDeliveryTick", "LastDepartureTick",
			"PendingCrop", "PendingCropBlueprint", "PendingCropZoneId", "HeartbeatNextSequence",
			"HeartbeatRetiredThrough", "ArrivalNextSequence", "ArrivalRetiredThrough",
			"DepartureNextSequence", "DepartureRetiredThrough", "DeliveryNextSequence",
			"DeliveryRetiredThrough", "FetchNextSequence", "FetchRetiredThrough", "MillNextSequence",
			"MillRetiredThrough", "ArrivalCandidateNextSequence", "ArrivalCandidateRetiredThrough",
			"HeartbeatOp", "ArrivalOp", "DepartureOp", "DeliveryOp", "FetchOp", "MillOp",
			"ArrivalCandidate", "FirstGuestTerminal", "FieldOps", "CropRows", "Resources",
			"RecentProofs" };
		private static readonly string[] RaidLedgerFields = { "Version", "StateRevision",
			"ScheduleRevision", "Grievances", "Incidents", "ActiveIncidentId",
			"LegacyEvidenceArchived", "LegacyRaidState", "LegacyFaction", "LegacyDueTick",
			"LegacyLastTick", "LegacyTimesDeferred", "OpaqueFuturePayload" };

		private static byte[] Write(KingdomLifecycleBook book)
		{
			using (MemoryStream stream = new MemoryStream())
			{
				KingdomLifecycleWireCodec.WriteLifecycle(new BinaryWriter(stream), book);
				return stream.ToArray();
			}
		}

		private static KingdomLifecycleBook Read(byte[] bytes)
		{
			KingdomLifecycleBook result = new KingdomLifecycleBook();
			using (MemoryStream stream = new MemoryStream(bytes, false))
			using (BinaryReader reader = new BinaryReader(stream))
			{
				KingdomLifecycleWireCodec.ReadLifecycle(reader, result);
				ClassicAssert.AreEqual(stream.Length, stream.Position, "no trailing lifecycle bytes");
			}
			return result;
		}

		private static int WireVersion(byte[] bytes) { return BitConverter.ToInt32(bytes, 4); }

		private static void Mint(out string realm, out string settlement)
		{
			KingdomIdentityFault fault;
			ClassicAssert.IsTrue(KingdomIdentityRules.TryMintRealm(Txn, out realm, out fault));
			ClassicAssert.IsTrue(KingdomIdentityRules.TryMintSettlement(realm, Txn, out settlement,
				out fault));
		}

		private static KingdomLifecycleBook Bound(string id)
		{
			KingdomLifecycleBook book = new KingdomLifecycleBook();
			ClassicAssert.IsTrue(KingdomLifecycleRules.BindSettlementIdentity(book, id, false, null,
				new List<string>()));
			return book;
		}

		private static KingdomLifecycleBook CountersAndOptions(string fault)
		{
			return new KingdomLifecycleBook
			{
				Quarantined = true, Fault = fault, PlainGuestNextSequence = 7L,
				PlainGuestRetiredThrough = 6L, LocusOption = KingdomLifecycleOptionState.Enabled,
				LocusOptionTick = 1200L
			};
		}

		/// <summary>Every state the dormant frame must admit: T1, T4 (two faults) and T6.</summary>
		private static IEnumerable<KeyValuePair<string, KingdomLifecycleBook>> AdmittedStates()
		{
			KingdomLifecycleBook pristine = new KingdomLifecycleBook();
			KingdomLifecycleRules.Normalize(pristine);
			yield return new KeyValuePair<string, KingdomLifecycleBook>("pristine", pristine);
			yield return new KeyValuePair<string, KingdomLifecycleBook>("seat bind fault",
				new KingdomLifecycleBook { Quarantined = true,
					Fault = "lifecycle book could not bind exact seated-city identity" });
			yield return new KeyValuePair<string, KingdomLifecycleBook>("non-seat bind fault",
				new KingdomLifecycleBook { Quarantined = true,
					Fault = "non-seat lifecycle book does not match immutable city identity" });
			yield return new KeyValuePair<string, KingdomLifecycleBook>("counters and options",
				CountersAndOptions(BindFault));
			yield return new KeyValuePair<string, KingdomLifecycleBook>("4096-char fault",
				CountersAndOptions(new string('q', KingdomLifecycleRules.MaxTextChars)));
		}

		private static string[] SerializedFieldNames(Type type)
		{
			List<string> names = new List<string>();
			foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
				if (!Attribute.IsDefined(field, typeof(NonSerializedAttribute))) names.Add(field.Name);
			names.Sort(StringComparer.Ordinal);
			return names.ToArray();
		}

		private static void AssertSameFields(object expected, object actual, string path, int depth)
		{
			if (expected == null || actual == null)
			{
				ClassicAssert.AreEqual(expected == null, actual == null, path);
				return;
			}
			Type type = expected.GetType();
			ClassicAssert.AreEqual(type, actual.GetType(), path);
			if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal))
			{
				ClassicAssert.AreEqual(expected, actual, path);
				return;
			}
			if (expected is Array array)
			{
				CollectionAssert.AreEqual(array, (Array)actual, path);
				return;
			}
			ClassicAssert.Less(depth, 8, path + " nests too deeply for a dormant book");
			if (expected is IList list)
			{
				IList other = (IList)actual;
				ClassicAssert.AreEqual(list.Count, other.Count, path + ".Count");
				for (int i = 0; i < list.Count; i++)
					AssertSameFields(list[i], other[i], path + "[" + i + "]", depth + 1);
				return;
			}
			foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
				if (!Attribute.IsDefined(field, typeof(NonSerializedAttribute)))
					AssertSameFields(field.GetValue(expected), field.GetValue(actual),
						path + "." + field.Name, depth + 1);
		}

		private static KingdomLifecycleBook UnboundQuarantine()
		{
			return new KingdomLifecycleBook { Quarantined = true, Fault = BindFault };
		}

		/// <summary>The dormant predicate refuses the state, and the strict writer throws rather
		/// than truncate it into the growth-free v5 frame.</summary>
		private static void Refuse(string name, KingdomLifecycleBook book)
		{
			ClassicAssert.IsFalse(KingdomLifecycleRules.DormantLifecycleWireExact(book), name);
			Assert.Throws<InvalidDataException>(() => Write(book), name);
		}

		/// <summary>What the historical v5 frame alone would carry back for this book.</summary>
		private static KingdomLifecycleBook V5Image(KingdomLifecycleBook book)
		{
			using (MemoryStream stream = new MemoryStream())
			{
				KingdomLifecycleWireCodec.WriteLifecycleV5Fixture(new BinaryWriter(stream), book);
				return Read(stream.ToArray());
			}
		}

		/// <summary>Production-minted raid authority: TryApply mints one grievance and its live
		/// incident from an authored warning (the KingdomRaidIncidentRulesTests shape); a source
		/// cancellation then resolves it and leaves no active incident.</summary>
		private static KingdomRaidLedger RaidHistory(bool resolved)
		{
			KingdomLifecycleOperation warning = new KingdomLifecycleOperation
			{
				Lane = KingdomLifecycleLane.Raid, Action = KingdomLifecycleAction.RaidWarning,
				SettlementId = "city-raid-history", ZoneId = "zone-a", Origin = "source-a",
				ObjectName = "authored act", Faction = "Snapjaws", DisplayFaction = "salt-road scouts",
				Creed = "explicit-slight", Detail = "specific authored evidence",
				ArrivalText = "zone-source", Target = 1, Count = 2, CreatedTick = 10L,
				DepartTick = 110L, PlunderRequested = 6, Kind = 24, Blueprint = "snapjaw-foragers"
			};
			warning.ObjectId = KingdomRaidIncidentRules.GrievanceId("source-a");
			warning.ObjectMarker = KingdomRaidIncidentRules.IncidentId(warning.ObjectId);
			ClassicAssert.IsTrue(KingdomRaidIncidentRules.TryApply(new KingdomRaidLedger(), warning,
				out KingdomRaidLedger ledger), "authored warning mints a grievance and an incident");
			ClassicAssert.NotNull(ledger.ActiveIncidentId);
			if (!resolved) return ledger;
			KingdomRaidIncident incident = KingdomRaidIncidentRules.Active(ledger);
			KingdomLifecycleOperation cancel = new KingdomLifecycleOperation
			{
				Id = KingdomLifecycleRules.ChildId(incident.Id,
					"test-response-" + (byte)KingdomLifecycleAction.RaidCancel, 0),
				Lane = KingdomLifecycleLane.Raid, Action = KingdomLifecycleAction.RaidCancel,
				SettlementId = incident.SettlementId, ZoneId = incident.TargetZoneId,
				ObjectId = incident.Id, Faction = incident.AttackerFactionId, CreatedTick = 40L,
				Kind = (int)KingdomRaidResolution.SourceInvalid
			};
			ClassicAssert.IsTrue(KingdomRaidIncidentRules.TryApply(ledger, cancel, out ledger),
				"source cancellation resolves the only incident");
			ClassicAssert.IsNull(ledger.ActiveIncidentId);
			ClassicAssert.AreEqual(1, ledger.Incidents.Count);
			return ledger;
		}

		/// <summary>A value different from the current one; unsupported field types fail loudly so
		/// a new field cannot slip past the reflective dormant-frame pin.</summary>
		private static object NonDefault(Type type, object current)
		{
			if (type == typeof(bool)) return !(bool)current;
			if (type == typeof(int)) return (int)current + 1;
			if (type == typeof(long)) return (long)current + 1L;
			if (type == typeof(ulong)) return (ulong)current + 1UL;
			if (type == typeof(string)) return current == null ? "x" : (string)current + "x";
			if (type == typeof(byte[])) return current == null ? new byte[] { 1 } : null;
			if (type.IsEnum)
			{
				foreach (object value in Enum.GetValues(type))
					if (!value.Equals(current)) return value;
			}
			else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
			{
				IList list = (IList)Activator.CreateInstance(type);
				Type element = type.GetGenericArguments()[0];
				list.Add(element.IsValueType ? Activator.CreateInstance(element) : null);
				return list;
			}
			else if (type.IsClass) return current == null ? Activator.CreateInstance(type, true) : null;
			throw new NotSupportedException("no non-default value for " + type.Name);
		}

		[Test]
		public void NewGameDormantLifecycleSavesAndColdLoadsPristine()
		{
			// Core/KingdomSystem.z02.State.City.cs:105 field initializer, as every new game has it.
			KingdomLifecycleBook dormant = new KingdomLifecycleBook();
			KingdomLifecycleRules.Normalize(dormant);
			byte[] saved = Write(dormant);
			ClassicAssert.AreEqual(KingdomLifecycleRules.LegacyLifecycleFormatVersion, WireVersion(saved));
			KingdomLifecycleBook loaded = Read(saved);
			ClassicAssert.IsFalse(loaded.Quarantined);
			ClassicAssert.IsFalse(loaded.WireRejected);
			ClassicAssert.AreEqual(KingdomLifecycleRules.CurrentFormatVersion, loaded.FormatVersion);
			ClassicAssert.IsTrue(KingdomLifecycleRules.DormantLifecycleWireExact(loaded));
			CollectionAssert.AreEqual(saved, Write(loaded), "save/load/save is byte-stable");
		}

		[Test]
		public void ColdLoadedDormantBookStillFoundsTheFirstCity()
		{
			KingdomLifecycleBook loaded = Read(Write(new KingdomLifecycleBook()));
			Mint(out string realm, out string settlement);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPrepareFirstIdentityBooks(loaded,
				new KingdomCarryBook(), realm, settlement, out KingdomLifecycleBook founded,
				out KingdomCarryBook carry));
			ClassicAssert.NotNull(carry);
			ClassicAssert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(founded));
			byte[] foundedWire = Write(founded);
			ClassicAssert.AreEqual(KingdomLifecycleRules.CurrentFormatVersion, WireVersion(foundedWire),
				"bound authority keeps the current frame and growth section");
			ClassicAssert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(Read(foundedWire)));
		}

		[Test]
		public void GrowthEnvelopeGateStaysStrictForUnboundGrowth()
		{
			KingdomGrowthBook unbound = new KingdomGrowthBook();
			ClassicAssert.IsFalse(KingdomLifecycleRules.GrowthEnvelopeWritable(unbound));
			InvalidDataException refused = Assert.Throws<InvalidDataException>(() =>
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(unbound));
			StringAssert.StartsWith(Refusal, refused.Message);
			ClassicAssert.AreEqual(Refusal + " (identity-unbound)", refused.Message);
		}

		[TestCase("lifecycle book could not bind exact seated-city identity")]
		[TestCase("non-seat lifecycle book does not match immutable city identity")]
		public void UnboundBindQuarantineSavesAndColdLoadsExactly(string fault)
		{
			// Core/KingdomSystem.z06.Identity.Topology.cs:121-123 and z25:62-64 leave this shape.
			KingdomLifecycleBook quarantined = new KingdomLifecycleBook { Quarantined = true, Fault = fault };
			byte[] saved = Write(quarantined);
			ClassicAssert.AreEqual(KingdomLifecycleRules.LegacyLifecycleFormatVersion, WireVersion(saved));
			KingdomLifecycleBook loaded = Read(saved);
			ClassicAssert.IsTrue(loaded.Quarantined);
			ClassicAssert.AreEqual(fault, loaded.Fault);
			ClassicAssert.IsFalse(loaded.IdentityBound);
			ClassicAssert.IsFalse(loaded.WireRejected);
			ClassicAssert.IsFalse(KingdomLifecycleRules.CanOwnAuthority(loaded));
			CollectionAssert.AreEqual(saved, Write(loaded));
		}

		[Test]
		public void SettlementRowDefaultBookSaves()
		{
			KingdomLifecycleBook row = new KingdomSettlement().LifecycleBook;
			ClassicAssert.AreEqual(KingdomLifecycleRules.LegacyLifecycleFormatVersion,
				WireVersion(Write(row)));
		}

		[TestCase(false)]
		[TestCase(true)]
		public void CanonicalUnboundQuarantineWithCountersAndOptionsRoundTripsExactly(bool longFault)
		{
			string fault = longFault ? new string('q', KingdomLifecycleRules.MaxTextChars) : BindFault;
			KingdomLifecycleBook book = CountersAndOptions(fault);
			ClassicAssert.IsTrue(KingdomLifecycleRules.DormantLifecycleWireExact(book));
			byte[] saved = Write(book);
			ClassicAssert.AreEqual(KingdomLifecycleRules.LegacyLifecycleFormatVersion, WireVersion(saved));
			KingdomLifecycleBook loaded = Read(saved);
			ClassicAssert.IsTrue(loaded.Quarantined);
			ClassicAssert.AreEqual(fault, loaded.Fault);
			ClassicAssert.AreEqual(7L, loaded.PlainGuestNextSequence);
			ClassicAssert.AreEqual(6L, loaded.PlainGuestRetiredThrough);
			ClassicAssert.AreEqual(KingdomLifecycleOptionState.Enabled, loaded.LocusOption);
			ClassicAssert.AreEqual(1200L, loaded.LocusOptionTick);
			ClassicAssert.IsFalse(KingdomLifecycleRules.CanOwnAuthority(loaded));
			AssertSameFields(book, loaded, "book", 0);
			CollectionAssert.AreEqual(saved, Write(loaded), "save/load/save is byte-stable");
		}

		[Test]
		public void NonDormantUnboundStateIsNeverRedirectedOrTruncated()
		{
			KingdomLifecycleOperation operation = KingdomLifecycleRules.PrepareOperation(
				Bound("city-dormant-operation"), KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Spawn, 1L);
			ClassicAssert.NotNull(operation, "production lane-operation fixture");
			Action<string, Action<KingdomLifecycleBook>> refuse = (name, change) =>
			{
				KingdomLifecycleBook book = new KingdomLifecycleBook();
				change(book);
				ClassicAssert.IsFalse(KingdomLifecycleRules.DormantLifecycleWireExact(book), name);
				Assert.Throws<InvalidDataException>(() => Write(book), name);
			};
			refuse("growth clock", b => b.Growth.OptionTick = 1L);
			refuse("growth fault", b => b.Growth.Fault = "x");
			refuse("raid ledger revision", b => b.RaidLedger.StateRevision = 1L);
			// A payload on a current-version ledger is malformed (ValidLedger); the genuine future
			// ledger is pinned by GenuineFutureRaidLedgerIsNeverDroppedIntoTheV5Frame.
			refuse("malformed raid ledger payload", b => b.RaidLedger.OpaqueFuturePayload = new byte[] { 1 });
			refuse("quarantine without fault", b => b.Quarantined = true);
			refuse("settlement id without binding", b => b.SettlementId = "city");
			refuse("wire rejected", b => b.WireRejected = true);
			// The predicate runs on every save: absent parts are refused, never dereferenced.
			refuse("null growth", b => b.Growth = null);
			refuse("null raid ledger", b => b.RaidLedger = null);
			refuse("null resource rows", b => b.Resources = null);
			refuse("null proof rows", b => b.RecentProofs = null);
			refuse("locus option set", b =>
			{
				b.LocusOption = KingdomLifecycleOptionState.Enabled;
				b.LocusOptionTick = 1L;
			});
			refuse("lane operation", b => b.PlainGuest = operation);
			refuse("quarantined dirty raid ledger", b =>
			{
				b.Quarantined = true;
				b.Fault = BindFault;
				b.RaidLedger.StateRevision = 1L;
			});
			refuse("quarantined malformed raid ledger payload", b =>
			{
				b.Quarantined = true;
				b.Fault = BindFault;
				b.RaidLedger.OpaqueFuturePayload = new byte[] { 1 };
			});
		}

		[TestCase(false)]
		[TestCase(true)]
		public void GenuineFutureRaidLedgerIsNeverDroppedIntoTheV5Frame(bool quarantined)
		{
			// KingdomRaidLedger.cs:23-24: an older build keeps a newer framed ledger byte-exact.
			// ValidLedger admits it and PristineLifecycleBook never tests Version or the payload, so
			// only ConstructorDefaultRaidLedger keeps it out of the ledger-free v5 frame.
			KingdomLifecycleBook book = quarantined ? UnboundQuarantine() : new KingdomLifecycleBook();
			book.RaidLedger.Version = KingdomRaidLedger.CurrentVersion + 1;
			book.RaidLedger.OpaqueFuturePayload = new byte[] { 4, 2 };
			ClassicAssert.IsTrue(KingdomRaidIncidentRules.ValidLedger(book.RaidLedger), "genuine future ledger");
			Refuse("genuine future raid ledger", book);
			AssertSameFields(new KingdomRaidLedger(), V5Image(book).RaidLedger, "v5 image drops it", 0);
		}

		[Test]
		public void NonDefaultRaidLedgerOnAnUnboundQuarantineIsNeverDropped()
		{
			// CanonicalLifecycleQuarantine accepts any ValidLedger, so on this arm only
			// ConstructorDefaultRaidLedger keeps raid authority out of the ledger-free v5 frame.
			Action<string, Action<KingdomLifecycleBook>> refuse = (name, change) =>
			{
				KingdomLifecycleBook book = UnboundQuarantine();
				change(book);
				ClassicAssert.IsTrue(KingdomRaidIncidentRules.ValidLedger(book.RaidLedger), name + ": valid");
				Refuse(name, book);
				AssertSameFields(new KingdomRaidLedger(), V5Image(book).RaidLedger,
					name + ": the v5 image drops it", 0);
			};
			refuse("schedule revision", b => b.RaidLedger.ScheduleRevision = 1L);
			refuse("archived legacy evidence", b => b.RaidLedger.LegacyEvidenceArchived = true);
			refuse("archived legacy raid values", b =>
			{
				b.RaidLedger.LegacyEvidenceArchived = true;
				b.RaidLedger.LegacyRaidState = 2;
				b.RaidLedger.LegacyFaction = "Snapjaws";
				b.RaidLedger.LegacyDueTick = 100L;
				b.RaidLedger.LegacyLastTick = 90L;
				b.RaidLedger.LegacyTimesDeferred = 1;
			});
			refuse("live raid incident", b => b.RaidLedger = RaidHistory(false));
			refuse("resolved raid history", b =>
			{
				// Zeroed revisions leave the grievance and incident counts as the only difference.
				KingdomRaidLedger history = RaidHistory(true);
				history.StateRevision = 0L;
				history.ScheduleRevision = 0L;
				b.RaidLedger = history;
			});
		}

		[Test]
		public void NonCanonicalUnboundQuarantineIsNeverRedirected()
		{
			// TryStageGrowthMigrationFromV5 rebuilds an unbound v5 book only through
			// PristineLifecycleBook or CanonicalLifecycleQuarantine, so any other quarantine
			// written in that frame would save and then refuse to load.
			KingdomLifecycleOperation operation = KingdomLifecycleRules.PrepareOperation(
				Bound("city-dormant-operation"), KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Spawn, 1L);
			ClassicAssert.NotNull(operation, "production lane-operation fixture");
			Action<string, Action<KingdomLifecycleBook>> refuse = (name, change) =>
			{
				KingdomLifecycleBook book = UnboundQuarantine();
				change(book);
				Refuse(name, book);
				Assert.Throws<InvalidDataException>(() => V5Image(book), name + ": v5 image never loads");
			};
			refuse("identity proof without binding", b => b.IdentityProof = "proof");
			refuse("settlement id without binding", b => b.SettlementId = "city");
			refuse("legacy identity without binding", b =>
			{
				b.LegacyIdentity = true;
				b.LegacyMigrationKey = "legacy-key";
			});
			refuse("legacy key without legacy identity", b => b.LegacyMigrationKey = "legacy-key");
			refuse("broken lane counter", b =>
			{
				b.NotableGuestNextSequence = 5L;
				b.NotableGuestRetiredThrough = 2L;
			});
			refuse("unknown option", b => b.NotableOption = (KingdomLifecycleOptionState)250);
			refuse("negative option tick", b => b.RaidOptionTick = -1L);
			refuse("overlong fault", b => b.Fault = new string('q', KingdomLifecycleRules.MaxTextChars + 1));
			refuse("lane operation", b => b.PlainGuest = operation);
		}

		[Test]
		public void OuterRowsOnAnUnboundQuarantineKeepTheStrictGate()
		{
			// The dormant frame admits only an empty outer registry. These valid rows would survive
			// the v5 frame, but they are not dormant state, so the book keeps the strict gate.
			KingdomLifecycleOperation operation = KingdomLifecycleRules.PrepareOperation(
				Bound("city-dormant-proof"), KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Spawn, 1L);
			ClassicAssert.NotNull(operation, "production lane-operation fixture");
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPlanHash(operation, out string planHash));
			KingdomLifecycleBook resource = CountersAndOptions(BindFault);
			resource.Resources.Add(new KingdomLifecycleResourceRevision
			{
				Kind = KingdomLifecycleResourceKind.Population, ScopeId = "realm-scope",
				SubjectId = "population", Revision = 3L,
				Key = KingdomLifecycleRules.ResourceKey(KingdomLifecycleResourceKind.Population,
					"realm-scope", "population")
			});
			KingdomLifecycleBook proof = CountersAndOptions(BindFault);
			proof.RecentProofs.Add(new KingdomLifecycleProof
			{
				Sequence = 1L, Lane = KingdomLifecycleLane.PlainGuest,
				Action = KingdomLifecycleAction.Spawn, Tick = 1L, PlanHash = planHash,
				Id = KingdomLifecycleRules.OperationId(null, KingdomLifecycleLane.PlainGuest, 1L)
			});
			foreach (KeyValuePair<string, KingdomLifecycleBook> row in new[]
			{
				new KeyValuePair<string, KingdomLifecycleBook>("resource row", resource),
				new KeyValuePair<string, KingdomLifecycleBook>("recent proof", proof)
			})
			{
				AssertSameFields(row.Value, V5Image(row.Value), row.Key + ": valid and representable", 0);
				Refuse(row.Key, row.Value);
			}
		}

		[TestCase("growth", false)]
		[TestCase("growth", true)]
		[TestCase("raid ledger", false)]
		[TestCase("raid ledger", true)]
		public void EveryNonDefaultFieldValueIsRefusedByTheDormantFrame(string part, bool quarantined)
		{
			// Field-level pin of PristineGrowthBook and ConstructorDefaultRaidLedger: neither
			// survives the v5 frame, which rebuilds both as constructor defaults.
			bool growth = part == "growth";
			Type type = growth ? typeof(KingdomGrowthBook) : typeof(KingdomRaidLedger);
			int fields = 0;
			foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
			{
				if (Attribute.IsDefined(field, typeof(NonSerializedAttribute))) continue;
				KingdomLifecycleBook book = quarantined ? UnboundQuarantine() : new KingdomLifecycleBook();
				ClassicAssert.IsTrue(KingdomLifecycleRules.DormantLifecycleWireExact(book), "baseline");
				object owner = growth ? (object)book.Growth : book.RaidLedger;
				field.SetValue(owner, NonDefault(field.FieldType, field.GetValue(owner)));
				ClassicAssert.IsFalse(KingdomLifecycleRules.DormantLifecycleWireExact(book),
					type.Name + "." + field.Name);
				fields++;
			}
			ClassicAssert.AreEqual(growth ? GrowthFields.Length : RaidLedgerFields.Length, fields,
				type.Name + " field census");
		}

		[TestCase("quarantined")]
		[TestCase("staged")]
		public void NonPristineGrowthOnAnUnboundQuarantineKeepsTheCurrentFrame(string growth)
		{
			// GrowthAttachmentValid admits these growth shapes under an unbound canonical
			// quarantine, and the v5 reader would rebuild them as pristine growth.
			KingdomLifecycleBook book = new KingdomLifecycleBook { Quarantined = true, Fault = BindFault };
			if (growth == "quarantined")
			{
				book.Growth.Quarantined = true;
				book.Growth.Fault = "growth evidence quarantined";
			}
			else
			{
				book.Growth.MigrationPending = true;
				book.Growth.MigratedFromLifecycleVersion = KingdomLifecycleRules.LegacyLifecycleFormatVersion;
			}
			ClassicAssert.IsTrue(KingdomLifecycleRules.GrowthEnvelopeWritable(book.Growth));
			ClassicAssert.IsFalse(KingdomLifecycleRules.DormantLifecycleWireExact(book));
			byte[] saved = Write(book);
			ClassicAssert.AreEqual(KingdomLifecycleRules.CurrentFormatVersion, WireVersion(saved));
			KingdomLifecycleBook loaded = Read(saved);
			AssertSameFields(book, loaded, "book", 0);
			CollectionAssert.AreEqual(saved, Write(loaded), "save/load/save is byte-stable");
		}

		[Test]
		public void DormantFrameIsTheShippedV5ImageOfThePristineBook()
		{
			// Same bytes the historical fixture writer emits, which every reader since v0.3.0 parses.
			using (MemoryStream stream = new MemoryStream())
			{
				KingdomLifecycleWireCodec.WriteLifecycleV5Fixture(new BinaryWriter(stream),
					new KingdomLifecycleBook());
				CollectionAssert.AreEqual(stream.ToArray(), Write(new KingdomLifecycleBook()));
				ClassicAssert.AreEqual(DormantHex,
					BitConverter.ToString(Write(new KingdomLifecycleBook())).Replace("-", ""));
			}
		}

		[Test]
		public void DormantFrameFieldCensusIsFrozen()
		{
			string review = "review DormantLifecycleWireExact, PristineGrowthBook, "
				+ "ConstructorDefaultRaidLedger and the v5 frame, then update the frozen array";
			string[] lifecycle = (string[])LifecycleFields.Clone();
			string[] growth = (string[])GrowthFields.Clone();
			string[] ledger = (string[])RaidLedgerFields.Clone();
			Array.Sort(lifecycle, StringComparer.Ordinal);
			Array.Sort(growth, StringComparer.Ordinal);
			Array.Sort(ledger, StringComparer.Ordinal);
			CollectionAssert.AreEqual(lifecycle, SerializedFieldNames(typeof(KingdomLifecycleBook)),
				"KingdomLifecycleBook: " + review);
			CollectionAssert.AreEqual(growth, SerializedFieldNames(typeof(KingdomGrowthBook)),
				"KingdomGrowthBook: " + review);
			CollectionAssert.AreEqual(ledger, SerializedFieldNames(typeof(KingdomRaidLedger)),
				"KingdomRaidLedger: " + review);
		}

		[Test]
		public void DormantRoundTripPreservesEveryField()
		{
			int states = 0;
			foreach (KeyValuePair<string, KingdomLifecycleBook> state in AdmittedStates())
			{
				ClassicAssert.IsTrue(KingdomLifecycleRules.DormantLifecycleWireExact(state.Value),
					state.Key);
				byte[] saved = Write(state.Value);
				ClassicAssert.AreEqual(KingdomLifecycleRules.LegacyLifecycleFormatVersion,
					WireVersion(saved), state.Key);
				AssertSameFields(state.Value, Read(saved), state.Key, 0);
				states++;
			}
			ClassicAssert.AreEqual(5, states);
		}

		/// <summary>The refusal fixture for each reason token, from the root of the gate order.</summary>
		private static KingdomGrowthBook RefusalFixture(string shape)
		{
			switch (shape)
			{
				case "absent": return null;
				case "pristine-unbound": return new KingdomGrowthBook();
				case "older-format":
					return new KingdomGrowthBook { FormatVersion = KingdomLifecycleRules.CurrentGrowthFormatVersion - 1 };
				case "overlong-fault":
					return new KingdomGrowthBook { Fault = new string('q', KingdomLifecycleRules.MaxTextChars + 1) };
				case "malformed-opaque":
					return new KingdomGrowthBook { Quarantined = true, Fault = "growth evidence quarantined",
						OpaquePayload = new byte[] { 1 } };
				case "opaque-version-without-payload": return new KingdomGrowthBook { OpaqueWireVersion = 1 };
				case "field-cap":
					KingdomGrowthBook capped = new KingdomGrowthBook();
					for (int i = 0; i <= KingdomLifecycleRules.MaxGrowthFields; i++) capped.FieldOps.Add(null);
					return capped;
				case "staged-without-source": return new KingdomGrowthBook { MigrationPending = true };
				case "quarantine-without-fault": return new KingdomGrowthBook { Quarantined = true };
			}
			KingdomGrowthBook book = Bound("city-" + shape).Growth;
			ClassicAssert.IsTrue(KingdomLifecycleRules.GrowthEnvelopeWritable(book), "bound fixture starts writable");
			ClassicAssert.AreEqual("unknown", KingdomLifecycleRules.GrowthEnvelopeRefusalReason(book),
				"a writable book reproduces no refusal");
			if (shape == "altered-proof") book.IdentityProof = book.IdentityProof + "0";
			if (shape == "lone-surrogate")
			{
				book.PendingCrop = 1;
				book.PendingCropBlueprint = "\uD800";
				book.PendingCropZoneId = "zone-a";
			}
			if (shape == "unknown-option") book.OptionState = (KingdomLifecycleOptionState)250;
			return book;
		}

		[TestCase("absent", "absent")]
		[TestCase("pristine-unbound", "identity-unbound")]
		[TestCase("altered-proof", "identity-proof")]
		[TestCase("older-format", "format")]
		[TestCase("overlong-fault", "fault-text")]
		[TestCase("malformed-opaque", "opaque-evidence")]
		[TestCase("opaque-version-without-payload", "opaque-wire-version")]
		[TestCase("field-cap", "collections")]
		[TestCase("staged-without-source", "staged-shape")]
		[TestCase("quarantine-without-fault", "quarantine-shape")]
		[TestCase("lone-surrogate", "root-shape")]
		[TestCase("unknown-option", "root-shape")]
		public void RefusalReasonNamesTheFailingCheck(string shape, string reason)
		{
			KingdomGrowthBook book = RefusalFixture(shape);
			ClassicAssert.IsFalse(KingdomLifecycleRules.GrowthEnvelopeWritable(book), "verdict unchanged");
			ClassicAssert.AreEqual(reason, KingdomLifecycleRules.GrowthEnvelopeRefusalReason(book));
			InvalidDataException refused = Assert.Throws<InvalidDataException>(() =>
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(book));
			// An absent book is refused before any reason is computed; the opaque branch keeps its
			// own prefix.
			string expected = book == null ? "growth authority is absent"
				: (book.OpaquePayload != null ? OpaqueRefusal : Refusal) + " (" + reason + ")";
			ClassicAssert.AreEqual(expected, refused.Message);
			ClassicAssert.IsFalse(KingdomLifecycleRules.GrowthEnvelopeWritable(book), "reason never repairs");
		}
	}
}
#endif
