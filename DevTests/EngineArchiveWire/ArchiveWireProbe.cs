using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.Loader;
using System.Security.Cryptography;

// Executes installed engine serializers and the supplied real mod assembly, never source substitutes.
// Quarantined fixtures are codec-valid, deliberately not authoritative realm-return fixtures.
internal static class ArchiveWireProbe
{
    private sealed class CheckFailure : Exception { internal CheckFailure(string text) : base(text) { } }
    private sealed class ProbeBlocked : Exception { internal ProbeBlocked(string text) : base(text) { } }
    private sealed class Wire { internal byte[] Bytes; internal long Start, End, Tail; internal bool Composite; }
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private const int Sentinel = 0x13579bdf;
    private static readonly string[] Names = { "ExileChronicle", "ExileAbility", "ReturnChronicle", "ReturnReputation", "ReturnFeelings", "ReturnSeat", "ReturnAbility" };
    private static readonly string[] Scopes = { "Chronicle", "Ability", "Chronicle", "Reputation", "Feelings", "Seat", "Ability" };
    private static readonly List<string> LoaderErrors = new List<string>();
    private static string Managed, ModDirectory;
    private static Type Archive, Receipt, Cache, Writer, Reader, Carry, Topology, ExpectedException;
    private static MethodInfo ArchiveRead, ArchiveWrite, TailRead, TailWrite, CallbackRead, CallbackWrite, Reset;
    private static FieldInfo[] ReceiptFields;
    private static Exception OriginalException;
    private static Wire FullWire;
    private static int SaveVersion, MaxBasis, Passed, Failed, Blocked;
    private static bool FullControl;

    private static int Main(string[] args)
    {
        if (args.Length != 2) { Console.WriteLine("BLOCKED usage: MOD_DLL INSTALLED_MANAGED_DIRECTORY"); return 2; }
        try
        {
            string mod = Path.GetFullPath(args[0]); Managed = Path.GetFullPath(args[1]); ModDirectory = Path.GetDirectoryName(mod);
            Check(File.Exists(mod) && Directory.Exists(Managed) && string.Equals(Path.GetExtension(mod), ".dll", StringComparison.OrdinalIgnoreCase), "missing mod DLL or Managed directory");
            AssemblyLoadContext.Default.Resolving += Resolve;
            AppDomain.CurrentDomain.FirstChanceException += Watch;
            try { return Run(mod); }
            finally
            {
                AppDomain.CurrentDomain.FirstChanceException -= Watch;
                AssemblyLoadContext.Default.Resolving -= Resolve;
            }
        }
        catch (Exception error) { Console.WriteLine("BLOCKED setup " + Describe(error)); PrintLoader(0); return 2; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Run(string mod)
    {
        Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(mod);
        Archive = assembly.GetType("ThousandAndFirst.KingdomRealmArchive", true);
        Receipt = assembly.GetType("ThousandAndFirst.KingdomRealmCallbackReceipt", true);
        Carry = assembly.GetType("ThousandAndFirst.KingdomCarryBook", true);
        Topology = assembly.GetType("ThousandAndFirst.KingdomSettlementTopology", true);
        Check(ReferenceEquals(Archive.Assembly, assembly) && ReferenceEquals(Receipt.Assembly, assembly), "foreign archive or receipt type");
        Type composite = Archive.GetInterfaces().Single(type => type.FullName == "XRL.World.IComposite");
        Assembly engine = composite.Assembly;
        Cache = engine.GetType("XRL.Serialization.FastSerialization+Cache", true);
        Writer = engine.GetType("XRL.World.SerializationWriter", true); Reader = engine.GetType("XRL.World.SerializationReader", true);
        Check(engine.GetType("XRL.The", true).GetProperty("Game").GetValue(null) == null, "unexpected live game");
        SaveVersion = (int)engine.GetType("XRL.XRLGame", true).GetField("SaveVersion").GetRawConstantValue();
        MaxBasis = (int)assembly.GetType("ThousandAndFirst.KingdomArchivedSettlementCodec", true).GetField("CurrentVersion", All).GetRawConstantValue();
        Check((int)Archive.GetField("CurrentVersion").GetRawConstantValue() == 9 && MaxBasis >= 14, "probe requires archive v9 and fourteen distinct legal bases");
        ArchiveRead = Method(Archive, "Read", Reader); ArchiveWrite = Method(Archive, "Write", Writer);
        TailRead = Unique(Archive, "TryReadHashBasisTail"); TailWrite = Unique(Archive, "WriteHashBasisTail");
        CallbackRead = Unique(Archive, "ReadCallback"); CallbackWrite = Unique(Archive, "WriteCallback"); Reset = Unique(Archive, "ResetToPoisonEnvelope");
        ReceiptFields = Receipt.GetFields(BindingFlags.Instance | BindingFlags.Public).OrderBy(field => field.Name, StringComparer.Ordinal).ToArray();
        Check(ReceiptFields.All(field => Atom(field.FieldType)) && CountErrors() == 0, "unknown receipt field type or loader failure");
        Console.WriteLine("mod-sha256=" + Hash(mod) + " engine-sha256=" + Hash(engine.Location));
        Console.WriteLine("scope=real-engine-direct-archive-wire-and-private-tail; game=false; historical-save=false; outer-composite-load=false; realm-authority=false; save-version=" + SaveVersion);
        Case("tail-v9-fourteen-values-real-callback-roundtrip", TailHealthy);
        for (int version = 2; version <= 8; version++) { int v = version; Case("legacy-v" + v + "-real-callback-defaults-no-tail-consumption", () => Legacy(v)); }
        for (int count = 0; count < 56; count++) { int cut = count; Case("tail-v9-truncated-at-byte-" + cut, () => TailTruncated(cut)); }
        for (int index = 0; index < 14; index++)
        {
            int slot = index;
            Case("tail-v9-negative-slot-" + slot, () => TailInvalid(slot, -1));
            Case("tail-v9-above-maximum-slot-" + slot, () => TailInvalid(slot, MaxBasis + 1));
        }
        Case("tail-v9-none-receipt-cannot-gain-basis", () => PhaseInvalid("None"));
        Case("tail-v9-intent-cannot-gain-settled-basis", () => PhaseInvalid("Intent"));
        Case("tail-v9-noncanonical-phase-atomic-refusal", () => PhaseInvalid("invalid"));
        Case("tail-v9-absent-final-receipt-atomic-refusal", () => PhaseInvalid("null"));
        FullControl = Case("public-v9-codec-envelope-fourteen-values-control", FullHealthy);
        Case("public-v8-synthetic-no-tail-sentinel-defaults", FullLegacy);
        Case("public-invalid-marker-poison-reset-original-rethrow", PublicBadMarker);
        Case("public-v9-invalid-first-tail-value-poison-reset", () => FullRejected(0, -1, null));
        Case("public-v9-invalid-last-tail-value-poison-reset", () => FullRejected(13, MaxBasis + 1, null));
        Case("public-v9-empty-tail-poison-reset", () => FullRejected(0, 0, 0));
        Case("public-v9-last-tail-byte-missing-poison-reset", () => FullRejected(0, 0, 55));
        int cases = Passed + Failed + Blocked;
        Console.WriteLine("cases=" + cases + " passed=" + Passed + " failed=" + Failed + " blocked=" + Blocked + " full-envelope-control=" + FullControl);
        Console.WriteLine("Private-tail passes alone do not prove the envelope reader. Public tail-fault cases require a passing public control. No callback execution, save-file admission, FinalizeRead, or visible-message proof.");
        Check(cases == 103, "fixture count changed");
        return Blocked != 0 ? 2 : Failed != 0 ? 1 : 0;
    }

    private static object[] Receipts(bool pinned)
    {
        object[] result = new object[7];
        for (int i = 0; i < result.Length; i++)
        {
            object value = Activator.CreateInstance(Receipt); result[i] = value;
            SetEnum(value, "Phase", "Settled"); SetEnum(value, "Disposition", "Delivered"); SetEnum(value, "Scope", Scopes[i]);
            foreach (string name in new[] { "BeforeGraph", "AfterGraph", "BeforeArchiveGraph", "AfterArchiveGraph" }) Set(value, name, new string("abcdef0123456789"[i], 64));
            Set(value, "BeforeEffect", "before:" + Names[i] + ":\u00e9"); Set(value, "AfterEffect", "after:" + Names[i]); Set(value, "ObservedEffect", "observed:" + Names[i]);
            if (Scopes[i] == "Feelings") { Set(value, "BeforeStamp", 1); Set(value, "AfterStamp", 2); }
            Set(value, "IntentSettlementSchema", pinned ? i * 2 + 1 : 0); Set(value, "SettledSettlementSchema", pinned ? i * 2 + 2 : 0);
            Check((bool)Call(Method(Receipt, "Validate"), value), "invalid source callback " + i);
            object[] arguments = { value, null };
            Check((bool)Call(Method(Archive, "ValidBasisShape", Receipt, typeof(string).MakeByRefType()), null, arguments), "invalid source basis " + i);
        }
        return result;
    }
    private static object Fixture()
    {
        object value = Activator.CreateInstance(Archive); Call(Reset, value, "synthetic non-authoritative wire fixture");
        object[] receipts = Receipts(true); for (int i = 0; i < Names.Length; i++) Set(value, Names[i], receipts[i]);
        Set(value, "RealmId", "synthetic-retained-realm"); Set(value, "DisplayName", "synthetic archive"); Set(value, "ClosedTick", 17L);
        ((IList)Get(value, "ChronicleEntries")).Add("retained fixture chronicle"); ((IList)Get(value, "OutsiderEntries")).Add("retained fixture outsider");
        Envelope(value); object[] arguments = { null }; Check(!(bool)Call(Method(Archive, "Validate", typeof(string).MakeByRefType()), value, arguments), "fixture unexpectedly claims realm authority");
        return value;
    }
    private static void Envelope(object archive)
    {
        object[] arguments = { null };
        Check((bool)Call(Method(Archive, "ValidateEnvelope", typeof(string).MakeByRefType()), archive, arguments), "synthetic envelope is not codec-valid");
    }
    private static object[] HeldReceipts(object archive) { return Names.Select(name => Get(archive, name)).ToArray(); }
    private static object[][] Snapshot(object[] receipts) { return receipts.Select(value => value == null ? null : ReceiptFields.Select(field => field.GetValue(value)).ToArray()).ToArray(); }
    private static void Same(object[] receipts, object[][] before, bool zeroBasis = false)
    {
        Check(receipts.Length == before.Length, "receipt count changed");
        for (int i = 0; i < before.Length; i++)
        {
            Check((receipts[i] == null) == (before[i] == null), "receipt presence changed"); if (before[i] == null) continue;
            for (int j = 0; j < ReceiptFields.Length; j++)
            {
                FieldInfo field = ReceiptFields[j]; bool basis = field.Name == "IntentSettlementSchema" || field.Name == "SettledSettlementSchema";
                object expected = zeroBasis && basis ? 0 : before[i][j];
                Check(Equals(expected, field.GetValue(receipts[i])), "receipt " + i + " changed field " + field.Name);
            }
        }
    }

    private static Wire ReceiptWire(object[] receipts, bool tail)
    {
        long tailStart = 0;
        Wire wire = WriteWire((writer, stream) =>
        {
            foreach (object receipt in receipts) Call(CallbackWrite, null, writer, receipt);
            tailStart = stream.Position; if (tail) WriteTail(writer, receipts);
        }, false);
        wire.Tail = tailStart; return wire;
    }
    private static object[] ReadCallbacks(object reader)
    {
        object[] receipts = new object[7];
        for (int i = 0; i < receipts.Length; i++)
        {
            receipts[i] = Call(CallbackRead, null, reader);
            Check((bool)Call(Method(Receipt, "Validate"), receipts[i]), "real decoded callback invalid");
            Check((int)Get(receipts[i], "IntentSettlementSchema") == 0 && (int)Get(receipts[i], "SettledSettlementSchema") == 0, "legacy callback frame acquired basis");
        }
        return receipts;
    }
    private static void WriteTail(object writer, object[] receipts)
    {
        object[] arguments = new object[8]; arguments[0] = writer; Array.Copy(receipts, 0, arguments, 1, 7); Call(TailWrite, null, arguments);
    }
    private static bool ReadTail(object reader, int version, object[] receipts, out string failure)
    {
        object[] arguments = new object[10]; arguments[0] = reader; arguments[1] = version; Array.Copy(receipts, 0, arguments, 2, 7);
        bool result = (bool)Call(TailRead, null, arguments); failure = (string)arguments[9]; return result;
    }
    private static void TailHealthy()
    {
        object[] expected = Receipts(true); object[][] held = Snapshot(expected); Wire wire = ReceiptWire(expected, true);
        Check(wire.End - wire.Tail == 56, "v9 tail is not fourteen Int32 values");
        for (int i = 0; i < 14; i++) Check(BitConverter.ToInt32(wire.Bytes, checked((int)wire.Tail + i * 4)) == i + 1, "physical tail order changed");
        WithReader(wire, (reader, stream) =>
        {
            object[] actual = ReadCallbacks(reader); Check(stream.Position == wire.Tail, "callback frame moved");
            Check(ReadTail(reader, 9, actual, out string failure) && failure == null, "healthy tail refused");
            Same(actual, held); Check(stream.Position == wire.End, "tail consumed wrong length"); ReadSentinel(reader, stream, wire.End);
        });
        Same(expected, held);
    }
    private static void Legacy(int version)
    {
        object[] expected = Receipts(true); object[][] held = Snapshot(expected); Wire wire = ReceiptWire(expected, false);
        WithReader(wire, (reader, stream) =>
        {
            object[] actual = ReadCallbacks(reader); Check(stream.Position == wire.End, "legacy callbacks consumed sentinel");
            long before = stream.Position;
            Check(ReadTail(reader, version, actual, out string failure) && failure == null, "legacy tail refused");
            Check(stream.Position == before, "legacy version consumed tail bytes"); Same(actual, held, true); ReadSentinel(reader, stream, wire.End);
        });
        Same(expected, held);
    }
    private static Wire TailWire() { return WriteWire((writer, stream) => WriteTail(writer, Receipts(true)), false); }
    private static void TailTruncated(int count)
    {
        object[] target = Receipts(false); object[][] before = Snapshot(target); Wire wire = TailWire();
        WithReader(wire, (reader, stream) =>
        {
            stream.SetLength(wire.Start + count);
            ExpectThrow(() => ReadTail(reader, 9, target, out _), typeof(EndOfStreamException)); Same(target, before);
        });
    }
    private static void TailInvalid(int slot, int invalid)
    {
        object[] target = Receipts(false); object[][] before = Snapshot(target); Wire wire = TailWire();
        WithReader(wire, (reader, stream) =>
        {
            PatchInt(stream, wire.Start + slot * 4, invalid);
            Check(!ReadTail(reader, 9, target, out string failure) && !string.IsNullOrEmpty(failure), "invalid tail accepted or unexplained");
            Same(target, before); Check(stream.Position == wire.End, "invalid tail did not read all candidate values"); ReadSentinel(reader, stream, wire.End);
        });
    }
    private static void PhaseInvalid(string kind)
    {
        object[] target = Receipts(false);
        if (kind == "null") target[6] = null;
        else if (kind == "None") target[6] = Activator.CreateInstance(Receipt);
        else if (kind == "invalid") Set(target[6], "Phase", Enum.ToObject(Field(Receipt, "Phase").FieldType, 255));
        else
        {
            SetEnum(target[6], "Phase", "Intent"); SetEnum(target[6], "Disposition", "None");
            Set(target[6], "AfterGraph", null); Set(target[6], "AfterArchiveGraph", null); Set(target[6], "ObservedEffect", null);
            Check((bool)Call(Method(Receipt, "Validate"), target[6]), "intent fixture is not individually valid");
        }
        object[][] before = Snapshot(target); Wire wire = TailWire();
        WithReader(wire, (reader, stream) =>
        {
            Check(!ReadTail(reader, 9, target, out string failure) && !string.IsNullOrEmpty(failure), "phase/absence tail accepted");
            Same(target, before); Check(stream.Position == wire.End, "shape rejection consumed wrong length"); ReadSentinel(reader, stream, wire.End);
        });
    }

    private static void FullHealthy()
    {
        object expected = Fixture(); object[][] receipts = Snapshot(HeldReceipts(expected));
        FullWire = WriteWire((writer, stream) => Call(ArchiveWrite, expected, writer), true); FullWire.Tail = FullWire.End - 56;
        for (int i = 0; i < 14; i++) Check(BitConverter.ToInt32(FullWire.Bytes, checked((int)FullWire.Tail + i * 4)) == i + 1, "public writer missing exact tail");
        WithReader(FullWire, (reader, stream) =>
        {
            object actual = Activator.CreateInstance(Archive);
            try { Call(ArchiveRead, actual, reader); }
            catch (Exception)
            {
                Console.WriteLine("public-control-read-offset=" + (stream.Position - FullWire.Start)
                    + " tail-offset=" + (FullWire.Tail - FullWire.Start) + " reader-errors=" + Get(reader, "Errors"));
                throw;
            }
            Check(stream.Position == FullWire.End, "public reader did not consume exact envelope");
            Envelope(actual); Same(HeldReceipts(actual), receipts); Check((bool)Get(actual, "Quarantined"), "fixture quarantine lost");
            Check((string)Get(actual, "RealmId") == (string)Get(expected, "RealmId"), "retained non-authoritative identity lost");
            ReadSentinel(reader, stream, FullWire.End);
        });
    }
    private static void NeedFull() { if (!FullControl) throw new ProbeBlocked("public healthy control did not pass; tail path not established"); }
    private static void FullLegacy()
    {
        NeedFull(); object[][] expected = Snapshot(Receipts(true));
        WithReader(FullWire, (reader, stream) =>
        {
            // Synthetic v8 counterpart: current empty job columns have the same v8 layout.
            // Header/token tables were initialized from the complete real writer output first.
            PatchInt(stream, FullWire.Start + 4, 8); PatchInt(stream, FullWire.Tail, Sentinel); stream.SetLength(FullWire.Tail + 4);
            object actual = Activator.CreateInstance(Archive); Call(ArchiveRead, actual, reader);
            Check(stream.Position == FullWire.Tail, "v8 public reader consumed sentinel"); Envelope(actual); Same(HeldReceipts(actual), expected, true);
            Check((int)Get(actual, "Version") == 9, "v8 did not promote envelope version"); ReadSentinel(reader, stream, FullWire.Tail);
        });
    }
    private static void FullRejected(int slot, int invalid, int? remaining)
    {
        NeedFull(); object target = Fixture();
        WithReader(FullWire, (reader, stream) =>
        {
            if (remaining.HasValue) stream.SetLength(FullWire.Tail + remaining.Value);
            else PatchInt(stream, FullWire.Tail + slot * 4, invalid);
            Exception error = ExpectThrow(() => Call(ArchiveRead, target, reader), remaining.HasValue ? typeof(EndOfStreamException) : typeof(InvalidDataException));
            Poison(target, error);
        });
    }
    private static void PublicBadMarker()
    {
        Wire wire = WriteWire((writer, stream) => IntWrite(writer, 0), false); object target = Fixture();
        WithReader(wire, (reader, stream) => Poison(target, ExpectThrow(() => Call(ArchiveRead, target, reader), typeof(InvalidDataException))));
    }
    private static void Poison(object target, Exception error)
    {
        Check((bool)Get(target, "Quarantined") && Get(target, "Phase").ToString() == "Quarantined", "failed read retained authority");
        Check((int)Get(target, "Version") == 9 && Get(target, "RealmId") == null && Get(target, "Seat") == null
            && Get(target, "Away") == null && Get(target, "Seceded") == null, "poison retained partial realm graph");
        string fault = (string)Get(target, "Fault"); Check(!string.IsNullOrEmpty(fault) && fault.Length <= 4096, "missing/unbounded poison fault");
        object[] receipts = HeldReceipts(target);
        foreach (object receipt in receipts)
        {
            Check((bool)Call(Method(Receipt, "Validate"), receipt) && Get(receipt, "Phase").ToString() == "None", "poison retained callback");
            Check((int)Get(receipt, "IntentSettlementSchema") == 0 && (int)Get(receipt, "SettledSettlementSchema") == 0, "poison retained partial basis");
        }
        object clean = Activator.CreateInstance(Archive); Call(Reset, clean, error.Message);
        SameGraph(clean, target, 0); Envelope(target);
        Wire writable = WriteWire((writer, stream) => Call(ArchiveWrite, target, writer), true);
        Check(writable.End > writable.Start, "poison was not writable");
    }
    private static void SameGraph(object expected, object actual, int depth)
    {
        Check(depth <= 12, "poison graph exceeds depth bound");
        if (expected == null || actual == null) { Check(expected == null && actual == null, "poison field presence differs"); return; }
        Type type = expected.GetType(); Check(actual.GetType() == type, "poison field type differs");
        if (Atom(type)) { Check(Equals(expected, actual), "poison scalar differs"); return; }
        if (expected is IDictionary)
        {
            IDictionary left = (IDictionary)expected, right = (IDictionary)actual;
            Check(left.Count == right.Count && left.Count <= 4096, "poison dictionary count differs");
            foreach (DictionaryEntry entry in left) { Check(right.Contains(entry.Key), "poison dictionary key missing"); SameGraph(entry.Value, right[entry.Key], depth + 1); }
            return;
        }
        if (expected is IList)
        {
            IList left = (IList)expected, right = (IList)actual; Check(left.Count == right.Count && left.Count <= 4096, "poison list count differs");
            for (int i = 0; i < left.Count; i++) SameGraph(left[i], right[i], depth + 1); return;
        }
        Check(type.Assembly == Archive.Assembly && (type.Namespace ?? "").StartsWith("ThousandAndFirst", StringComparison.Ordinal), "unknown poison graph type");
        FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        Check(fields.Length <= 256, "poison field bound"); foreach (FieldInfo field in fields) SameGraph(field.GetValue(expected), field.GetValue(actual), depth + 1);
    }

    private static Wire WriteWire(Action<object, MemoryStream> payload, bool composite)
    {
        object cache = Activator.CreateInstance(Cache, new object[] { 0 }); MemoryStream stream = (MemoryStream)Get(cache, "MemoryStream");
        using (stream)
        using (IDisposable writer = (IDisposable)Activator.CreateInstance(Writer, new[] { cache }))
        {
            Invoke(writer, "Start", new[] { typeof(int), typeof(bool) }, new object[] { SaveVersion, true });
            object rack = Get(cache, "GameObjects"); IList player = (IList)rack;
            Check(player.Count == 1 && player[0] == null, "writer seeded a live player or foreign rack"); Invoke(rack, "Clear");
            Wire wire = new Wire { Start = stream.Position, Composite = composite }; payload(writer, stream); wire.End = stream.Position; wire.Tail = wire.Start;
            IntWrite(writer, Sentinel); SafeCache(cache, composite); Invoke(writer, "FinalizeWrite"); SafeCache(cache, composite); wire.Bytes = stream.ToArray();
            Check(wire.Start >= 68 && wire.End > wire.Start && wire.End + 4 < wire.Bytes.Length && wire.Bytes.Length <= 1048576, "unbounded/empty wire"); return wire;
        }
    }
    private static void WithReader(Wire wire, Action<object, MemoryStream> action)
    {
        object cache = Activator.CreateInstance(Cache, new object[] { 0 }); MemoryStream stream = (MemoryStream)Get(cache, "MemoryStream");
        using (stream)
        using (IDisposable reader = (IDisposable)Activator.CreateInstance(Reader, new[] { cache }))
        {
            stream.Write(wire.Bytes, 0, wire.Bytes.Length); stream.Position = 0;
            Invoke(reader, "Start", new[] { typeof(bool) }, new object[] { true });
            Check((int)Get(reader, "FileVersion") == SaveVersion && stream.Position == wire.Start, "reader did not reach direct payload");
            SafeCache(cache, wire.Composite, true); Check((int)Get(reader, "Errors") == 0, "reader Start recorded errors");
            action(reader, stream); Check((int)Get(reader, "Errors") == 0, "reader recorded nested errors"); SafeCache(cache, wire.Composite, true);
            // No FinalizeRead: it clears engine-global load bindings, and this probe owns none.
        }
    }
    private static void SafeCache(object cache, bool composite, bool reading = false)
    {
        foreach (string name in new[] { "GameObjects", "GameObjectReferences", "EventRegistries", "Tokenized", "Objects" })
        {
            object rack = Get(cache, name); Check((int)rack.GetType().GetProperty("Count").GetValue(rack) == 0, "unexpected object rack " + name);
            if (reading) Check((int)rack.GetType().GetProperty("Capacity").GetValue(rack) == 0, "unexpected allocated object rack " + name);
        }
        IList types = (IList)Get(cache, "Types"); Check(types.Count <= (composite ? 2 : 0), "unexpected type-token count");
        int present = 0; Array slots = (Array)Invoke(types, "GetArray"); Check(slots.Length <= 16, "type-token capacity bound");
        foreach (object type in slots) if (type != null)
        { present++; Check(ReferenceEquals(type, Carry) || ReferenceEquals(type, Topology), "foreign composite token"); }
        Check(present == (composite ? 2 : 0), "missing or duplicate type-token slots");
    }
    private static void ReadSentinel(object reader, MemoryStream stream, long at)
    {
        Check(stream.Position == at && (int)Invoke(reader, "ReadInt32") == Sentinel && stream.Position == at + 4, "sentinel consumed or changed");
    }
    private static void PatchInt(MemoryStream stream, long at, int value)
    {
        long position = stream.Position; byte[] bytes = BitConverter.GetBytes(value); Check(BitConverter.IsLittleEndian, "unsupported fixture byte order");
        stream.Position = at; stream.Write(bytes, 0, bytes.Length); stream.Position = position;
    }
    private static Exception ExpectThrow(Action action, Type expected)
    {
        ExpectedException = expected; OriginalException = null;
        try
        {
            try { action(); }
            catch (Exception error)
            {
                if (LoaderFailure(error)) throw;
                Check(error.GetType() == expected && ReferenceEquals(error, OriginalException), "original expected exception was replaced or wrong type"); return error;
            }
            throw new CheckFailure("malformed wire did not throw");
        }
        finally { ExpectedException = null; }
    }
    private static FieldInfo Field(Type type, string name) { return type.GetField(name, All) ?? throw new CheckFailure("missing field " + name); }
    private static object Get(object target, string name) { return Field(target.GetType(), name).GetValue(target); }
    private static void Set(object target, string name, object value) { Field(target.GetType(), name).SetValue(target, value); }
    private static void SetEnum(object target, string name, string value) { FieldInfo field = Field(target.GetType(), name); field.SetValue(target, Enum.Parse(field.FieldType, value)); }
    private static MethodInfo Method(Type type, string name, params Type[] parameters) { return type.GetMethod(name, All, null, parameters, null) ?? throw new CheckFailure("missing method " + name); }
    private static MethodInfo Unique(Type type, string name) { return type.GetMethods(All).Single(method => method.Name == name); }
    private static object Invoke(object target, string name) { return Call(Method(target.GetType(), name), target); }
    private static object Invoke(object target, string name, Type[] parameters, object[] args) { return Call(Method(target.GetType(), name, parameters), target, args); }
    private static void IntWrite(object writer, int value) { Invoke(writer, "Write", new[] { typeof(int) }, new object[] { value }); }
    private static object Call(MethodInfo method, object target, params object[] args)
    {
        try { return method.Invoke(target, args); }
        catch (TargetInvocationException error) when (error.InnerException != null) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    private static bool Case(string id, Action action)
    {
        int before = CountErrors(); Exception failure = null;
        try { action(); } catch (Exception error) { failure = error; }
        if (CountErrors() != before || failure is ProbeBlocked || failure != null && LoaderFailure(failure))
        { Blocked++; Console.WriteLine("BLOCKED " + id + " " + (failure == null ? "production-caught loader failure" : Describe(failure))); PrintLoader(before); return false; }
        if (failure != null) { Failed++; Console.WriteLine("FAIL " + id + " " + Describe(failure)); return false; }
        Passed++; Console.WriteLine("PASS " + id); return true;
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
    private static bool Atom(Type type) { return type.IsPrimitive || type.IsEnum || type == typeof(string); }
    private static bool LoaderFailure(Exception error) { return error is FileNotFoundException || error is FileLoadException || error is BadImageFormatException || error is TypeLoadException || error is TypeInitializationException || error is ReflectionTypeLoadException || error is DllNotFoundException || error is EntryPointNotFoundException || error is MissingMethodException; }
    private static void Watch(object sender, FirstChanceExceptionEventArgs args)
    {
        if (LoaderFailure(args.Exception)) Note(args.Exception.GetType().FullName);
        if (ExpectedException != null && OriginalException == null && args.Exception.GetType() == ExpectedException) OriginalException = args.Exception;
    }
    private static string Hash(string path) { using (FileStream stream = File.OpenRead(path)) using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
    private static void Note(string detail) { lock (LoaderErrors) LoaderErrors.Add(detail); }
    private static int CountErrors() { lock (LoaderErrors) return LoaderErrors.Count; }
    private static void PrintLoader(int start) { lock (LoaderErrors) foreach (string detail in LoaderErrors.Skip(start).Distinct().Take(16)) Console.WriteLine("loader=" + detail); }
    private static void Check(bool condition, string text) { if (!condition) throw new CheckFailure(text); }
    private static string Describe(Exception error) { return error.GetType().FullName + (error is CheckFailure || error is ProbeBlocked ? ": " + error.Message : "") + (error.InnerException == null ? "" : " inner=" + error.InnerException.GetType().FullName); }
}
