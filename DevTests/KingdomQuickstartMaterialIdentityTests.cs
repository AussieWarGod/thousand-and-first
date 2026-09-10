#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Issue #142: the shipped quickstart's starter material stacks were created, counted and
	/// inserted without ever being given an engine identity, and the construction-input observer
	/// refuses an empty one, abandoning routed-input observation. Ordinary local stock permits
	/// unassigned identities; its separate missing-survey defect caused the reported menu failure.
	/// <para>
	/// In the pinned engine (2.0.211.51) <c>GameObject.IDIfAssigned</c>
	/// (<c>XRL/World/GameObject.cs:424-434</c>) reads the <c>"id"</c> property without allocating,
	/// while <c>GameObject.ID</c> (<c>:436-452</c>) allocates on first read. The grant itself is
	/// identified through <c>RequireID()</c> in <c>TryPrepareGrant</c>; its contents were not.
	/// </para>
	/// </summary>
	public class KingdomQuickstartMaterialIdentityTests
	{
		private const string Materials = "World/KingdomQuickstartBootstrap.Materials.cs";
		private const string Registry = "Growth/KingdomConstruction.InputObservationRegistry.cs";
		private const string Recovery = "World/KingdomQuickstartBootstrap.Recovery.cs";
		private static string Read(string path) => TestMain.ReadRepositoryText(path);

		private static void Ordered(string source, params string[] tokens)
		{
			int cursor = 0;
			foreach (string token in tokens)
			{
				int at = source.IndexOf(token, cursor, StringComparison.Ordinal);
				ClassicAssert.GreaterOrEqual(at, cursor, token);
				cursor = at + token.Length;
			}
		}

		private static int Count(string source, string token)
		{
			int found = 0, at = 0;
			while ((at = source.IndexOf(token, at, StringComparison.Ordinal)) >= 0)
			{
				found++;
				at += token.Length;
			}
			return found;
		}

		[Test]
		public void EveryStarterStackTakesItsIdentityAtCreationBeforeItIsInserted()
		{
			// Allocate, prove the allocation landed, only then set the count and insert.
			Ordered(Read(Materials),
				"GameObject item = string.IsNullOrEmpty(blueprint)",
				"string identity = item.ID;",
				"!string.Equals(item.IDIfAssigned, identity, StringComparison.Ordinal))",
				"item.Count = Count;",
				"Stockpile.Inventory.AddObject(");
		}

		[Test]
		public void TheIdentityIsReProvedAfterInsertionSoASubstitutionRefuses()
		{
			string materials = Read(Materials);
			int insert = materials.IndexOf("Stockpile.Inventory.AddObject(",
				StringComparison.Ordinal);
			int after = materials.LastIndexOf(
				"!string.Equals(item.IDIfAssigned, identity, StringComparison.Ordinal))",
				StringComparison.Ordinal);
			ClassicAssert.Greater(insert, 0);
			ClassicAssert.Greater(after, insert);
			StringAssert.Contains("A private starter material changed identity entering its chest.",
				materials);
		}

		[Test]
		public void TheIdentityIsAllocatedExactlyOncePerCreatedStack()
		{
			string materials = Read(Materials);
			// One allocating read. Every other look is the non-allocating one, so a later reader
			// can still tell "never given an identity" from "given one".
			ClassicAssert.AreEqual(1, Count(materials, "item.ID;"));
			ClassicAssert.AreEqual(0, Count(materials, "item.RequireID()"));
			ClassicAssert.AreEqual(2, Count(materials, "item.IDIfAssigned"));
		}

		[Test]
		public void TheChestKeepsItsExistingGrantIdentityAndIsNotIdentifiedTwice()
		{
			// The grant's own identity already comes from TryPrepareGrant; this fix adds none.
			StringAssert.Contains("string identity = Grant.RequireID();", Read(Recovery));
			foreach (string forbidden in new[] { "stockpile.ID;", "stockpile.RequireID()" })
				StringAssert.DoesNotContain(forbidden, Read(Materials));
		}

		[Test]
		public void TheObserverStillReadsIdentityWithoutAllocatingAndStillRefusesAnEmptyOne()
		{
			// The fix belongs at the grant, never at the observer: an observer that allocated
			// would invent identity for anything it happened to look at.
			string registry = Read(Registry);
			StringAssert.Contains(
				"string holderId = holder.IDIfAssigned, itemId = item.IDIfAssigned;", registry);
			StringAssert.Contains(
				"Attended construction-input source identity is absent or ambiguous.", registry);
			foreach (string forbidden in new[] { "item.ID;", "item.RequireID()",
				"holder.RequireID()" })
				StringAssert.DoesNotContain(forbidden, registry);
		}

		[Test]
		public void NothingExistingIsClearedRecreatedOrMintedByTheFix()
		{
			string materials = Read(Materials);
			foreach (string forbidden in new[] { "Obliterate(", ".Destroy(", "RemoveObject(",
				"Inventory.Clear(" })
				StringAssert.DoesNotContain(forbidden, materials);
		}
	}
}
#endif
