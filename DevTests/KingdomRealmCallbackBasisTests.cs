#if TAF_TESTS
using System;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>Executable coverage is limited to the pure shape rule and the reflected receipt shape
	/// table. Everything about the envelope is a SOURCE pin, because Write/ReadCore,
	/// WriteCallback and ReadCallback all live behind #if !TAF_TESTS and this repository has no
	/// SerializationWriter/SerializationReader shim. These tests therefore make NO native-wire
	/// claim: they do not prove save compatibility, on-disk byte layout, or that the engine
	/// round-trips an envelope.</summary>
	[TestFixture]
	public sealed class KingdomRealmCallbackBasisTests
	{
		private const string Receipt = "Core/KingdomRealmCallbackReceipt.cs";
		private const string Core = "Core/KingdomRealmArchive.00Core.cs";
		private const string Bounded = "Core/KingdomRealmArchive.05BoundedValidation.cs";
		private const string Envelope = "Core/KingdomRealmArchive.10WireEnvelope.cs";
		private const string Primitives = "Core/KingdomRealmArchive.11WirePrimitives.cs";
		private const string BasisWire = "Core/KingdomRealmArchive.14HashBasisWire.cs";
		private const string AuthorityHash = "Core/KingdomRealmArchive.02AuthorityHash.cs";
		private const string GraphMatch = "Core/KingdomRealmArchive.04GraphMatch.cs";
		private const string Topology = "Core/KingdomRealmArchive.13SettlementTopology.cs";
		private const string TailReader = "private static bool TryReadHashBasisTail(";

		/// <summary>The seven receipts in Write's frame order, which is also the tail order.</summary>
		private static readonly string[] ReceiptOrder =
		{
			"ExileChronicle", "ExileAbility", "ReturnChronicle", "ReturnReputation",
			"ReturnFeelings", "ReturnSeat", "ReturnAbility"
		};

		[TestCase(KingdomRealmCallbackPhase.None, 0, 0, true)]
		[TestCase(KingdomRealmCallbackPhase.None, 1, 0, false)]
		[TestCase(KingdomRealmCallbackPhase.None, 0, 1, false)]
		[TestCase(KingdomRealmCallbackPhase.None, 1, 1, false)]
		[TestCase(KingdomRealmCallbackPhase.Intent, 0, 0, true)]
		[TestCase(KingdomRealmCallbackPhase.Intent, 1, 0, true)]
		[TestCase(KingdomRealmCallbackPhase.Intent, 18, 0, true)]
		[TestCase(KingdomRealmCallbackPhase.Intent, 19, 0, true)]
		[TestCase(KingdomRealmCallbackPhase.Intent, 18, 1, false)]
		[TestCase(KingdomRealmCallbackPhase.Intent, 20, 0, false)]
		[TestCase(KingdomRealmCallbackPhase.Intent, -1, 0, false)]
		[TestCase(KingdomRealmCallbackPhase.Attempting, 0, 0, true)]
		[TestCase(KingdomRealmCallbackPhase.Attempting, 1, 0, true)]
		[TestCase(KingdomRealmCallbackPhase.Attempting, 18, 0, true)]
		[TestCase(KingdomRealmCallbackPhase.Attempting, 19, 0, true)]
		[TestCase(KingdomRealmCallbackPhase.Attempting, 18, 1, false)]
		[TestCase(KingdomRealmCallbackPhase.Attempting, 20, 0, false)]
		[TestCase(KingdomRealmCallbackPhase.Attempting, -1, 0, false)]
		[TestCase(KingdomRealmCallbackPhase.Settled, 0, 0, true)]
		[TestCase(KingdomRealmCallbackPhase.Settled, 18, 18, true)]
		[TestCase(KingdomRealmCallbackPhase.Settled, 18, 19, true)]
		[TestCase(KingdomRealmCallbackPhase.Settled, 0, 19, true)]
		[TestCase(KingdomRealmCallbackPhase.Settled, 19, 0, true)]
		[TestCase(KingdomRealmCallbackPhase.Settled, 20, 5, false)]
		[TestCase(KingdomRealmCallbackPhase.Settled, 5, 20, false)]
		[TestCase(KingdomRealmCallbackPhase.Settled, -1, 0, false)]
		[TestCase(KingdomRealmCallbackPhase.Settled, 0, -1, false)]
		public void BasisShapeAcceptsOnlyBoundedPairsLegalForThePhase(
			KingdomRealmCallbackPhase phase, int intent, int settled, bool expected)
		{
			string failure;
			ClassicAssert.AreEqual(expected, KingdomRealmArchive.ValidBasisShape(phase, intent, settled,
				out failure), phase + " " + intent + "/" + settled);
			ClassicAssert.AreEqual(expected, failure == null, "a refusal must carry failure text");
		}

		[Test]
		public void BasisShapeUsesFixedFailureTextThatNeverEchoesTheStoredPair()
		{
			string failure;
			KingdomRealmArchive.ValidBasisShape(KingdomRealmCallbackPhase.Intent, 12345, 0, out failure);
			ClassicAssert.AreEqual("callback hash basis version is out of range", failure);
			KingdomRealmArchive.ValidBasisShape(KingdomRealmCallbackPhase.None, 3, 0, out failure);
			ClassicAssert.AreEqual("unresolved callback carries a hash basis", failure);
			KingdomRealmArchive.ValidBasisShape(KingdomRealmCallbackPhase.Attempting, 3, 4, out failure);
			ClassicAssert.AreEqual("unsettled callback carries a settle hash basis", failure);
			KingdomRealmArchive.ValidBasisShape((KingdomRealmCallbackPhase)200, 0, 0, out failure);
			ClassicAssert.AreEqual("callback receipt phase is noncanonical", failure);
			KingdomRealmArchive.ValidBasisShape((KingdomRealmCallbackReceipt)null, out failure);
			ClassicAssert.AreEqual("callback receipt is absent", failure);
		}

		[Test]
		public void BasisUpperBoundTracksTheSettlementCodecRatherThanALiteral()
		{
			int codec = KingdomArchivedSettlementCodec.CurrentVersion;
			ClassicAssert.IsTrue(KingdomRealmArchive.ValidBasisShape(KingdomRealmCallbackPhase.Settled,
				codec, codec, out _));
			ClassicAssert.IsFalse(KingdomRealmArchive.ValidBasisShape(KingdomRealmCallbackPhase.Settled,
				codec + 1, 0, out _));
			StringAssert.Contains("KingdomArchivedSettlementCodec.CurrentVersion", Read(BasisWire),
				"the bound must not be a hardcoded 19");
		}

		[Test]
		public void ReceiptsFromEnvelopesTwoThroughEightStayUnresolvedAndLegal()
		{
			KingdomRealmCallbackReceipt fresh = new KingdomRealmCallbackReceipt();
			ClassicAssert.AreEqual(0, fresh.IntentSettlementSchema);
			ClassicAssert.AreEqual(0, fresh.SettledSettlementSchema);
			ClassicAssert.IsTrue(KingdomRealmArchive.ValidBasisShape(fresh, out _));
			foreach (KingdomRealmCallbackPhase phase in
				Enum.GetValues(typeof(KingdomRealmCallbackPhase)))
				ClassicAssert.IsTrue(KingdomRealmArchive.ValidBasisShape(phase, 0, 0, out _), "" + phase);
		}

		[Test]
		public void ReceiptShapeAppendsExactlyTwoIntsAfterTheFrozenTwelveFields()
		{
			Type type = typeof(KingdomRealmCallbackReceipt);
			ClassicAssert.IsTrue(Attribute.IsDefined(type, typeof(SerializableAttribute)), type.FullName);
			FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public
				| BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
			Array.Sort(fields, (left, right) => left.MetadataToken.CompareTo(right.MetadataToken));
			object value = Activator.CreateInstance(type);
			string[] actual = new string[fields.Length];
			for (int i = 0; i < fields.Length; i++)
			{
				ClassicAssert.IsTrue(fields[i].IsPublic && !fields[i].IsInitOnly, fields[i].Name);
				ClassicAssert.IsFalse(Attribute.IsDefined(fields[i], typeof(NonSerializedAttribute)),
					fields[i].Name);
				actual[i] = TypeName(fields[i].FieldType) + " " + fields[i].Name + "="
					+ DefaultValue(fields[i].GetValue(value));
			}
			CollectionAssert.AreEqual(new[]
			{
				"ThousandAndFirst.KingdomRealmCallbackPhase Phase=0",
				"ThousandAndFirst.KingdomRealmCallbackDisposition Disposition=0",
				"ThousandAndFirst.KingdomRealmCallbackScope Scope=0",
				"string BeforeGraph=<null>", "string AfterGraph=<null>",
				"string BeforeArchiveGraph=<null>", "string AfterArchiveGraph=<null>",
				"string BeforeEffect=<null>", "string AfterEffect=<null>",
				"string ObservedEffect=<null>",
				"int BeforeStamp=-2147483648", "int AfterStamp=-2147483648",
				"int IntentSettlementSchema=0", "int SettledSettlementSchema=0"
			}, actual, "the two basis ints append after the frozen twelve, defaulting to 0");
			StringAssert.DoesNotContain("SettlementSchema",
				Block(Read(Receipt), "public bool Validate()"),
				"Validate keeps its pre-basis contract; ValidBasisShape owns basis shape");
		}

		[Test]
		public void EnvelopeVersionMovesToNineWhileEveryOlderVersionStaysAdmitted()
		{
			string core = Read(Core);
			foreach (string pin in new[]
			{
				"public const int CurrentVersion = 9;", "internal const int HashBasisVersion = 9;",
				"internal const int LegacyJobVersion = 2;",
				"internal const int MissionJobVersion = 3;",
				"internal const int ExactDeliveryJobVersion = 4;",
				"internal const int ExpandedDeliveryJobVersion = 5;",
				"internal const int SettlementTopologyVersion = 6;",
				"internal const int DirectionalStandingVersion = 7;",
				"internal const int ExpeditionResultJobVersion = 8;"
			})
				StringAssert.Contains(pin, core);
			string read = Compact(Block(Read(Envelope), "private void ReadCore("));
			foreach (string admitted in new[]
			{
				"Version!=LegacyJobVersion", "Version!=MissionJobVersion",
				"Version!=ExactDeliveryJobVersion", "Version!=ExpandedDeliveryJobVersion",
				"Version!=SettlementTopologyVersion", "Version!=DirectionalStandingVersion",
				"Version!=ExpeditionResultJobVersion", "Version!=CurrentVersion"
			})
				StringAssert.Contains(admitted, read, "wire 2-9 must all remain admitted");
			StringAssert.Contains("if(Version==1)thrownewInvalidDataException", read);
		}

		[Test]
		public void TailIsWrittenImmediatelyAfterTheDigestAndNowhereElse()
		{
			string envelope = Read(Envelope);
			string write = Compact(Block(envelope, "public void Write(SerializationWriter Writer)"));
			StringAssert.EndsWith("WriteString(Writer,DirectionalStandingDigest,64);"
				+ "WriteHashBasisTail(Writer,ExileChronicle,ExileAbility,ReturnChronicle,"
				+ "ReturnReputation,ReturnFeelings,ReturnSeat,ReturnAbility);", write,
				"the tail is the last frame and follows the digest with nothing between");
			ClassicAssert.AreEqual(1, Occurrences(envelope, "WriteHashBasisTail("));
			ClassicAssert.AreEqual(1, Occurrences(envelope, "TryReadHashBasisTail("));
			string receipts = "";
			for (int i = 0; i < ReceiptOrder.Length; i++)
				receipts += "WriteCallback(Writer," + ReceiptOrder[i] + ");";
			StringAssert.Contains(receipts, write, "the tail order is pinned to this same order");
		}

		[Test]
		public void TailIsReadInsideTheDirectionalDispatchImmediatelyAfterTheDigest()
		{
			string read = Compact(Block(Read(Envelope), "private void ReadCore("));
			StringAssert.Contains("DirectionalStandingDigest=ReadString(Reader,64);"
				+ "if(!TryReadHashBasisTail(Reader,wireVersion,ExileChronicle,ExileAbility,"
				+ "ReturnChronicle,ReturnReputation,ReturnFeelings,ReturnSeat,ReturnAbility,"
				+ "outstringbasisFailure))thrownewInvalidDataException(basisFailure);", read);
			StringAssert.Contains("if(wireVersion>=DirectionalStandingVersion)", read);
			StringAssert.Contains("DirectionalStandingDigest=null;ClearHashBasis();", read,
				"a pre-directional envelope leaves every basis unresolved");
		}

		[Test]
		public void TailWritesFourteenIntsIntentThenSettledInReceiptOrder()
		{
			string expected = "";
			for (int i = 0; i < ReceiptOrder.Length; i++)
				expected += "Writer.Write(" + ReceiptOrder[i] + "Receipt.IntentSettlementSchema);"
					+ "Writer.Write(" + ReceiptOrder[i] + "Receipt.SettledSettlementSchema);";
			ClassicAssert.AreEqual(expected, Compact(Block(Read(BasisWire),
				"private static void WriteHashBasisTail(")),
				"exactly 14 Int32, (intent,settled) per receipt, source-pinned order");
		}

		[Test]
		public void OldFramesConsumeNoTailAndAllFourteenIntsLandInLocalsFirst()
		{
			string body = Compact(Block(Read(BasisWire), TailReader));
			string reads = "";
			for (int i = 0; i < ReceiptOrder.Length; i++)
				reads += "int" + Camel(ReceiptOrder[i]) + "Intent=Reader.ReadInt32();"
					+ "int" + Camel(ReceiptOrder[i]) + "Settled=Reader.ReadInt32();";
			StringAssert.StartsWith(
				"Failure=null;if(Version<HashBasisVersion)returntrue;" + reads, body,
				"wire 2-8 consumes no tail byte; wire 9 reads all 14 before touching a field");
			ClassicAssert.Less(body.IndexOf(reads, StringComparison.Ordinal) + reads.Length,
				body.IndexOf("ValidBasisShape(", StringComparison.Ordinal));
			ClassicAssert.Less(body.IndexOf("ValidBasisShape(", StringComparison.Ordinal),
				body.IndexOf("IntentSettlementSchema=", StringComparison.Ordinal));
		}

		[Test]
		public void EveryPairIsShapeCheckedAndRefusalHappensBeforeAnyPublication()
		{
			string body = Compact(Block(Read(BasisWire), TailReader));
			string publishes = "";
			for (int i = 0; i < ReceiptOrder.Length; i++)
			{
				StringAssert.Contains("!ValidBasisShape(" + ReceiptOrder[i] + "Receipt,"
					+ Camel(ReceiptOrder[i]) + "Intent," + Camel(ReceiptOrder[i]) + "Settled,"
					+ "outFailure)", body, "each pair is validated on the local, not the field");
				publishes += ReceiptOrder[i] + "Receipt.IntentSettlementSchema="
					+ Camel(ReceiptOrder[i]) + "Intent;" + ReceiptOrder[i]
					+ "Receipt.SettledSettlementSchema=" + Camel(ReceiptOrder[i]) + "Settled;";
			}
			StringAssert.Contains("outFailure))returnfalse;", body,
				"an out-of-range or shape-invalid pair refuses without publishing");
			StringAssert.EndsWith(publishes + "returntrue;", body,
				"publication is contiguous and reached only after all seven checks pass");
		}

		[Test]
		public void ReadFailureKeepsTheExistingPoisonResetAndRethrowContract()
		{
			string envelope = Read(Envelope);
			string read = Compact(Block(envelope, "public void Read(SerializationReader Reader)"));
			StringAssert.Contains("try{ReadCore(Reader);}catch(Exceptionex){", read);
			StringAssert.EndsWith("ResetToPoisonEnvelope(ex.Message);throw;}", read,
				"the tail adds no new failure contract");
			StringAssert.EndsWith("ReturnAbility=newKingdomRealmCallbackReceipt();"
				+ "ClearHashBasis();", Compact(Block(envelope,
				"internal void ResetToPoisonEnvelope(string Failure)")),
				"the poison envelope publishes fresh receipts with no basis");
			ClassicAssert.AreEqual(14, Occurrences(Block(Read(BasisWire),
				"private void ClearHashBasis()"), "SettlementSchema = 0;"));
		}

		[Test]
		public void EnvelopeValidationRefusesAMalformedBasisSoItCanNeverBeWritten()
		{
			StringAssert.EndsWith("KingdomRealmCallbackReceipt.MaxEffectChars*4)&&"
				+ "ValidBasisShape(Value,out_);", Compact(Block(Read(Bounded),
				"private static bool ValidCallbackEnvelope(KingdomRealmCallbackReceipt Value)")),
				"ValidCallbackEnvelope gates both ValidateEnvelope and WriteCallback");
			StringAssert.StartsWith("if(!ValidCallbackEnvelope(Value))thrownew"
				+ "InvalidDataException(\"Archivedcallbackreceiptexceedscap.\");",
				Compact(Block(Read(Primitives), "private static void WriteCallback(")),
				"so a malformed basis stops the write at the receipt frame");
		}

		[Test]
		public void LegacyReceiptFrameIsUnchangedByTheBasisTail()
		{
			string primitives = Read(Primitives);
			string write = Compact(Block(primitives, "private static void WriteCallback("));
			string read = Compact(Block(primitives, "private static KingdomRealmCallbackReceipt "
				+ "ReadCallback(SerializationReader Reader)"));
			StringAssert.EndsWith("Writer.Write((byte)Value.Phase);"
				+ "Writer.Write((byte)Value.Disposition);Writer.Write((byte)Value.Scope);"
				+ "WriteString(Writer,Value.BeforeGraph,64);"
				+ "WriteString(Writer,Value.AfterGraph,64);"
				+ "WriteString(Writer,Value.BeforeArchiveGraph,64);"
				+ "WriteString(Writer,Value.AfterArchiveGraph,64);"
				+ "WriteString(Writer,Value.BeforeEffect,"
				+ "KingdomRealmCallbackReceipt.MaxEffectChars);"
				+ "WriteString(Writer,Value.AfterEffect,"
				+ "KingdomRealmCallbackReceipt.MaxEffectChars);"
				+ "WriteString(Writer,Value.ObservedEffect,"
				+ "KingdomRealmCallbackReceipt.MaxEffectChars);"
				+ "Writer.Write(Value.BeforeStamp);Writer.Write(Value.AfterStamp);", write);
			StringAssert.Contains("BeforeStamp=Reader.ReadInt32(),AfterStamp=Reader.ReadInt32()};"
				+ "if(!ValidCallbackEnvelope(value))", read);
			StringAssert.DoesNotContain("SettlementSchema", primitives);
			StringAssert.DoesNotContain("HashBasis", primitives,
				"11WirePrimitives.cs stays byte-unchanged: the v2-v8 frames do not move");
		}

		[Test]
		public void NeitherAuthorityHashStreamNorGraphStreamCarriesTheBasis()
		{
			foreach (string path in new[] { AuthorityHash, GraphMatch, Topology })
			{
				string source = Read(path);
				StringAssert.DoesNotContain("IntentSettlementSchema", source, path);
				StringAssert.DoesNotContain("SettledSettlementSchema", source, path);
				StringAssert.DoesNotContain("HashBasis", source, path);
			}
			ClassicAssert.AreEqual("Writer.Write((byte)1);Writer.Write((byte)Value.Phase);"
				+ "Writer.Write((byte)Value.Disposition);Writer.Write((byte)Value.Scope);"
				+ "WriteGraphString(Writer,Value.BeforeGraph);"
				+ "WriteGraphString(Writer,Value.AfterGraph);"
				+ "WriteGraphString(Writer,Value.BeforeArchiveGraph);"
				+ "WriteGraphString(Writer,Value.AfterArchiveGraph);"
				+ "WriteGraphString(Writer,Value.BeforeEffect);"
				+ "WriteGraphString(Writer,Value.AfterEffect);"
				+ "WriteGraphString(Writer,Value.ObservedEffect);"
				+ "Writer.Write(Value.BeforeStamp);Writer.Write(Value.AfterStamp);",
				Compact(Block(Read(AuthorityHash),
					"private static void WriteAuthorityCallback(BinaryWriter Writer,")),
				"TAA1 embeds the same thirteen receipt fields as before, so a receipt's authority "
				+ "hash is identical with basis (0,0) and with (18,18) and no hash is rewritten");
		}

		private static string Read(string path)
		{
			return TestMain.ReadRepositoryText(path);
		}

		private static string Camel(string value)
		{
			return char.ToLowerInvariant(value[0]) + value.Substring(1);
		}

		private static string TypeName(Type type)
		{
			if (type == typeof(int)) return "int";
			if (type == typeof(string)) return "string";
			return type.FullName;
		}

		private static string DefaultValue(object value)
		{
			if (value == null) return "<null>";
			if (value.GetType().IsEnum) return Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture);
			return Convert.ToString(value, CultureInfo.InvariantCulture);
		}

		private static int Occurrences(string source, string marker)
		{
			int count = 0;
			int at = source.IndexOf(marker, StringComparison.Ordinal);
			while (at >= 0)
			{
				count++;
				at = source.IndexOf(marker, at + marker.Length, StringComparison.Ordinal);
			}
			return count;
		}

		private static string Compact(string source)
		{
			return Regex.Replace(source, @"\s+", "");
		}

		private static string Block(string source, string signature)
		{
			int at = source.IndexOf(signature, StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(at, 0, signature);
			int start = source.IndexOf('{', at);
			ClassicAssert.GreaterOrEqual(start, 0, signature);
			int depth = 1;
			for (int i = start + 1; i < source.Length; i++)
			{
				if (source[i] == '{') depth++;
				if (source[i] == '}' && --depth == 0)
					return source.Substring(start + 1, i - start - 1);
			}
			Assert.Fail("Unterminated source block: " + signature);
			return null;
		}
	}
}
#endif
