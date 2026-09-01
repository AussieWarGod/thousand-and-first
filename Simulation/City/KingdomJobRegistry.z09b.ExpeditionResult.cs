using System;

namespace ThousandAndFirst.Simulation.City
{
	internal sealed partial class KingdomJobTable
	{
		/// <summary>CAS-like pure transition for the expedition outbox. Exact replay returns the
		/// stored receipt; any later attempt to substitute death or another disposition is refused.</summary>
		internal bool TryPrepareExpeditionResult(KingdomJobRow expected, int outcomeCode,
			long resolutionTick, string resolutionZoneId,
			KingdomExpeditionDeedDisposition deedDisposition, string deedPolityId,
			string deedCauseRef, string deedFigureRef, out KingdomJobTable next,
			out KingdomJobRow receipt, out bool changed, out KingdomCityFault fault)
		{
			next = null; receipt = default(KingdomJobRow); changed = false;
			if (!TryGet(expected.JobId, out KingdomJobRow current))
			{
				fault = KingdomCityFault.UnknownBinding;
				return false;
			}
			KingdomJobRow requested = current.WithExpeditionResolution(outcomeCode,
				resolutionTick, resolutionZoneId, deedDisposition, deedPolityId,
				deedCauseRef, deedFigureRef);
			if (current.OriginCode == (int)KingdomExpeditionPhase.ResolutionPrepared)
			{
				if (!Exact(current, requested))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				next = this; receipt = current; fault = KingdomCityFault.None;
				return true;
			}
			if (!Exact(current, expected) || resolutionTick <= current.StartTick
				|| string.IsNullOrEmpty(resolutionZoneId)
				|| !KingdomJobRules.ValidExpeditionResultReceipt(requested))
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			if (!TryReplace(requested, out next, out fault)) return false;
			receipt = requested; changed = true; fault = KingdomCityFault.None;
			return true;
		}
	}
}
