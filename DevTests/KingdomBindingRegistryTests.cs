#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using ThousandAndFirst;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// One identity, at most one body. LIVING-CITY-ARCHITECTURE §3.8 and invariant I3.
	/// <para>
	/// The registry is substrate, so these tests are written against the CONTRACT rather than
	/// against today's callers: the transient half is exercised in full even though nothing mints
	/// a job until W3, because a rule nobody can run is a rule nobody can trust, and retrofitting
	/// identity across six waves is a rewrite.
	/// </para>
	/// </summary>
	public class KingdomBindingRegistryTests
	{
		private const string Here = "taf:zone:here";

		private const string Next = "taf:zone:next";

		[Test]
		public void RegistryAbiKeepsExactEnumsRowsAndSaveColumns()
		{
			AssertEnum(typeof(KingdomBindingKind), new[] { "Resident", "Transient" },
				new byte[] { 0, 1 });
			AssertEnum(typeof(KingdomUnbindCause),
				new[] { "None", "Death", "Departure", "Abroad", "JobClosed", "Dissolved", "Accession", "ZoneHandoff" },
				new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 });
			AssertEnum(typeof(KingdomBodyPresence),
				new[] { "None", "Here", "Elsewhere", "Frozen" }, new byte[] { 0, 1, 2, 3 });
			AssertEnum(typeof(KingdomBindingVerdict),
				new[] { "Mint", "Move", "MoveAcross", "Refuse" }, new byte[] { 0, 1, 2, 3 });
			AssertEnum(typeof(KingdomSweepVerdict),
				new[] { "NotTransient", "Bound", "Stale" }, new byte[] { 0, 1, 2 });

			Assert.AreEqual("ThousandAndFirst.Simulation.City.KingdomBinding", typeof(KingdomBinding).FullName);
			Assert.IsFalse(typeof(KingdomBinding).IsPublic);
			Assert.IsTrue(typeof(KingdomBinding).IsValueType);
			FieldInfo[] rowFields = typeof(KingdomBinding).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
			Array.Sort(rowFields, (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
			CollectionAssert.AreEqual(new[] { "BindingKey", "Kind", "ZoneId", "ObjectId", "MintedTick" },
				Array.ConvertAll(rowFields, field => field.Name));
			CollectionAssert.AreEqual(new[] { typeof(int), typeof(KingdomBindingKind), typeof(string), typeof(string), typeof(long) },
				Array.ConvertAll(rowFields, field => field.FieldType));
			foreach (FieldInfo field in rowFields)
				Assert.IsTrue(field.IsAssembly && field.IsInitOnly, field.Name);

			Type registryType = typeof(KingdomBindingRegistry);
			Assert.AreEqual("ThousandAndFirst.Simulation.City.KingdomBindingRegistry", registryType.FullName);
			Assert.IsTrue(registryType.IsPublic);
			Assert.IsTrue(Attribute.IsDefined(registryType, typeof(SerializableAttribute)));
			FieldInfo[] columns = registryType.GetFields(BindingFlags.Instance | BindingFlags.Public);
			Array.Sort(columns, (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
			CollectionAssert.AreEqual(new[] { "Keys", "Kinds", "ZoneIds", "ObjectIds", "MintedTicks" },
				Array.ConvertAll(columns, field => field.Name));
			CollectionAssert.AreEqual(new[] { typeof(List<int>), typeof(List<int>), typeof(List<string>), typeof(List<string>), typeof(List<long>) },
				Array.ConvertAll(columns, field => field.FieldType));
			foreach (FieldInfo field in columns) Assert.IsFalse(field.IsInitOnly, field.Name);
			KingdomBindingRegistry registry = new KingdomBindingRegistry();
			Assert.AreEqual(0, registry.Count);
			Assert.AreEqual(0, registry.Keys.Count);
			Assert.AreEqual(0, registry.Kinds.Count);
			Assert.AreEqual(0, registry.ZoneIds.Count);
			Assert.AreEqual(0, registry.ObjectIds.Count);
			Assert.AreEqual(0, registry.MintedTicks.Count);
		}

		[Test]
		public void LogicalSourceKeepsTopLevelIdentitiesAndRegistryOrder()
		{
			string source = LogicalSource();
			Assert.AreEqual(1, Count(source, "public enum KingdomBindingKind : byte"));
			Assert.AreEqual(1, Count(source, "public enum KingdomUnbindCause : byte"));
			Assert.AreEqual(1, Count(source, "public enum KingdomBodyPresence : byte"));
			Assert.AreEqual(1, Count(source, "public enum KingdomBindingVerdict : byte"));
			Assert.AreEqual(1, Count(source, "public enum KingdomSweepVerdict : byte"));
			Assert.AreEqual(1, Count(source, "internal readonly struct KingdomBinding"));
			Assert.AreEqual(1, Count(source, "internal static class KingdomBindingRules"));
			Assert.AreEqual(1, Count(source, "internal sealed class KingdomBindingTable"));
			Assert.AreEqual(1, Count(source, "public class KingdomBindingRegistry"));
			Assert.Less(source.IndexOf("internal static KingdomBindingVerdict Judge", StringComparison.Ordinal),
				source.IndexOf("internal static bool TryCreate", StringComparison.Ordinal));
			Assert.Less(source.IndexOf("internal static bool TryCreate", StringComparison.Ordinal),
				source.IndexOf("public void Normalize", StringComparison.Ordinal));
		}

		private static KingdomBindingTable Bound(int key, KingdomBindingKind kind, string zone)
		{
			KingdomBindingTable next;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomBindingTable.Empty.TryBind(key, kind, zone, "obj-" + key, 700L, out next, out fault), fault.ToString());
			return next;
		}

		// ---- Check-before-mint, the table of §3.8 -------------------------------------------

		/// <summary>
		/// The four outcomes, by name. A body already here is MOVED and never minted; a body live
		/// in another resident zone moves across if it is a person and is refused if it is a
		/// porter; a body whose zone is on disk refuses the mint outright.
		/// </summary>
		[TestCase(KingdomBindingKind.Resident, KingdomBodyPresence.None, KingdomBindingVerdict.Mint)]
		[TestCase(KingdomBindingKind.Resident, KingdomBodyPresence.Here, KingdomBindingVerdict.Move)]
		[TestCase(KingdomBindingKind.Resident, KingdomBodyPresence.Elsewhere, KingdomBindingVerdict.MoveAcross)]
		[TestCase(KingdomBindingKind.Resident, KingdomBodyPresence.Frozen, KingdomBindingVerdict.Refuse)]
		[TestCase(KingdomBindingKind.Transient, KingdomBodyPresence.None, KingdomBindingVerdict.Mint)]
		[TestCase(KingdomBindingKind.Transient, KingdomBodyPresence.Here, KingdomBindingVerdict.Move)]
		[TestCase(KingdomBindingKind.Transient, KingdomBodyPresence.Elsewhere, KingdomBindingVerdict.Refuse)]
		[TestCase(KingdomBindingKind.Transient, KingdomBodyPresence.Frozen, KingdomBindingVerdict.Refuse)]
		public void CheckBeforeMintAnswersExactlyWhatTheConstitutionTabulates(KingdomBindingKind kind, KingdomBodyPresence presence, KingdomBindingVerdict expected)
		{
			Assert.AreEqual(expected, KingdomBindingRules.Judge(kind, presence));
		}

		/// <summary>
		/// §3.8's line with the teeth: <i>an unresolvable binding is a refusal to mint, never a
		/// licence to mint.</i> Only an outright MISS ever mints.
		/// </summary>
		[Test]
		public void OnlyAMissEverMints()
		{
			foreach (KingdomBindingKind kind in new KingdomBindingKind[2] { KingdomBindingKind.Resident, KingdomBindingKind.Transient })
			{
				for (int presence = 0; presence <= 8; presence++)
				{
					KingdomBindingVerdict verdict = KingdomBindingRules.Judge(kind, (KingdomBodyPresence)presence);
					Assert.AreEqual(presence == (int)KingdomBodyPresence.None, KingdomBindingRules.Mints(verdict),
						"presence " + presence + " of a " + kind + " minted when it should not have");
				}
			}
		}

		/// <summary>A presence a later build invents is refused, not minted. The default of a
		/// duplication rule is always the side that cannot duplicate.</summary>
		[Test]
		public void APresenceThisBuildHasNoWordForRefuses()
		{
			Assert.AreEqual(KingdomBindingVerdict.Refuse, KingdomBindingRules.Judge(KingdomBindingKind.Resident, (KingdomBodyPresence)200));
		}

		// ---- The table --------------------------------------------------------------------

		[Test]
		public void ABoundKeyIsFoundAndAnUnboundOneIsNot()
		{
			KingdomBindingTable table = Bound(7, KingdomBindingKind.Resident, Here);
			KingdomBinding binding;
			Assert.IsTrue(table.TryGet(7, KingdomBindingKind.Resident, out binding));
			Assert.AreEqual(Here, binding.ZoneId);
			Assert.AreEqual("obj-7", binding.ObjectId);
			Assert.AreEqual(700L, binding.MintedTick);
			Assert.IsFalse(table.TryGet(8, KingdomBindingKind.Resident, out binding));
		}

		/// <summary>
		/// A resident id and a job id of the same number are different keys, because the KIND is
		/// half the key. Without this, the first porter minted in a sixty-settler city would
		/// collide with a person.
		/// </summary>
		[Test]
		public void AResidentIdAndAJobIdOfTheSameNumberAreDifferentKeys()
		{
			KingdomBindingTable table = Bound(7, KingdomBindingKind.Resident, Here);
			KingdomBindingTable both;
			KingdomCityFault fault;
			Assert.IsTrue(table.TryBind(7, KingdomBindingKind.Transient, Next, "porter", 800L, out both, out fault), fault.ToString());
			Assert.AreEqual(2, both.Count);
			KingdomBinding person;
			KingdomBinding porter;
			Assert.IsTrue(both.TryGet(7, KingdomBindingKind.Resident, out person));
			Assert.IsTrue(both.TryGet(7, KingdomBindingKind.Transient, out porter));
			Assert.AreEqual(Here, person.ZoneId);
			Assert.AreEqual(Next, porter.ZoneId);
		}

		/// <summary>Invariant I3 at the door: binding a key that is already bound is refused rather
		/// than overwritten, because an overwrite is the instant the old body stops being accounted
		/// for and starts being a duplicate.</summary>
		[Test]
		public void BindingAKeyTwiceIsRefusedAndTheFirstBindingStands()
		{
			KingdomBindingTable table = Bound(7, KingdomBindingKind.Resident, Here);
			KingdomBindingTable next;
			KingdomCityFault fault;
			Assert.IsFalse(table.TryBind(7, KingdomBindingKind.Resident, Next, "impostor", 900L, out next, out fault));
			Assert.AreEqual(KingdomCityFault.DuplicateBinding, fault);
			Assert.IsNull(next, "a refused bind must publish nothing");
			KingdomBinding binding;
			Assert.IsTrue(table.TryGet(7, KingdomBindingKind.Resident, out binding));
			Assert.AreEqual(Here, binding.ZoneId);
		}

		/// <summary>A key of zero is not an identity, and a table cannot be built out of one.</summary>
		[Test]
		public void AKeyOfZeroIsRefused()
		{
			KingdomBindingTable table;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomBindingTable.Empty.TryBind(0, KingdomBindingKind.Resident, Here, "nobody", 700L, out table, out fault));
			Assert.AreEqual(KingdomCityFault.UnknownBinding, fault);
			Assert.IsFalse(KingdomBindingTable.TryCreate(
				new KingdomBinding[1] { new KingdomBinding(0, KingdomBindingKind.Resident, Here, "nobody", 700L) },
				out table, out fault));
			Assert.AreEqual(KingdomCityFault.UnknownBinding, fault);
		}

		/// <summary>Rebinding moves the ground and the object and keeps the minted tick: a body
		/// that walked across a zone line is the same body, and redating it would lose the one fact
		/// the registry is for.</summary>
		[Test]
		public void RebindingMovesTheGroundAndKeepsTheMintedTick()
		{
			KingdomBindingTable table = Bound(7, KingdomBindingKind.Resident, Here);
			KingdomBindingTable moved;
			KingdomCityFault fault;
			Assert.IsTrue(table.TryRebind(7, KingdomBindingKind.Resident, Next, "obj-7-again", out moved, out fault), fault.ToString());
			KingdomBinding binding;
			Assert.IsTrue(moved.TryGet(7, KingdomBindingKind.Resident, out binding));
			Assert.AreEqual(Next, binding.ZoneId);
			Assert.AreEqual("obj-7-again", binding.ObjectId);
			Assert.AreEqual(700L, binding.MintedTick);
			Assert.AreEqual(1, moved.Count, "a rebind moves a binding, it never adds one");
		}

		[Test]
		public void RebindingSomethingNothingHoldsIsRefused()
		{
			KingdomBindingTable moved;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomBindingTable.Empty.TryRebind(7, KingdomBindingKind.Resident, Next, "ghost", out moved, out fault));
			Assert.AreEqual(KingdomCityFault.UnknownBinding, fault);
			Assert.IsNull(moved);
		}

		/// <summary>
		/// Absence IS proof of closure (§3.8), which is why there is no second list — and why the
		/// eviction has to name a cause: it is the only moment anything is recorded about why the
		/// binding stopped.
		/// </summary>
		[Test]
		public void UnbindingEvictsAtOnceAndAbsenceIsProofOfClosure()
		{
			KingdomBindingTable table = Bound(7, KingdomBindingKind.Resident, Here);
			KingdomBindingTable next;
			KingdomBinding evicted;
			KingdomCityFault fault;
			Assert.IsTrue(table.TryUnbind(7, KingdomBindingKind.Resident, KingdomUnbindCause.Death, out next, out evicted, out fault), fault.ToString());
			Assert.AreEqual(0, next.Count);
			Assert.IsFalse(next.Holds(7, KingdomBindingKind.Resident));
			Assert.AreEqual(Here, evicted.ZoneId, "the evicted binding is handed back so the cause can be told about somewhere real");
		}

		/// <summary>An unbinding with no cause is refused. A settler who disappears and nothing in
		/// the game says why is the failure this rule exists to make impossible.</summary>
		[Test]
		public void UnbindingWithoutACauseIsRefused()
		{
			KingdomBindingTable table = Bound(7, KingdomBindingKind.Resident, Here);
			KingdomBindingTable next;
			KingdomBinding evicted;
			KingdomCityFault fault;
			Assert.IsFalse(table.TryUnbind(7, KingdomBindingKind.Resident, KingdomUnbindCause.None, out next, out evicted, out fault));
			Assert.AreEqual(KingdomCityFault.CauseRequired, fault);
			Assert.IsNull(next);
			Assert.IsTrue(table.Holds(7, KingdomBindingKind.Resident), "a refused unbind leaves the registry byte-identical");
		}

		[Test]
		public void UnbindingTheWrongKindLeavesTheOtherAlone()
		{
			KingdomBindingTable table = Bound(7, KingdomBindingKind.Resident, Here);
			KingdomBindingTable next;
			KingdomBinding evicted;
			KingdomCityFault fault;
			Assert.IsFalse(table.TryUnbind(7, KingdomBindingKind.Transient, KingdomUnbindCause.JobClosed, out next, out evicted, out fault));
			Assert.AreEqual(KingdomCityFault.UnknownBinding, fault);
			Assert.IsTrue(table.Holds(7, KingdomBindingKind.Resident));
		}

		/// <summary>Every transition is copy-on-write: the table handed in is never the table
		/// handed back, and a caller holding the old one still sees the old world.</summary>
		[Test]
		public void EveryTransitionIsCopyOnWrite()
		{
			KingdomBindingTable before = Bound(7, KingdomBindingKind.Resident, Here);
			KingdomBindingTable moved;
			KingdomBindingTable gone;
			KingdomBinding evicted;
			KingdomCityFault fault;
			Assert.IsTrue(before.TryRebind(7, KingdomBindingKind.Resident, Next, "obj-7", out moved, out fault));
			Assert.IsTrue(before.TryUnbind(7, KingdomBindingKind.Resident, KingdomUnbindCause.Departure, out gone, out evicted, out fault));
			KingdomBinding original;
			Assert.IsTrue(before.TryGet(7, KingdomBindingKind.Resident, out original));
			Assert.AreEqual(Here, original.ZoneId, "the original table moved when it should have been frozen");
			Assert.AreEqual(1, before.Count);
			Assert.AreEqual(0, gone.Count);
		}

		// ---- Caps --------------------------------------------------------------------------

		/// <summary>Bounded like everything else: sixty residents times two cities, and sixteen open
		/// jobs realm-wide. These are copies of constants that live elsewhere, and a copy that stops
		/// agreeing with its source is the defect the ladder idiom guards against.</summary>
		[Test]
		public void TheCapsAgreeWithTheConstantsTheyWereCopiedFrom()
		{
			Assert.AreEqual(KingdomCityState.MaxResidents * KingdomCityMemoryRules.CitiesPerRealm, KingdomBindingTable.MaxResidentBindings);
			Assert.AreEqual(KingdomCityMemoryRules.MaxOpenJobs, KingdomBindingTable.MaxTransientBindings);
		}

		[Test]
		public void TheTransientCapIsEnforcedAndDoesNotEatTheResidentOne()
		{
			KingdomBindingTable table = KingdomBindingTable.Empty;
			KingdomBindingTable next;
			KingdomCityFault fault;
			for (int i = 1; i <= KingdomBindingTable.MaxTransientBindings; i++)
			{
				Assert.IsTrue(table.TryBind(i, KingdomBindingKind.Transient, Here, "porter-" + i, 700L, out next, out fault), fault.ToString());
				table = next;
			}
			Assert.IsFalse(table.TryBind(9999, KingdomBindingKind.Transient, Here, "one-too-many", 700L, out next, out fault));
			Assert.AreEqual(KingdomCityFault.RowCapExceeded, fault);
			Assert.IsTrue(table.TryBind(9999, KingdomBindingKind.Resident, Here, "a-person", 700L, out next, out fault),
				"a realm full of porters must still be able to enrol a person");
		}

		// ---- The stale-transient sweep (§3.8 t3) ---------------------------------------------

		/// <summary>
		/// The nasty case, at the instant it is closed. The founder left mid-walk, the porter froze
		/// into the zone with the goods, the model reached the job's completion tick and evicted the
		/// binding, and the founder came back. The body is stale, and this is the verdict that says
		/// so — before intake and before any reify, which is the one moment the goods could exist
		/// twice.
		/// </summary>
		[Test]
		public void AThawedZonesBodyForAClosedJobReadsStale()
		{
			KingdomBindingTable open = Bound(42, KingdomBindingKind.Transient, Here);
			Assert.AreEqual(KingdomSweepVerdict.Bound, KingdomBindingRules.JudgeStale(42, open.Holds(42, KingdomBindingKind.Transient)));
			KingdomBindingTable closed;
			KingdomBinding evicted;
			KingdomCityFault fault;
			Assert.IsTrue(open.TryUnbind(42, KingdomBindingKind.Transient, KingdomUnbindCause.JobClosed, out closed, out evicted, out fault), fault.ToString());
			Assert.AreEqual(KingdomSweepVerdict.Stale, KingdomBindingRules.JudgeStale(42, closed.Holds(42, KingdomBindingKind.Transient)));
		}

		/// <summary>An object with no job id is not ours to judge, whatever the registry says. The
		/// sweep is keyed on a job id, which is why a person can never be swept: they do not have
		/// one.</summary>
		[TestCase(true)]
		[TestCase(false)]
		public void AnObjectWithNoJobIdIsNeverSwept(bool bound)
		{
			Assert.AreEqual(KingdomSweepVerdict.NotTransient, KingdomBindingRules.JudgeStale(0, bound));
		}

		// ---- The audit -----------------------------------------------------------------------

		/// <summary>Invariant I3, asserted directly rather than inferred: no binding key ever
		/// resolves to two living bodies, in any zone, at any time.</summary>
		[Test]
		public void TheAuditPassesOnACleanRegistryAndNamesADuplicate()
		{
			KingdomBindingTable table = Bound(7, KingdomBindingKind.Resident, Here);
			KingdomCityFault fault;
			Assert.IsTrue(table.TryAudit(out fault), fault.ToString());
			Assert.AreEqual(KingdomCityFault.None, fault);

			KingdomBindingTable doubled;
			Assert.IsFalse(KingdomBindingTable.TryCreate(new KingdomBinding[2]
			{
				new KingdomBinding(7, KingdomBindingKind.Resident, Here, "obj-a", 700L),
				new KingdomBinding(7, KingdomBindingKind.Resident, Next, "obj-b", 800L)
			}, out doubled, out fault));
			Assert.AreEqual(KingdomCityFault.DuplicateBinding, fault);
			Assert.IsNull(doubled, "a registry that could put one settler in two places must not be built at all");
		}

		// ---- The carrier ---------------------------------------------------------------------

		[Test]
		public void TheRegistryRoundTripsThroughItsColumns()
		{
			KingdomBindingTable table = Bound(7, KingdomBindingKind.Resident, Here);
			KingdomBindingTable both;
			KingdomCityFault fault;
			Assert.IsTrue(table.TryBind(42, KingdomBindingKind.Transient, Next, "porter", 900L, out both, out fault), fault.ToString());
			KingdomBindingRegistry registry = new KingdomBindingRegistry();
			Assert.IsTrue(registry.TryPublish(both, out fault), fault.ToString());
			Assert.AreEqual(2, registry.Count);

			KingdomBindingTable read;
			Assert.IsTrue(registry.TryRead(out read, out fault), fault.ToString());
			KingdomBinding person;
			KingdomBinding porter;
			Assert.IsTrue(read.TryGet(7, KingdomBindingKind.Resident, out person));
			Assert.IsTrue(read.TryGet(42, KingdomBindingKind.Transient, out porter));
			Assert.AreEqual(Here, person.ZoneId);
			Assert.AreEqual("obj-7", person.ObjectId);
			Assert.AreEqual(700L, person.MintedTick);
			Assert.AreEqual(Next, porter.ZoneId);
			Assert.AreEqual(900L, porter.MintedTick);
		}

		/// <summary>A publish rewrites every column from one snapshot, so a registry that used to
		/// hold more bindings does not keep the tail of the old ones.</summary>
		[Test]
		public void PublishingRewritesRatherThanAppends()
		{
			KingdomBindingRegistry registry = new KingdomBindingRegistry();
			KingdomCityFault fault;
			Assert.IsTrue(registry.TryPublish(Bound(7, KingdomBindingKind.Resident, Here), out fault));
			Assert.IsTrue(registry.TryPublish(KingdomBindingTable.Empty, out fault));
			Assert.AreEqual(0, registry.Count);
			Assert.AreEqual(0, registry.ZoneIds.Count);
			Assert.AreEqual(0, registry.MintedTicks.Count);
		}

		/// <summary>A ragged registry out of an older save is truncated to the shortest column: a
		/// binding half of whose fields are missing is not a binding, and a reader that trusted the
		/// longest column would invent one out of a default key.</summary>
		[Test]
		public void RaggedColumnsAreTruncatedToTheShortest()
		{
			KingdomBindingRegistry registry = new KingdomBindingRegistry();
			registry.Keys = new List<int> { 1, 2, 3 };
			registry.Kinds = new List<int> { 0, 0, 0 };
			registry.ZoneIds = new List<string> { Here, Next };
			registry.ObjectIds = new List<string> { "a", "b", "c" };
			registry.MintedTicks = new List<long> { 1L, 2L, 3L };
			registry.Normalize();
			Assert.AreEqual(2, registry.Count);
		}

		[Test]
		public void NullColumnsAreRepairedRatherThanTrippedOver()
		{
			KingdomBindingRegistry registry = new KingdomBindingRegistry();
			registry.Keys = null;
			registry.Kinds = null;
			registry.ZoneIds = null;
			registry.ObjectIds = null;
			registry.MintedTicks = null;
			registry.Normalize();
			Assert.AreEqual(0, registry.Count);
			KingdomBindingTable table;
			KingdomCityFault fault;
			Assert.IsTrue(registry.TryRead(out table, out fault), fault.ToString());
			Assert.AreEqual(0, table.Count);
		}

		/// <summary>
		/// A save that came back holding one key twice is a save that can put a settler in two
		/// places. The duplicate is dropped and the FIRST row wins, because it is the one every
		/// earlier session was already answering with.
		/// </summary>
		[Test]
		public void ADuplicateKeyOutOfASaveIsDroppedAndTheFirstRowWins()
		{
			KingdomBindingRegistry registry = new KingdomBindingRegistry();
			registry.Keys = new List<int> { 7, 7, 0, 9 };
			registry.Kinds = new List<int> { 0, 0, 0, 0 };
			registry.ZoneIds = new List<string> { Here, Next, Here, Next };
			registry.ObjectIds = new List<string> { "first", "second", "keyless", "other" };
			registry.MintedTicks = new List<long> { 100L, 200L, 300L, 400L };
			registry.Normalize();
			Assert.AreEqual(2, registry.Count, "the duplicate and the keyless row are both dropped");
			KingdomBindingTable table;
			KingdomCityFault fault;
			Assert.IsTrue(registry.TryRead(out table, out fault), fault.ToString());
			KingdomBinding binding;
			Assert.IsTrue(table.TryGet(7, KingdomBindingKind.Resident, out binding));
			Assert.AreEqual("first", binding.ObjectId);
			Assert.IsTrue(table.Holds(9, KingdomBindingKind.Resident));
		}

		/// <summary>A registry read out of a save that somehow held more than the caps is trimmed
		/// rather than refused: no dimension of this model grows, and a realm that cannot load is
		/// worse than one that loads bounded.</summary>
		[Test]
		public void ARegistryOverItsCapIsTrimmedRatherThanRefused()
		{
			KingdomBindingRegistry registry = new KingdomBindingRegistry();
			for (int i = 1; i <= KingdomBindingTable.MaxTransientBindings + 5; i++)
			{
				registry.Keys.Add(i);
				registry.Kinds.Add((int)KingdomBindingKind.Transient);
				registry.ZoneIds.Add(Here);
				registry.ObjectIds.Add("porter-" + i);
				registry.MintedTicks.Add(700L);
			}
			registry.Normalize();
			Assert.AreEqual(KingdomBindingTable.MaxTransientBindings, registry.Count);
			KingdomBindingTable table;
			KingdomCityFault fault;
			Assert.IsTrue(registry.TryRead(out table, out fault), fault.ToString());
		}

		/// <summary>A kind this build has no word for reads as a transient — the side that can be
		/// swept and refused, never the side that is a person.</summary>
		[Test]
		public void AnUnknownKindReadsAsTheSideThatIsNotAPerson()
		{
			KingdomBindingRegistry registry = new KingdomBindingRegistry();
			registry.Keys.Add(7);
			registry.Kinds.Add(99);
			registry.ZoneIds.Add(Here);
			registry.ObjectIds.Add("something");
			registry.MintedTicks.Add(700L);
			KingdomBindingTable table;
			KingdomCityFault fault;
			Assert.IsTrue(registry.TryRead(out table, out fault), fault.ToString());
			Assert.IsFalse(table.Holds(7, KingdomBindingKind.Resident));
			Assert.IsTrue(table.Holds(7, KingdomBindingKind.Transient));
		}

		[Test]
		public void PublishingNothingIsRefusedRatherThanClearingTheRegistry()
		{
			KingdomBindingRegistry registry = new KingdomBindingRegistry();
			KingdomCityFault fault;
			Assert.IsTrue(registry.TryPublish(Bound(7, KingdomBindingKind.Resident, Here), out fault));
			Assert.IsFalse(registry.TryPublish(null, out fault));
			Assert.AreEqual(KingdomCityFault.NullArgument, fault);
			Assert.AreEqual(1, registry.Count, "a refused publish leaves the registry byte-identical");
		}

		private static void AssertEnum(Type type, string[] names, byte[] values)
		{
			Assert.AreEqual(typeof(byte), Enum.GetUnderlyingType(type), type.FullName);
			Assert.IsTrue(type.IsPublic, type.FullName);
			CollectionAssert.AreEqual(names, Enum.GetNames(type), type.FullName);
			Array raw = Enum.GetValues(type);
			byte[] actual = new byte[raw.Length];
			for (int i = 0; i < raw.Length; i++) actual[i] = Convert.ToByte(raw.GetValue(i));
			CollectionAssert.AreEqual(values, actual, type.FullName);
		}

		private static string LogicalSource()
		{
			return string.Join("\n", new[]
			{
				TestMain.ReadRepositoryText(Path.Combine("Simulation", "City", "KingdomBindingRegistry.Declarations.cs")),
				TestMain.ReadRepositoryText(Path.Combine("Simulation", "City", "KingdomBindingRegistry.Rules.cs")),
				TestMain.ReadRepositoryText(Path.Combine("Simulation", "City", "KingdomBindingRegistry.Table.cs")),
				TestMain.ReadRepositoryText(Path.Combine("Simulation", "City", "KingdomBindingRegistry.cs"))
			});
		}

		private static int Count(string source, string term)
		{
			int count = 0;
			int at = 0;
			while ((at = source.IndexOf(term, at, StringComparison.Ordinal)) >= 0)
			{
				count++;
				at += term.Length;
			}
			return count;
		}
	}
}
#endif
