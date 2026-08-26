#if TAF_TESTS
using System;

using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomCitySourceTests
	{
		private static void Ordered(string Source, params string[] Markers)
		{
			int position = -1;
			for (int i = 0; i < Markers.Length; i++)
			{
				int next = Source.IndexOf(Markers[i], position + 1, StringComparison.Ordinal);
				Assert.Greater(next, position, Markers[i]);
				position = next;
			}
		}

		[Test]
		public void LogicalFamilyKeepsAuthorityOrderAndNestedContainerGround()
		{
			string source = KingdomCityLogicalSource.Read();
			Ordered(source,
				"public sealed class KingdomCityJournal",
				"public const string DedicationOrderProperty",
				"public static void CheckIn(",
				"public static void CheckOut(",
				"public static void RecordSupports(",
				"private static KingdomCityState Reckon(",
				"public static bool SpendTurn(",
				"private static KingdomCityState Reify(",
				"private sealed class ContainerGround",
				"private static KingdomCityState Networks(",
				"private static KingdomCityState Carry(",
				"private static KingdomCityState Reconcile(",
				"private static KingdomCityState ReadWorks(",
				"public static string AuditLine(",
				"private static bool Ensure(",
				"private static void Publish(",
				"private static void StampDedicationOrder(",
				"private static long DayStamp(");
			Assert.AreEqual(1, Occurrences(source, "private sealed class ContainerGround"));
		}

		[Test]
		public void PublicAndNestedTypeDeclarationsStayStable()
		{
			string source = KingdomCityLogicalSource.Read();
			Assert.AreEqual(1, Occurrences(source,
				"public sealed class KingdomCityJournal : IKingdomComputeJournal"));
			Assert.AreEqual(12, Occurrences(source,
				"public static partial class KingdomCity"));
			Assert.AreEqual(1, Occurrences(source,
				"private sealed class ContainerGround"));
			StringAssert.DoesNotContain("public static class KingdomCity", source);
		}

		[Test]
		public void StaticInitializerFieldsRemainTogetherAndOrdered()
		{
			Ordered(KingdomCityLogicalSource.Read(),
				"private static readonly KingdomCityJournal Journal = new KingdomCityJournal()",
				"private static readonly KingdomExecutor Executor = new KingdomExecutor(",
				"internal static KingdomExecutor Seam",
				"internal static void Record(");
		}

		private static int Occurrences(string Source, string Needle)
		{
			int count = 0;
			int position = 0;
			while ((position = Source.IndexOf(Needle, position, StringComparison.Ordinal)) >= 0)
			{
				count++;
				position += Needle.Length;
			}
			return count;
		}
	}
}
#endif
