// SteamProbe - read-only Steam Workshop item probe for The Thousand and First.
// Given one Workshop item id it asks the running Steam client for that item's details and
// key/value tags, then prints one line of JSON: whether the logged-in account owns the item
// and what manifest_id / manifest_version it advertises. The SDK may print native lines of
// its own, so consumers take the last stdout line that parses as a JSON object with a status.
// Read-only: no publish, subscribe, download, login or relaunch, and no file writes.
// Compiled by Tools/WorkshopSteam/WorkshopSteam.csproj against the licensed installed
// Steamworks.NET assembly; the launcher copies that assembly only into a fresh local build
// output - nothing is redistributed. Tools/ is excluded from the staged mod.
// Exit codes: 0 ok, 2 usage, 3 not initialized, 4 not logged on / no license,
// 5 app mismatch, 6 query or verification failure, 7 item not found, 9 exception.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Steamworks;

namespace ThousandAndFirst.Tools.WorkshopSteam
{
    public static class SteamProbe
    {
        private const uint AppId = 333640u;
        private const int TimeoutMs = 15000;
        private const int PumpSleepMs = 25;
        private const uint MaxKeyValueTags = 64u;
        private const int MaxNotesChars = 4096;
        private const uint KeyMax = 256u;
        private const uint ValueMax = 1024u;
        private const string ManifestIdKey = "manifest_id";
        private const string ManifestVersionKey = "manifest_version";

        private static bool s_initialized;
        private static UGCQueryHandle_t s_handle = UGCQueryHandle_t.Invalid;
        private static CallResult<SteamUGCQueryCompleted_t> s_callResult;
        private static SteamUGCQueryCompleted_t s_completion;
        private static bool s_completed;
        private static bool s_ioFailure;
        private static string s_notes;

        /// <summary>The whole report, emitted in declaration order as one line of JSON.</summary>
        public sealed class ProbeResult
        {
            [JsonPropertyName("status")] public string Status { get; set; }
            [JsonPropertyName("exitCode")] public int ExitCode { get; set; }
            [JsonPropertyName("app")] public uint App { get; set; }
            [JsonPropertyName("appMatch")] public bool? AppMatch { get; set; }
            [JsonPropertyName("item")] public ulong Item { get; set; }
            [JsonPropertyName("itemMatch")] public bool? ItemMatch { get; set; }
            [JsonPropertyName("ownerMatch")] public bool? OwnerMatch { get; set; }
            [JsonPropertyName("visibility")] public string Visibility { get; set; }
            [JsonPropertyName("visibilityCode")] public int? VisibilityCode { get; set; }
            [JsonPropertyName("manifest_id")] public string ManifestId { get; set; }
            [JsonPropertyName("manifest_version")] public string ManifestVersion { get; set; }
            [JsonPropertyName("kvTagCount")] public uint? KvTagCount { get; set; }
            [JsonPropertyName("result")] public string Result { get; set; }
            [JsonPropertyName("timeUpdated")] public uint? TimeUpdated { get; set; }
            [JsonPropertyName("elapsedMs")] public long ElapsedMs { get; set; }
        }

        public static int Main(string[] args)
        {
            Stopwatch clock = Stopwatch.StartNew();
            ProbeResult result = new ProbeResult();
            result.App = AppId;
            Fail(result, "exception", 9);
            try
            {
                ulong itemId;
                if (!TryParseItemId(args, out itemId))
                {
                    Fail(result, "usage", 2);
                }
                else
                {
                    result.Item = itemId;
                    try { Probe(itemId, result); }
                    finally { Cleanup(); }
                }
            }
            catch (Exception e)
            {
                Fail(result, "exception", 9);
                Note(e.GetType().FullName + ": " + e.Message);
            }
            result.ElapsedMs = clock.ElapsedMilliseconds;
            // The JSON fence first, diagnostics after it.
            Console.Out.WriteLine(JsonSerializer.Serialize(result));
            Console.Out.Flush();
            if (s_notes != null)
            {
                Console.Error.WriteLine(s_notes);
                Console.Error.Flush();
            }
            return result.ExitCode;
        }

        private static bool Probe(ulong itemId, ProbeResult result)
        {
            if (!SteamAPI.Init()) return Fail(result, "steam_not_initialized", 3);
            s_initialized = true;
            if (SteamUtils.GetAppID().m_AppId != AppId)
            {
                result.AppMatch = false;
                return Fail(result, "app_mismatch", 5);
            }
            if (!SteamUser.BLoggedOn()) return Fail(result, "not_logged_on", 4);
            if (!SteamApps.BIsSubscribedApp(new AppId_t(AppId))) return Fail(result, "no_license", 4);
            if (!StartQuery(itemId, result)) return false;
            if (!AwaitQuery(result)) return false;
            if (!ReadDetails(itemId, result)) return false;
            if (!ScanKeyValueTags(result)) return false;
            result.Status = "ok";
            result.ExitCode = 0;
            return true;
        }

        private static bool StartQuery(ulong itemId, ProbeResult result)
        {
            PublishedFileId_t[] requested = new PublishedFileId_t[1];
            requested[0] = new PublishedFileId_t(itemId);
            s_handle = SteamUGC.CreateQueryUGCDetailsRequest(requested, 1u);
            if (s_handle == UGCQueryHandle_t.Invalid) return Fail(result, "query_create_failed", 6);
            if (!SteamUGC.SetReturnKeyValueTags(s_handle, true)) return Fail(result, "query_setup_failed", 6);

            // The handler is registered before Set() binds it to a live call.
            s_callResult = CallResult<SteamUGCQueryCompleted_t>.Create(OnQueryCompleted);
            SteamAPICall_t call = SteamUGC.SendQueryUGCRequest(s_handle);
            if (call == SteamAPICall_t.Invalid) return Fail(result, "query_send_failed", 6);
            s_callResult.Set(call);
            return true;
        }

        private static void OnQueryCompleted(SteamUGCQueryCompleted_t param, bool bIOFailure)
        {
            s_completion = param;
            s_ioFailure = bIOFailure;
            s_completed = true;
        }

        private static bool AwaitQuery(ProbeResult result)
        {
            Stopwatch pump = Stopwatch.StartNew();
            while (!s_completed && pump.ElapsedMilliseconds < TimeoutMs)
            {
                SteamAPI.RunCallbacks();
                Thread.Sleep(PumpSleepMs);
            }
            if (!s_completed) return Fail(result, "timeout", 6);
            if (s_ioFailure) return Fail(result, "query_io_failure", 6);
            if (s_completion.m_handle != s_handle) return Fail(result, "query_handle_mismatch", 6);
            result.Result = s_completion.m_eResult.ToString();
            if (s_completion.m_eResult != EResult.k_EResultOK) return Fail(result, "query_result_" + result.Result, 6);
            if (s_completion.m_unNumResultsReturned == 0u) return Fail(result, "item_not_found", 7);
            if (s_completion.m_unNumResultsReturned != 1u) return Fail(result,
                "result_count_" + s_completion.m_unNumResultsReturned.ToString(CultureInfo.InvariantCulture), 6);
            return true;
        }

        private static bool ReadDetails(ulong itemId, ProbeResult result)
        {
            SteamUGCDetails_t details;
            if (!SteamUGC.GetQueryUGCResult(s_handle, 0u, out details)) return Fail(result, "query_read_failed", 6);
            result.Result = details.m_eResult.ToString();
            if (details.m_eResult != EResult.k_EResultOK) return Fail(result, "details_result_" + result.Result, 6);
            result.ItemMatch = details.m_nPublishedFileId.m_PublishedFileId == itemId;
            if (!result.ItemMatch.Value) return Fail(result, "item_mismatch", 6);
            result.AppMatch = details.m_nConsumerAppID.m_AppId == AppId;
            if (!result.AppMatch.Value) return Fail(result, "consumer_app_mismatch", 6);

            // Reached only once the item and its consumer app are both exactly as requested.
            result.OwnerMatch = details.m_ulSteamIDOwner == SteamUser.GetSteamID().m_SteamID;
            result.Visibility = details.m_eVisibility.ToString();
            result.VisibilityCode = (int)details.m_eVisibility;
            result.TimeUpdated = details.m_rtimeUpdated;
            return true;
        }

        /// <summary>Refuses on an over-bound count, a failed read or a duplicate key.</summary>
        private static bool ScanKeyValueTags(ProbeResult result)
        {
            uint count = SteamUGC.GetQueryUGCNumKeyValueTags(s_handle, 0u);
            result.KvTagCount = count;
            if (count > MaxKeyValueTags) return Fail(result, "kv_tags_over_bound", 6);
            bool haveId = false;
            bool haveVersion = false;
            for (uint index = 0u; index < count; index++)
            {
                string key;
                string value;
                if (!SteamUGC.GetQueryUGCKeyValueTag(s_handle, 0u, index, out key, KeyMax, out value, ValueMax))
                {
                    return Fail(result, "kv_tag_read_failed", 6);
                }
                if (string.Equals(key, ManifestIdKey, StringComparison.Ordinal))
                {
                    if (haveId) return Fail(result, "kv_tag_duplicate", 6);
                    haveId = true;
                    result.ManifestId = value;
                }
                else if (string.Equals(key, ManifestVersionKey, StringComparison.Ordinal))
                {
                    if (haveVersion) return Fail(result, "kv_tag_duplicate", 6);
                    haveVersion = true;
                    result.ManifestVersion = value;
                }
            }
            return true;
        }

        /// <summary>Every step guarded on its own, so one failure cannot mask the rest.</summary>
        private static void Cleanup()
        {
            if (s_callResult != null)
            {
                try { s_callResult.Cancel(); }
                catch (Exception e) { Note("cleanup_cancel " + e.GetType().FullName + ": " + e.Message); }
                try { s_callResult.Dispose(); }
                catch (Exception e) { Note("cleanup_dispose " + e.GetType().FullName + ": " + e.Message); }
            }
            if (s_handle != UGCQueryHandle_t.Invalid)
            {
                try { SteamUGC.ReleaseQueryUGCRequest(s_handle); }
                catch (Exception e) { Note("cleanup_release " + e.GetType().FullName + ": " + e.Message); }
            }
            if (s_initialized)
            {
                try { SteamAPI.Shutdown(); }
                catch (Exception e) { Note("cleanup_shutdown " + e.GetType().FullName + ": " + e.Message); }
            }
        }

        /// <summary>One canonical positive decimal u64: no sign, padding or leading zeros.</summary>
        private static bool TryParseItemId(string[] args, out ulong itemId)
        {
            itemId = 0UL;
            if (args == null || args.Length != 1) return false;
            ulong parsed;
            if (!ulong.TryParse(args[0], NumberStyles.None, CultureInfo.InvariantCulture, out parsed)) return false;
            if (parsed == 0UL) return false;
            if (!string.Equals(args[0], parsed.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)) return false;
            itemId = parsed;
            return true;
        }

        private static bool Fail(ProbeResult result, string status, int exitCode)
        {
            result.Status = status;
            result.ExitCode = exitCode;
            return false;
        }

        private static void Note(string line)
        {
            string next = s_notes == null ? line : s_notes + Environment.NewLine + line;
            if (next.Length > MaxNotesChars) next = next.Substring(0, MaxNotesChars) + "[truncated]";
            s_notes = next;
        }
    }
}
