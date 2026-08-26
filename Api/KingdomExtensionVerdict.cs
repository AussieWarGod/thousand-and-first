using System;
using System.Text;

namespace ThousandAndFirst.Api
{
	/// <summary>
	/// Why a marked extension was or was not admitted. LIVING-CITY-ARCHITECTURE &sect;6.6:
	/// <i>"refused by mod name, on screen and in the log &hellip; never silently skipped and never
	/// half-loaded."</i>
	/// <para>
	/// Values are appended and never reordered: a refusal is quoted in a log line a player pastes
	/// into a bug report, and the ordinal is what a test pins.
	/// </para>
	/// </summary>
	public enum KingdomExtensionVerdict : byte
	{
		/// <summary>Admitted. The extension runs under the invariants in MODDING.md.</summary>
		Accepted = 0,

		/// <summary>The type carries the marker but declares no API version at all.</summary>
		RefusedNoVersion = 1,

		/// <summary>Built against a later API than this copy of the mod publishes.</summary>
		RefusedAhead = 2,

		/// <summary>Built against an earlier API than this copy of the mod publishes.</summary>
		RefusedBehind = 3,

		/// <summary>Marked, but implements none of the published contracts.</summary>
		RefusedNoContract = 4,

		/// <summary>Nothing to name in the refusal, which is itself a refusal: a contract that
		/// cannot say whose fault a fault is has no owner.</summary>
		RefusedUnnamed = 5,

		/// <summary>The extension's own constructor or version property threw. Distinct from
		/// <see cref="RefusedNoVersion"/> on purpose: telling a modder their class "declares no API
		/// version" when what actually happened is that it threw sends them to the wrong line.</summary>
		RefusedThrew = 6,

		/// <summary>Another installed manifest ID maps to the same bounded durable namespace. Both
		/// owners are refused so load order cannot decide which mod receives shared state.</summary>
		RefusedNamespaceCollision = 7
	}

}
