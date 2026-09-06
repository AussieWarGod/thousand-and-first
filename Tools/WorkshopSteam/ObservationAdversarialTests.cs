using System;
using System.Text;
using ThousandAndFirst.WorkshopSteam.Evidence;

public static class ObservationAdversarialTests
{
    private static int failures, cases;
    private static ReleaseSubmissionObservation Create(int result, string returned)
    {
        ReleaseSubmissionObservation value; string reason;
        bool ok = ReleaseSubmissionObservation.TryCreate(new string('a', 64), null, "3796495680", "0.3.1",
            new string('b', 64), new string('c', 64), "2026-09-06T04:43:00.0000000Z", true, result,
            returned, false, false, ReleaseSubmissionCompletion.Unknown, "probe", out value, out reason);
        if (!ok) throw new InvalidOperationException(reason);
        return value;
    }
    private static void Case(string name, Action test)
    {
        cases++;
        try { test(); Console.WriteLine("PASS " + name); }
        catch (Exception error) { failures++; Console.WriteLine("FAIL " + name + " " + error.GetType().Name + " " + error.Message); }
    }
    private static void Need(bool condition) { if (!condition) throw new InvalidOperationException("assertion failed"); }
    public static int Main()
    {
        foreach (int raw in new[] { int.MinValue, -1, 65536, int.MaxValue })
            Case("opaque-result-" + raw, delegate {
                ReleaseSubmissionObservation source = Create(raw, "3796495680"), decoded; string reason;
                Need(ReleaseSubmissionObservationCodec.TryDecode(ReleaseSubmissionObservationCodec.EncodeUtf8(source), out decoded, out reason));
                Need(decoded.RawEResult == raw);
            });
        Case("failed-returned-zero", delegate {
            ReleaseSubmissionObservation source = Create(2, "0"), decoded; string reason;
            Need(ReleaseSubmissionObservationCodec.TryDecode(ReleaseSubmissionObservationCodec.EncodeUtf8(source), out decoded, out reason));
            Need(decoded.ReturnedItem == "0");
        });
        foreach (string escape in new[] { "\\uD800", "\\uDC00", "\\uD800x" })
            Case("malformed-surrogate-" + escape, delegate {
                string wire = ReleaseSubmissionObservationCodec.Encode(Create(2, "3796495680"));
                wire = wire.Replace("\"probe\"", "\"" + escape + "\"");
                ReleaseSubmissionObservation decoded; string reason;
                Need(!ReleaseSubmissionObservationCodec.TryDecode(Encoding.UTF8.GetBytes(wire), out decoded, out reason));
                Need(decoded == null && reason != null);
            });
        Console.WriteLine("adversarial_cases=" + cases + "; failures=" + failures + "; sdk_calls=false");
        return failures == 0 ? 0 : 1;
    }
}
