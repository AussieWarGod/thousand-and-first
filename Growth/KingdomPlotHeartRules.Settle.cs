using System;
using System.Globalization;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPlots
	{
		/// <summary>
		/// The heart's own rung effects, settled once, for every route that can raise a rung.
		/// <para>
		/// Extracted verbatim from the plot route's completion so the improvement route &mdash;
		/// which is the ONLY route a rung above the first can climb by
		/// (<c>KingdomPlot2.10.Commission.cs</c> refuses to commission one) &mdash; settles the
		/// same effects in the same order rather than keeping a second copy that drifts.
		/// </para>
		/// <para>
		/// The functional stamp is inspectable and idempotent; the ceremony callback is
		/// AT MOST ONCE through the 0/1/2 marker, so an interrupted Attempting marker becomes
		/// honestly lost rather than firing twice. The caller's own exact endpoint and custody
		/// proof is passed in and re-asked after the callback, because what "this is still the
		/// same building on the same job on the same ground" means is the caller's question:
		/// the plot route's proof reads its plot final-root custody, and the improvement route's
		/// reads its successor handover. Neither is bypassed for the other.
		/// </para>
		/// </summary>
		/// <param name="TargetKey">The design key the job raised. Anything off the heart ladder
		/// settles nothing and is not a refusal: only a heart plot raising a heart rung has rung
		/// effects owed.</param>
		/// <param name="Prove">The caller's exact endpoint and custody proof, re-asked after the
		/// ceremony callback. A caller that cannot prove its own endpoint refuses here.</param>
		/// <returns>False only when a rung WAS owed and could not be settled exactly, so the
		/// caller keeps its receipt retryable. True when the effects settled, and true when
		/// nothing was owed.</returns>
		internal static bool TrySettleHeartRung(KingdomSystem System, Zone Z,
			GameObject Building, string TargetKey, Func<bool> Prove)
		{
			if (System == null || !System.Founded || Z == null || Prove == null
				|| !GameObject.Validate(Building)) return false;
			if (Building.GetIntProperty(HeartPlotProperty) != 1) return true;
			int rung = KingdomPlotRules.HeartRungOf(TargetKey);
			if (rung <= 0) return true;
			// A rung is never stamped backward: see KingdomPlotRules.RungMaySettle. A refusal here
			// stops the receipt closing, and on the plot route it would otherwise stop it with no
			// trace at all, so the one thing this branch does besides refuse is say which ground,
			// which standing rung and which receipt could not agree.
			int standing = HeartRung(Z);
			if (!KingdomPlotRules.RungMaySettle(rung, standing))
			{
				KingdomLog.Log("heart rung: refused a receipt below the standing rung: zone "
					+ Z.ZoneID + "; standing " + standing.ToString(CultureInfo.InvariantCulture)
					+ "; receipt " + rung.ToString(CultureInfo.InvariantCulture)
					+ "; design " + (TargetKey ?? "(none)"));
				return false;
			}
			string wire = rung.ToString(CultureInfo.InvariantCulture);
			Z.SetZoneProperty(HeartRungProperty, wire);
			if (Z.GetZoneProperty(HeartRungProperty, null) != wire) return false;
			int state = Building.GetIntProperty(HeartEffectProperty);
			if (state < 0 || state > 2) return false;
			if (state == 0)
			{
				Building.SetIntProperty(HeartEffectProperty, 1);
				if (Building.GetIntProperty(HeartEffectProperty) != 1) return false;
				bool callbackReturned = false;
				try
				{
					KingdomCeremonyHeart.OnRungRaised(System, Z, TargetKey, true);
					callbackReturned = true;
				}
				catch (Exception) { }
				if (!Prove()) return false;
				if (!callbackReturned) return false;
			}
			if (Building.GetIntProperty(HeartEffectProperty) == 1)
				Building.SetIntProperty(HeartEffectProperty, 2);
			if (Building.GetIntProperty(HeartEffectProperty) != 2) return false;
			// The rung's own water. Idempotent and only ever upward, so an interrupted ceremony
			// above costs the basin nothing: the next load or activation repeats it. Guarded like
			// every other callback here: a refusal must skip one idempotent step, never abort the
			// settlement pass after the rung stamp landed.
			KingdomSystem.Guard("heart basin capacity", delegate
			{
				ReconcileBasinCapacity(System, Building, Z);
			});
			// OUTSIDE the guard on purpose: the guard must keep swallowing a basin failure, which
			// is one idempotent step the next load repeats, but a TORN ENDPOINT is not that. The
			// basin's own widening refreshes the active survey, so the last callback is a callback
			// too and the endpoint is proved once more before this says the rung settled. A
			// refusal here leaves the honest state behind it: the rung is stamped, the ceremony
			// marker is settled, so the retry quarantines without ever re-firing the ceremony.
			if (!Prove()) return false;
			return true;
		}
	}
}
