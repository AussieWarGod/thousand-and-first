// SteamUploadAuthority - fresh, read-only Workshop authority for one upload target. TryQuery asks
// the running Steam client, explicitly uncached, for one item's details, its manifest key/value
// tags and its normal tags; AcceptsLane decides whether that remote state may be replaced by this
// request's lane. Raw account and owner ids are compared in memory and never emitted. Built by
// root's csproj against the licensed installed Steamworks.NET assembly; nothing is redistributed.
// This adapter is not an entrypoint and performs no upload of its own.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using Steamworks;

namespace ThousandAndFirst.WorkshopSteam
{
    /// <summary>What one fresh query proved about the remote item. Carries no account id.</summary>
    internal sealed class AuthorityFacts
    {
        internal ERemoteStoragePublishedFileVisibility Visibility;
        internal string ManifestId, ManifestVersion, Title, Description;
        internal string[] Tags;
        internal bool OwnerMatches, TagsTruncated, Cached;
        internal uint TimeUpdated;
    }

    internal static class SteamUploadAuthority
    {
        internal const uint AppId = 333640u;
        internal const ulong PublicAlphaItem = 3794797472UL;
        internal const string ManifestIdKey = "manifest_id";
        internal const string ManifestVersionKey = "manifest_version";
        internal const int VisibilityPublic = 0;
        internal const int VisibilityPrivate = 2;
        private const int PumpSleepMs = 25;
        private const int MaxNotesChars = 4096;
        private const uint MaxKeyValueTags = 64u;
        private const uint MaxTags = 64u;
        private const uint KeyMax = 256u;
        private const uint TagMax = 256u;
        private const uint ValueMax = 1024u;
        private const string CanonicalPrefix = "0.3.";

        private static string s_cleanupNotes;

        private sealed class Pending
        {
            internal SteamUGCQueryCompleted_t Completion;
            internal bool IoFailure, Completed;

            internal void OnCompleted(SteamUGCQueryCompleted_t param, bool bIOFailure)
            { Completion = param; IoFailure = bIOFailure; Completed = true; }
        }

        /// <summary>One bounded, explicitly uncached query. Cleanup failures are drained, never swallowed.</summary>
        internal static bool TryQuery(ulong itemId, int timeoutMs, out AuthorityFacts facts, out string refusal)
        {
            facts = null;
            Pending pending = new Pending();
            CallResult<SteamUGCQueryCompleted_t> callResult = null;
            UGCQueryHandle_t handle = UGCQueryHandle_t.Invalid;
            try
            {
                PublishedFileId_t[] requested = new PublishedFileId_t[1];
                requested[0] = new PublishedFileId_t(itemId);
                handle = SteamUGC.CreateQueryUGCDetailsRequest(requested, 1u);
                if (handle == UGCQueryHandle_t.Invalid) return Refuse(out refusal, "authority_query_create_failed");
                if (!SteamUGC.SetReturnKeyValueTags(handle, true))
                    return Refuse(out refusal, "authority_setter_failed_SetReturnKeyValueTags");
                if (!SteamUGC.SetReturnLongDescription(handle, true))
                    return Refuse(out refusal, "authority_setter_failed_SetReturnLongDescription");
                if (!SteamUGC.SetAllowCachedResponse(handle, 0u))
                    return Refuse(out refusal, "authority_setter_failed_SetAllowCachedResponse");

                // The handler is registered before Set() binds it to a live call, exactly once.
                callResult = CallResult<SteamUGCQueryCompleted_t>.Create(pending.OnCompleted);
                SteamAPICall_t call = SteamUGC.SendQueryUGCRequest(handle);
                if (call == SteamAPICall_t.Invalid) return Refuse(out refusal, "authority_query_send_failed");
                callResult.Set(call);
                if (!Await(pending, handle, timeoutMs, out refusal)) return false;
                return Read(handle, itemId, pending.Completion.m_bCachedData, out facts, out refusal);
            }
            finally
            {
                Release(callResult, handle);
            }
        }

        /// <summary>Every release step guarded on its own, so one failure cannot mask the rest.</summary>
        private static void Release(CallResult<SteamUGCQueryCompleted_t> callResult, UGCQueryHandle_t handle)
        {
            if (callResult != null)
            {
                try { callResult.Cancel(); } catch (Exception e) { Note("authority_cancel " + e.GetType().FullName); }
                try { callResult.Dispose(); } catch (Exception e) { Note("authority_dispose " + e.GetType().FullName); }
            }
            if (handle != UGCQueryHandle_t.Invalid)
            {
                try { if (!SteamUGC.ReleaseQueryUGCRequest(handle)) Note("authority_release_refused"); }
                catch (Exception e) { Note("authority_release " + e.GetType().FullName); }
            }
        }

        private static bool Await(Pending pending, UGCQueryHandle_t handle, int timeoutMs, out string refusal)
        {
            Stopwatch pump = Stopwatch.StartNew();
            while (!pending.Completed && pump.ElapsedMilliseconds < timeoutMs)
            {
                SteamAPI.RunCallbacks();
                Thread.Sleep(PumpSleepMs);
            }
            if (!pending.Completed) return Refuse(out refusal, "authority_timeout");
            if (pending.IoFailure) return Refuse(out refusal, "authority_io_failure");
            if (pending.Completion.m_handle != handle) return Refuse(out refusal, "authority_handle_mismatch");
            if (pending.Completion.m_eResult != EResult.k_EResultOK)
                return Refuse(out refusal, "authority_query_result_" + pending.Completion.m_eResult.ToString());
            if (pending.Completion.m_unNumResultsReturned != 1u)
                return Refuse(out refusal, "authority_result_count_"
                    + pending.Completion.m_unNumResultsReturned.ToString(CultureInfo.InvariantCulture));
            // A max-age-zero request answered from cache proves nothing about the item's state now.
            if (pending.Completion.m_bCachedData) return Refuse(out refusal, "authority_cached");
            refusal = null;
            return true;
        }

        private static bool Read(UGCQueryHandle_t handle, ulong itemId, bool cached,
            out AuthorityFacts facts, out string refusal)
        {
            facts = null;
            SteamUGCDetails_t details;
            if (!SteamUGC.GetQueryUGCResult(handle, 0u, out details)) return Refuse(out refusal, "authority_read_failed");
            if (details.m_eResult != EResult.k_EResultOK)
                return Refuse(out refusal, "authority_details_result_" + details.m_eResult.ToString());
            if (details.m_nPublishedFileId.m_PublishedFileId != itemId) return Refuse(out refusal, "authority_item_mismatch");
            if (details.m_nConsumerAppID.m_AppId != AppId) return Refuse(out refusal, "authority_consumer_app_mismatch");

            // Raw ids are compared here and nowhere else; neither side is stored or reported.
            ulong account = SteamUser.GetSteamID().m_SteamID;
            AuthorityFacts found = new AuthorityFacts();
            found.OwnerMatches = account != 0UL && details.m_ulSteamIDOwner == account;
            if (!found.OwnerMatches) return Refuse(out refusal, "authority_owner_mismatch");
            found.Visibility = details.m_eVisibility;
            found.TimeUpdated = details.m_rtimeUpdated;
            found.Title = details.m_rgchTitle;
            found.Description = details.m_rgchDescription;
            found.TagsTruncated = details.m_bTagsTruncated;
            found.Cached = cached;
            if (!KeyValueTags(handle, found, out refusal)) return false;
            if (!NormalTags(handle, found, out refusal)) return false;
            facts = found;
            refusal = null;
            return true;
        }

        /// <summary>Refuses an over-bound count, a failed read or a duplicate manifest key.</summary>
        private static bool KeyValueTags(UGCQueryHandle_t handle, AuthorityFacts facts, out string refusal)
        {
            uint count = SteamUGC.GetQueryUGCNumKeyValueTags(handle, 0u);
            if (count > MaxKeyValueTags) return Refuse(out refusal, "authority_kv_over_bound");
            bool haveId = false;
            bool haveVersion = false;
            for (uint index = 0u; index < count; index++)
            {
                string key, value;
                if (!SteamUGC.GetQueryUGCKeyValueTag(handle, 0u, index, out key, KeyMax, out value, ValueMax))
                    return Refuse(out refusal, "authority_kv_read_failed");
                if (string.Equals(key, ManifestIdKey, StringComparison.Ordinal))
                {
                    if (haveId) return Refuse(out refusal, "authority_kv_duplicate_manifest_id");
                    haveId = true;
                    facts.ManifestId = value;
                }
                else if (string.Equals(key, ManifestVersionKey, StringComparison.Ordinal))
                {
                    if (haveVersion) return Refuse(out refusal, "authority_kv_duplicate_manifest_version");
                    haveVersion = true;
                    facts.ManifestVersion = value;
                }
            }
            refusal = null;
            return true;
        }

        /// <summary>The item's normal tags, read one by one; an over-bound count or any failed read refuses.</summary>
        private static bool NormalTags(UGCQueryHandle_t handle, AuthorityFacts facts, out string refusal)
        {
            uint count = SteamUGC.GetQueryUGCNumTags(handle, 0u);
            if (count > MaxTags) return Refuse(out refusal, "authority_tag_over_bound");
            string[] found = new string[count];
            for (uint index = 0u; index < count; index++)
            {
                string tag;
                if (!SteamUGC.GetQueryUGCTag(handle, 0u, index, out tag, TagMax))
                    return Refuse(out refusal, "authority_tag_read_failed");
                found[index] = tag;
            }
            facts.Tags = found;
            refusal = null;
            return true;
        }

        // Canonical tag comparison: both sides trimmed, ordering ignored, exact multiset equality, and
        // case-SENSITIVE - a case change is a real published difference and must not be signed off.
        internal static bool SameTagSet(IList<string> requested, string[] actual)
        {
            if (requested == null || actual == null || requested.Count != actual.Length) return false;
            List<string> remaining = new List<string>(actual.Length);
            for (int i = 0; i < actual.Length; i++)
            { if (actual[i] == null) return false; remaining.Add(actual[i].Trim()); }
            for (int i = 0; i < requested.Count; i++)
            { if (requested[i] == null || !remaining.Remove(requested[i].Trim())) return false; }
            return remaining.Count == 0;
        }

        /// <summary>The login, account and app proved at Prepare are all still exactly current.</summary>
        internal static bool ContextStillExact(ulong owner)
        {
            return SteamUser.BLoggedOn() && SteamUser.GetSteamID().m_SteamID == owner
                && SteamUtils.GetAppID().m_AppId == AppId;
        }

        /// <summary>Pure lane decision: the remote item must already be in this request's lane, and the
        /// manifest it advertises must be one this request is entitled to replace.</summary>
        internal static bool AcceptsLane(UploadRequest request, AuthorityFacts facts, out string refusal)
        {
            if (request == null || facts == null) return Refuse(out refusal, "lane_facts_missing");
            if (!facts.OwnerMatches) return Refuse(out refusal, "lane_owner_mismatch");
            if (request.SteamVisibility != VisibilityPublic && request.SteamVisibility != VisibilityPrivate)
                return Refuse(out refusal, "lane_unknown_visibility");
            if ((int)facts.Visibility != request.SteamVisibility)
                return Refuse(out refusal, "lane_remote_visibility_mismatch");
            if (request.SteamVisibility == VisibilityPrivate && request.Item == PublicAlphaItem)
                return Refuse(out refusal, "lane_private_request_targets_public_item");
            int requestedPatch;
            if (!TryCanonical(request.Version, out requestedPatch))
                return Refuse(out refusal, "lane_requested_version_not_canonical");
            if (request.SteamVisibility == VisibilityPrivate
                && facts.ManifestId == null && facts.ManifestVersion == null)
            {
                // First staging upload: the private item advertises neither manifest key yet.
                refusal = null;
                return true;
            }
            if (!string.Equals(facts.ManifestId, request.ManifestId, StringComparison.Ordinal))
                return Refuse(out refusal, "lane_remote_manifest_id_mismatch");
            int remotePatch;
            if (!TryCanonical(facts.ManifestVersion, out remotePatch))
                return Refuse(out refusal, "lane_remote_version_not_canonical");
            if (request.SteamVisibility == VisibilityPublic && remotePatch >= requestedPatch)
                return Refuse(out refusal, "lane_public_version_not_ahead");
            if (request.SteamVisibility == VisibilityPrivate && remotePatch > requestedPatch)
                return Refuse(out refusal, "lane_private_version_regresses");
            refusal = null;
            return true;
        }

        /// <summary>Canonical means strictly 0.3.x: three numeric parts, no sign, no leading zeros.</summary>
        private static bool TryCanonical(string version, out int patch)
        {
            patch = -1;
            if (version == null || !version.StartsWith(CanonicalPrefix, StringComparison.Ordinal)) return false;
            string tail = version.Substring(CanonicalPrefix.Length);
            int parsed;
            if (!int.TryParse(tail, NumberStyles.None, CultureInfo.InvariantCulture, out parsed)) return false;
            if (!string.Equals(tail, parsed.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)) return false;
            patch = parsed;
            return true;
        }

        /// <summary>Hands the caller any cleanup diagnostics collected since the last drain.</summary>
        internal static string DrainCleanupNotes()
        {
            string value = s_cleanupNotes;
            s_cleanupNotes = null;
            return value;
        }

        private static bool Refuse(out string refusal, string reason)
        { refusal = reason; return false; }

        private static void Note(string line)
        {
            string next = s_cleanupNotes == null ? line : s_cleanupNotes + Environment.NewLine + line;
            if (next.Length > MaxNotesChars) next = next.Substring(0, MaxNotesChars) + "[truncated]";
            s_cleanupNotes = next;
        }
    }
}
