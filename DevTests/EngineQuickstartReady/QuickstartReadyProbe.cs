using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.Loader;

// Real installed-engine component calls over synthetic minimal native objects, not a boot fixture.
internal static class QuickstartReadyProbe
{
    private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private sealed class Failure : Exception { internal Failure(string text) : base(text) { } }
    private static string Managed, ModDirectory, Stage;
    private static Assembly Engine, Mod;
    private static Type BodyType, ZoneType, Rules;
    private static MethodInfo Ready, FounderReady, CellAt;
    private static FieldInfo CoreSlot;
    private static int Passed, Failed, Blocked, LoaderFailures;

    private static int Main(string[] args)
    {
        if (args.Length != 2) { Console.WriteLine("BLOCKED usage: MOD_DLL INSTALLED_MANAGED_DIRECTORY"); return 2; }
        int result = 2;
        try
        {
            string path = Path.GetFullPath(args[0]); Managed = Path.GetFullPath(args[1]);
            ModDirectory = Path.GetDirectoryName(path);
            if (!File.Exists(path) || !Directory.Exists(Managed) || Path.GetExtension(path) != ".dll")
                throw new Failure("supplied mod DLL or Managed directory is unavailable");
            AssemblyLoadContext.Default.Resolving += Resolve;
            AppDomain.CurrentDomain.FirstChanceException += Observe;
            result = Run(path);
        }
        catch (Exception ex) { Console.WriteLine("BLOCKED setup stage=" + Stage + " " + Describe(ex)); }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= Observe;
            AssemblyLoadContext.Default.Resolving -= Resolve;
        }
        return result;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Run(string path)
    {
        Stage = "load-exact-assemblies";
        Mod = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        Type camp = Mod.GetType("XRL.World.ZoneBuilders.KingdomQuickstartCampBuilder", true);
        Check(ReferenceEquals(camp.Assembly, Mod), "camp type is not from supplied DLL");
        Ready = camp.GetMethod("Ready", Static);
        Check(Ready != null && Ready.ReturnType == typeof(bool), "real Ready is unavailable");
        ZoneType = Ready.GetParameters().Single().ParameterType; Engine = ZoneType.Assembly;
        BodyType = Engine.GetType("XRL.World.GameObject", true);
        FounderReady = camp.GetMethod("ReadyForFounder", Static, null, new[] { ZoneType, BodyType }, null);
        if (FounderReady != null) Check(FounderReady.ReturnType == typeof(bool), "founder Ready signature changed");
        Rules = Mod.GetType("ThousandAndFirst.KingdomQuickstartRules", true);
        CoreSlot = Engine.GetType("XRL.Core.XRLCore", true).GetField("Core", Static);
        Check(CoreSlot != null && CoreSlot.GetValue(null) == null, "probe requires no pre-existing engine Core");
        CellAt = ZoneType.GetMethod("GetCell", Instance, null, new[] { typeof(int), typeof(int) }, null);
        Check(CellAt != null, "real Zone.GetCell is unavailable");
        Console.WriteLine("scope=installed-engine-readiness; synthetic-context=true; factory=false; placement=false; boot=false; grants=false; save=false");
        Console.WriteLine("founder-target=" + (FounderReady == null ? "Ready (historical actual call)" : "ReadyForFounder"));
        foreach (string profile in new[] { "marsh", "canyon", "dunes" })
        {
            Case(profile, "prepared-empty", false, true, f => { });
            Case(profile, "prepared-bare-floor", false, true, f => f.Add("floor", 40, 12));
            Case(profile, "exact-nonsolid-founder", true, true, f => f.Founder(false));
            Case(profile, "exact-solid-founder", true, true, f => f.Founder(true));
            Case(profile, "founder-and-bare-floor", true, true, f => { f.Founder(false); f.Add("floor", 40, 12); });
            foreach (string kind in new[] { "combat", "creature", "solid", "terrain", "liquid" })
                Case(profile, "foreign-" + kind, true, false, f => { f.Founder(false); f.Add(kind, 40, 12); });
            Case(profile, "foreign-solid-apron", true, false, f => { f.Founder(false); f.Add("solid", 41, 12); });
            Case(profile, "duplicate-founder-reference", true, false, f =>
            { f.Founder(false); ((IList)Get(f.Start, "Objects")).Add(f.PlayerBody); });
            Case(profile, "founder-wrong-cell", true, false, f => f.Founder(false, 41));
            Case(profile, "foreign-player-reference", true, false, f =>
            { f.Founder(false); Set(f.Player, "_Body", f.Add("creature", 41, 12)); });
            Case(profile, "inactive-zone", true, false, f => { f.Founder(false); Set(f.Manager, "ActiveZone", null); });
        }
        Check(CoreSlot.GetValue(null) == null, "probe left engine Core installed");
        Console.WriteLine("cases=45 passed=" + Passed + " failed=" + Failed + " blocked=" + Blocked);
        Console.WriteLine("not-covered=GAMESTARTING,world-reservation-effects,founding,grant-delivery,liquid-founder,save-load,arbitrary-callbacks");
        return Blocked != 0 ? 2 : Failed != 0 ? 1 : Passed == 45 ? 0 : 2;
    }

    private static void Case(string profile, string id, bool founder, bool expected, Action<Fixture> arrange)
    {
        string label = profile + "/" + id; Exception problem = null; bool setup = true;
        object previous = CoreSlot.GetValue(null); int loaderBefore = LoaderFailures;
        Fixture fixture = null;
        try
        {
            Stage = label + "/context"; fixture = new Fixture(profile); arrange(fixture);
            Stage = label + "/snapshot"; Snapshot before = fixture.Capture(); setup = false;
            Stage = label + "/actual-readiness";
            bool actual;
            try
            {
                actual = (bool)(founder && FounderReady != null
                    ? Invoke(FounderReady, null, fixture.Zone, fixture.PlayerBody)
                    : Invoke(Ready, null, fixture.Zone));
            }
            finally { before.Check(); }
            Check(actual == expected, "expected=" + expected + " actual=" + actual);
        }
        catch (Exception ex) { problem = ex; }
        finally
        {
            try
            {
                Check(fixture == null || ReferenceEquals(CoreSlot.GetValue(null), fixture.Core), "engine Core changed during case");
                CoreSlot.SetValue(null, previous);
                Check(ReferenceEquals(CoreSlot.GetValue(null), previous), "engine Core restoration failed");
            }
            catch (Exception ex) { problem = ex; setup = true; }
        }
        if (LoaderFailures != loaderBefore || problem != null && (setup || !(problem is Failure)))
        { Blocked++; Console.WriteLine("BLOCKED " + label + " stage=" + Stage + " " + Describe(problem)); }
        else if (problem != null) { Failed++; Console.WriteLine("FAIL " + label + " " + Describe(problem)); }
        else { Passed++; Console.WriteLine("PASS " + label); }
    }

    private sealed class Fixture
    {
        internal readonly object Core, Game, Player, Manager, Zone, Start;
        internal object PlayerBody;
        private readonly List<object> Owned = new List<object>();
        internal Fixture(string profile)
        {
            Core = Raw("XRL.Core.XRLCore"); Game = Raw("XRL.XRLGame");
            Player = Raw("XRL.World.GamePlayer"); Manager = Raw("XRL.World.ZoneManager");
            Set(Core, "Game", Game); Set(Game, "Player", Player); Set(Game, "ZoneManager", Manager);
            foreach (string name in new[] { "StringGameState", "IntGameState", "Int64GameState", "ObjectGameState", "BooleanGameState" })
                Set(Game, name, Activator.CreateInstance(Field(Game.GetType(), name).FieldType));
            object[] values = { profile, null };
            Check((bool)Invoke(Rules.GetMethod("TryProfile", Static), null, values), "real profile lookup failed");
            object selected = values[1];
            IDictionary strings = (IDictionary)Get(Game, "StringGameState");
            strings[Constant("ProfileState")] = profile; strings["GameMode"] = Constant("ModeId");
            strings[Constant("WorldReservationState")] = Invoke(Rules.GetMethod("WorldReservation", Static), null, selected);
            ((IDictionary)Get(Game, "BooleanGameState"))["r_TAF_KingdomMode"] = true;
            CoreSlot.SetValue(null, Core);
            Stage += "/Zone-constructor";
            Zone = Activator.CreateInstance(ZoneType, new object[] { 80, 25 }); Owned.Add(Zone);
            Set(Zone, "_ZoneID", Get(selected, "ZoneId")); Set(Manager, "ActiveZone", Zone);
            Start = Cell(40, 12);
            for (int y = 0; y < 25; y++) for (int x = 0; x < 80; x++) Owned.Add(Cell(x, y));
        }
        internal object Cell(int x, int y) { return Invoke(CellAt, Zone, x, y); }
        internal void Founder(bool solid, int x = 40)
        {
            PlayerBody = Add("founder", x, 12);
            Set(Get(PlayerBody, "Physics"), "Flags", solid ? 5 : 4);
            Set(Player, "_Body", PlayerBody);
        }
        internal object Add(string kind, int x, int y)
        {
            Stage += "/body-" + kind;
            object body = Raw("XRL.World.GameObject"); object physics = Raw("XRL.World.Parts.Physics");
            Set(body, "_BaseID", Owned.Count + 1); Set(body, "Live", true);
            Set(body, "Property", new Dictionary<string, string>()); Set(body, "IntProperty", new Dictionary<string, int>());
            Set(body, "Statistics", Activator.CreateInstance(Field(BodyType, "Statistics").FieldType));
            Set(body, "PartsList", Activator.CreateInstance(Engine.GetType("XRL.World.PartRack", true), new object[] { 4 }));
            object blueprint = Activator.CreateInstance(Engine.GetType("XRL.World.GameObjectBlueprint", true)); Owned.Add(blueprint);
            Set(blueprint, "Name", "ReadyProbe" + kind); Set(blueprint, "Inherits", "Floor");
            Set(body, "Blueprint", Get(blueprint, "Name")); Set(body, "_BlueprintCache", blueprint);
            Part(body, physics, "Physics"); Set(physics, "Flags", kind == "solid" ? 5 : 4);
            if (kind == "founder" || kind == "combat")
            { Set(body, "Flags", 2); Part(body, Raw("XRL.World.Parts.Brain"), "Brain"); }
            if (kind == "founder" || kind == "creature") ((IDictionary)Get(body, "Property"))["Creature"] = "true";
            if (kind == "terrain")
            { Set(blueprint, "Inherits", ""); ((IDictionary)Get(blueprint, "Tags"))["Tree"] = "true"; }
            if (kind == "liquid")
            {
                object liquid = Raw("XRL.World.Parts.LiquidVolume");
                Set(liquid, "MaxVolume", -1); Set(liquid, "Volume", 1000); Part(body, liquid, "LiquidVolume");
            }
            object cell = Cell(x, y); Set(physics, "_CurrentCell", cell);
            ((IList)Get(cell, "Objects")).Add(body);
            Check((bool)Invoke(BodyType.GetMethod("Validate", Static, null, new[] { BodyType }, null), null, body), "synthetic real body is invalid");
            return body;
        }
        private object Raw(string name)
        {
            Stage += "/allocate-" + name;
            object value = RuntimeHelpers.GetUninitializedObject(Engine.GetType(name, true)); Owned.Add(value); return value;
        }
        private static void Part(object body, object part, string name)
        {
            Set(part, "_ParentObject", body); Set(part, "_Name", name); Set(body, name, part);
            ((IList)Get(body, "PartsList")).Add(part);
        }
        internal Snapshot Capture()
        {
            Snapshot result = new Snapshot();
            foreach (object value in Owned) result.Fields(value);
            return result;
        }
    }

    // Field/reference, array, rack and dictionary snapshots: no game-object property getters or serialization.
    private sealed class Snapshot
    {
        private readonly List<Action> Checks = new List<Action>();
        private readonly HashSet<object> Containers = new HashSet<object>(ReferenceEqualityComparer.Instance);
        internal void Fields(object owner)
        {
            for (Type type = owner.GetType(); type != null; type = type.BaseType)
                foreach (FieldInfo field in type.GetFields(Instance | BindingFlags.DeclaredOnly))
                {
                    object before = field.GetValue(owner);
                    Checks.Add(() => QuickstartReadyProbe.Check(Same(before, field.GetValue(owner)), "field changed: " + field.DeclaringType.Name + "." + field.Name));
                    Container(before);
                }
        }
        private void Container(object value)
        {
            if (value == null || !Containers.Add(value)) return;
            IDictionary dictionary = value as IDictionary;
            if (dictionary != null)
            {
                List<DictionaryEntry> entries = new List<DictionaryEntry>();
                IDictionaryEnumerator cursor = dictionary.GetEnumerator();
                while (cursor.MoveNext()) entries.Add(cursor.Entry);
                Checks.Add(() =>
                {
                    QuickstartReadyProbe.Check(dictionary.Count == entries.Count, "dictionary count changed");
                    foreach (DictionaryEntry entry in entries)
                        QuickstartReadyProbe.Check(dictionary.Contains(entry.Key) && Same(entry.Value, dictionary[entry.Key]), "dictionary entry changed");
                });
                return;
            }
            if (!(value is Array) && !(value is IList)) return;
            object[] items = ((IEnumerable)value).Cast<object>().ToArray();
            Checks.Add(() =>
            {
                object[] current = ((IEnumerable)value).Cast<object>().ToArray();
                QuickstartReadyProbe.Check(items.Length == current.Length, "array/rack length changed");
                for (int i = 0; i < items.Length; i++) QuickstartReadyProbe.Check(Same(items[i], current[i]), "array/rack entry changed");
            });
            foreach (object item in items) if (item is Array) Container(item);
        }
        internal void Check() { foreach (Action check in Checks) check(); }
        private static bool Same(object a, object b)
        { return ReferenceEquals(a, b) || a != null && (a.GetType().IsValueType || a is string) && a.Equals(b); }
    }

    private static FieldInfo Field(Type type, string name)
    {
        for (; type != null; type = type.BaseType)
        { FieldInfo field = type.GetField(name, Instance | BindingFlags.DeclaredOnly); if (field != null) return field; }
        throw new MissingFieldException(name);
    }
    private static object Get(object owner, string name) { return Field(owner.GetType(), name).GetValue(owner); }
    private static void Set(object owner, string name, object value) { Field(owner.GetType(), name).SetValue(owner, value); }
    private static string Constant(string name) { return (string)Rules.GetField(name, Static).GetRawConstantValue(); }
    private static object Invoke(MethodInfo method, object owner, params object[] args)
    {
        if (method == null) throw new MissingMethodException("required real method");
        try { return method.Invoke(owner, args); }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
    }
    private static Assembly Resolve(AssemblyLoadContext context, AssemblyName name)
    {
        if (string.IsNullOrEmpty(name.Name) || name.Name.Any(c => !char.IsLetterOrDigit(c) && c != '.' && c != '_' && c != '-')) return null;
        foreach (string root in new[] { Managed, ModDirectory })
        {
            string path = Path.Combine(root, name.Name + ".dll"); if (!File.Exists(path)) continue;
            AssemblyName actual = AssemblyName.GetAssemblyName(path);
            if (actual.Name != name.Name || actual.Version != name.Version
                || (actual.CultureName ?? "") != (name.CultureName ?? "")
                || !(actual.GetPublicKeyToken() ?? Array.Empty<byte>()).SequenceEqual(name.GetPublicKeyToken() ?? Array.Empty<byte>()))
                throw new FileLoadException("dependency identity mismatch");
            return context.LoadFromAssemblyPath(path);
        }
        return null;
    }
    private static void Observe(object sender, FirstChanceExceptionEventArgs args)
    {
        Exception ex = args.Exception;
        if (ex is TypeLoadException || ex is TypeInitializationException || ex is FileLoadException
            || ex is FileNotFoundException || ex is DllNotFoundException || ex is EntryPointNotFoundException
            || ex is System.Security.SecurityException || ex is BadImageFormatException) LoaderFailures++;
    }
    private static void Check(bool condition, string reason) { if (!condition) throw new Failure(reason); }
    private static string Describe(Exception ex)
    {
        if (ex == null) return "dependency-failure-observed";
        string result = ""; int depth = 0;
        for (; ex != null && depth++ < 8; ex = ex.InnerException)
            result += (result.Length == 0 ? "" : " -> ") + ex.GetType().FullName + ": " + new string((ex.Message ?? "").Take(384).Select(c => c >= ' ' && c <= '~' ? c : ' ').ToArray());
        return result + (ex == null ? "" : " -> [chain truncated]");
    }
}
