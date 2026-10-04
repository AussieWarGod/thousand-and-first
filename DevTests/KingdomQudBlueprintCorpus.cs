#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// An engine-free reading of an installed Caves of Qud Base directory's object blueprints, in
	/// the order and shape the engine's factory builds them (decompile 2.0.211.56). DataManager
	/// caches every Base *.xml whose root is "objects" and sorts by LoadPriority, then ordinal
	/// path (XRL/DataFile.cs CompareTo); ObjectBlueprintLoader.ReadObjectsNode keeps first-seen
	/// order and folds Load="Merge"; Bake applies Fill mixins, the parent, the other mixins, then
	/// the object's own nodes, dropping inherited "*noinherit" tags and applying named remove*
	/// nodes (XRL/World/Loaders/ObjectBlueprintLoader.cs); LoadBakedXML files a stag as
	/// "Semantic" + name and drops "*delete" values (XRL/World/GameObjectFactory.cs).
	/// GameObjectFactory.LoadBlueprints fills its Blueprints dictionary in that order, and every
	/// mod loads after every Base file. Not modelled, because no field read here depends on it:
	/// part-type resolution, attribute validation, compatibility renames and unnamed removals.
	/// </summary>
	internal sealed class KingdomQudBlueprintCorpus
	{
		private sealed class Collection
		{
			internal Dictionary<string, Dictionary<string, string>> Named;
			internal List<Dictionary<string, string>> Unnamed;

			internal void Add(Dictionary<string, string> Node)
			{
				string name = Node.TryGetValue("Name", out string found) ? found : null;
				if (string.IsNullOrEmpty(name)) { (Unnamed = Unnamed ?? new List<Dictionary<string, string>>()).Add(Node); return; }
				Named = Named ?? new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
				if (Named.TryGetValue(name, out var existing)) foreach (var pair in Node) existing[pair.Key] = pair.Value;
				else Named[name] = Node;
			}

			internal Collection Clone()
			{
				var copy = new Collection();
				if (Named != null)
					copy.Named = Named.ToDictionary(p => p.Key, p => new Dictionary<string, string>(p.Value), StringComparer.Ordinal);
				if (Unnamed != null) copy.Unnamed = Unnamed.Select(n => new Dictionary<string, string>(n)).ToList();
				return copy;
			}

			internal void Merge(Collection Other)
			{
				if (Other.Named != null)
				{
					if (Named == null) Named = Other.Named;
					else foreach (var pair in Other.Named)
						if (Named.TryGetValue(pair.Key, out var existing)) foreach (var a in pair.Value) existing[a.Key] = a.Value;
						else Add(pair.Value);
				}
				if (Other.Unnamed != null)
				{
					if (Unnamed == null) Unnamed = Other.Unnamed;
					else Unnamed.AddRange(Other.Unnamed);
				}
			}
		}

		private sealed class Data
		{
			internal string Name, Inherits, Load;
			internal readonly Dictionary<string, Collection> Children = new Dictionary<string, Collection>(StringComparer.Ordinal);
		}

		/// <summary>One factory entry as the mint's engine-free rules read it.</summary>
		internal sealed class Entry : KingdomCampHeartHighCraftRules.IBlueprint
		{
			internal KingdomQudBlueprintCorpus Corpus;
			internal Dictionary<string, Dictionary<string, string>> Parts;
			internal readonly HashSet<string> Marks = new HashSet<string>(StringComparer.Ordinal);
			internal string Inherits;
			public string Key { get; internal set; }
			public string Name => Key;
			public bool HasPart(string Part) => Parts.ContainsKey(Part);
			public string Text(string Part, string Parameter) =>
				Parts.TryGetValue(Part, out var p) && p.TryGetValue(Parameter, out string v) ? v : null;
			public bool? Flag(string Part, string Parameter) =>
				bool.TryParse(Text(Part, Parameter), out bool value) ? value : (bool?)null;
			public bool Marked(string Name) => Marks.Contains(Name);
			public bool DescendsFrom(string Root)
			{
				var seen = new HashSet<string>(StringComparer.Ordinal);
				for (string at = Key; !string.IsNullOrEmpty(at) && seen.Add(at);
					at = Corpus.Objects.TryGetValue(at, out Data d) ? d.Inherits : null)
					if (at == Root) return true;
				return false;
			}
		}

		private readonly Dictionary<string, Data> Objects = new Dictionary<string, Data>(StringComparer.Ordinal);
		private readonly Dictionary<string, Data> Finalized = new Dictionary<string, Data>(StringComparer.Ordinal);
		private readonly List<string> Order = new List<string>();
		/// <summary>Factory order. A null entry is one LoadBakedXML would fail to build.</summary>
		internal readonly List<Entry> Entries = new List<Entry>();
		internal readonly List<string> Files = new List<string>();

		internal static KingdomQudBlueprintCorpus Load(string BaseRoot)
		{
			var corpus = new KingdomQudBlueprintCorpus();
			var files = new List<Tuple<int, string, string>>();
			foreach (string path in Directory.GetFiles(BaseRoot, "*.xml", SearchOption.AllDirectories))
			{
				if (!RootIsObjects(path, out int priority)) continue;
				// The engine compares Windows full paths; only the part below Base can differ.
				files.Add(Tuple.Create(priority, Path.GetRelativePath(BaseRoot, path).Replace('/', '\\'), path));
			}
			files.Sort((a, b) => a.Item1 != b.Item1 ? b.Item1.CompareTo(a.Item1)
				: string.Compare(a.Item2, b.Item2, StringComparison.Ordinal));
			foreach (var file in files) { corpus.Files.Add(file.Item2); corpus.Read(file.Item3); }
			foreach (string name in corpus.Order) corpus.Entries.Add(corpus.Build(name));
			return corpus;
		}

		internal Entry Find(string Name) => Entries.FirstOrDefault(e => e != null && e.Key == Name);

		private static XmlReaderSettings Settings() => new XmlReaderSettings
		{
			// The engine reads with XmlTextReader, which accepts the control-character references
			// Qud ships (an AmmoChar of "&#15;", say); CheckCharacters=false does the same.
			CheckCharacters = false, IgnoreComments = true, IgnoreWhitespace = true,
			DtdProcessing = DtdProcessing.Ignore
		};

		private static bool RootIsObjects(string Path, out int Priority)
		{
			Priority = 0;
			using (XmlReader reader = XmlReader.Create(Path, Settings()))
				while (reader.Read())
					if (reader.NodeType == XmlNodeType.Element)
					{
						Priority = int.TryParse(reader.GetAttribute("LoadPriority"), out int p) ? p : 0;
						return string.Equals(reader.Name, "objects", StringComparison.OrdinalIgnoreCase);
					}
			return false;
		}

		private void Read(string Path)
		{
			using (XmlReader reader = XmlReader.Create(Path, Settings()))
				while (reader.Read())
				{
					if (reader.NodeType != XmlNodeType.Element || reader.Name != "object") continue;
					var data = new Data { Name = reader.GetAttribute("Name"),
						Inherits = reader.GetAttribute("Inherits"), Load = reader.GetAttribute("Load") };
					int depth = reader.Depth, mixins = 0;
					if (!reader.IsEmptyElement)
						while (reader.Read() && reader.Depth > depth)
						{
							if (reader.NodeType != XmlNodeType.Element || reader.Depth != depth + 1) continue;
							var node = new Dictionary<string, string>(StringComparer.Ordinal);
							for (bool more = reader.MoveToFirstAttribute(); more; more = reader.MoveToNextAttribute())
								node[reader.Name] = reader.Value;
							reader.MoveToElement();
							string kind = reader.Name;
							if (kind.Equals("mixin", StringComparison.OrdinalIgnoreCase) && !node.ContainsKey("Priority"))
								node["Priority"] = (mixins++).ToString();
							if (!data.Children.TryGetValue(kind, out Collection into)) data.Children[kind] = into = new Collection();
							into.Add(node);
						}
					if (data.Load == "Merge" || data.Load == "MergeIfExists")
					{
						if (data.Name != null && Objects.TryGetValue(data.Name, out Data existing)) Fold(existing, data);
						continue;
					}
					if (data.Name == null) continue;
					if (!Objects.ContainsKey(data.Name)) Order.Add(data.Name);
					Objects[data.Name] = data;
				}
		}

		private static void Fold(Data Into, Data Other)
		{
			if (!string.IsNullOrEmpty(Other.Inherits)) Into.Inherits = Other.Inherits;
			foreach (var child in Other.Children)
				if (Into.Children.TryGetValue(child.Key, out Collection existing)) existing.Merge(child.Value);
				else Into.Children[child.Key] = child.Value;
		}

		private Data Bake(Data Obj, List<string> Stack)
		{
			if (Finalized.TryGetValue(Obj.Name, out Data done)) return done;
			if (Stack.Contains(Obj.Name)) return new Data();
			Stack.Add(Obj.Name);
			var result = new Data { Name = Obj.Name, Inherits = Obj.Inherits };
			var mixins = new List<Tuple<int, string, string, string, bool>>();
			if (Obj.Children.TryGetValue("mixin", out Collection declared) && declared.Named != null)
				foreach (var pair in declared.Named)
					mixins.Add(Tuple.Create(int.TryParse(Get(pair.Value, "Priority"), out int p) ? p : 0, pair.Key,
						Get(pair.Value, "Include"), Get(pair.Value, "Exclude"), Get(pair.Value, "Load") == "Fill"));
			mixins.Sort((a, b) => a.Item1.CompareTo(b.Item1));
			foreach (var m in mixins) if (m.Item5) Inherit(m.Item2, result, m.Item3, m.Item4, Stack);
			if (!string.IsNullOrEmpty(Obj.Inherits)) Inherit(Obj.Inherits, result, null, null, Stack);
			foreach (var m in mixins) if (!m.Item5) Inherit(m.Item2, result, m.Item3, m.Item4, Stack);
			foreach (var child in Obj.Children)
				if (child.Key != "mixin") MergeInto(result, child.Key, child.Value.Clone());
			foreach (string kind in new[] { "builder", "intproperty", "mutation", "part", "property", "skill", "stag", "stat", "tag" })
				RemoveNamed(result, kind, "remove" + kind);
			Finalized.Add(Obj.Name, result);
			Stack.Remove(Obj.Name);
			return result;
		}

		private void Inherit(string Name, Data Result, string Include, string Exclude, List<string> Stack)
		{
			if (!Objects.TryGetValue(Name, out Data parent)) return;
			foreach (var child in Bake(parent, Stack).Children)
			{
				if ((!string.IsNullOrEmpty(Include) && !Delimited(Include, child.Key))
					|| (!string.IsNullOrEmpty(Exclude) && Delimited(Exclude, child.Key))) continue;
				MergeInto(Result, child.Key, child.Value.Clone());
			}
			if (Result.Children.TryGetValue("tag", out Collection tags) && tags.Named != null)
				foreach (string key in tags.Named.Where(t => t.Value.ContainsValue("*noinherit")).Select(t => t.Key).ToList())
					tags.Named.Remove(key);
		}

		private static void MergeInto(Data Result, string Key, Collection Value)
		{
			if (!Result.Children.TryGetValue(Key, out Collection existing)) { Result.Children[Key] = Value; return; }
			if (Key.StartsWith("xtag", StringComparison.Ordinal) && existing.Unnamed?.Count > 0 && Value.Unnamed?.Count > 0)
				foreach (var a in Value.Unnamed[0]) existing.Unnamed[0][a.Key] = a.Value;
			else existing.Merge(Value);
		}

		private static void RemoveNamed(Data Result, string Target, string Removal)
		{
			if (!Result.Children.TryGetValue(Removal, out Collection removal) || removal.Named == null
				|| !Result.Children.TryGetValue(Target, out Collection target) || target.Named == null) return;
			bool exhaustedAll = true;
			var exhausted = new List<string>();
			foreach (var pair in removal.Named)
			{
				target.Named.Remove(pair.Key);
				if (int.TryParse(Get(pair.Value, "Depth"), out int depth) && depth >= 1)
				{
					exhaustedAll = false;
					if (depth == 1) pair.Value.Remove("Depth"); else pair.Value["Depth"] = (depth - 1).ToString();
				}
				else exhausted.Add(pair.Key);
			}
			if (exhaustedAll) Result.Children.Remove(Removal);
			else foreach (string key in exhausted) removal.Named.Remove(key);
		}

		private Entry Build(string Name)
		{
			Data baked = Bake(Objects[Name], new List<string>());
			var entry = new Entry { Corpus = this, Key = Name, Inherits = baked.Inherits,
				Parts = baked.Children.TryGetValue("part", out Collection parts) && parts.Named != null
					? parts.Named : new Dictionary<string, Dictionary<string, string>>() };
			var tags = new HashSet<string>(StringComparer.Ordinal);
			foreach (string kind in new[] { "tag", "stag", "property", "intproperty" })
			{
				if (!baked.Children.TryGetValue(kind, out Collection nodes) || nodes.Named == null) continue;
				foreach (var pair in nodes.Named)
				{
					string value = Get(pair.Value, "Value");
					if (value != null && (value.Contains("{{{remove}}}") || value.Contains("*delete"))) continue;
					if (kind == "intproperty" && value != null && !int.TryParse(value, out int _)) return null;
					string mark = kind == "stag" ? "Semantic" + pair.Key : pair.Key;
					// GameObjectBlueprint.Tags.Add throws on a repeated name, failing the whole blueprint.
					if ((kind == "tag" || kind == "stag") && !tags.Add(mark)) return null;
					entry.Marks.Add(mark);
				}
			}
			return entry;
		}

		private static string Get(Dictionary<string, string> Node, string Key) =>
			Node != null && Node.TryGetValue(Key, out string value) ? value : null;

		/// <summary>Extensions.HasDelimitedSubstring: an exact comma-delimited token, untrimmed.</summary>
		private static bool Delimited(string Text, string Token) =>
			Text.Split(',').Any(part => string.Equals(part, Token, StringComparison.Ordinal));
	}
}
#endif
