using System;
using System.Text;
using ThousandAndFirst.WorkshopSteam;
using ThousandAndFirst.WorkshopSteam.Evidence;

public static class ExtraObservationTests
{
    public static int Main()
    {
        int failed = ReleaseSubmissionObservationTests.Run();
        failed += ObservationAdversarialTests.Main();
        failed += UploadCallbackRulesTests.Run();
        ReleaseSubmissionObservation source;
        string reason;
        if (!ReleaseSubmissionObservation.TryCreate(new string('a', 64), null, "3796495680", "0.3.1",
            new string('b', 64), new string('c', 64), "2026-09-06T05:00:00.0000000Z", true, 2,
            "0", false, false, ReleaseSubmissionCompletion.Unknown, null, out source, out reason))
            throw new InvalidOperationException(reason);
        string wire = ReleaseSubmissionObservationCodec.Encode(source);
        int names = 0;
        foreach (string escape in new[] { "\\uD800", "\\uDC00", "\\uD800x" })
        {
            names++;
            try
            {
                ReleaseSubmissionObservation decoded;
                bool ok = ReleaseSubmissionObservationCodec.TryDecode(Encoding.UTF8.GetBytes(
                    wire.Replace("\"note\":", "\"" + escape + "\":")), out decoded, out reason);
                if (ok || decoded != null || reason != "malformed_text")
                    throw new InvalidOperationException("malformed property name did not refuse exactly");
                Console.WriteLine("PASS malformed-property-name-" + escape);
            }
            catch (Exception error)
            {
                failed++;
                Console.WriteLine("FAIL malformed-property-name-" + escape + " " + error.GetType().Name);
            }
        }
        string[] protocolNames = Enum.GetNames(typeof(UploadCompletion));
        string[] evidenceNames = Enum.GetNames(typeof(ReleaseSubmissionCompletion));
        if (protocolNames.Length != 6 || evidenceNames.Length != protocolNames.Length) failed++;
        foreach (string name in protocolNames)
        {
            if (!Enum.IsDefined(typeof(ReleaseSubmissionCompletion), name)
                || (int)Enum.Parse(typeof(UploadCompletion), name)
                    != (int)Enum.Parse(typeof(ReleaseSubmissionCompletion), name)) failed++;
        }
        Console.WriteLine("extra_property_cases=" + names + "; enum_mapping_cases=" + protocolNames.Length
            + "; aggregate_failure_count=" + failed + "; sdk_calls=false");
        return failed == 0 ? 0 : 1;
    }
}
