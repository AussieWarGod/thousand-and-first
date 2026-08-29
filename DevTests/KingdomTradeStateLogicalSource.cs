#if TAF_TESTS
using System;
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomTradeStateLogicalSource
	{
		private const string BookMarker = "\t/// <summary>\n\t/// Realm trade authority";

		internal static string Read()
		{
			string anchor = TestMain.ReadRepositoryText("Trade/KingdomTradeState.cs");
			int book = anchor.IndexOf(BookMarker, StringComparison.Ordinal);
			if (book < 0) throw new InvalidOperationException("Trade-state book marker is missing.");
			StringBuilder source = new StringBuilder();
			source.Append(anchor.Substring(0, book));
			source.Append(TestMain.ReadRepositoryText(
				"Trade/KingdomTradeState.00.ManifestAndProjectionDeclarations.cs")).Append('\n');
			source.Append(TestMain.ReadRepositoryText(
				"Trade/KingdomTradeState.01.OperationAndAuthorityDeclarations.cs")).Append('\n');
			source.Append(anchor.Substring(book)).Append('\n');
			source.Append(TestMain.ReadRepositoryText(
				"Trade/KingdomTradeState.02.CodecEnvelopeAndPayload.cs")).Append('\n');
			source.Append(TestMain.ReadRepositoryText(
				"Trade/KingdomTradeState.02b.CodecWireV4Payload.cs")).Append('\n');
			source.Append(TestMain.ReadRepositoryText(
				"Trade/KingdomTradeState.03.CodecDecodeAndPrimitiveHelpers.cs")).Append('\n');
			source.Append(TestMain.ReadRepositoryText(
				"Trade/KingdomTradeState.03b.CodecWireV4Decode.cs")).Append('\n');
			source.Append(TestMain.ReadRepositoryText(
				"Trade/KingdomTradeState.04.CodecCargoAndPatternRows.cs")).Append('\n');
			source.Append(TestMain.ReadRepositoryText(
				"Trade/KingdomTradeState.05.CodecOperationProofArchiveRows.cs")).Append('\n');
			source.Append(TestMain.ReadRepositoryText(
				"Trade/KingdomTradeState.05b.CodecWireV4Rows.cs")).Append('\n');
			return source.ToString();
		}
	}
}
#endif
