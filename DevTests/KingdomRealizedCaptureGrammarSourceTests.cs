#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Source contracts for the realized capture's grammar edge: overflow-safe dimensions, byte-level
	/// injectivity, the fields that must be measured, and the ones that must not.
	/// <para>
	/// Split from the authority fixture only to hold the house line cap. Each contract holds a fact
	/// the pure fixtures cannot see, because it is about which values the runtime feeds the grammar.
	/// </para>
	/// </summary>
	[TestFixture]
	public sealed class KingdomRealizedCaptureGrammarSourceTests
	{
		private static string Read(string path)
		{
			return TestMain.ReadRepositoryText(path);
		}

		/// <summary>
		/// One named region of a file. A whole-file search is the defect these contracts keep
		/// catching: the name appears somewhere and the code that runs omits it.
		/// </summary>
		private static string Section(string source, string start, string end)
		{
			int begin = source.IndexOf(start, StringComparison.Ordinal);
			Assert.Greater(begin, -1, start);
			int stop = source.IndexOf(end, begin + start.Length, StringComparison.Ordinal);
			if (stop < 0) stop = source.Length;
			Assert.Greater(stop, begin, end);
			return source.Substring(begin, stop - begin);
		}

		// ----- RED 8: overflow-safe dimensions and injective bytes -------------------------------

		[Test]
		public void RectDimensionsAreBoundedInLongBeforeTheyBecomeInts()
		{
			string capture = Read("Core/KingdomRealizedArchitectureCapture.cs");
			StringAssert.Contains("long width = (long)intent.Rect.X2 - (long)x1 + 1L;", capture);
			StringAssert.Contains("long height = (long)intent.Rect.Y2 - (long)y1 + 1L;", capture);
			StringAssert.Contains("width > KingdomRealizedCaptureRules.MaxCells", capture);
			StringAssert.Contains("Width = (int)width;", capture);
		}

		[Test]
		public void TheCanonicalTextIsHashedThroughStrictUtf8()
		{
			string rules = Read("Core/KingdomRealizedCaptureRules.cs");
			StringAssert.Contains("new UTF8Encoding(false, true)", rules);
			StringAssert.Contains("EncoderFallbackException", rules);
			StringAssert.DoesNotContain("Encoding.UTF8.GetBytes", rules);
		}

		// ----- RED 9: lot identity is a precondition, not cross-path identity ---------------------

		/// <summary>
		/// The token is proved and then left behind. It hashes the lot id, and an ordinary
		/// commission and a gallery staging necessarily hold different lot ids, so a row carrying it
		/// could never match across the two paths that the differential exists to compare.
		/// </summary>
		[Test]
		public void TheMeasuredRowCarriesNoLotBearingIdentity()
		{
			string facts = Read("Core/KingdomRealizedCaptureFacts.cs");
			StringAssert.DoesNotContain("public string Token;", facts);
			StringAssert.DoesNotContain("public string Lot", facts);
			StringAssert.Contains("public bool AuthorityProved;", facts);
			// Scoped to the row builder itself: the name appearing elsewhere in the file proves
			// nothing about what the encoded row actually carries.
			string row = Section(Read("Core/KingdomRealizedCaptureRules.cs"),
				"private static string ObjectRow(", "private static bool Append(");
			StringAssert.DoesNotContain("Item.Token", row);
			StringAssert.Contains("Flag(Item.AuthorityProved)", row);
			string capture = Read("Core/KingdomRealizedArchitectureCapture.Facts.cs");
			StringAssert.DoesNotContain("Token = ", capture);
			// Proved, then omitted: the runtime still recomputes and compares it.
			string authority = Read("Core/KingdomRealizedArchitectureCapture.Authority.cs");
			StringAssert.Contains("ComponentTokenProperty", authority);
			StringAssert.Contains("does not recompute", authority);
		}

		/// <summary>Every RED 5 rendering field is measured; dropping one hides a visual difference.</summary>
		[Test]
		public void EveryRenderingFieldIsMeasured()
		{
			string facts = Read("Core/KingdomRealizedCaptureFacts.cs");
			// The row builder, not the file: a field named in a comment is not a measured field.
			string row = Section(Read("Core/KingdomRealizedCaptureRules.cs"),
				"private static string ObjectRow(", "private static bool Append(");
			string capture = Section(Read("Core/KingdomRealizedArchitectureCapture.Facts.cs"),
				"Fact = new KingdomRealizedObjectFact", "return true;");
			foreach (string field in new string[]
				{ "Tile", "RenderString", "ColorString", "DetailColor", "TileColor", "RenderLayer",
					"PathState", "PhysicsPresent", "Solid", "BlueprintSolid" })
			{
				StringAssert.Contains(field, facts, field);
				StringAssert.Contains("Item." + field, row, field);
				StringAssert.Contains(field + " =", capture, field);
			}
		}

		/// <summary>
		/// Anchor absence and an explicitly stored empty anchor are different states, and a default
		/// getter compares them equal.
		/// </summary>
		[Test]
		public void AnchorAbsenceIsDistinguishedFromAStoredEmptyKey()
		{
			string authority = Read("Core/KingdomRealizedArchitectureCapture.Authority.cs");
			StringAssert.Contains("stores an anchor key its", authority);
			StringAssert.Contains("is missing the anchor key its receipt", authority);
			string facts = Read("Core/KingdomRealizedArchitectureCapture.Facts.cs");
			StringAssert.Contains(
				"Item.HasStringProperty(KingdomArchitectureStamper.ComponentAnchorProperty)", facts);
		}

		/// <summary>The owner's per-slot receipt keys are read by type presence, not by default.</summary>
		[Test]
		public void ThePerSlotReceiptKeysAreReadByExactTypePresence()
		{
			string objects = Read("Core/KingdomRealizedArchitectureCapture.Objects.cs");
			StringAssert.Contains("!Owner.HasIntProperty(stateKey) || Owner.HasStringProperty(stateKey)",
				objects);
			StringAssert.Contains("!Owner.HasStringProperty(idKey) || Owner.HasIntProperty(idKey)",
				objects);
			string authority = Read("Core/KingdomRealizedArchitectureCapture.Authority.cs");
			StringAssert.Contains("KingdomPlots.PlotIdProperty", authority);
			StringAssert.Contains("AuditedTextKeys", authority);
			StringAssert.Contains("AuditedIntKeys", authority);
		}

		[Test]
		public void TheLiquidSubgrammarUsesTheFramedEncoder()
		{
			string facts = Read("Core/KingdomRealizedArchitectureCapture.Facts.cs");
			StringAssert.Contains("KingdomRealizedCaptureRules.Pair(", facts);
			StringAssert.Contains("KingdomRealizedCaptureRules.Liquid(", facts);
			StringAssert.DoesNotContain("string.Join(\";\"", facts);
		}

	}
}
#endif
