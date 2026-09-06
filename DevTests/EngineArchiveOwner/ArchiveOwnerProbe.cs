using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.Loader;
using System.Security.Cryptography;

// Real supplied mod types and native Binding.Intact; no game, SDK, or substitute owner logic.
// Controlled hashers isolate the owner gate. This is not a live callback or wire-format proof.
internal static class ArchiveOwnerProbe
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static readonly string[] Names = { "ExileChronicle", "ExileAbility", "ReturnChronicle", "ReturnReputation", "ReturnFeelings", "ReturnSeat", "ReturnAbility" };
    private static readonly string[] Operations = { "TryCaptureIntent", "TryProveAttempting", "TrySettle", "TryVerifySettled", "TryProveIntentGraph" };
    private static readonly string[] FieldNames = { "Phase", "Disposition", "Scope", "BeforeGraph", "AfterGraph", "BeforeArchiveGraph", "AfterArchiveGraph", "BeforeEffect", "AfterEffect", "ObservedEffect", "BeforeStamp", "AfterStamp", "IntentSettlementSchema", "SettledSettlementSchema" };
    private static string Managed, ModDirectory;
    private static Type Archive, Realm, Receipt, Runtime, Binding, OwnerType, HasherType, Scope, Disposition;
    private static MethodInfo Intact;
    private static PropertyInfo Game;
    private static FieldInfo[] ReceiptFields;
    private static int Current, Passed, Failed;

    private static int Main(string[] args)
    {
        if (args.Length != 3) { Console.WriteLine("BLOCKED usage: MOD_DLL INSTALLED_MANAGED_DIRECTORY EXPECTED_MOD_SHA256"); return 2; }
        try
        {
            string mod = Path.GetFullPath(args[0]); Managed = Path.GetFullPath(args[1]); ModDirectory = Path.GetDirectoryName(mod);
            Check(File.Exists(mod) && Directory.Exists(Managed), "missing mod or Managed directory");
            Check(args[2].Length == 64 && args[2].All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')), "noncanonical expected hash");
            Check(Hash(mod) == args[2], "supplied mod hash differs");
            AssemblyLoadContext.Default.Resolving += Resolve;
            try
            {
                int result = Run(mod);
                Check(Hash(mod) == args[2], "mod changed during probe");
                return result;
            }
            finally { AssemblyLoadContext.Default.Resolving -= Resolve; }
        }
        catch (Exception error) { Console.WriteLine("BLOCKED setup-or-final-check " + Safe(error)); return 2; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Run(string mod)
    {
        Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(mod);
        Archive = ModType(assembly, "KingdomRealmArchive"); Realm = ModType(assembly, "KingdomSystem");
        Receipt = ModType(assembly, "KingdomRealmCallbackReceipt"); Runtime = ModType(assembly, "KingdomRealmHashBasisRuntime");
        Scope = ModType(assembly, "KingdomRealmCallbackScope"); Disposition = ModType(assembly, "KingdomRealmCallbackDisposition");
        Binding = Runtime.GetNestedType("Binding", BindingFlags.NonPublic);
        OwnerType = Runtime.GetNestedType("OwnerProof", BindingFlags.NonPublic);
        HasherType = Runtime.GetNestedType("VersionedHasher", BindingFlags.NonPublic);
        Check(Binding != null && OwnerType != null && HasherType != null, "native Binding or runtime delegates absent");
        Intact = Binding.GetMethod("Intact", All, null, Type.EmptyTypes, null);
        Check(Intact != null && Intact.ReturnType == typeof(bool), "unexpected Intact contract");
        Current = (int)ModType(assembly, "KingdomArchivedSettlementCodec").GetField("CurrentVersion", All).GetRawConstantValue();
        Check(Current == 19, "fixture expects current19 and historical18");
        Assembly engine = Archive.GetInterfaces().Single(t => t.FullName == "XRL.World.IComposite").Assembly;
        Game = engine.GetType("XRL.The", true).GetProperty("Game");
        NoGame();
        ReceiptFields = Receipt.GetFields(BindingFlags.Instance | BindingFlags.Public).OrderBy(f => f.Name, StringComparer.Ordinal).ToArray();
        Check(ReceiptFields.Select(f => f.Name).SequenceEqual(FieldNames.OrderBy(n => n, StringComparer.Ordinal)), "receipt field inventory changed");
        Check(ReceiptFields.All(f => f.FieldType == typeof(string) || f.FieldType == typeof(int) || f.FieldType.IsEnum), "unknown receipt field type");
        Console.WriteLine("mod-sha256=" + Hash(mod) + " engine-sha256=" + Hash(engine.Location));
        Console.WriteLine("scope=actual-native-Binding-owner-and-real-runtime-publication-gates; hashers=controlled; game=false; callback=false; serialization=false");
        Case("exact-published-root-owned", () => OwnerOnly("owned"));
        Case("null-published-root-refused", () => OwnerOnly("null"));
        Case("same-receipts-foreign-published-root-refused", () => OwnerOnly("foreign"));
        Case("null-realm-refused", NullRealm);
        foreach (string name in Names) { string receipt = name; Case("replaced-receipt-refused-" + receipt, () => ReplacedReceipt(receipt)); }
        foreach (string name in Operations)
        {
            string operation = name;
            Case(operation + "-exact-owner-control", () => Exercise(operation, "owned", "none"));
            foreach (string state in new[] { "null", "foreign" })
                foreach (string cut in new[] { "before", "graph", "authority" })
                {
                    string root = state, boundary = cut;
                    Case(operation + "-" + root + "-root-" + boundary, () => Exercise(operation, root, boundary));
                }
        }
        NoGame();
        Check(Passed + Failed == 46, "fixture count changed");
        Console.WriteLine("cases=46 passed=" + Passed + " failed=" + Failed);
        return Failed == 0 ? 0 : 1;
    }

    private sealed class Hasher
    {
        internal readonly bool Authority;
        internal Action Interfere;
        internal int Asked;
        internal Hasher(bool authority) { Authority = authority; }
        internal string At(int version)
        {
            return version == 18 ? new string(Authority ? 'b' : 'a', 64) :
                new string(Authority ? 'd' : 'c', 62) + version.ToString("x2");
        }
        public bool Compute(int version, out string hash, out string failure)
        {
            Asked++;
            Action action = Interfere; Interfere = null; if (action != null) action();
            hash = At(version); failure = null; return true;
        }
    }

    private sealed class Fixture
    {
        internal readonly object ArchiveObject, RealmObject, Active, Foreign, Bound;
        internal readonly object[] Receipts;
        internal readonly Delegate Owner, GraphDelegate, AuthorityDelegate;
        internal readonly Hasher Graph = new Hasher(false), Authority = new Hasher(true);
        internal Fixture(string operation)
        {
            ArchiveObject = Activator.CreateInstance(Archive);
            RealmObject = RuntimeHelpers.GetUninitializedObject(Realm);
            Foreign = Activator.CreateInstance(Archive);
            Receipts = Names.Select(name => Get(ArchiveObject, name)).ToArray();
            for (int i = 0; i < Names.Length; i++) Set(Foreign, Names[i], Receipts[i]);
            Active = Receipts[6];
            if (operation != "TryCaptureIntent")
            {
                SetEnum(Active, "Phase", operation == "TryVerifySettled" ? "Settled" : operation == "TrySettle" ? "Attempting" : "Intent");
                SetEnum(Active, "Scope", "Chronicle"); Set(Active, "BeforeEffect", "frozen-before"); Set(Active, "AfterEffect", "frozen-after");
                Set(Active, "BeforeGraph", Graph.At(18)); Set(Active, "BeforeArchiveGraph", Authority.At(18));
                Set(Active, "IntentSettlementSchema", operation == "TrySettle" ? 18 : 0);
                if (operation == "TryVerifySettled")
                {
                    SetEnum(Active, "Disposition", "Delivered"); Set(Active, "ObservedEffect", "observed");
                    Set(Active, "AfterGraph", Graph.At(Current)); Set(Active, "AfterArchiveGraph", Authority.At(18));
                }
            }
            Check((bool)Call(Receipt.GetMethod("Validate"), Active), "invalid fixture receipt");
            Root("owned");
            Bound = Activator.CreateInstance(Binding, All, null, new[] { ArchiveObject, RealmObject, Active, Enum.Parse(Scope, "Chronicle") }, null);
            Owner = Delegate.CreateDelegate(OwnerType, Bound, Intact);
            MethodInfo compute = typeof(Hasher).GetMethod("Compute");
            GraphDelegate = Delegate.CreateDelegate(HasherType, Graph, compute);
            AuthorityDelegate = Delegate.CreateDelegate(HasherType, Authority, compute);
        }
        internal void Root(string state) { Set(RealmObject, "ExiledRealmArchive", state == "owned" ? ArchiveObject : state == "foreign" ? Foreign : null); }
        internal bool IsIntact() { return (bool)Call(Intact, Bound); }
    }

    private static void OwnerOnly(string state)
    {
        Fixture fixture = new Fixture("TryCaptureIntent"); object[][] before = Snapshot(fixture.Receipts);
        fixture.Root(state);
        Check(fixture.IsIntact() == (state == "owned"), "owner predicate accepted detached root");
        Same(fixture.Receipts, before); RootStill(fixture, state);
    }
    private static void NullRealm()
    {
        Fixture fixture = new Fixture("TryCaptureIntent"); object[][] before = Snapshot(fixture.Receipts);
        object bound = Activator.CreateInstance(Binding, All, null, new[] { fixture.ArchiveObject, null, fixture.Active, Enum.Parse(Scope, "Chronicle") }, null);
        Check(!(bool)Call(Intact, bound), "null realm accepted"); Same(fixture.Receipts, before);
    }
    private static void ReplacedReceipt(string name)
    {
        Fixture fixture = new Fixture("TryCaptureIntent"); object[][] before = Snapshot(fixture.Receipts);
        object replacement = Activator.CreateInstance(Receipt); Set(fixture.ArchiveObject, name, replacement);
        Check(!fixture.IsIntact(), "replaced receipt accepted");
        Check(ReferenceEquals(Get(fixture.ArchiveObject, name), replacement), "replacement rewritten");
        Same(fixture.Receipts, before); RootStill(fixture, "owned");
    }

    private static void Exercise(string operation, string state, string cut)
    {
        Fixture fixture = new Fixture(operation); object[][] before = Snapshot(fixture.Receipts);
        Check(fixture.IsIntact(), "healthy owner control failed");
        if (cut == "before") fixture.Root(state);
        else if (cut == "graph") fixture.Graph.Interfere = () => fixture.Root(state);
        else if (cut == "authority") fixture.Authority.Interfere = () => fixture.Root(state);
        object[] arguments;
        if (operation == "TryCaptureIntent") arguments = new object[] { fixture.Active, fixture.Owner, Enum.Parse(Scope, "Chronicle"), "frozen-before", "frozen-after", int.MinValue, int.MinValue, fixture.GraphDelegate, fixture.AuthorityDelegate, null };
        else if (operation == "TrySettle") arguments = new object[] { fixture.Active, fixture.Owner, Enum.Parse(Disposition, "Delivered"), "observed", fixture.GraphDelegate, fixture.AuthorityDelegate, null };
        else arguments = new object[] { fixture.Active, fixture.Owner, fixture.GraphDelegate, fixture.AuthorityDelegate, null };
        bool result = (bool)Call(Runtime.GetMethods(All).Single(method => method.Name == operation), null, arguments);
        string failure = (string)arguments[arguments.Length - 1];
        Check(fixture.Authority.Asked > 0, "owner case did not reach its representable authority hasher");
        if (operation != "TrySettle" || cut == "graph" || state == "owned") Check(fixture.Graph.Asked > 0, "expected graph proof was not reached");
        Check(fixture.Graph.Interfere == null && fixture.Authority.Interfere == null, "planned mutation did not execute");
        if (state != "owned")
        {
            Check(!result, "detached-root runtime acceptance");
            Check(failure == (string)Runtime.GetField("MutatedFailure", All).GetRawConstantValue(), "refusal was not the owner/snapshot guard");
            Same(fixture.Receipts, before); Check(!fixture.IsIntact(), "detached owner became intact");
        }
        else
        {
            Check(result && failure == null, "healthy owner was refused");
            Check(fixture.IsIntact() && (bool)Call(Receipt.GetMethod("Validate"), fixture.Active), "invalid positive poststate");
            if (operation == "TryCaptureIntent") Check((int)Get(fixture.Active, "IntentSettlementSchema") == Current && (int)Get(fixture.Active, "SettledSettlementSchema") == 0, "fresh basis changed");
            else
            {
                Check((string)Get(fixture.Active, "BeforeGraph") == fixture.Graph.At(18) && (string)Get(fixture.Active, "BeforeArchiveGraph") == fixture.Authority.At(18), "historical hashes rewritten");
                if (operation == "TryProveAttempting") Check((int)Get(fixture.Active, "IntentSettlementSchema") == 18, "legacy basis not uniquely pinned");
                else if (operation == "TrySettle") Check((int)Get(fixture.Active, "IntentSettlementSchema") == 18 && (int)Get(fixture.Active, "SettledSettlementSchema") == Current && (string)Get(fixture.Active, "AfterArchiveGraph") == fixture.Authority.At(18), "mixed basis settlement changed");
                else Same(fixture.Receipts, before);
            }
            for (int i = 0; i < 6; i++) Same(new[] { fixture.Receipts[i] }, new[] { before[i] });
        }
        RootStill(fixture, state);
        for (int i = 0; i < Names.Length; i++) Check(ReferenceEquals(Get(fixture.ArchiveObject, Names[i]), fixture.Receipts[i]) && ReferenceEquals(Get(fixture.Foreign, Names[i]), fixture.Receipts[i]), "receipt owner reference changed");
    }

    private static void RootStill(Fixture fixture, string state)
    {
        object expected = state == "owned" ? fixture.ArchiveObject : state == "foreign" ? fixture.Foreign : null;
        Check(ReferenceEquals(Get(fixture.RealmObject, "ExiledRealmArchive"), expected), "runtime rewrote published root");
    }
    private static object[][] Snapshot(object[] receipts) { return receipts.Select(value => ReceiptFields.Select(field => field.GetValue(value)).ToArray()).ToArray(); }
    private static void Same(object[] receipts, object[][] before)
    {
        for (int i = 0; i < receipts.Length; i++)
            for (int j = 0; j < ReceiptFields.Length; j++)
                Check(Equals(before[i][j], ReceiptFields[j].GetValue(receipts[i])), "receipt field changed: " + ReceiptFields[j].Name);
    }
    private static object Get(object target, string name) { return target.GetType().GetField(name, All).GetValue(target); }
    private static void Set(object target, string name, object value) { target.GetType().GetField(name, All).SetValue(target, value); }
    private static void SetEnum(object target, string name, string value) { Set(target, name, Enum.Parse(target.GetType().GetField(name, All).FieldType, value)); }
    private static Type ModType(Assembly assembly, string name)
    {
        Type type = assembly.GetType("ThousandAndFirst." + name, true);
        Check(ReferenceEquals(type.Assembly, assembly), "foreign supplied type"); return type;
    }
    private static object Call(MethodInfo method, object target, params object[] arguments)
    {
        try { return method.Invoke(target, arguments); }
        catch (TargetInvocationException error) when (error.InnerException != null) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    private static void NoGame() { Check(Game != null && Game.GetValue(null) == null, "unexpected live game"); }
    private static void Case(string name, Action action)
    {
        try { NoGame(); action(); NoGame(); Passed++; Console.WriteLine("PASS " + name); }
        catch (Exception error) { Failed++; Console.WriteLine("FAIL " + name + " " + Safe(error)); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static string Safe(Exception error)
    {
        string type = error.GetType().Name;
        return error is InvalidOperationException && error.Message.Length <= 160 && error.Message.All(c => c >= ' ' && c <= '~') ? type + ": " + error.Message : type;
    }
    private static string Hash(string path)
    {
        using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
    private static Assembly Resolve(AssemblyLoadContext context, AssemblyName name)
    {
        if (name.Name == null || name.Name.IndexOfAny(new[] { '/', '\\' }) >= 0) return null;
        foreach (string directory in new[] { Managed, ModDirectory })
        {
            string candidate = Path.Combine(directory, name.Name + ".dll");
            if (File.Exists(candidate)) return context.LoadFromAssemblyPath(candidate);
        }
        return null;
    }
}
