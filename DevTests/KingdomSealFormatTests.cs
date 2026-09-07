#if TAF_TESTS
using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomSealFormatTests
	{
		[Test]
		public void FormatDeclarationsKeepExactMetadata()
		{
			ClassicAssert.AreEqual(typeof(int), Enum.GetUnderlyingType(typeof(KingdomSealFault)));
			ClassicAssert.AreEqual("0:None,1:Empty,2:NotASeal,3:UnsupportedSchema,4:MalformedFraming,5:LengthMismatch,6:ChecksumMismatch,7:TrailingData,8:TooLarge,9:Malformed,10:DuplicateKey,11:UnknownKey,12:MissingKey,13:WrongKind,14:OutOfBounds,15:DigestUnavailable",
				string.Join(",", Array.ConvertAll((KingdomSealFault[])Enum.GetValues(
					typeof(KingdomSealFault)), value => ((int)value) + ":" + value)));
			ClassicAssert.AreEqual(typeof(int), Enum.GetUnderlyingType(typeof(KingdomSealKind)));
			ClassicAssert.AreEqual("0:Text,1:Number,2:TextList,3:NumberList,4:EmptyList",
				string.Join(",", Array.ConvertAll((KingdomSealKind[])Enum.GetValues(
					typeof(KingdomSealKind)), value => ((int)value) + ":" + value)));
			Type body = typeof(KingdomSealBody);
			ClassicAssert.IsTrue(body.IsNotPublic);
			ClassicAssert.IsTrue(body.IsSealed);
			string[] fields = new string[]
				{ "_order", "_kinds", "_text", "_number", "_textList", "_numberList" };
			ClassicAssert.AreEqual(fields.Length, body.GetFields(System.Reflection.BindingFlags.Instance
				| System.Reflection.BindingFlags.NonPublic).Length);
			for (int i = 0; i < fields.Length; i++)
			{
				System.Reflection.FieldInfo field = body.GetField(fields[i],
					System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
				ClassicAssert.IsNotNull(field, fields[i]);
				ClassicAssert.IsTrue(field.IsInitOnly, fields[i]);
			}
		}

		private static string Frame(string payload)
		{
			byte[] bytes = new UTF8Encoding(false, true).GetBytes(payload);
			StringBuilder digest = new StringBuilder(64);
			using (SHA256 sha = SHA256.Create())
			{
				byte[] hash = sha.ComputeHash(bytes);
				for (int i = 0; i < hash.Length; i++)
				{
					digest.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
				}
			}
			return "taf-seal 2\nsha256 " + digest + "\nlength "
				+ bytes.Length.ToString(CultureInfo.InvariantCulture) + "\n" + payload + "\n";
		}

		[Test]
		public void ComposeAndParseRoundTripForAllValueKinds()
		{
			KingdomSealBody body = new KingdomSealBody();
			body.Put("name", "Kavvat");
			body.Put("water", 42);
			body.PutList("origins", new[] { "salt", "marsh" });
			body.PutList("counts", new long[] { 1, 2, 3 });
			body.PutList("empty", new string[0]);

			string fileText = KingdomSealFormat.Compose(1, body);
			ClassicAssert.AreEqual("taf-seal 1\nsha256 a6c54b357b6d78cb7c71561797b58fcc6c795402c05d8abf6d417ee5404ecdc8\nlength 83\n{\"name\":\"Kavvat\",\"water\":42,\"origins\":[\"salt\",\"marsh\"],\"counts\":[1,2,3],\"empty\":[]}\n",
				fileText);

			int schema;
			KingdomSealBody parsed;
			KingdomSealFault fault;
			string detail;
			bool ok = KingdomSealFormat.TryParse(fileText, 1, 1, out schema, out parsed, out fault, out detail);

			ClassicAssert.IsTrue(ok, detail);
			ClassicAssert.AreEqual(1, schema);
			ClassicAssert.AreEqual("Kavvat", parsed.Text("name"));
			ClassicAssert.AreEqual(42, parsed.Number("water"));
			ClassicAssert.AreEqual(2, parsed.TextList("origins").Count);
			ClassicAssert.AreEqual(3, parsed.NumberList("counts").Count);
			ClassicAssert.AreEqual(0, parsed.TextList("empty").Count);
			ClassicAssert.AreEqual(KingdomSealFault.None, fault);
		}

		[Test]
		public void ParseRejectsWhenChecksumWasTampered()
		{
			KingdomSealBody body = new KingdomSealBody();
			body.Put("k", "v");
			string fileText = KingdomSealFormat.Compose(1, body);

			int lineStart = fileText.IndexOf("sha256 ") + 7;
			char flipped = fileText[lineStart] == 'a' ? 'b' : 'a';
			string tampered = fileText.Substring(0, lineStart) + flipped + fileText.Substring(lineStart + 1);

			int schema;
			KingdomSealBody parsed;
			KingdomSealFault fault;
			string detail;
			bool ok = KingdomSealFormat.TryParse(tampered, 1, 1, out schema, out parsed, out fault, out detail);

			ClassicAssert.IsFalse(ok);
			ClassicAssert.AreEqual(KingdomSealFault.ChecksumMismatch, fault);
		}

		[Test]
		public void ParseRejectsWhenLengthLineLies()
		{
			KingdomSealBody body = new KingdomSealBody();
			body.Put("k", "v");
			string fileText = KingdomSealFormat.Compose(1, body);
			string tampered = fileText.Replace("length 9\n", "length 8\n");

			int schema;
			KingdomSealBody parsed;
			KingdomSealFault fault;
			string detail;
			bool ok = KingdomSealFormat.TryParse(tampered, 1, 1, out schema, out parsed, out fault, out detail);

			ClassicAssert.IsFalse(ok);
			ClassicAssert.AreEqual(KingdomSealFault.LengthMismatch, fault);
		}

		[Test]
		public void RefusalLineMapsKnownFaults()
		{
			ClassicAssert.AreEqual("", KingdomSealFormat.RefusalLine(KingdomSealFault.None));
			ClassicAssert.IsTrue(KingdomSealFormat.RefusalLine(KingdomSealFault.Empty).Length > 0);
			ClassicAssert.IsTrue(KingdomSealFormat.RefusalLine(KingdomSealFault.NotASeal).Length > 0);
		}

		[TestCase("{\"k\":[")]
		[TestCase("{\"k\":[ ")]
		[TestCase("{\"k\":[1,")]
		[TestCase("{\"k\":[1,\"x\"]}")]
		[TestCase("{\"k\":[\"x\",1]}")]
		[TestCase("{\"k\":true}")]
		[TestCase("{\"k\":null}")]
		[TestCase("{\"k\":\"unterminated}")]
		[TestCase("{\"k\":-}")]
		[TestCase("{\"k\":[]")]
		[TestCase("{\"k\":1,}")]
		[TestCase("{\"k\":1,\"k\":2}")]
		public void ValidFramingMalformedPayloadNeverThrows(string payload)
		{
			int schema;
			KingdomSealBody body;
			KingdomSealFault fault;
			string detail;
			bool ok = KingdomSealFormat.TryParse(Frame(payload), 1, 2, out schema, out body, out fault, out detail);
			ClassicAssert.IsFalse(ok, payload);
			ClassicAssert.IsNull(body);
			ClassicAssert.AreNotEqual(KingdomSealFault.None, fault);
		}

		[Test]
		public void DeterministicPayloadMutationCorpusNeverThrows()
		{
			const string seed = "{\"k\":[\"x\",2]}";
			char[] substitutions = new char[8] { '\0', '"', '\\', '[', ']', ',', '-', '9' };
			for (int cut = 0; cut <= seed.Length; cut++)
			{
				ParseWithoutThrow(Frame(seed.Substring(0, cut)));
			}
			for (int at = 0; at < seed.Length; at++)
			{
				for (int replacement = 0; replacement < substitutions.Length; replacement++)
				{
					string payload = seed.Substring(0, at) + substitutions[replacement]
						+ seed.Substring(at + 1);
					ParseWithoutThrow(Frame(payload));
				}
			}
		}

		[Test]
		public void ParserBoundsArraysStringsKeysAndFramingLines()
		{
			StringBuilder array = new StringBuilder("{\"k\":[");
			for (int i = 0; i <= KingdomSealFormat.MaxArrayItems; i++)
			{
				if (i > 0)
				{
					array.Append(',');
				}
				array.Append('0');
			}
			array.Append("]}");

			string[] files = new string[3]
			{
				Frame(array.ToString()),
				Frame("{\"k\":\"" + new string('x', KingdomSealFormat.MaxValueChars + 1) + "\"}"),
				Frame("{\"" + new string('k', KingdomSealFormat.MaxKeyChars + 1) + "\":0}")
			};
			for (int i = 0; i < files.Length; i++)
			{
				int schema;
				KingdomSealBody body;
				KingdomSealFault fault;
				string detail;
				ClassicAssert.IsFalse(KingdomSealFormat.TryParse(files[i], 1, 2, out schema, out body, out fault, out detail));
				ClassicAssert.IsNull(body);
			}

			string longFrame = new string('x', KingdomSealFormat.MaxFramingLineChars + 1) + "\nsha256 "
				+ new string('0', 64) + "\nlength 2\n{}\n";
			int parsedSchema;
			KingdomSealBody parsedBody;
			KingdomSealFault parsedFault;
			string parsedDetail;
			ClassicAssert.IsFalse(KingdomSealFormat.TryParse(longFrame, 1, 2, out parsedSchema, out parsedBody, out parsedFault, out parsedDetail));
			ClassicAssert.AreEqual(KingdomSealFault.MalformedFraming, parsedFault);

			string oversizedWhitespace = new string(' ', KingdomSealFormat.MaxFileChars + 1);
			ClassicAssert.IsFalse(KingdomSealFormat.TryParse(oversizedWhitespace, 1, 2, out parsedSchema,
				out parsedBody, out parsedFault, out parsedDetail));
			ClassicAssert.AreEqual(KingdomSealFault.TooLarge, parsedFault);
		}

		private static void ParseWithoutThrow(string fileText)
		{
			int schema;
			KingdomSealBody body;
			KingdomSealFault fault;
			string detail;
			KingdomSealFormat.TryParse(fileText, 1, 2, out schema, out body, out fault, out detail);
		}
	}
}
#endif
