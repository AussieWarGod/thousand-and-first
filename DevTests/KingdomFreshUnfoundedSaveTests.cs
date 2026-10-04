#if TAF_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	/// <summary>#271/#272: a game that has not founded a city must be saveable. KingdomSystem
	/// needs the engine, so its serialized field census, each book's writer and the projections
	/// its Write refreshes first are pinned from source. Every durable book a fresh KingdomSystem
	/// holds is built exactly as its field initializer builds it and passed through its production
	/// writer and load validator engine-free. Not covered here: the engine's save hooks around
	/// KingdomSystem.Write (BeforeSave, the save-roster patch), state changed by events before the
	/// first save, and the mod's other save records (seal, civic memory, succession, object parts);
	/// only a native unfounded save exercises those.</summary>
	public class KingdomFreshUnfoundedSaveTests
	{
		// Every serialized KingdomSystem field whose value reaches TAF serialization code, in
		// declaration order, with its fresh-game initializer. The engine writes a null field as a
		// null type code, so no TAF writer runs for it in a fresh game.
		private static readonly string[] Census =
		{
			"KingdomLedger Ledger new", "KingdomLifecycleBook LifecycleBook new",
			"Simulation.City.KingdomCityBook City new",
			"Simulation.City.KingdomBindingRegistry Bindings new",
			"Simulation.City.KingdomJobRegistry Jobs new", "KingdomTradeBook TradeBook new",
			"KingdomPolityLedger PolityLedger new", "KingdomFounderHistoryReceipt FounderHistory new",
			"KingdomPolityRealmTransition PolityTransition new",
			"KingdomExperienceLedger Experience new", "KingdomPolityDispatchState PolityDispatch new",
			"KingdomSettlementTopology SettlementTopology new", "KingdomSettlement Away null",
			"KingdomManifest Manifest null", "KingdomManifest LegacyManifestEvidence null",
			"KingdomCarryHaul Haul null", "KingdomCarryBook CarryBook new",
			"KingdomRealmArchive ExiledRealmArchive null", "KingdomSettlement ExiledSeat null",
			"KingdomSettlement ExiledAway null",
			"KingdomSettlementTopology ExiledSettlementTopology new", "KingdomSettlement Seceded null",
			"KingdomResidentDepartureOperation ResidentDeparture new",
			"KingdomResidentAdmissionOperation ResidentAdmission new"
		};

		// Field types the engine's named-field writer serializes itself. Each enum is listed with
		// the production file that declares it.
		private static readonly string[] EngineNative = { "string", "int", "long", "bool", "ulong",
			"List<string>", "List<long>", "Dictionary<string, int>", "Dictionary<string, string>" };
		private static readonly string[][] Enums =
		{
			new[] { "KingdomMasterLatchValue", "Core/KingdomMasterRules.cs" },
			new[] { "KingdomIdentityOrigin", "Core/KingdomIdentityOrigin.cs" },
			new[] { "GrowthStage", "Core/KingdomRules.cs" },
			new[] { "KingdomRules.MealVerdict", "Core/KingdomRules.Meals.cs" },
			new[] { "KingdomRules.GatePolicy", "Core/KingdomRules.Policy.cs" },
			new[] { "KingdomRules.StoresPolicy", "Core/KingdomRules.Policy.cs" },
			new[] { "KingdomRules.PetitionKind", "Core/KingdomRules.Policy.cs" },
			new[] { "PetitionLifecycle", "Quests/PetitionLifecycle.cs" }
		};

		private const string Named = "Writer.WriteNamedFields(this,typeof({0}));";
		private const string Envelope = "byte[]envelope={0}.EncodeEnvelope(this);"
			+ "Writer.Write(envelope.Length);Writer.Write(envelope,0,envelope.Length);";
		private const string Composite = "#if!TAF_TESTS:IComposite#endif";

		// Production Write(SerializationWriter) bodies, whitespace and comments removed, each
		// compared whole: the engine's named-field writer (emulated by Walk), or a writer this
		// fixture runs engine-free. Every class keeps field reflection off, and every book declares
		// IComposite (in the file given last when another partial declares it), so the engine runs
		// exactly this body. KingdomSystem.Write itself runs only the projections pinned below.
		private static readonly string[][] Writers =
		{
			new[] { "Core/KingdomSystem.z19a.Serialization.cs", "KingdomSystem", "SerializationVersion="
				+ "CurrentSerializationVersion;SynchronizeLegacyManifestProjection();SynchronizeLegacy"
				+ "SettlementProjection();SynchronizeLegacyExiledProjection();Writer.Write(Serialization"
				+ "Magic);Writer.Write(CurrentSerializationVersion);" + Named },
			new[] { "Core/KingdomLedger.cs", "KingdomLedger", Named },
			new[] { "Experience/KingdomLifecycleBook.cs", "KingdomLifecycleBook",
				"KingdomLifecycleWireCodec.WriteLifecycle(Writer,this);" },
			new[] { "Simulation/City/KingdomCityBook.03.CompositeAndCounts.cs", "KingdomCityBook",
				"if(SubsidenceReadFailed)thrownewSystem.IO.InvalidDataException("
				+ "\"Citysubsidencestoragedidnotfinishloading.\");" + Named, "Simulation/City/KingdomCityBook.cs" },
			new[] { "Simulation/City/KingdomBindingRegistry.cs", "KingdomBindingRegistry", Named },
			new[] { "Simulation/City/KingdomJobRegistry.z10.RegistryFields.cs", "KingdomJobRegistry", Named },
			new[] { "Trade/KingdomTradeState.cs", "KingdomTradeBook", Envelope.Replace("{0}", "KingdomTradeCodec") },
			new[] { "Polity/KingdomPolityLedger.cs", "KingdomPolityLedger", Envelope.Replace("{0}", "KingdomPolityCodec") },
			new[] { "Core/KingdomFounderHistoryReceipt.cs", "KingdomFounderHistoryReceipt", Named },
			new[] { "Polity/KingdomPolityActivationModels.cs", "KingdomPolityRealmTransition", Named },
			new[] { "Experience/KingdomExperienceState.cs", "KingdomExperienceLedger",
				Envelope.Replace("{0}", "KingdomExperienceCodec") },
			new[] { "Polity/KingdomPolityDispatchModels.cs", "KingdomPolityDispatchState", Named },
			new[] { "Core/KingdomSettlementTopology.cs", "KingdomSettlementTopology", "if(settlements.Count!="
				+ "opaque.Count||settlements.Count>KingdomSettlementTopologyRules.MaxNonSeatSettlements)"
				+ "thrownewInvalidDataException(\"Settlementtopologyexceedsitsbound.\");Writer.Write("
				+ "Magic);Writer.Write(CurrentVersion);Writer.Write(settlements.Count);for(inti=0;i<"
				+ "settlements.Count;i++){byte[]payload=opaque[i];if(payload==null&&!KingdomArchived"
				+ "SettlementCodec.TryEncode(settlements[i],outpayload,outstringfailure))thrownew"
				+ "InvalidDataException(failure);if(payload==null||payload.Length<8||payload.Length>"
				+ "KingdomArchivedSettlementCodec.MaxPayloadBytes)thrownewInvalidDataException(\"Settlement"
				+ "topologypayloadexceedsitsbound.\");Writer.Write(payload.Length);Writer.Write(payload,0,"
				+ "payload.Length);}" },
			new[] { "Experience/KingdomCarryBook.cs", "KingdomCarryBook", "KingdomLifecycleWireCodec.WriteCarry(Writer,this);" },
			new[] { "Growth/KingdomResidentDepartureOperation.cs", "KingdomResidentDepartureOperation", Named },
			new[] { "Growth/KingdomResidentAdmissionOperation.cs", "KingdomResidentAdmissionOperation", Named },
			new[] { "Core/KingdomNamedCookReceipt.cs", "KingdomNamedCookReceipt", Named },
			new[] { "Core/KingdomAssentingMootReceipt.cs", "KingdomAssentingMootReceipt", Named }
		};

		// The three projections KingdomSystem.Write refreshes before its named fields, and the
		// legacy manifest snapshot they call, as whole compacted bodies. In a fresh game all three
		// stay null: both topologies are empty and the Trade book has no manifest.
		private static readonly string[][] Projections =
		{
			new[] { "Core/KingdomSystem.z08.SettlementTopology.cs", "private void SynchronizeLegacySettlementProjection()",
				"#pragmawarningdisable618Away=SettlementTopology?.Get(0);#pragmawarningrestore618" },
			new[] { "Core/KingdomSystem.z08.SettlementTopology.cs", "private void SynchronizeLegacyExiledProjection()",
				"#pragmawarningdisable618ExiledAway=ExiledSettlementTopology?.Get(0);#pragmawarningrestore618" },
			new[] { "Core/KingdomSystem.z26.TradeNormalization.cs", "internal void SynchronizeLegacyManifestProjection()",
				"#pragmawarningdisable618Manifest=KingdomTrade.LegacyManifestSnapshot(TradeBook?.Manifest);"
				+ "#pragmawarningrestore618" },
			new[] { "Trade/KingdomTrade.cs",
				"internal static KingdomManifest LegacyManifestSnapshot(KingdomTradeManifestState Manifest)",
				"if(Manifest==null)returnnull;returnnewKingdomManifest{OriginName=Manifest.OriginName,"
				+ "DestinationName=Manifest.DestinationName,Drams=Manifest.EscrowDrams,LoadedTick="
				+ "Manifest.LoadedTick,DeadlineTick=Manifest.DeadlineTick,TurnedBack=Manifest.TurnedBack};" }
		};

		[Test]
		public void FreshUnfoundedKingdomSystemEveryDurableBookIsWritable()
		{
			List<string> failures = new List<string>();
			Check(failures, "Ledger", new KingdomLedger(), b => Settled(b, x => ((KingdomLedger)x).Normalize()));
			Check(failures, "LifecycleBook", new KingdomLifecycleBook(), b => Lifecycle((KingdomLifecycleBook)b));
			Check(failures, "City", new KingdomCityBook(), b => City((KingdomCityBook)b));
			Check(failures, "Bindings", new KingdomBindingRegistry(),
				b => Settled(b, x => ((KingdomBindingRegistry)x).Normalize()));
#if !TAF_CONSTRUCTION_INPUT_PORTABLE
			Check(failures, "Jobs", new KingdomJobRegistry(), b => Settled(b, x => ((KingdomJobRegistry)x).Normalize()));
			Check(failures, "TradeBook", new KingdomTradeBook(), b => Trade((KingdomTradeBook)b));
#endif
			Check(failures, "PolityLedger", new KingdomPolityLedger(), b => Polity((KingdomPolityLedger)b));
			Check(failures, "FounderHistory", new KingdomFounderHistoryReceipt(), b => History((KingdomFounderHistoryReceipt)b));
			Check(failures, "PolityTransition", new KingdomPolityRealmTransition(), b =>
				KingdomPolityRules.TryValidateRealmTransition((KingdomPolityRealmTransition)b, out string f) ? null : f);
			Check(failures, "Experience", new KingdomExperienceLedger(), b => Experience((KingdomExperienceLedger)b));
			Check(failures, "PolityDispatch", new KingdomPolityDispatchState(), b =>
				KingdomPolityDispatchRules.ValidState((KingdomPolityDispatchState)b, out string f) ? null : f);
#if !TAF_CONSTRUCTION_INPUT_PORTABLE
			Check(failures, "SettlementTopology", new KingdomSettlementTopology(), b => Topology((KingdomSettlementTopology)b));
			Check(failures, "ExiledSettlementTopology", new KingdomSettlementTopology(),
				b => Topology((KingdomSettlementTopology)b));
#endif
			Check(failures, "CarryBook", new KingdomCarryBook(), b => Carry((KingdomCarryBook)b));
			Check(failures, "ResidentDeparture", new KingdomResidentDepartureOperation(), b =>
				KingdomResidentDepartureRules.IsEmpty((KingdomResidentDepartureOperation)b) ? null : "not empty");
#if !TAF_CONSTRUCTION_INPUT_PORTABLE
			Check(failures, "ResidentAdmission", new KingdomResidentAdmissionOperation(), b =>
				KingdomResidentAdmissionRules.Empty((KingdomResidentAdmissionOperation)b)
				&& KingdomResidentAdmissionRules.Valid((KingdomResidentAdmissionOperation)b) ? null : "not empty");
			// KingdomSystem.Write refreshes Away and ExiledAway from the topologies and Manifest
			// from the Trade manifest before writing; all three stay null in a fresh game.
			ClassicAssert.IsNull(new KingdomSettlementTopology().Get(0));
			ClassicAssert.IsNull(new KingdomTradeBook().Manifest);
#endif
			CollectionAssert.IsEmpty(failures, string.Join("\n", failures));
		}

		[Test]
		public void FreshUnfoundedKingdomSystemDurableBookCensusIsFrozen()
		{
			List<string> composites = new List<string>(), names = new List<string>();
			foreach (string[] field in SystemFields())
			{
				names.Add(field[1]);
				if (Array.IndexOf(EngineNative, field[0]) >= 0
					|| Array.Exists(Enums, row => row[0] == field[0])) continue;
				composites.Add(field[0] + " " + field[1] + " "
					+ (field[2] == "" ? "null" : field[2] == "new " + field[0] + "()" ? "new" : field[2]));
			}
			CollectionAssert.AreEqual(Census, composites, "a KingdomSystem durable field changed: extend "
				+ "FreshUnfoundedKingdomSystemEveryDurableBookIsWritable. Read but not in the census: "
				+ string.Join("; ", composites.FindAll(row => Array.IndexOf(Census, row) < 0)) + ". In the census but not read: "
				+ string.Join("; ", Array.FindAll(Census, row => !composites.Contains(row))));
			ClassicAssert.AreEqual(names.Count, new HashSet<string>(names).Count, "field names are unique");
			foreach (string[] row in Enums)
				StringAssert.IsMatch(@"\benum " + row[0].Substring(row[0].LastIndexOf('.') + 1) + @"\b",
					TestMain.ReadRepositoryText(row[1]), row[0]);
			foreach (string[] row in Writers)
			{
				string[] lines = Lines(row[0]);
				int type = Array.FindIndex(lines, line => Regex.IsMatch(line, @"\bclass " + row[1] + @"\b"));
				int write = Find(lines, Math.Max(type, 0), @"public (override )?void Write\(SerializationWriter Writer\)");
				int reflection = Find(lines, Math.Max(type, 0), @"\bbool WantFieldReflection\b");
				ClassicAssert.IsTrue(type >= 0 && write > reflection && reflection > type, row[1] + " writer not found");
				ClassicAssert.AreEqual((row[1] == "KingdomSystem" ? "publicoverride" : "public")
					+ "boolWantFieldReflection=>false;", Compact(lines[reflection]), row[1] + " reflects fields");
				ClassicAssert.AreEqual(row[2].Replace("{0}", row[1]), Compact(Body(lines, write)),
					row[1] + " writer changed: review this fixture");
				// KingdomSystem is an IComposite through IGameSystem (decompiled 2.0.211.56
				// XRL/IGameSystem.cs:12). The engine runs a book's own writer only for an IComposite
				// (XRL/World/SerializationWriter.cs:1084-1088, 2756-2769); any other object falls
				// through to its BinaryFormatter fallback (:1220-1225).
				if (row[1] == "KingdomSystem") continue;
				string[] declaring = Lines(row.Length > 3 ? row[3] : row[0]);
				int header = Array.FindIndex(declaring, line => Regex.IsMatch(line, @"\bclass " + row[1] + @"\b"));
				ClassicAssert.AreEqual(Composite, header < 0 || header + 3 >= declaring.Length ? "absent"
					: Compact(declaring[header + 1] + declaring[header + 2] + declaring[header + 3]), row[1] + " is not an IComposite");
			}
			foreach (string[] row in Projections)
			{
				string[] lines = Lines(row[0]);
				List<int> found = new List<int>();
				for (int i = 0; i < lines.Length; i++)
					if (Compact(Regex.Replace(lines[i], "//.*$", "")).Length > 0
						&& Compact(Code(lines, i, Compact(row[1]).Length)) == Compact(row[1])) found.Add(i);
				ClassicAssert.AreEqual(1, found.Count, row[1] + " is not declared once in " + row[0]);
				ClassicAssert.AreEqual(row[2], Compact(Body(lines, found[0])), row[1] + " changed: review this fixture");
			}
		}

		[Test]
		public void KingdomSystemCensusParserReadsAnyLayoutOrFailsClosed()
		{
			List<string> failures = new List<string>();
			foreach (string[] probe in ParserProbes)
			{
				List<string[]> rows = new List<string[]>();
				ReadSystemFields("Probe.cs", probe[1], rows);
				string read = rows.Exists(row => row[0].StartsWith("UNPARSED ", StringComparison.Ordinal)) ? "UNPARSED"
					: string.Join(";", rows.ConvertAll(row => string.Join("|", row)));
				if (read != probe[2]) failures.Add(probe[0] + ": expected " + probe[2] + ", read " + read);
			}
			CollectionAssert.IsEmpty(failures, string.Join("\n", failures));
		}

		// Whole source files read by ReadSystemFields, with the rows they must yield
		// (type|name|initializer, ';'-separated), or UNPARSED when at least one UNPARSED row must
		// result. The earlier line-based census silently dropped the field in the first three and
		// in "field after a body on the same line".
		private static readonly string[][] ParserProbes =
		{
			new[] { "one-line partial", "namespace N { public partial class KingdomSystem { public KingdomRealmArchive A"
				+ " = new KingdomRealmArchive(); } }", "KingdomRealmArchive|A|new KingdomRealmArchive()" },
			new[] { "tuple-typed field", Partial("\t\tpublic (string Id, KingdomRealmArchive Archive) A = (null, new KingdomRealmArchive());"),
				"(string Id, KingdomRealmArchive Archive)|A|(null, new KingdomRealmArchive())" },
			new[] { "comment holding a parenthesis before a continuation line", Partial("\t\tpublic KingdomRealmArchive A"
				+ " // staged (#280)\n\t\t\t= new KingdomRealmArchive();"), "KingdomRealmArchive|A|new KingdomRealmArchive()" },
			new[] { "block comment holding a parenthesis", Partial("\t\tpublic KingdomRealmArchive A /* (#280) */"
				+ " = new KingdomRealmArchive();"), "KingdomRealmArchive|A|new KingdomRealmArchive()" },
			new[] { "field after a body on the same line", Partial("\t\tpublic void F() { } public KingdomRealmArchive A;"),
				"KingdomRealmArchive|A|" },
			new[] { "K&R braces and space indentation", "namespace N {\n    public partial class KingdomSystem {\n"
				+ "        public KingdomRealmArchive A;\n    }\n}\n", "KingdomRealmArchive|A|" },
			new[] { "file-scoped namespace", "namespace N;\npublic partial class KingdomSystem\n{\n\tpublic KingdomRealmArchive A;\n"
				+ "\tpublic sealed class Inner\n\t{\n\t\tpublic KingdomRealmArchive X;\n\t}\n}\n", "KingdomRealmArchive|A|" },
			new[] { "directive lines around a field", Partial("#region Archive's notes\n#pragma warning disable 618\n"
				+ "\t\t[Obsolete(\"x\")] public KingdomRealmArchive A;\n#pragma warning restore 618\n#endregion"), "KingdomRealmArchive|A|" },
			new[] { "attributes, modifier order and type spacing", Partial("\t\t[Obsolete(\"(x)\")] public KingdomRealmArchive A;\n"
				+ "\t\t[NonSerialized] public KingdomRealmArchive B;\n\t\treadonly public KingdomRealmArchive C;\n"
				+ "\t\tpublic Dictionary<string,int> D = new Dictionary<string,int>();"),
				"KingdomRealmArchive|A|;KingdomRealmArchive|C|;Dictionary<string, int>|D|new Dictionary<string,int>()" },
			new[] { "initializer and lambda bodies", Partial("\t\tpublic List<KingdomRealmArchive> L = new List<KingdomRealmArchive>\n"
				+ "\t\t{\n\t\t\tnew KingdomRealmArchive(),\n\t\t};\n\t\tpublic Func<int> F = () => { return 1; }; public KingdomRealmArchive A;"),
				"List<KingdomRealmArchive>|L|new List<KingdomRealmArchive> {};Func<int>|F|() => {};KingdomRealmArchive|A|" },
			new[] { "members that are not serialized fields", Partial("\t\tpublic KingdomRealmArchive P { get; set; } = new"
				+ " KingdomRealmArchive();\n\t\tpublic int Q => 1;\n\t\tpublic T Get<T>() where T : new() { return new T(); }\n"
				+ "\t\tpublic int this[int i] => i;\n\t\tpublic sealed class Inner { public KingdomRealmArchive X; }\n"
				+ "\t\tpublic sealed class Box<T> { public T Value; }\n\t\tpublic delegate void D();\n"
				+ "\t\tpublic static KingdomRealmArchive S;\n\t\tpublic const int C = 1;\n\t\tpublic event Action E;\n"
				+ "\t\tprivate KingdomRealmArchive H;\n\t\tinternal KingdomRealmArchive I = new KingdomRealmArchive();")
				+ "namespace M { public class KingdomSeal { public KingdomRealmArchive Z; } }\n", "" },
			new[] { "public constructor", Partial("\t\tpublic KingdomSystem() { }"), "UNPARSED" },
			new[] { "static constructor", Partial("\t\tstatic KingdomSystem() { }"), "UNPARSED" },
			new[] { "private constructor", Partial("\t\tprivate KingdomSystem(int x) { }"), "UNPARSED" },
			new[] { "primary constructor", "namespace N { public partial class KingdomSystem(int x) { } }", "UNPARSED" },
			new[] { "two declarators", Partial("\t\tpublic KingdomRealmArchive A, B;"), "UNPARSED" },
			new[] { "initializer braces followed by a keyword", Partial("\t\tpublic KingdomRealmArchive A = new KingdomRealmArchive { }"
				+ " as KingdomRealmArchive;"), "UNPARSED" },
			new[] { "public after another token", Partial("\t\tKingdomRealmArchive public A;"), "UNPARSED" },
			new[] { "unbalanced braces", "namespace N { public partial class KingdomSystem { public void F() { } }", "UNPARSED" },
			new[] { "raw string literal", Partial("\t\tpublic string S = \"\"\"{\"\"\";"), "UNPARSED" },
			new[] { "quote inside an interpolation hole", Partial("\t\tpublic string S() => $\"{F(\"}\")}\";"), "UNPARSED" },
			new[] { "directive branches that miscount braces", Partial("#if A\n\t\tpublic void F() {\n#else\n"
				+ "\t\tpublic void F(int x) {\n#endif\n\t\t}\n\t\tpublic KingdomRealmArchive B;\n#if A\n\t}\n#endif"), "UNPARSED" }
		};

		private static string Partial(string members)
		{
			return "namespace N\n{\n\tpublic partial class KingdomSystem\n\t{\n" + members + "\n\t}\n}\n";
		}

		/// <summary>Serialized KingdomSystem fields (type, name, initializer) in declaration order:
		/// public instance fields that are not static, const or [NonSerialized], as
		/// SerializationWriter.WriteNamedFields selects them (decompiled 2.0.211.56
		/// XRL/World/SerializationWriter.cs:2981-3008), from every production partial.</summary>
		private static List<string[]> SystemFields()
		{
			List<string[]> fields = new List<string[]>();
			foreach (string file in ProductionFiles()) ReadSystemFields(file, TestMain.ReadRepositoryText(file), fields);
			return fields;
		}

		/// <summary>Adds the serialized fields of every KingdomSystem body in one source file. A body
		/// is read as one stream of code, whatever its line layout: comments, literals and directive
		/// lines are removed, each member ends at its own ';' or at the end of its own body, and
		/// nested bodies fold to "{}" (classified by <see cref="ClassifyMember"/>). A string form
		/// <see cref="CodeOnly"/> does not model, unbalanced braces, a primary constructor, or
		/// conventional member indentation inside a nested block becomes an UNPARSED row, which
		/// fails the census.</summary>
		private static void ReadSystemFields(string file, string source, List<string[]> fields)
		{
			if (!source.Contains("KingdomSystem")) return;
			string[] code = CodeOnly(source.Replace("\r\n", "\n").Split('\n'), out bool unread);
			string text = string.Join("\n", code);
			MatchCollection classes = Regex.Matches(text, @"\bclass\s+KingdomSystem\b");
			if (classes.Count == 0) return;
			if (unread) fields.Add(Unparsed(file, "a string literal form this reader does not model"));
			List<string> members = new List<string>();
			StringBuilder member = new StringBuilder();
			int depth = 0, body = -1, next = 0, line = 0;
			bool open = false, broken = false;
			for (int p = 0; p < text.Length; p++)
			{
				if (p > 0 && text[p - 1] == '\n') line++;
				// Conventional member indentation inside a nested block means the depth count is off.
				if ((p == 0 || text[p - 1] == '\n') && body == 2 && depth > body
					&& code[line].StartsWith("\t\tpublic ", StringComparison.Ordinal))
					fields.Add(Unparsed(file + ":" + (line + 1), code[line].Trim()));
				char c = text[p];
				if (next < classes.Count && p == classes[next].Index)
				{
					broken |= body >= 0;
					open = true;
					next++;
				}
				if (open)
				{
					// The class body opens at its first brace; a primary constructor or a missing
					// body is not read.
					if (c == '{') { body = ++depth; open = false; member.Clear(); }
					else if (c == '(' || c == ';' || c == '}') broken = true;
					continue;
				}
				if (body < 0)
				{
					if (c == '{') depth++;
					else if (c == '}' && --depth < 0) broken = true;
					continue;
				}
				if (depth == body)
				{
					if (c == '}') { body = -1; depth--; }
					else member.Append(c);
					if (c == '}' || c == ';') Flush(members, member);
					if (c == '{') depth++;
					continue;
				}
				// Inside a member's own braces only the depth is kept: the body folds to "{}".
				if (c == '{') depth++;
				else if (c == '}' && --depth == body)
				{
					member.Append('}');
					// A body ends its member unless an expression goes on after it: the braces of an
					// initializer, a property initializer, or the optional ';' after a nested type.
					if (";,.)]=?:+-*/%&|^!<>".IndexOf(NextCode(text, p + 1)) < 0) Flush(members, member);
				}
			}
			if (broken || open || body != -1 || depth != 0) fields.Add(Unparsed(file, "unbalanced braces"));
			foreach (string declaration in members) ClassifyMember(file, declaration, fields);
		}

		private static readonly Regex Modifiers = new Regex(@"^(?:(?:public|private|protected|internal|static|readonly"
			+ @"|const|volatile|new|unsafe|extern|override|virtual|abstract|sealed|partial|async|required|event|ref)\b\s*)*");

		/// <summary>Adds the census row of one member-level declaration (comments removed, literals
		/// emptied, nested bodies folded to "{}") when it is a serialized field. A member whose
		/// modifiers lack public, or include static, const or event, is not serialized; a public
		/// nested type, method (a name, optionally with type parameters, right before '('), property
		/// (a name right before '{' or "=>") or indexer is not a field. A constructor of any
		/// accessibility, "public" anywhere after the leading modifiers, and a public member of any
		/// other shape become UNPARSED rows.</summary>
		private static void ClassifyMember(string file, string declaration, List<string[]> fields)
		{
			string text = Regex.Replace(declaration, @"\s+", " ").Trim(), rest = text;
			bool notSerialized = false;
			while (rest.StartsWith("[", StringComparison.Ordinal))
			{
				int end = Closing(rest, 0, '[', ']');
				if (end < 0) { fields.Add(Unparsed(file, text)); return; }
				notSerialized |= Regex.IsMatch(rest.Substring(0, end), @"\bNonSerialized(Attribute)?\b");
				rest = rest.Substring(end + 1).TrimStart();
			}
			Match modifiers = Modifiers.Match(rest);
			List<string> words = new List<string>(modifiers.Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
			rest = rest.Substring(modifiers.Length);
			// A constructor would change the fresh state the census reads from initializers. With
			// bodies folded and literals emptied, "public" after the modifiers means two members ran
			// together.
			if (Regex.IsMatch(rest, @"^(~\s*)?KingdomSystem\s*\(|\bpublic\b")) { fields.Add(Unparsed(file, text)); return; }
			if (!words.Contains("public") || words.Contains("static") || words.Contains("const") || words.Contains("event")
				|| Regex.IsMatch(rest, @"^(class|struct|interface|enum|record|delegate)\b")) return;
			int type = TypeLength(rest);
			Match shape = Regex.Match(rest.Substring(type), @"^\s*(?<name>@?[A-Za-z_]\w*)\s*(?<generic><.*?>)?\s*(?<tail>=>|[;=({\[,])");
			string name = shape.Groups["name"].Value, tail = shape.Groups["tail"].Value;
			bool plain = !shape.Groups["generic"].Success && name != "this" && name != "operator";
			int after = type + shape.Length;
			if (type > 0 && shape.Success)
			{
				if (tail == "(" && name != "this" && name != "operator") return;
				if (tail == "[" ? name == "this" && !shape.Groups["generic"].Success : plain && (tail == "{" || tail == "=>")) return;
				if (plain && (tail == "=" ? rest.EndsWith(";", StringComparison.Ordinal) : tail == ";" && after == rest.Length))
				{
					if (!notSerialized) fields.Add(new[] { NormalizeType(rest.Substring(0, type)), name,
						tail == "=" ? rest.Substring(after, rest.Length - 1 - after).Trim() : "" });
					return;
				}
			}
			fields.Add(Unparsed(file, text));
		}

		/// <summary>Length of the type that starts a declaration: a tuple, or a dotted name with
		/// type arguments, then nullable, pointer and array suffixes; 0 when there is none.</summary>
		private static int TypeLength(string text)
		{
			int i = text.StartsWith("(", StringComparison.Ordinal) ? Closing(text, 0, '(', ')') + 1
				: Regex.Match(text, @"^(?:global::)?@?[A-Za-z_]\w*").Length;
			for (Match part; i > 0 && (part = Regex.Match(text.Substring(i), @"^(?:<|\.@?[A-Za-z_]\w*|\?|\*|\[[\s,]*\])")).Success; )
				i = part.Value == "<" ? Closing(text, i, '<', '>') + 1 : i + part.Length;
			return i;
		}

		private static string NormalizeType(string type)
		{
			return Regex.Replace(Regex.Replace(type, @"\s*([<>(),\[\]?*.])\s*", "$1"), @",(?=[^,\]])", ", ");
		}

		/// <summary>Index of the bracket that closes the one at <paramref name="start"/>, or -1.</summary>
		private static int Closing(string text, int start, char open, char close)
		{
			for (int i = start, depth = 0; i < text.Length; i++)
				if (text[i] == open) depth++;
				else if (text[i] == close && --depth == 0) return i;
			return -1;
		}

		private static char NextCode(string text, int from)
		{
			while (from < text.Length && char.IsWhiteSpace(text[from])) from++;
			return from < text.Length ? text[from] : '\0';
		}

		private static void Flush(List<string> members, StringBuilder member)
		{
			if (member.ToString().Trim().Length > 0) members.Add(member.ToString());
			member.Clear();
		}

		private static string[] Unparsed(string where, string text)
		{
			return new[] { "UNPARSED " + where + ": " + text, "", "" };
		}

		private static List<string> ProductionFiles()
		{
			List<string> files = new List<string>();
			foreach (string file in Directory.GetFiles(TestMain.RepositoryRoot, "*.cs", SearchOption.AllDirectories))
			{
				string relative = file.Substring(TestMain.RepositoryRoot.Length + 1).Replace('\\', '/');
				if (!relative.StartsWith("DevTests/", StringComparison.Ordinal)
					&& !relative.StartsWith("Tools/", StringComparison.Ordinal)) files.Add(relative);
			}
			files.Sort(StringComparer.Ordinal);
			return files;
		}

		/// <summary>Each line with comments and directive lines removed and string and character
		/// literals emptied, so braces and keywords are read from code only. Regular, verbatim and
		/// character literals, and interpolated strings whose holes hold no quote, brace or literal
		/// prefix, are modelled; any other string form, or a literal left open at the end of its
		/// line, sets <paramref name="unread"/>.</summary>
		private static string[] CodeOnly(string[] lines, out bool unread)
		{
			string[] code = new string[lines.Length];
			bool block = false, verbatim = false;
			unread = false;
			for (int n = 0; n < lines.Length; n++)
			{
				StringBuilder kept = new StringBuilder();
				string line = lines[n];
				// A directive owns its whole line; reading every branch never drops a member.
				if (!block && !verbatim && line.TrimStart().StartsWith("#", StringComparison.Ordinal)) line = "";
				for (int i = 0; i < line.Length; i++)
				{
					char c = line[i], next = i + 1 < line.Length ? line[i + 1] : '\0';
					if (block) { if (c == '*' && next == '/') { block = false; i++; } continue; }
					if (verbatim) { if (c == '"' && next == '"') i++; else if (c == '"') verbatim = false; continue; }
					if (c == '/' && next == '/') break;
					if (c == '/' && next == '*') { block = true; i++; continue; }
					// Raw strings and interpolated verbatim or raw strings are not modelled.
					unread |= c == '"' && next == '"' && i + 2 < line.Length && line[i + 2] == '"'
						|| (c == '$' || c == '@') && (next == '$' || next == '@');
					if (c == '@' && next == '"') { verbatim = true; i++; kept.Append("\"\""); continue; }
					if (c == '"' || c == '\'')
					{
						bool interpolated = c == '"' && i > 0 && line[i - 1] == '$', hole = false;
						for (i++; i < line.Length && (hole || line[i] != c); i++)
						{
							char d = line[i];
							if (hole) { unread |= d == '"' || d == '\'' || d == '{' || d == '@' || d == '$'; hole = d != '}'; }
							else if (d == '\\') i++;
							else if (interpolated && d == '{') { if (i + 1 < line.Length && line[i + 1] == '{') i++; else hole = true; }
						}
						unread |= i >= line.Length;
						kept.Append(c == '"' ? "\"\"" : "''");
						continue;
					}
					kept.Append(c);
				}
				code[n] = kept.ToString();
			}
			return code;
		}

		private static string[] Lines(string path)
		{
			return TestMain.ReadRepositoryText(path).Replace("\r\n", "\n").Split('\n');
		}

		private static int Find(string[] lines, int start, string pattern)
		{
			return Array.FindIndex(lines, start, line => Regex.IsMatch(line, pattern));
		}

		/// <summary>Lines from <paramref name="start"/> on, comments removed, until at least
		/// <paramref name="length"/> non-blank characters are read: a declaration may span lines.</summary>
		private static string Code(string[] lines, int start, int length)
		{
			StringBuilder text = new StringBuilder();
			for (int i = start; i < lines.Length && Compact(text.ToString()).Length < length; i++)
				text.Append(Regex.Replace(lines[i], "//.*$", "")).Append('\n');
			return text.ToString();
		}

		/// <summary>The body of the member declared at <paramref name="declaration"/>: from its
		/// opening brace line to the closing brace at the same indentation, comments removed.</summary>
		private static string Body(string[] lines, int declaration)
		{
			int i = declaration;
			while (lines[i].Trim() != "{") i++;
			string indent = lines[i].Substring(0, lines[i].Length - lines[i].TrimStart().Length);
			StringBuilder body = new StringBuilder();
			for (i++; lines[i] != indent + "}"; i++) body.Append(Regex.Replace(lines[i], "//.*$", "")).Append('\n');
			return body.ToString();
		}

		private static string Compact(string text) { return Regex.Replace(text, @"\s+", ""); }

		private static void Check(List<string> failures, string field, object fresh, Func<object, string> validate)
		{
			string failure;
			try { failure = Walk(fresh, field, 0, new StringBuilder()) ?? validate(fresh); }
			catch (Exception e) { failure = e.GetType().Name + ": " + e.Message; }
			if (failure != null) failures.Add(field + ": " + failure);
			Console.WriteLine("FRESH-BOOK " + field + " " + (failure == null ? "writable" : "REFUSED " + failure));
		}

		// Mirrors SerializationWriter.WriteObject (decompiled 2.0.211.56
		// XRL/World/SerializationWriter.cs:715-1236) for what a fresh book holds: engine-native
		// values, collections of them and named-field composites, recursively. Any other object
		// would reach the engine's BinaryFormatter fallback, so it fails here.
		private static string Walk(object value, string path, int depth, StringBuilder image)
		{
			if (value == null || value is string || value is Enum || value.GetType().IsPrimitive)
			{
				image.Append(path).Append('=').Append(value ?? "<null>").Append('\n');
				return null;
			}
			if (depth > 6) return path + " nests too deeply";
			if (value is IDictionary map)
			{
				foreach (DictionaryEntry entry in map)
					if ((Walk(entry.Key, path + "{}", depth + 1, image)
						?? Walk(entry.Value, path + "{" + entry.Key + "}", depth + 1, image)) is string failure)
						return failure;
				return null;
			}
			if (value is IList list)
			{
				for (int i = 0; i < list.Count; i++)
					if (Walk(list[i], path + "[" + i + "]", depth + 1, image) is string failure) return failure;
				return null;
			}
			string[] writer = Array.Find(Writers, row => row[1] == value.GetType().Name);
			if (writer == null) return path + " (" + value.GetType().Name + ") has no engine-free writer";
			// A book with its own writer is exercised only at top level, by its validator. A top-level
			// writer that ends in the named-field writer (the city book, after its load guard) writes
			// every field through WriteObject, so its fields are walked too.
			if (writer[2] != Named && (depth > 0 || !writer[2].EndsWith(Named, StringComparison.Ordinal)))
				return depth == 0 ? null : path + " nests a book with its own writer";
			// SerializationWriter.WriteNamedFields selects public instance fields that are not
			// static, literal or NotSerialized (decompiled XRL/World/SerializationWriter.cs:2981-3008).
			foreach (FieldInfo field in value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
				if (!field.IsLiteral && !Attribute.IsDefined(field, typeof(NonSerializedAttribute))
					&& Walk(field.GetValue(value), path + "." + field.Name, depth + 1, image) is string failure) return failure;
			return null;
		}

		/// <summary>Load normalization reaches a fixed point that is still writable.</summary>
		private static string Settled(object book, Action<object> normalize)
		{
			StringBuilder once = new StringBuilder(), twice = new StringBuilder();
			normalize(book);
			string failure = Walk(book, "book", 0, once);
			normalize(book);
			Walk(book, "book", 0, twice);
			return failure ?? (once.ToString() == twice.ToString() ? null : "load normalization does not settle");
		}

		private static string History(KingdomFounderHistoryReceipt receipt)
		{
			string failure = Settled(receipt, x => ((KingdomFounderHistoryReceipt)x).Normalize());
			return failure ?? (receipt.Phase == KingdomFounderHistoryPhase.None && receipt.Fault == ""
				? null : "fresh founder history does not load idle");
		}

		private static string Lifecycle(KingdomLifecycleBook book)
		{
			byte[] saved = Bytes(w => KingdomLifecycleWireCodec.WriteLifecycle(w, book));
			KingdomLifecycleBook loaded = new KingdomLifecycleBook();
			using (BinaryReader reader = new BinaryReader(new MemoryStream(saved, false)))
				KingdomLifecycleWireCodec.ReadLifecycle(reader, loaded);
			return KingdomLifecycleRules.DormantLifecycleWireExact(loaded)
				&& Same(saved, Bytes(w => KingdomLifecycleWireCodec.WriteLifecycle(w, loaded)))
				? null : "lifecycle book does not reload dormant and byte-stable";
		}

		private static string City(KingdomCityBook book)
		{
			if (book.SubsidenceReadFailed || !book.HasValidSubsidenceStorage())
				return "subsidence storage is not writable and readable";
			if (!KingdomNamedCookRules.Validate(book.NamedCook, out string failure)
				|| !KingdomAssentingMootRules.Validate(book.AssentingMoot, out failure)) return failure;
			// The engine reads the written fields into a new book through the production load path:
			// subsidence storage migration, then validated load normalization.
			StringBuilder saved = new StringBuilder(), reloaded = new StringBuilder();
			KingdomCityBook loaded = new KingdomCityBook();
			Walk(book, "book", 0, saved);
			loaded.ReadNamedState(() => CopyNamedFields(book, loaded));
			if ((Walk(loaded, "book", 0, reloaded) ?? (loaded.HasValidSubsidenceStorage() ? null : "invalid"))
				!= null || saved.ToString() != reloaded.ToString()) return "city book does not reload exactly";
			return Settled(book, x => ((KingdomCityBook)x).Normalize())
				?? (book.HasValidSubsidenceStorage() ? null : "normalized subsidence storage is unreadable");
		}

#if !TAF_CONSTRUCTION_INPUT_PORTABLE
		private static string Trade(KingdomTradeBook book)
		{
			byte[] saved = KingdomTradeCodec.EncodeEnvelope(book);
			KingdomTradeBook loaded = KingdomTradeCodec.DecodeEnvelopeRaw(saved);
			KingdomTradeRules.Normalize(loaded);
			return loaded.SchemaState == KingdomTradeSchemaState.Compatible
				&& Same(saved, KingdomTradeCodec.EncodeEnvelope(loaded)) ? null : "trade book does not reload exactly";
		}

		private static string Topology(KingdomSettlementTopology topology)
		{
			// An empty, canonical topology passes the writer's bound check and writes only its
			// marker, version and a zero count; the per-settlement loop never runs.
			return topology.Count == 0 && !topology.HasOpaqueEvidence
				&& topology.NormalizeCurrent(out string failure) ? null : "topology is not empty and canonical";
		}
#endif

		private static string Polity(KingdomPolityLedger ledger)
		{
			if (!KingdomPolityRules.TryValidate(ledger, out string failure)) return failure;
			byte[] saved = KingdomPolityCodec.EncodeEnvelope(ledger);
			KingdomPolityLedger loaded = KingdomPolityCodec.DecodeEnvelope(saved);
			return KingdomPolityRules.TryValidate(loaded, out failure)
				&& Same(saved, KingdomPolityCodec.EncodeEnvelope(loaded)) ? null : "polity ledger does not reload exactly";
		}

		private static string Experience(KingdomExperienceLedger ledger)
		{
			if (!KingdomExperienceRules.TryValidate(ledger, out string failure)) return failure;
			byte[] saved = KingdomExperienceCodec.EncodeEnvelope(ledger);
			KingdomExperienceLedger loaded = KingdomExperienceCodec.DecodeEnvelope(saved);
			return KingdomExperienceRules.TryValidate(loaded, out failure)
				&& Same(saved, KingdomExperienceCodec.EncodeEnvelope(loaded)) ? null : "experience does not reload exactly";
		}

		private static string Carry(KingdomCarryBook book)
		{
			byte[] saved = Bytes(w => KingdomLifecycleWireCodec.WriteCarry(w, book));
			KingdomCarryBook loaded = new KingdomCarryBook();
			using (BinaryReader reader = new BinaryReader(new MemoryStream(saved, false)))
				KingdomLifecycleWireCodec.ReadCarry(reader, loaded);
			KingdomLifecycleRules.Normalize(loaded);
			return !loaded.Quarantined && !loaded.WireRejected
				&& Same(saved, Bytes(w => KingdomLifecycleWireCodec.WriteCarry(w, loaded))) ? null : "carry does not reload exactly";
		}

		/// <summary>What SerializationReader.ReadNamedFields assigns (decompiled 2.0.211.56
		/// XRL/World/SerializationReader.cs:395-415): each field the named-field writer wrote, by
		/// name, read back into new objects.</summary>
		private static void CopyNamedFields(object source, object target)
		{
			foreach (FieldInfo field in source.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
				if (!field.IsLiteral && !Attribute.IsDefined(field, typeof(NonSerializedAttribute)))
					field.SetValue(target, Clone(field.GetValue(source)));
		}

		private static object Clone(object value)
		{
			if (value == null || value is string || value is Enum || value.GetType().IsPrimitive) return value;
			object copy = Activator.CreateInstance(value.GetType());
			if (value is IDictionary map)
				foreach (DictionaryEntry entry in map) ((IDictionary)copy).Add(Clone(entry.Key), Clone(entry.Value));
			else if (value is IList list)
				foreach (object item in list) ((IList)copy).Add(Clone(item));
			else CopyNamedFields(value, copy);
			return copy;
		}

		private static byte[] Bytes(Action<BinaryWriter> write)
		{
			using (MemoryStream stream = new MemoryStream())
			{
				write(new BinaryWriter(stream));
				return stream.ToArray();
			}
		}

		private static bool Same(byte[] left, byte[] right)
		{
			return Convert.ToBase64String(left) == Convert.ToBase64String(right);
		}
	}
}
#endif
