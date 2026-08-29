using System;
using ThousandAndFirst.Api;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomExternalOwnershipBindingRuntime
	{
		internal const string StageProperty = "r_TAF_ExternalOwnerStage_v1";
		internal const string StageAuthorityProperty = "r_TAF_ExternalOwnerStageAuthority_v1";
		internal const string BindingProperty = "r_TAF_ExternalOwnerBinding_v1";
		internal const string BindingAuthorityProperty = "r_TAF_ExternalOwnerBindingAuthority_v1";
		private const string ContestedProperty = "r_TAF_ExternalOwnerContested_v1";
		private const string ToldProperty = "r_TAF_ExternalOwnerContestedTold_v1";
		internal const string ClaimAuthorityPrefix = "taf-claim-bind-v2:";

		internal static string ClaimAuthority(string Realm, string ZoneId)
		{
			if (string.IsNullOrEmpty(Realm) || string.IsNullOrEmpty(ZoneId)) return null;
			return ClaimAuthorityPrefix + Realm.Length + ":" + Realm + ":" + ZoneId;
		}

		public static bool TryStage(Zone Site, string Authority, string Encoded,
			out string Failure)
		{
			Failure = null;
			if (!ValidForSite(Site, Authority, Encoded, out Failure)) return false;
			string oldAuthority = Site.GetZoneProperty(StageAuthorityProperty, null);
			string oldBinding = Site.GetZoneProperty(StageProperty, null);
			if (!KingdomExternalOwnershipRules.PairAbsentOrExact(
				oldAuthority, oldBinding, Authority, Encoded, RequireEvidence: false))
				return Fail("Another external-owner receipt already reserves this ground.", out Failure);
			Site.SetZoneProperty(StageProperty, Encoded);
			// The binding is the self-describing half. Write it first so direct-founder
			// recovery can digest-prove and repair a missing authority half after a save cut.
			Site.SetZoneProperty(StageAuthorityProperty, Authority);
			return Site.GetZoneProperty(StageAuthorityProperty, null) == Authority
				&& Site.GetZoneProperty(StageProperty, null) == Encoded
				|| Fail("External-owner staging did not read back exactly.", out Failure);
		}

		public static bool RevalidateStage(Zone Site, string Authority, string Encoded,
			out string Failure)
		{
			Failure = null;
			if (!ValidForSite(Site, Authority, Encoded, out Failure)
				|| Site.GetZoneProperty(StageAuthorityProperty, null) != Authority
				|| Site.GetZoneProperty(StageProperty, null) != Encoded)
				return Fail("External-owner staging receipt changed.", out Failure);
			KingdomExternalOwnershipRules.TryDecode(Encoded, out var binding);
			string provider = binding.Mode == KingdomExternalOwnershipMode.Bind
				? binding.Observation.ProviderId : null;
			KingdomExternalOwnershipReading reading = KingdomExternalOwnership.Inspect(Site, provider);
			KingdomExternalBindingVerdict verdict = KingdomExternalOwnershipRules.Judge(binding, reading);
			if (verdict == KingdomExternalBindingVerdict.Open
				|| verdict == KingdomExternalBindingVerdict.Exact) return true;
			return Fail(Describe(verdict, reading), out Failure);
		}

		public static bool TryCommit(Zone Site, string Authority, string Encoded,
			out string Failure)
		{
			Failure = null;
			if (!RevalidateStage(Site, Authority, Encoded, out Failure)) return false;
			string oldAuthority = Site.GetZoneProperty(BindingAuthorityProperty, null);
			string oldBinding = Site.GetZoneProperty(BindingProperty, null);
			if (!KingdomExternalOwnershipRules.PairAbsentOrExact(
				oldAuthority, oldBinding, Authority, Encoded, RequireEvidence: false))
				return Fail("Another permanent external-owner binding already stands here.", out Failure);
			Site.SetZoneProperty(BindingProperty, Encoded);
			Site.SetZoneProperty(BindingAuthorityProperty, Authority);
			return Site.GetZoneProperty(BindingAuthorityProperty, null) == Authority
				&& Site.GetZoneProperty(BindingProperty, null) == Encoded
				|| Fail("Permanent external-owner binding did not read back exactly.", out Failure);
		}

		public static bool FinishStage(Zone Site, string Authority, string Encoded)
		{
			if (Site == null || Site.GetZoneProperty(StageAuthorityProperty, null) != Authority
				|| Site.GetZoneProperty(StageProperty, null) != Encoded) return false;
			Site.RemoveZoneProperty(StageAuthorityProperty);
			Site.RemoveZoneProperty(StageProperty);
			return !Site.HasZoneProperty(StageAuthorityProperty)
				&& !Site.HasZoneProperty(StageProperty);
		}

		internal static bool HasStage(Zone Site)
		{
			return Site != null && (Site.HasZoneProperty(StageAuthorityProperty)
				|| Site.HasZoneProperty(StageProperty));
		}

		internal static bool StageMatches(Zone Site, string Authority, string Encoded)
		{
			return Site != null && !string.IsNullOrEmpty(Authority)
				&& Site.GetZoneProperty(StageAuthorityProperty, null) == Authority
				&& Site.GetZoneProperty(StageProperty, null) == Encoded;
		}

		internal static bool TryReadStage(Zone Site, out string Authority,
			out string Encoded)
		{
			Authority = Site?.GetZoneProperty(StageAuthorityProperty, null);
			Encoded = Site?.GetZoneProperty(StageProperty, null);
			return !string.IsNullOrEmpty(Authority)
				&& KingdomExternalOwnershipRules.TryDecode(Encoded, out var binding);
		}

		internal static bool CompletionMatches(Zone Site, string Authority, string Encoded)
		{
			if (!StageMatches(Site, Authority, Encoded)
				|| !KingdomExternalOwnershipRules.TryDecode(Encoded, out var binding)) return false;
			if (Site.GetZoneProperty(BindingAuthorityProperty, null) != Authority
				|| Site.GetZoneProperty(BindingProperty, null) != Encoded) return false;
			string provider = binding.Mode == KingdomExternalOwnershipMode.Bind
				? binding.Observation.ProviderId : null;
			KingdomExternalOwnershipReading reading =
				KingdomExternalOwnership.Inspect(Site, provider);
			KingdomExternalBindingVerdict verdict =
				KingdomExternalOwnershipRules.Judge(binding, reading);
			return verdict == KingdomExternalBindingVerdict.Open
				|| verdict == KingdomExternalBindingVerdict.Exact;
		}

		public static bool RollbackStage(Zone Site, string Authority, string Encoded,
			bool PublicationObserved)
		{
			if (Site == null || PublicationObserved) return false;
			string stageAuthority = Site.GetZoneProperty(StageAuthorityProperty, null);
			string stageBinding = Site.GetZoneProperty(StageProperty, null);
			if (!KingdomExternalOwnershipRules.PairAbsentOrExact(stageAuthority,
				stageBinding, Authority, Encoded, RequireEvidence: true)) return false;
			string permanentAuthority = Site.GetZoneProperty(BindingAuthorityProperty, null);
			string permanentBinding = Site.GetZoneProperty(BindingProperty, null);
			if (!KingdomExternalOwnershipRules.PairAbsentOrExact(permanentAuthority,
				permanentBinding, Authority, Encoded, RequireEvidence: false)) return false;
			Site.RemoveZoneProperty(BindingAuthorityProperty);
			Site.RemoveZoneProperty(BindingProperty);
			Site.RemoveZoneProperty(StageAuthorityProperty);
			Site.RemoveZoneProperty(StageProperty);
			return !Site.HasZoneProperty(StageAuthorityProperty)
				&& !Site.HasZoneProperty(StageProperty)
				&& !Site.HasZoneProperty(BindingAuthorityProperty)
				&& !Site.HasZoneProperty(BindingProperty);
		}

		public static bool CanOperate(Zone Site, out string Failure)
		{
			Failure = null;
			if (Site == null) return Fail("The claimed ground is not loaded.", out Failure);
			string encoded = Site.GetZoneProperty(BindingProperty, null);
			string authority = Site.GetZoneProperty(BindingAuthorityProperty, null);
			KingdomExternalOwnershipReading reading;
			KingdomExternalBindingVerdict verdict;
			if (string.IsNullOrEmpty(encoded) && string.IsNullOrEmpty(authority))
			{
				reading = KingdomExternalOwnership.Inspect(Site);
				if (reading.State == KingdomExternalOwnershipState.Unowned) return ClearContest(Site);
				verdict = reading.State == KingdomExternalOwnershipState.ProviderFailed
					? KingdomExternalBindingVerdict.ProviderUnavailable
					: KingdomExternalBindingVerdict.Diverged;
			}
			else if (string.IsNullOrEmpty(authority)
				|| !KingdomExternalOwnershipRules.TryDecode(encoded, out var binding))
			{
				reading = null;
				verdict = KingdomExternalBindingVerdict.Malformed;
			}
			else
			{
				string provider = binding.Mode == KingdomExternalOwnershipMode.Bind
					? binding.Observation.ProviderId : null;
				reading = KingdomExternalOwnership.Inspect(Site, provider);
				verdict = KingdomExternalOwnershipRules.Judge(binding, reading);
				if (verdict == KingdomExternalBindingVerdict.Open
					|| verdict == KingdomExternalBindingVerdict.Exact) return ClearContest(Site);
			}
			Failure = Describe(verdict, reading);
			Site.SetZoneProperty(ContestedProperty, Failure);
			if (Site.GetZoneProperty(ToldProperty, null) != Failure)
			{
				Site.SetZoneProperty(ToldProperty, Failure);
				MessageQueue.AddPlayerMessage("{{R|Civic work is paused here: " + Failure + "}}");
			}
			return false;
		}

		internal static void FinishPublishedClaimStage(Zone Site)
		{
			if (!TryReadStage(Site, out string authority, out string encoded)
				|| !authority.StartsWith(ClaimAuthorityPrefix, StringComparison.Ordinal)) return;
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			if (system != null && authority == ClaimAuthority(
					system.KingdomFactionName, Site.ZoneID)
				&& system.ClaimedZones.Contains(Site.ZoneID)
				&& CompletionMatches(Site, authority, encoded))
				FinishStage(Site, authority, encoded);
		}

		public static string Status(Zone Site)
		{
			if (Site == null) return "external ownership: ground unavailable";
			if (CanOperate(Site, out string failure))
			{
				string encoded = Site.GetZoneProperty(BindingProperty, null);
				if (!KingdomExternalOwnershipRules.TryDecode(encoded, out var binding)
					|| binding.Mode == KingdomExternalOwnershipMode.None)
					return "external ownership: none observed";
				return "external ownership: bound to " + binding.Observation.ProviderId + " "
					+ binding.Observation.OwnerGuid;
			}
			return "external ownership: contested (" + failure + ")";
		}

		private static bool ValidForSite(Zone Site, string Authority, string Encoded,
			out string Failure)
		{
			Failure = null;
			if (Site == null || string.IsNullOrEmpty(Authority)
				|| !KingdomExternalOwnershipRules.TryDecode(Encoded, out var binding))
				return Fail("External-owner receipt is malformed.", out Failure);
			if (binding.Mode == KingdomExternalOwnershipMode.Bind
				&& binding.Observation.ZoneId != Site.ZoneID)
				return Fail("External-owner receipt names different ground.", out Failure);
			return true;
		}

		private static bool ClearContest(Zone Site)
		{
			Site.RemoveZoneProperty(ContestedProperty);
			Site.RemoveZoneProperty(ToldProperty);
			return true;
		}

		private static string Describe(KingdomExternalBindingVerdict Verdict,
			KingdomExternalOwnershipReading Reading)
		{
			if (!string.IsNullOrEmpty(Reading?.Failure)) return Reading.Failure;
			switch (Verdict)
			{
			case KingdomExternalBindingVerdict.ProviderUnavailable:
				return "required ownership provider is unavailable";
			case KingdomExternalBindingVerdict.Diverged:
				return "observed external ownership differs from the accepted binding";
			default:
				return "external ownership receipt is malformed";
			}
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
