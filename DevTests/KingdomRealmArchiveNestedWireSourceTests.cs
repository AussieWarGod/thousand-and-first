#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomRealmArchiveNestedWireSourceTests
	{
		[TestCase("CarryBook", "KingdomCarryBook")]
		[TestCase("SettlementTopology", "KingdomSettlementTopology")]
		public void HistoricalDynamicWriterHasMatchingReader(string field, string type)
		{
			string source = TestMain.ReadRepositoryText("Core/KingdomRealmArchive.10WireEnvelope.cs");
			StringAssert.Contains("Writer.Write((IComposite)" + field + ")", source);
			StringAssert.Contains(field + " = ReadArchiveComposite<" + type + ">(Reader)", source);
			StringAssert.DoesNotContain("Reader.ReadComposite<" + type + ">", source);
		}

		[Test]
		public void NestedTypeAndErrorCountAreProvedBeforeAcceptance()
		{
			string source = TestMain.ReadRepositoryText("Core/KingdomRealmArchive.15NestedRead.cs");
			int before = source.IndexOf("int errors = Reader.Errors;", StringComparison.Ordinal);
			int read = source.IndexOf("IComposite value = Reader.ReadComposite();", StringComparison.Ordinal);
			int proof = source.IndexOf("Reader.Errors != errors || value == null || value.GetType() != typeof(T)", StringComparison.Ordinal);
			int accept = source.IndexOf("return (T)value;", StringComparison.Ordinal);
			Assert.GreaterOrEqual(before, 0); Assert.Greater(read, before);
			Assert.Greater(proof, read); Assert.Greater(accept, proof);
			StringAssert.Contains("throw new InvalidDataException", source.Substring(proof, accept - proof));
		}
	}
}
#endif
