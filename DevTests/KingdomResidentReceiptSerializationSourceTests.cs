#if TAF_TESTS
using System;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>Source wiring and reflected field-shape coverage only. These do not execute
	/// native serialization or prove BinaryFormatter/save compatibility.</summary>
	[TestFixture]
	public class KingdomResidentReceiptSerializationSourceTests
	{
		// Frozen from git show 7d331fe8a77b630c8245889d36f811d1baf84059:<source path>,
		// before these four named-field adapters. Null and empty defaults are intentionally distinct.
		[TestCase(typeof(KingdomResidentDepartureOperation))]
		[TestCase(typeof(KingdomResidentAdmissionOperation))]
		[TestCase(typeof(KingdomCivicOfficeReceipt))]
		[TestCase(typeof(KingdomPolityNamedFigureRecord))]
		public void ShapeOnlyPreservesPreAdapterInstanceFieldsTypesOrderAndDefaults(Type type)
		{
			ClassicAssert.IsTrue(Attribute.IsDefined(type, typeof(SerializableAttribute)), type.FullName);
			FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public
				| BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
			Array.Sort(fields, (left, right) => left.MetadataToken.CompareTo(right.MetadataToken));
			object value = Activator.CreateInstance(type);
			string[] actual = new string[fields.Length];
			for (int i = 0; i < fields.Length; i++)
			{
				ClassicAssert.IsTrue(fields[i].IsPublic && !fields[i].IsInitOnly, fields[i].Name);
				ClassicAssert.IsFalse(Attribute.IsDefined(fields[i], typeof(NonSerializedAttribute)), fields[i].Name);
				actual[i] = TypeName(fields[i].FieldType) + " " + fields[i].Name
					+ "=" + DefaultValue(fields[i].GetValue(value));
			}
			CollectionAssert.AreEqual(FrozenShape(type), actual, type.FullName);
		}

		[TestCase(typeof(KingdomResidentDepartureOperation), "Growth/KingdomResidentDepartureOperation.cs")]
		[TestCase(typeof(KingdomResidentAdmissionOperation), "Growth/KingdomResidentAdmissionOperation.cs")]
		[TestCase(typeof(KingdomCivicOfficeReceipt), "Experience/KingdomExperienceState.Civic.cs")]
		[TestCase(typeof(KingdomPolityNamedFigureRecord), "Polity/KingdomPolityIncidentState.cs")]
		public void SourceOnlyUsesConditionalNamedCompositeWithoutNormalizingEvidence(Type type, string path)
		{
			string source = TestMain.ReadRepositoryText(path);
			string declaration = "public sealed class " + type.Name;
			StringAssert.Contains("#if!TAF_TESTSusingXRL.World;#endif", Compact(source));
			StringAssert.Contains("[Serializable]" + Compact(declaration)
				+ "#if!TAF_TESTS:IComposite#endif{", Compact(source));
			string body = Block(source, declaration);
			StringAssert.Contains("#if!TAF_TESTSpublicboolWantFieldReflection=>false;", Compact(body));
			ClassicAssert.AreEqual("Writer.WriteNamedFields(this,typeof(" + type.Name + "));",
				Compact(Block(body, "public void Write(SerializationWriter Writer)")));
			ClassicAssert.AreEqual("Reader.ReadNamedFields(this,typeof(" + type.Name + "));",
				Compact(Block(body, "public void Read(SerializationReader Reader)")));
			StringAssert.DoesNotContain("Normalize", body,
				"Adding a serializer must not erase or rewrite in-flight evidence.");
		}

		[Test]
		public void SourceOnlyEnclosingSystemRejectsNestedReadErrorsBeforeAcceptingLoadedState()
		{
			string read = Block(TestMain.ReadRepositoryText("Core/KingdomSystem.z19a.Serialization.cs"),
				"public override void Read(SerializationReader Reader)");
			Ordered(read, "CustomReadCompleted = false;", "int nestedErrorsBefore = Reader.Errors;",
				"Reader.ReadNamedFields(this, typeof(KingdomSystem));",
				"if (Reader.Errors != nestedErrorsBefore)", "throw new InvalidOperationException",
				"NormalizeState(AllowLegacyIdentityMigration: false);", "LoadFailed = false;",
				"CustomReadCompleted = true;");
			StringAssert.IsMatch(@"if\s*\(Reader.Errors\s*!=\s*nestedErrorsBefore\)\s*throw new InvalidOperationException", read);
			StringAssert.Contains("catch{LoadFailed=true;throw;}", Compact(read));
		}

		private static string[] FrozenShape(Type type)
		{
			if (type == typeof(KingdomResidentDepartureOperation)) return new[]
			{
				"int Version=0", "int Phase=0", "long Revision=0",
				"string OperationId=<empty>", "string RealmId=<empty>", "string SettlementId=<empty>",
				"int ResidentId=0", "string BodyObjectId=<empty>", "string ZoneId=<empty>",
				"string ResidentName=<empty>", "string Origin=<empty>", "long PreparedTick=0",
				"int DeparturesBefore=0", "bool Chronicled=false", "string ChronicleLine=<empty>",
				"string LedgerLine=<empty>", "string Cause=<empty>",
				"ThousandAndFirst.KingdomNamedCookReceipt PriorCook=<null>",
				"ThousandAndFirst.KingdomCivicOfficeReceipt PriorOffice=<null>",
				"ThousandAndFirst.KingdomPolityNamedFigureRecord PriorPolity=<null>",
				"string PolityConclusionRef=<empty>", "int AuthorizationKind=0",
				"string AuthorizationEventId=<empty>", "string AuthorizationOwnerObjectId=<empty>",
				"string AuthorizationCauseDigest=<empty>"
			};
			if (type == typeof(KingdomResidentAdmissionOperation)) return new[]
			{
				"int Version=0", "int Phase=0", "long Revision=0", "string OperationId=<empty>",
				"string HandoffId=<empty>", "string RealmId=<empty>", "string SourcePolityId=<empty>",
				"string CohortId=<empty>", "string MemberId=<empty>", "string SettlementId=<empty>",
				"string BodyObjectId=<empty>", "string SourceZoneId=<empty>", "string ProjectionId=<empty>",
				"string BodyBlueprint=<empty>", "string ProposedName=<empty>", "string Origin=<empty>",
				"string Creed=<empty>", "string Arrived=<empty>", "string LodgingProof=<empty>",
				"string FigureId=<empty>", "long PreparedTick=0", "int ResidentCounterBefore=0",
				"int ResidentId=0", "bool Rejected=false", "int RejectionReason=0", "string Fault=<empty>"
			};
			if (type == typeof(KingdomCivicOfficeReceipt)) return new[]
			{
				"int Version=1", "ThousandAndFirst.KingdomCivicOfficePhase Phase=0",
				"ThousandAndFirst.KingdomCivicOfficeVacancyCause VacancyCause=0", "int Generation=0",
				"string SettlementId=<null>", "string SettlementName=<null>", "int WorkId=0",
				"int HolderResidentId=0", "string HolderName=<null>", "string HolderObjectId=<null>",
				"bool OwnsRole=false", "int PredecessorResidentId=0", "string PredecessorName=<null>",
				"long ChangedTick=0", "string Fault=<null>"
			};
			if (type == typeof(KingdomPolityNamedFigureRecord)) return new[]
			{
				"string FigureId=<null>", "string PolityId=<null>", "string DisplayName=<null>",
				"string RoleKey=<null>", "ThousandAndFirst.KingdomPolityFigureOrigin Origin=0",
				"ThousandAndFirst.KingdomPolityFigurePhase Phase=0", "string CauseRef=<null>",
				"string ChronicleRef=<null>", "string ConclusionRef=<null>", "string DeedSummary=<null>",
				"int ResidentId=0", "string ResidentSettlementId=<null>"
			};
			throw new ArgumentException("No frozen receipt shape for " + type.FullName);
		}

		private static string TypeName(Type type)
		{
			if (type == typeof(int)) return "int";
			if (type == typeof(long)) return "long";
			if (type == typeof(bool)) return "bool";
			if (type == typeof(string)) return "string";
			return type.FullName;
		}

		private static string DefaultValue(object value)
		{
			if (value == null) return "<null>";
			if (value is string) return (string)value == "" ? "<empty>" : "string:" + value;
			if (value is bool) return (bool)value ? "true" : "false";
			if (value.GetType().IsEnum) return Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture);
			return Convert.ToString(value, CultureInfo.InvariantCulture);
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
				if (source[i] == '}' && --depth == 0) return source.Substring(start + 1, i - start - 1);
			}
			Assert.Fail("Unterminated source block: " + signature);
			return null;
		}

		private static void Ordered(string source, params string[] markers)
		{
			int after = 0;
			foreach (string marker in markers)
			{
				int at = source.IndexOf(marker, after, StringComparison.Ordinal);
				ClassicAssert.GreaterOrEqual(at, after, marker);
				after = at + marker.Length;
			}
		}
	}
}
#endif
