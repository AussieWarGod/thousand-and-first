using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.Loader;

internal static class WearWireProbe
{
	private sealed class CheckFailure : Exception { internal CheckFailure(string message) : base(message) { } }
	private sealed class Wire { internal byte[] Bytes; internal long Start, End; }
	private static readonly List<string> LoaderErrors = new List<string>();
	private static readonly string[] Legacy = { "Wear", "LastCause", "Held", "RepairEffortLeft", "LastLeakTick", "LeakAnnounced", "AnnouncedBlock" };
	private static readonly object[] LegacyValues = { 31, 1, true, 7, 123456789L, true, 2 };
	private static string Managed, ModDirectory, Observed;
	private static Type Wear, Cache, Writer, Reader, ExpectedException;
	private static MethodInfo PartRead, PartWrite;
	private static FieldInfo[] Fields;
	private static Exception OriginalException;
	private static int SaveVersion, Passed, Failed, Blocked;

	private static int Main(string[] args)
	{
		if (args.Length != 2) { Console.WriteLine("BLOCKED usage: MOD_DLL INSTALLED_MANAGED_DIRECTORY"); return 2; }
		try
		{
			string mod = Path.GetFullPath(args[0]); Managed = Path.GetFullPath(args[1]); ModDirectory = Path.GetDirectoryName(mod);
			Check(File.Exists(mod) && Directory.Exists(Managed) && string.Equals(Path.GetExtension(mod), ".dll", StringComparison.OrdinalIgnoreCase), "mod DLL or Managed directory unavailable");
			AssemblyLoadContext.Default.Resolving += Resolve; AppDomain.CurrentDomain.FirstChanceException += Watch;
			try { return Run(mod); }
			finally { AppDomain.CurrentDomain.FirstChanceException -= Watch; AssemblyLoadContext.Default.Resolving -= Resolve; }
		}
		catch (Exception error)
		{
			Console.WriteLine("BLOCKED setup " + Describe(error));
			lock (LoaderErrors) foreach (string detail in LoaderErrors.Distinct()) Console.WriteLine("loader=" + detail);
			return 2;
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static int Run(string mod)
	{
		Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(mod);
		Wear = assembly.GetType("XRL.World.Parts.r_KingdomWear", true); Check(ReferenceEquals(Wear.Assembly, assembly), "foreign wear type");
		Assembly engine = Wear.BaseType.Assembly;
		Cache = engine.GetType("XRL.Serialization.FastSerialization+Cache", true);
		Writer = engine.GetType("XRL.World.SerializationWriter", true); Reader = engine.GetType("XRL.World.SerializationReader", true);
		Type body = engine.GetType("XRL.World.GameObject", true);
		Check(engine.GetType("XRL.The", true).GetProperty("Game").GetValue(null) == null, "probe unexpectedly has a live game");
		PartRead = Wear.GetMethod("Read", new[] { body, Reader }); PartWrite = Wear.GetMethod("Write", new[] { body, Writer });
		Check(PartRead != null && PartWrite != null, "real part Read/Write signatures missing");
		SaveVersion = (int)engine.GetType("XRL.XRLGame", true).GetField("SaveVersion").GetRawConstantValue();
		Fields = Wear.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).OrderBy(f => f.Name, StringComparer.Ordinal).ToArray();
		Check(Fields.Length > 7 && CountErrors() == 0, "receipt fields or dependency initialization unavailable");
		Console.WriteLine("scope=real-direct-part-wire; game=false; framed-IPart.Load=false; historical-save=false; visible-message-proof=false; save-version=" + SaveVersion);
		Case("named-current-v1-roundtrip", () => Healthy(false));
		Case("legacy-seven-position-roundtrip", () => Healthy(true));
		Case("unsupported-version-preserves-original-exception", () => Rejected(VersionWire(99), false, typeof(InvalidOperationException), false, true));
		Case("wrong-version-type-preserves-original-exception", () => Rejected(VersionWire("not-a-version"), false, typeof(InvalidOperationException), false, true));
		Case("named-field-type-failure-retains-assigned-prefix", () => Rejected(BadFieldWire(), false, typeof(ArgumentException), true, true));
		Case("named-payload-truncated-last-byte", () => Rejected(Encode(Fixture(true), false), true, typeof(EndOfStreamException), false, false));
		Case("legacy-payload-truncated-last-byte", () => Rejected(Encode(Fixture(false), true), true, typeof(EndOfStreamException), false, false));
		Console.WriteLine("cases=7 passed=" + Passed + " failed=" + Failed + " blocked=" + Blocked);
		Console.WriteLine("Truncations remove the payload's final byte after real token-table initialization; no whole-file admission or FinalizeRead proof.");
		return Blocked != 0 ? 2 : Failed != 0 ? 1 : 0;
	}

	private static void Healthy(bool legacy)
	{
		object expected = Fixture(!legacy); Wire wire = Encode(expected, legacy); object actual = Activator.CreateInstance(Wear);
		WithReader(wire, false, (reader, stream) =>
		{
			if (!legacy)
			{
				Check(Equals(Invoke(reader, "ReadObject"), 1415009618) && Equals(Invoke(reader, "ReadObject"), 1), "named magic/version changed");
				stream.Position = wire.Start;
			}
			Call(PartRead, actual, null, reader); Check(stream.Position == wire.End, "part did not consume exact payload");
			Same(actual, Snapshot(expected), false); Check(!Get<bool>(actual, "LifecycleQuarantined"), "healthy read quarantined"); Latch(actual, false);
			Invoke(reader, "ReadGameObjects"); Invoke(reader, "ReadEventRegistries");
		});
		Check(!ReferenceEquals(expected, actual), "read reused fixture part");
	}

	private static void Rejected(Wire wire, bool truncate, Type exceptionType, bool assignedPrefix, bool compareBefore)
	{
		object part = Fixture(true); object[] before = Snapshot(part);
		WithReader(wire, truncate, (reader, stream) =>
		{
			Exception thrown = ReadThrows(part, reader, exceptionType);
			Check(thrown.GetType() == exceptionType && ReferenceEquals(thrown, OriginalException), "original engine/part exception was replaced");
		});
		Check(Get<bool>(part, "LifecycleQuarantined"), "failed wire read did not quarantine"); Latch(part, true);
		if (compareBefore)
		{
			if (assignedPrefix) before[Array.FindIndex(Fields, f => f.Name == "Wear")] = 27;
			Same(part, before, true);
		}
		object[] held = Snapshot(part); Set(part, "LifecycleQuarantined", false);
		WithReader(Encode(Fixture(true), false), false, (reader, stream) =>
		{
			long start = stream.Position; Exception retry = ReadThrows(part, reader, typeof(InvalidOperationException));
			Check(retry.GetType() == typeof(InvalidOperationException) && ReferenceEquals(retry, OriginalException), "sticky refusal was replaced");
			Check(stream.Position == start, "sticky failure consumed a healthy replacement wire");
		});
		Check(Get<bool>(part, "LifecycleQuarantined"), "retry cleared quarantine"); Latch(part, true); Same(part, held, true);
	}

	private static object Fixture(bool named)
	{
		object part = Activator.CreateInstance(Wear);
		for (int i = 0; i < Legacy.Length; i++) Set(part, Legacy[i], LegacyValues[i]);
		Set(part, "LeakClockInitialized", true);
		if (!named) return part;
		foreach (FieldInfo field in Fields)
			if (field.FieldType == typeof(string)) field.SetValue(part, "wire:" + field.Name + ":\u00e9\u03a9");
		Set(part, "QuarantineTold", true); Set(part, "QuarantineLedgerState", 3); Set(part, "QuarantineMessageState", 4);
		Set(part, "IncidentPhase", 1); Set(part, "IncidentCause", 1); Set(part, "IncidentBeforeWear", 20); Set(part, "IncidentAfterWear", 31); Set(part, "IncidentMessageState", 1);
		Set(part, "LeakPhase", 1); Set(part, "LeakKind", 1); Set(part, "LeakFromTick", 100L); Set(part, "LeakToTick", 200L);
		Set(part, "LeakBefore", 80); Set(part, "LeakAfter", 70); Set(part, "LeakWanted", 10); Set(part, "LeakActualLost", 10);
		Set(part, "LeakCellX", 4); Set(part, "LeakCellY", 5); Set(part, "LeakCapacity", 100); Set(part, "LeakLedgerState", 1); Set(part, "LeakMessageState", 2);
		return part;
	}
	private static Wire Encode(object part, bool legacy)
	{
		return WriteWire(writer =>
		{
			if (legacy) foreach (string name in Legacy) ObjectWrite(writer, Field(name).GetValue(part));
			else Call(PartWrite, part, null, writer);
		});
	}
	private static Wire VersionWire(object version) { return WriteWire(writer => { ObjectWrite(writer, 1415009618); ObjectWrite(writer, version); }); }
	private static Wire BadFieldWire()
	{
		return WriteWire(writer =>
		{
			ObjectWrite(writer, 1415009618); ObjectWrite(writer, 1); Invoke(writer, "WriteOptimized", new[] { typeof(int) }, new object[] { 2 });
			Invoke(writer, "WriteOptimized", new[] { typeof(string) }, new object[] { "Wear" }); ObjectWrite(writer, 27);
			Invoke(writer, "WriteOptimized", new[] { typeof(string) }, new object[] { "LastCause" }); ObjectWrite(writer, "not-an-integer");
		});
	}
	private static void ObjectWrite(object writer, object value) { Invoke(writer, "WriteObject", new[] { typeof(object) }, new[] { value }); }
	private static Wire WriteWire(Action<object> payload)
	{
		object cache = Activator.CreateInstance(Cache, new object[] { 0 }); MemoryStream stream = Get<MemoryStream>(cache, "MemoryStream");
		using (stream)
		using (IDisposable writer = (IDisposable)Activator.CreateInstance(Writer, new[] { cache }))
		{
			Invoke(writer, "Start", new[] { typeof(int), typeof(bool) }, new object[] { SaveVersion, true });
			object rack = Get<object>(cache, "GameObjects"); var playerRack = (System.Collections.IList)rack;
			Check(playerRack.Count == 1 && playerRack[0] == null, "writer seeded a real player or unexpected rack");
			Invoke(rack, "Clear"); // Only this private writer's proved null-player slot.
			Wire wire = new Wire { Start = stream.Position }; payload(writer); wire.End = stream.Position;
			Empty(cache, false); Invoke(writer, "FinalizeWrite"); Empty(cache, false); wire.Bytes = stream.ToArray();
			Check(wire.Start >= 68 && wire.End > wire.Start && wire.End < wire.Bytes.Length && wire.Bytes.Length <= 1048576, "unbounded/empty wire"); return wire;
		}
	}
	private static void WithReader(Wire wire, bool truncate, Action<object, MemoryStream> action)
	{
		object cache = Activator.CreateInstance(Cache, new object[] { 0 }); MemoryStream stream = Get<MemoryStream>(cache, "MemoryStream");
		using (stream)
		using (IDisposable reader = (IDisposable)Activator.CreateInstance(Reader, new[] { cache }))
		{
			stream.Write(wire.Bytes, 0, wire.Bytes.Length); stream.Position = 0;
			Invoke(reader, "Start", new[] { typeof(bool) }, new object[] { true });
			Check(Get<int>(reader, "FileVersion") == SaveVersion && stream.Position == wire.Start, "real reader did not reach payload start"); Empty(cache, true);
			if (truncate) stream.SetLength(wire.End - 1);
			action(reader, stream); Check(Get<int>(reader, "Errors") == 0, "engine reader recorded an error"); Empty(cache, true);
			// FinalizeRead clears global load bindings; these direct primitive receipts have none.
		}
	}
	private static void Empty(object cache, bool reading)
	{
		foreach (string name in new[] { "GameObjects", "GameObjectReferences", "EventRegistries", "Tokenized", "Objects", "Types" })
		{
			object rack = Get<object>(cache, name); Check((int)rack.GetType().GetProperty("Count").GetValue(rack) == 0, "unexpected rack entries: " + name);
			if (reading) Check((int)rack.GetType().GetProperty("Capacity").GetValue(rack) == 0, "unexpected allocated rack: " + name);
		}
	}
	private static Exception ReadThrows(object part, object reader, Type expected)
	{
		ExpectedException = expected; OriginalException = null;
		try { Call(PartRead, part, null, reader); }
		catch (Exception error) { Observed += " read-exception=" + error.GetType().FullName; if (LoaderFailure(error)) throw; return error; }
		finally { ExpectedException = null; }
		throw new CheckFailure("malformed/repeated Read returned without an exception");
	}
	private static object[] Snapshot(object part) { return Fields.Select(f => f.GetValue(part)).ToArray(); }
	private static void Same(object part, object[] expected, bool ignoreQuarantine)
	{
		for (int i = 0; i < Fields.Length; i++) if (!ignoreQuarantine || Fields[i].Name != "LifecycleQuarantined")
			Check(Equals(expected[i], Fields[i].GetValue(part)), "receipt field changed: " + Fields[i].Name);
	}
	private static void Latch(object part, bool expected)
	{
		PropertyInfo property = Wear.GetProperty("LoadFailed", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
		if (property != null) Check(property.PropertyType == typeof(bool) && (bool)property.GetValue(part) == expected, "LoadFailed disagrees");
		FieldInfo field = Wear.GetField("WearReadFailed", BindingFlags.NonPublic | BindingFlags.Instance);
		if (field != null) Check(field.FieldType == typeof(bool) && (bool)field.GetValue(part) == expected, "private failure latch disagrees");
	}
	private static FieldInfo Field(string name) { return Wear.GetField(name) ?? throw new CheckFailure("missing receipt field: " + name); }
	private static void Set(object target, string name, object value) { Field(name).SetValue(target, value); }
	private static T Get<T>(object target, string name) { return (T)target.GetType().GetField(name).GetValue(target); }
	private static object Invoke(object target, string name) { return Invoke(target, name, Type.EmptyTypes, Array.Empty<object>()); }
	private static object Invoke(object target, string name, Type[] types, object[] args) { return Call(target.GetType().GetMethod(name, types) ?? throw new CheckFailure("missing engine method: " + name), target, args); }
	private static object Call(MethodInfo method, object target, params object[] args)
	{
		try { return method.Invoke(target, args); }
		catch (TargetInvocationException error) when (error.InnerException != null) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
	}
	private static void Case(string id, Action action)
	{
		Observed = ""; int prior = CountErrors(); Exception failure = null;
		try { action(); } catch (Exception error) { failure = error; }
		if (CountErrors() != prior || failure != null && !(failure is CheckFailure))
		{
			Blocked++; Console.WriteLine("BLOCKED " + id + " " + (failure == null ? "production-caught dependency failure" : Describe(failure)) + Observed);
			lock (LoaderErrors) foreach (string detail in LoaderErrors.Skip(prior).Distinct()) Console.WriteLine("loader=" + detail);
		}
		else if (failure != null) { Failed++; Console.WriteLine("FAIL " + id + " " + Describe(failure) + Observed); }
		else { Passed++; Console.WriteLine("PASS " + id + Observed); }
	}
	private static Assembly Resolve(AssemblyLoadContext context, AssemblyName requested)
	{
		string name = requested.Name;
		if (string.IsNullOrEmpty(name) || name.Any(c => !char.IsLetterOrDigit(c) && c != '.' && c != '_' && c != '-')) { Note("invalid dependency name"); return null; }
		foreach (string root in new[] { Managed, ModDirectory })
		{
			string path = Path.Combine(root, name + ".dll"); if (!File.Exists(path)) continue; AssemblyName actual = AssemblyName.GetAssemblyName(path);
			if (actual.Name != name || actual.Version != requested.Version || !string.Equals(actual.CultureName ?? "", requested.CultureName ?? "", StringComparison.Ordinal)
				|| !(actual.GetPublicKeyToken() ?? Array.Empty<byte>()).SequenceEqual(requested.GetPublicKeyToken() ?? Array.Empty<byte>())) { Note("dependency identity mismatch: " + name); return null; }
			return context.LoadFromAssemblyPath(path);
		}
		Note("unresolved dependency: " + name); return null;
	}
	private static bool LoaderFailure(Exception error) { return error is FileNotFoundException || error is FileLoadException || error is BadImageFormatException || error is TypeLoadException || error is TypeInitializationException || error is ReflectionTypeLoadException || error is DllNotFoundException || error is EntryPointNotFoundException || error is MissingMethodException; }
	private static void Watch(object sender, FirstChanceExceptionEventArgs args)
	{
		if (LoaderFailure(args.Exception)) Note(args.Exception.GetType().FullName);
		if (ExpectedException != null && OriginalException == null && args.Exception.GetType() == ExpectedException) OriginalException = args.Exception;
	}
	private static void Note(string detail) { lock (LoaderErrors) LoaderErrors.Add(detail); }
	private static int CountErrors() { lock (LoaderErrors) return LoaderErrors.Count; }
	private static void Check(bool condition, string message) { if (!condition) throw new CheckFailure(message); }
	private static string Describe(Exception error) { return error.GetType().FullName + (error is CheckFailure ? ": " + error.Message : "") + (error.InnerException == null ? "" : " inner=" + error.InnerException.GetType().FullName); }
}
