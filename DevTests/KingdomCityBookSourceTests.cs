#if TAF_TESTS
using System;

using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomCityBookSourceTests
	{
		[Test]
		public void LogicalFamilyKeepsColumnsSerializationNormalizationAndPublicationOrder()
		{
			string source = KingdomCityBookLogicalSource.Read();
			Ordered(source,
				"internal KingdomDistanceCache DistanceCache",
				"public int SchemaVersion",
				"public List<string> ZoneIds",
				"public List<int> ResidentIds",
				"public string ExtensionModel",
				"public bool WantFieldReflection",
				"public void Normalize()",
				"private void NormalizeSidecarFields()",
				"private void NormalizeResidentColumns()",
				"private void NormalizeCityMetadata()",
				"public bool TryResidentRow(",
				"internal bool TryRead(",
				"internal bool TryPublish(",
				"private void Clear()",
				"private static List<T> Repair<T>(");
		}

		[Test]
		public void NormalizeOrchestratorRetainsTheExactSevenPhaseOrder()
		{
			string source = KingdomCityBookLogicalSource.Read();
			int start = source.IndexOf("public void Normalize()", StringComparison.Ordinal);
			int end = source.IndexOf("private void NormalizeSidecarFields()", start,
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0);
			Assert.Greater(end, start);
			Ordered(source.Substring(start, end - start),
				"NormalizeSidecarFields();",
				"NormalizeZoneColumns();",
				"NormalizeWorkColumns();",
				"NormalizeResidentColumns();",
				"NormalizeClockColumns();",
				"NormalizeToldColumns();",
				"NormalizeCityMetadata();");
		}

		[Test]
		public void SerializableAuthorityAndKeyFieldsHaveOneOwner()
		{
			string source = KingdomCityBookLogicalSource.Read();
			Assert.AreEqual(13, Count(source, "public partial class KingdomCityBook"));
			Assert.AreEqual(1, Count(source, "[Serializable]"));
			Assert.AreEqual(1, Count(source, "internal KingdomDistanceCache DistanceCache"));
			Assert.AreEqual(1, Count(source, "public void Normalize()"));
			Assert.AreEqual(1, Count(source, "internal bool TryPublish("));
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
