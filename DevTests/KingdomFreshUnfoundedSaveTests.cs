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
	/// source. Every durable book a fresh KingdomSystem is constructed with is built exactly as its
	/// field initializer builds it and checked engine-free: codec books through their production
	/// codec and load validator, named-field books against an emulation of the engine's writer, and
	/// the empty settlement topologies against their write gate. The other save systems a new game
	/// creates (KingdomSeal, KingdomCivicMemorySystem, and the optional succession and inheritance
	/// systems) and state written by play before the first save are not covered here (#275).</summary>
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

		// Production Write(SerializationWriter) bodies, whitespace and comments removed ("..." marks
		// a prefix): the engine's named-field writer (emulated by Walk), or a writer this fixture
		// runs engine-free. KingdomSystem.Write itself runs only the three projections checked below.
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
				+ "\"Citysubsidencestoragedidnotfinishloading.\");" + Named },
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
				+ "settlements.Count;i++){..." },
			new[] { "Experience/KingdomCarryBook.cs", "KingdomCarryBook", "KingdomLifecycleWireCodec.WriteCarry(Writer,this);" },
			new[] { "Growth/KingdomResidentDepartureOperation.cs", "KingdomResidentDepartureOperation", Named },
			new[] { "Growth/KingdomResidentAdmissionOperation.cs", "KingdomResidentAdmissionOperation", Named },
			new[] { "Core/KingdomNamedCookReceipt.cs", "KingdomNamedCookReceipt", Named },
			new[] { "Core/KingdomAssentingMootReceipt.cs", "KingdomAssentingMootReceipt", Named }
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
				string body = Compact(WriteBody(row[0], row[1])), expected = row[2].Replace("{0}", row[1]);
				if (expected.EndsWith("...", StringComparison.Ordinal))
					StringAssert.StartsWith(expected.Substring(0, expected.Length - 3), body, row[1]);
				else ClassicAssert.AreEqual(expected, body, row[1] + " writer changed: review this fixture");
			}
			StringAssert.Contains("Away = SettlementTopology?.Get(0);",
				TestMain.ReadRepositoryText("Core/KingdomSystem.z08.SettlementTopology.cs"));
			StringAssert.Contains("ExiledAway = ExiledSettlementTopology?.Get(0);",
				TestMain.ReadRepositoryText("Core/KingdomSystem.z08.SettlementTopology.cs"));
			StringAssert.Contains("Manifest = KingdomTrade.LegacyManifestSnapshot(TradeBook?.Manifest);",
				TestMain.ReadRepositoryText("Core/KingdomSystem.z26.TradeNormalization.cs"));
			StringAssert.Contains("KingdomTradeManifestState Manifest)\n\t\t{\n\t\t\tif (Manifest == null) return null;",
				TestMain.ReadRepositoryText("Trade/KingdomTrade.cs"));
		}

		private const string Open = "namespace ThousandAndFirst\n{\n\tpublic partial class KingdomSystem\n\t{\n";
		private const string Probe = "public KingdomCarryBook ProbeBook = new KingdomCarryBook();";
		private const string Kept = "KingdomCarryBook ProbeBook new KingdomCarryBook()";

		// Synthetic KingdomSystem partials (name, source, parsed census rows joined by " | "). The
		// first row is the review probe on f17105cc: a one-line [NonSerialized] declaration used to
		// hide the next public field from the census. The control row keeps the two-line form.
		private static readonly string[][] ParserRows =
		{
			new[] { "one-line attributed declaration", Open + "\t\t[System.NonSerialized] private bool ProbeReadFailed;\n\n"
				+ "\t\t/// <summary>Probe.</summary>\n\t\t" + Probe + "\n\t}\n}\n", Kept },
			new[] { "attribute alone skips the next declaration", Open + "\t\t[System.NonSerialized]\n\n"
				+ "\t\t/// <summary>Probe.</summary>\n\t\t" + Probe + "\n\t}\n}\n", "" },
			new[] { "same-line declaration consumes a pending attribute", Open + "\t\t[NonSerialized]\n"
				+ "\t\t[Obsolete(\"x\")] private bool ProbeReadFailed;\n\t\t" + Probe + "\n\t}\n}\n", Kept },
			new[] { "attribute text is not an attribute name", Open + "\t\t[Obsolete(\"was NonSerialized\")]\n\t\t"
				+ Probe + "\n\t}\n}\n", Kept },
			new[] { "same-line attributed public member", Open + "\t\t[Obsolete(\"x\")] " + Probe + "\n\t}\n}\n",
				"UNPARSED [Obsolete(\"x\")] " + Probe },
			new[] { "unreadable attribute line", Open + "\t\t[Obsolete(\"x\",\n\t\t\tfalse)]\n\t\t" + Probe + "\n\t}\n}\n",
				"UNPARSED [Obsolete(\"x\", | " + Kept },
			new[] { "member outside the tab layout", Open + "        " + Probe + "\n\t}\n}\n", "UNPARSED " + Probe },
			new[] { "class declaration in another form", "namespace ThousandAndFirst\n{\n    public partial class "
				+ "KingdomSystem\n    {\n        " + Probe + "\n    }\n}\n", "UNPARSED public partial class KingdomSystem" },
			new[] { "field initialized by a lambda", Open + "\t\tpublic System.Func<int> ProbeHook = () => 1;\n"
				+ "\t\tpublic int ProbeCount => 1;\n\t}\n}\n", "System.Func<int> ProbeHook () => 1" }
		};

		[Test]
		public void FreshUnfoundedKingdomSystemCensusParserFailsClosed()
		{
			List<string> failures = new List<string>();
			foreach (string[] row in ParserRows)
			{
				List<string[]> fields = new List<string[]>();
				ParseSystemFields(row[1], fields);
				string parsed = string.Join(" | ", fields.ConvertAll(f => string.Join(" ", f).Trim()));
				if (parsed != row[2]) failures.Add(row[0] + ": expected <" + row[2] + "> but parsed <" + parsed + ">");
			}
			CollectionAssert.IsEmpty(failures, string.Join("\n", failures));
		}

		/// <summary>Serialized KingdomSystem fields (type, name, initializer) in declaration order:
		/// public instance fields that are not static, const or [NonSerialized], as
		/// SerializationWriter.WriteNamedFields selects them (decompiled 2.0.211.56
		/// XRL/World/SerializationWriter.cs:2981-3008), from every production partial.</summary>
		private static List<string[]> SystemFields()
		{
			List<string[]> fields = new List<string[]>();
			List<string> files = new List<string>();
			foreach (string file in Directory.GetFiles(TestMain.RepositoryRoot, "*.cs", SearchOption.AllDirectories))
			{
				string relative = file.Substring(TestMain.RepositoryRoot.Length + 1).Replace('\\', '/');
				if (!relative.StartsWith("DevTests/", StringComparison.Ordinal)
					&& !relative.StartsWith("Tools/", StringComparison.Ordinal)) files.Add(relative);
			}
			files.Sort(StringComparer.Ordinal);
			foreach (string file in files) ParseSystemFields(TestMain.ReadRepositoryText(file), fields);
			return fields;
		}

		/// <summary>Adds the serialized KingdomSystem fields one source file declares. It fails
		/// closed: a declaration it cannot classify, a public member outside the tab layout it reads,
		/// a KingdomSystem declaration in another form, or an attribute line it cannot read is added
		/// as UNPARSED, which breaks the census. Only an attribute section alone on its line that names
		/// NonSerialized skips a declaration (the next one, across blank and comment lines); an
		/// attribute that shares its line with a declaration applies to that declaration alone.</summary>
		private static void ParseSystemFields(string text, List<string[]> fields)
		{
			Regex field = new Regex(@"^public\s+(?:readonly\s+)?(?<type>[\w.]+(?:<[\w.,\s]+>)?)\s+(?<name>\w+)\s*(?:=\s*(?<init>.+))?;$");
			string[] lines = text.Replace("\r\n", "\n").Split('\n');
			bool inside = false, skip = false;
			for (int i = 0; i < lines.Length; i++)
			{
				string line = lines[i], code = line.Trim(), indent = line.Substring(0, line.Length - line.TrimStart().Length);
				if (!inside)
				{
					inside = Regex.IsMatch(line, @"^\t(public |internal )?(sealed )?partial class KingdomSystem\b");
					if (!inside && Regex.IsMatch(code, @"^[\w\s]*\bclass\s+KingdomSystem\b")) fields.Add(Unparsed(code));
					continue;
				}
				if (line == "\t}") { inside = false; continue; }
				bool trivia = code.Length == 0 || code.StartsWith("//", StringComparison.Ordinal);
				if (indent != "\t\t")
				{
					// Deeper tab-only lines are nested types and member bodies. A public member line at
					// any other indentation would escape the two-tab layout read below.
					if (Regex.IsMatch(code, @"^(\[.*\]\s*)?public\s") && !Regex.IsMatch(indent, "^\t{3,}$"))
						fields.Add(Unparsed(code));
					if (!trivia) skip = false;
					continue;
				}
				if (code.StartsWith("[", StringComparison.Ordinal))
				{
					Match attribute = Regex.Match(code, @"^(?<sections>(?:\[[^\[\]]*\]\s*)+)(?<rest>.*)$");
					string rest = attribute.Success ? attribute.Groups["rest"].Value : null;
					if (rest != null && (rest.Length == 0 || rest.StartsWith("//", StringComparison.Ordinal)))
						skip |= Regex.IsMatch(Regex.Replace(attribute.Groups["sections"].Value, "\"[^\"]*\"", "\"\""),
							@"[\[,]\s*(field\s*:\s*)?(global::)?(System\.)?NonSerialized(Attribute)?\s*[\](,]");
					else
					{
						// The declaration on this line consumes every pending attribute.
						if (rest == null || Regex.IsMatch(rest, @"\bpublic\b")) fields.Add(Unparsed(code));
						skip = false;
					}
					continue;
				}
				if (!Regex.IsMatch(code, @"^public\s"))
				{
					if (!trivia) skip = false;
					continue;
				}
				string declaration = Regex.Replace(code, @";\s*//.*$", ";");
				while (!declaration.EndsWith(";", StringComparison.Ordinal) && !declaration.Contains("{")
					&& !declaration.Contains("(")) declaration += " " + lines[++i].Trim();
				string head = declaration.Split('=')[0];
				// An expression-bodied member's first '=' opens its '=>'; a field initializer's does not.
				bool member = !head.Contains("(") && !head.Contains("{") && !Regex.IsMatch(declaration, "^[^=]*=>")
					&& !Regex.IsMatch(head, @"^public\s+(static|const|event|override|virtual|abstract|class|enum|struct|interface|delegate|sealed|partial)\b");
				Match match = field.Match(declaration);
				if (member && !skip) fields.Add(match.Success ? new[] { match.Groups["type"].Value,
					match.Groups["name"].Value, match.Groups["init"].Value } : Unparsed(declaration));
				skip = false;
			}
		}

		private static string[] Unparsed(string text) { return new[] { "UNPARSED " + text, "", "" }; }

		private static string WriteBody(string path, string type)
		{
			string[] lines = TestMain.ReadRepositoryText(path).Replace("\r\n", "\n").Split('\n');
			int i = Array.FindIndex(lines, line => Regex.IsMatch(line, @"\bclass " + type + @"\b"));
			while (i >= 0 && !Regex.IsMatch(lines[i], @"public (override )?void Write\(SerializationWriter Writer\)")) i++;
			string indent = lines[i].Substring(0, lines[i].Length - lines[i].TrimStart().Length);
			StringBuilder body = new StringBuilder();
			for (i += 2; lines[i] != indent + "}"; i++) body.Append(Regex.Replace(lines[i], "//.*$", "")).Append('\n');
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
			// residence and subsidence storage migration, then validated load normalization.
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
			// An empty topology writes only its marker, version and a zero count.
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
