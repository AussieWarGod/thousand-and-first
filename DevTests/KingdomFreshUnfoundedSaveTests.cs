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
	/// needs the engine, so its serialized field census and each book's writer are pinned from
	/// source. Every durable book a fresh new game holds is built exactly as its field initializer
	/// builds it and passed through its production writer and load validator engine-free.</summary>
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
			ClassicAssert.AreEqual(names.Count, new HashSet<string>(names).Count, "field names are unique");
			CollectionAssert.AreEqual(Census, composites,
				"a KingdomSystem durable field changed: extend FreshUnfoundedKingdomSystemEveryDurableBookIsWritable");
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

		/// <summary>Serialized KingdomSystem fields (type, name, initializer) in declaration order:
		/// public instance fields that are not static, const or [NonSerialized], as
		/// SerializationWriter.WriteNamedFields selects them (decompiled 2.0.211.56
		/// XRL/World/SerializationWriter.cs:2981-3008), from every production partial. Members are
		/// found by brace depth, whatever their indentation; a declaration this parser cannot read,
		/// a constructor, or unbalanced braces becomes an UNPARSED row, which fails the census.</summary>
		private static List<string[]> SystemFields()
		{
			List<string[]> fields = new List<string[]>();
			Regex field = new Regex(@"^public\s+(?:readonly\s+)?(?<type>[\w.]+(?:<[\w.,\s]+>)?)\s+(?<name>\w+)\s*(?:=\s*(?<init>.+))?;$");
			foreach (string file in ProductionFiles())
			{
				string[] lines = Lines(file), code = CodeOnly(lines);
				bool[] inside = new bool[lines.Length], member = new bool[lines.Length];
				int depth = 0, body = -1;
				bool open = false, system = false;
				for (int i = 0; i < lines.Length; i++)
				{
					inside[i] = body > 0;
					member[i] = body > 0 && depth == body;
					if (Regex.IsMatch(code[i], @"\bclass\s+KingdomSystem\b")) open = system = true;
					foreach (char c in code[i])
					{
						if (c == '{')
						{
							depth++;
							if (open) { body = depth; open = false; }
						}
						else if (c == '}' && --depth < body) body = -1;
					}
				}
				if (system && (depth != 0 || open || body != -1))
					fields.Add(new[] { "UNPARSED braces in " + file, "", "" });
				bool skip = false;
				for (int i = 0; i < lines.Length; i++)
				{
					string text = code[i].Trim();
					// Conventional member indentation inside a nested block means the depth count is off.
					if (inside[i] && !member[i] && lines[i].StartsWith("\t\tpublic ", StringComparison.Ordinal))
						fields.Add(new[] { "UNPARSED " + file + ":" + (i + 1) + " " + text, "", "" });
					if (!member[i] || text.Length == 0) continue;
					if (text.StartsWith("[", StringComparison.Ordinal))
					{
						skip |= Regex.IsMatch(text, @"\bNonSerialized(Attribute)?\b");
						if (Regex.IsMatch(text, @"\bpublic\b")) fields.Add(new[] { "UNPARSED " + text, "", "" });
						continue;
					}
					// A constructor would change the fresh state the census reads from initializers.
					bool constructor = Regex.IsMatch(text, @"(?<!\bnew\s+)\bKingdomSystem\s*\(");
					if (constructor || !text.StartsWith("public ", StringComparison.Ordinal))
					{
						if (constructor || Regex.IsMatch(text, @"\bpublic\b")) fields.Add(new[] { "UNPARSED " + text, "", "" });
						skip = false;
						continue;
					}
					string declaration = Regex.Replace(lines[i].Trim(), @";\s*//.*$", ";");
					while (!declaration.EndsWith(";", StringComparison.Ordinal) && !declaration.Contains("{")
						&& !declaration.Contains("(") && i + 1 < lines.Length) declaration += " " + lines[++i].Trim();
					string head = declaration.Split('=')[0];
					// "=>" right after the name is an expression-bodied member; a field may hold a lambda.
					bool arrow = declaration.Length > head.Length + 1 && declaration[head.Length + 1] == '>';
					bool instance = !head.Contains("(") && !head.Contains("{") && !arrow
						&& !Regex.IsMatch(head, @"^public\s+(static|const|event|override|virtual|abstract|class|enum|struct|interface|delegate|sealed|partial)\b");
					Match match = field.Match(declaration);
					if (instance && !skip) fields.Add(match.Success ? new[] { match.Groups["type"].Value,
						match.Groups["name"].Value, match.Groups["init"].Value } : new[] { "UNPARSED " + declaration, "", "" });
					skip = false;
				}
			}
			return fields;
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

		/// <summary>Each line with comments removed and string and character literals blanked, so
		/// braces and keywords are read from code only.</summary>
		private static string[] CodeOnly(string[] lines)
		{
			string[] code = new string[lines.Length];
			bool block = false, verbatim = false;
			for (int n = 0; n < lines.Length; n++)
			{
				StringBuilder kept = new StringBuilder();
				string line = lines[n];
				for (int i = 0; i < line.Length; i++)
				{
					char c = line[i], next = i + 1 < line.Length ? line[i + 1] : '\0';
					if (block) { if (c == '*' && next == '/') { block = false; i++; } continue; }
					if (verbatim) { if (c == '"' && next == '"') i++; else if (c == '"') verbatim = false; continue; }
					if (c == '/' && next == '/') break;
					if (c == '/' && next == '*') { block = true; i++; continue; }
					if (c == '@' && next == '"') { verbatim = true; i++; kept.Append(' '); continue; }
					if (c == '"' || c == '\'')
					{
						for (i++; i < line.Length && line[i] != c; i++) if (line[i] == '\\') i++;
						kept.Append(' ');
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
			// A book with its own writer is exercised only at top level, by its validator.
			if (writer[2] != Named) return depth == 0 ? null : path + " nests a book with its own writer";
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
