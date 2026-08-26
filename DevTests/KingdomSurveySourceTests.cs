#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomSurveySourceTests
	{
		[Test]
		public void LogicalFamilyPreservesAuthorityNestedTypesAndInitializerOrder()
		{
			string source = KingdomSurveyLogicalSource.Read();
			Assert.AreEqual(11, Count(source, "public partial class KingdomSurvey"));
			Assert.AreEqual(1, Count(source, "private sealed class ReferenceComparer"));
			Assert.AreEqual(1, Count(source, "private sealed class IndexedRow"));
			Assert.AreEqual(1, Count(source, "public sealed class PassScope"));
			Assert.AreEqual(1, Count(source, "private sealed class SpoilFrame"));
			StringAssert.DoesNotContain("public class KingdomSurvey", source);

			AssertOrdered(source,
				"private const int MaxIndexedObjects",
				"private sealed class ReferenceComparer",
				"private sealed class IndexedRow",
				"private static KingdomSurvey BoundSurvey",
				"private static int BoundDepth",
				"private readonly Dictionary<GameObject, IndexedRow> Rows",
				"private readonly HashSet<GameObject> LoadedSet",
				"public readonly List<GameObject> Objects",
				"public readonly List<GameObject> CitizenBodies",
				"public readonly List<GameObject> ConstructionRoots",
				"public readonly List<GameObject> PlotParts",
				"public sealed class PassScope",
				"public Zone Ground",
				"public readonly List<GameObject> Larders",
				"public readonly List<GameObject> Built",
				"public readonly List<GameObject> Defences");
		}

		[Test]
		public void LogicalFamilyPreservesMethodAndTransactionOrder()
		{
			string source = KingdomSurveyLogicalSource.Read();
			AssertOrdered(source,
				"public static KingdomSurvey Take(Zone Z)",
				"public static IEnumerable<GameObject> ObjectsFor(Zone Z)",
				"public static KingdomSurvey ActiveFor(Zone Z)",
				"public PassScope BindPass()",
				"private void AddRoot(",
				"private IndexedRow Capture(",
				"private static bool BelongsToRealm(",
				"private void Publish(IndexedRow Row, bool Add)",
				"public bool ObserveAdded(",
				"public bool ObserveChanged(",
				"public bool ObserveRemoved(",
				"internal bool SynchronizeReceiptObject(",
				"public GameObject FindCitizen(",
				"public static KingdomSurvey Take(Zone Z, KingdomSystem System)",
				"public int Consume(int Drams)",
				"public int ConsumeFood(int Amount)",
				"public int ConsumeCrop(",
				"public bool AdoptLarder(",
				"public int StoreFood(",
				"public int StoreFoodIn(",
				"public int SpoilFrom(",
				"private sealed class SpoilFrame",
				"public bool TrySpoilFromExact(",
				"public int LeakFrom(",
				"public bool TryLeakFromExact(",
				"public int Store(int Drams)",
				"public int StoreIn(",
				"public int DrawFromPools(",
				"private void SynchronizeLarders()");

			string add = Between(source, "private void AddRoot(",
				"private IndexedRow Capture(");
			AssertOrdered(add, "Capture(", "Rows.Add(", "Objects.Add(",
				"Publish(row, true)", "IndexLoadedBranch(row)");

			string changed = Between(source, "public bool ObserveChanged(",
				"public bool ObserveCurrentTopology(");
			AssertOrdered(changed, "Publish(old, false)", "RemoveLoadedBranch(old)",
				"Capture(Item", "Rows[Item] = fresh", "Publish(fresh, true)",
				"IndexLoadedBranch(fresh)", "ChangedMutations++");
		}

		private static string Between(string source, string startTerm, string endTerm)
		{
			int start = source.IndexOf(startTerm, StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0, startTerm);
			int end = source.IndexOf(endTerm, start + startTerm.Length,
				StringComparison.Ordinal);
			Assert.Greater(end, start, endTerm);
			return source.Substring(start, end - start);
		}

		private static int Count(string source, string value)
		{
			int count = 0;
			int at = 0;
			while ((at = source.IndexOf(value, at, StringComparison.Ordinal)) >= 0)
			{
				count++;
				at += value.Length;
			}
			return count;
		}

		private static void AssertOrdered(string source, params string[] values)
		{
			int cursor = -1;
			for (int i = 0; i < values.Length; i++)
			{
				int at = source.IndexOf(values[i], cursor + 1,
					StringComparison.Ordinal);
				Assert.Greater(at, cursor, values[i]);
				cursor = at;
			}
		}
	}
}
#endif
