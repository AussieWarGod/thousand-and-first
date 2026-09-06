using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.Loader;

internal static class WearReadProbe
{
	private sealed class CheckFailure : Exception
	{
		internal CheckFailure(string reason) : base(reason) { }
	}
	private static readonly List<string> LoaderErrors = new List<string>();
	private static string Managed, ModDirectory, Observed;
	private static Type Wear;
	private static MethodInfo Read, ReadError;
	private static FieldInfo Quarantined;
	private static FieldInfo[] ReceiptFields;
	private static int Passed, Failed, Blocked;

	private static int Main(string[] args)
	{
		if (args.Length != 2)
		{
			Console.WriteLine("BLOCKED usage: MOD_DLL INSTALLED_MANAGED_DIRECTORY"); return 2;
		}
		try
		{
			string mod = Path.GetFullPath(args[0]);
			Managed = Path.GetFullPath(args[1]); ModDirectory = Path.GetDirectoryName(mod);
			if (!File.Exists(mod) || !Directory.Exists(Managed)
				|| !string.Equals(Path.GetExtension(mod), ".dll", StringComparison.OrdinalIgnoreCase))
				throw new CheckFailure("exact mod DLL or Managed directory is unavailable");
			AssemblyLoadContext.Default.Resolving += Resolve;
			AppDomain.CurrentDomain.FirstChanceException += WatchLoaderFailure;
			try { return Run(mod); }
			finally
			{
				AppDomain.CurrentDomain.FirstChanceException -= WatchLoaderFailure;
				AssemblyLoadContext.Default.Resolving -= Resolve;
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("BLOCKED setup " + Describe(ex)); return 2;
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static int Run(string mod)
	{
		Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(mod);
		Wear = assembly.GetType("XRL.World.Parts.r_KingdomWear", true, false);
		Check(ReferenceEquals(Wear.Assembly, assembly), "wear type is not from the supplied DLL");
		Assembly engine = Wear.BaseType.Assembly;
		Type body = engine.GetType("XRL.World.GameObject", true, false);
		Type reader = engine.GetType("XRL.World.SerializationReader", true, false);
		Read = Wear.GetMethod("Read", BindingFlags.Public | BindingFlags.Instance, null,
			new[] { body, reader }, null);
		ReadError = Wear.GetMethod("ReadError", BindingFlags.Public | BindingFlags.Instance, null,
			new[] { typeof(Exception), reader, typeof(long), typeof(int) }, null);
		Check(Read != null && Read.ReturnType == typeof(void), "real public Read signature missing");
		Check(ReadError != null && ReadError.ReturnType == typeof(bool), "real public ReadError signature missing");
		Quarantined = Wear.GetField("LifecycleQuarantined", BindingFlags.Public | BindingFlags.Instance);
		Check(Quarantined != null && Quarantined.FieldType == typeof(bool), "quarantine field missing");
		ReceiptFields = Wear.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
			.Where(field => field.Name != "LifecycleQuarantined")
			.OrderBy(field => field.Name, StringComparer.Ordinal).ToArray();
		Check(ReceiptFields.Length > 0, "no receipt fields found");
		if (LoaderCount() != 0) throw new CheckFailure("dependency initialization failed before cases");
		Console.WriteLine("scope=real-part-reflection; game=false; framed-IPart.Load=false; wire-roundtrip=false; visible-message-proof=false");
		Console.WriteLine("receipt-fields=" + ReceiptFields.Length + "; ReadError-declaring-type=" + ReadError.DeclaringType.FullName);
		Case("null-read-original-exception-and-quarantine", () =>
		{
			object part = Fresh(out object[] before);
			Exception error = ReadThrows(part);
			Check(error.GetType() == typeof(NullReferenceException), "Read did not preserve the original null-reader exception");
			Check((bool)Quarantined.GetValue(part), "Read failure did not quarantine the real part");
			CheckLatch(part, true); Unchanged(part, before);
		});
		Case("pre-read-error-hook-refuses-and-quarantines", () =>
		{
			object part = Fresh(out object[] before);
			object result = Call(ReadError, part, new InvalidDataException("fixture pre-Read failure"), null, 0L, 0);
			Check(result is bool && !(bool)result, "ReadError did not return the engine skip result false");
			Check((bool)Quarantined.GetValue(part), "pre-Read hook did not quarantine the real part");
			CheckLatch(part, true); Unchanged(part, before);
		});
		Case("repeat-read-keeps-private-failure-after-public-reset", () =>
		{
			object part = Fresh(out object[] before);
			Exception first = ReadThrows(part);
			Check(first.GetType() == typeof(NullReferenceException), "initial Read did not reach the null reader");
			Quarantined.SetValue(part, false); // Only the public latch is reset, never private failure authority.
			Exception second = ReadThrows(part);
			Check(second.GetType() == typeof(InvalidOperationException), "repeat Read reached the reader instead of refusing sticky failure");
			Check((bool)Quarantined.GetValue(part), "repeat failure left public quarantine clear");
			CheckLatch(part, true); Unchanged(part, before);
		});
		Case("repeat-read-error-hook-preserves-evidence", () =>
		{
			object part = Fresh(out object[] before);
			Exception supplied = new InvalidDataException("fixture repeated diagnostic hook");
			for (int i = 0; i < 2; i++)
			{
				object result = Call(ReadError, part, supplied, null, 0L, 0);
				Check(result is bool && !(bool)result, "repeated ReadError did not return false");
				Check((bool)Quarantined.GetValue(part), "repeated ReadError did not retain quarantine");
				CheckLatch(part, true); Unchanged(part, before);
			}
		});
		Console.WriteLine("cases=4 passed=" + Passed + " failed=" + Failed + " blocked=" + Blocked);
		Console.WriteLine("AfterGameLoadedEvent-not-invoked: its message path requires native game state; no visible-message proof.");
		return Blocked != 0 ? 2 : Failed != 0 ? 1 : 0;
	}

	private static object Fresh(out object[] before)
	{
		object part = Activator.CreateInstance(Wear);
		Check(part != null, "real wear constructor returned null");
		CheckLatch(part, false);
		Quarantined.SetValue(part, false);
		for (int i = 0; i < ReceiptFields.Length; i++)
		{
			FieldInfo field = ReceiptFields[i]; object value;
			if (field.FieldType == typeof(string)) value = "probe-held:" + field.Name;
			else if (field.FieldType == typeof(int)) value = 101 + i;
			else if (field.FieldType == typeof(long)) value = 900001L + i;
			else if (field.FieldType == typeof(bool)) value = true;
			else throw new CheckFailure("uncovered real receipt field " + field.Name);
			Check(!field.IsInitOnly, "receipt field became readonly: " + field.Name);
			field.SetValue(part, value);
		}
		// These are distinctive surviving raw values, not a claim of a valid serialized receipt.
		before = ReceiptFields.Select(field => field.GetValue(part)).ToArray(); return part;
	}

	private static void Unchanged(object part, object[] before)
	{
		for (int i = 0; i < ReceiptFields.Length; i++)
			Check(Equals(before[i], ReceiptFields[i].GetValue(part)), "receipt field changed: " + ReceiptFields[i].Name);
	}

	private static void CheckLatch(object part, bool expected)
	{
		PropertyInfo property = Wear.GetProperty("LoadFailed", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
		FieldInfo field = Wear.GetField("WearReadFailed", BindingFlags.NonPublic | BindingFlags.Instance);
		if (property != null)
		{
			Check(property.PropertyType == typeof(bool), "LoadFailed is not a bool");
			Check((bool)property.GetValue(part) == expected, "LoadFailed disagrees with sticky failure");
		}
		if (field != null) Check(field.FieldType == typeof(bool) && (bool)field.GetValue(part) == expected,
			"private read-failure latch disagrees");
		// Old DLLs lack these members; their actual behavior, not missing metadata, fails the cases.
	}

	private static Exception ReadThrows(object part)
	{
		try { Call(Read, part, null, null); }
		catch (Exception ex)
		{
			Observed += " read-exception=" + ex.GetType().FullName;
			if (IsLoaderFailure(ex)) throw;
			return ex;
		}
		throw new CheckFailure("Read(null,null) returned without an exception");
	}

	private static object Call(MethodInfo method, object target, params object[] args)
	{
		try { return method.Invoke(target, args); }
		catch (TargetInvocationException ex) when (ex.InnerException != null)
		{
			ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw;
		}
	}

	private static void Case(string id, Action action)
	{
		Observed = ""; int prior = LoaderCount(); Exception failure = null;
		try { action(); } catch (Exception ex) { failure = ex; }
		if (LoaderCount() != prior || failure != null && !(failure is CheckFailure))
		{
			Blocked++; Console.WriteLine("BLOCKED " + id + " " + (failure == null ? "dependency failure caught by production" : Describe(failure)) + Observed);
			lock (LoaderErrors) foreach (string detail in LoaderErrors.Skip(prior).Distinct()) Console.WriteLine("loader=" + detail);
		}
		else if (failure != null) { Failed++; Console.WriteLine("FAIL " + id + " " + Describe(failure) + Observed); }
		else { Passed++; Console.WriteLine("PASS " + id + Observed); }
	}

	private static Assembly Resolve(AssemblyLoadContext context, AssemblyName requested)
	{
		string name = requested.Name;
		if (string.IsNullOrEmpty(name) || name.Any(c => !char.IsLetterOrDigit(c) && c != '.' && c != '_' && c != '-'))
		{ NoteLoader("invalid dependency name"); return null; }
		foreach (string root in new[] { Managed, ModDirectory })
		{
			string candidate = Path.Combine(root, name + ".dll");
			if (!File.Exists(candidate)) continue;
			AssemblyName actual = AssemblyName.GetAssemblyName(candidate);
			if (actual.Name != name || actual.Version != requested.Version
				|| !string.Equals(actual.CultureName ?? "", requested.CultureName ?? "", StringComparison.Ordinal)
				|| !(actual.GetPublicKeyToken() ?? Array.Empty<byte>()).SequenceEqual(requested.GetPublicKeyToken() ?? Array.Empty<byte>()))
			{ NoteLoader("dependency identity mismatch: " + name); return null; }
			return context.LoadFromAssemblyPath(candidate);
		}
		NoteLoader("unresolved dependency: " + name); return null;
	}

	private static bool IsLoaderFailure(Exception ex)
	{
		return ex is FileNotFoundException || ex is FileLoadException || ex is BadImageFormatException
			|| ex is TypeLoadException || ex is TypeInitializationException || ex is ReflectionTypeLoadException
			|| ex is DllNotFoundException || ex is EntryPointNotFoundException || ex is MissingMethodException;
	}
	private static void WatchLoaderFailure(object sender, FirstChanceExceptionEventArgs args)
	{
		if (IsLoaderFailure(args.Exception)) NoteLoader(args.Exception.GetType().FullName);
	}
	private static void NoteLoader(string detail) { lock (LoaderErrors) LoaderErrors.Add(detail); }
	private static int LoaderCount() { lock (LoaderErrors) return LoaderErrors.Count; }
	private static void Check(bool condition, string reason) { if (!condition) throw new CheckFailure(reason); }
	private static string Describe(Exception ex)
	{
		return ex.GetType().FullName + (ex is CheckFailure ? ": " + ex.Message : "")
			+ (ex.InnerException == null ? "" : " inner=" + ex.InnerException.GetType().FullName);
	}
}
