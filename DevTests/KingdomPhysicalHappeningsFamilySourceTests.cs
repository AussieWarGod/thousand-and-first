#if TAF_TESTS
using System;

using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomPhysicalHappeningsFamilySourceTests
	{
		[Test]
		public void LogicalFamilyKeepsQueueEffectBodyAndPersistenceOrder()
		{
			string source = KingdomPhysicalHappeningsLogicalSource.Read();
			Ordered(source,
				"internal enum KingdomPhysicalQueueResult",
				"internal sealed class KingdomHappeningMoveTo",
				"internal const string TokenProperty",
				"internal static KingdomPhysicalQueueResult QueueGeneric(",
				"private static KingdomPhysicalQueueResult Queue(",
				"private static int PublishGeneric(",
				"private static Evidence Observe(",
				"private static bool Restore(",
				"private static GameObject FindFixture(",
				"private static bool ExactBodyReceipt(",
				"private static GameObject FindById(",
				"private static bool Write(",
				"private enum SinkLane",
				"private sealed class Evidence");
		}

		[Test]
		public void TopLevelAndNestedDeclarationsHaveOneOwner()
		{
			string source = KingdomPhysicalHappeningsLogicalSource.Read();
			Assert.AreEqual(1, Count(source, "internal enum KingdomPhysicalQueueResult"));
			Assert.AreEqual(1, Count(source, "internal sealed class KingdomHappeningMoveTo"));
			Assert.AreEqual(9, Count(source, "internal static partial class KingdomPhysicalHappenings"));
			Assert.AreEqual(1, Count(source, "private enum SinkLane"));
			Assert.AreEqual(1, Count(source, "private sealed class Evidence"));
			StringAssert.DoesNotContain("internal static class KingdomPhysicalHappenings", source);
		}

		private static void Ordered(string source, params string[] markers)
		{
			int position = -1;
			for (int i = 0; i < markers.Length; i++)
			{
				int next = source.IndexOf(markers[i], position + 1, StringComparison.Ordinal);
				Assert.Greater(next, position, markers[i]);
				position = next;
			}
		}

		private static int Count(string source, string token)
		{
			int count = 0;
			for (int at = 0; (at = source.IndexOf(token, at, StringComparison.Ordinal)) >= 0;
				at += token.Length) count++;
			return count;
		}
	}
}
#endif
