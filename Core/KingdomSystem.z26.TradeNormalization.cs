using System;
using System.Collections.Generic;
using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		private static bool TryFirstClaimEvidence(List<string> Claims, out string ZoneId)
		{
			ZoneId = null;
			if (Claims == null || Claims.Count < 1 || Claims.Count > 4096) return false;
			string first = Claims[0];
			if (string.IsNullOrEmpty(first) || first.Length > 512) return false;
			for (int i = 0; i < Claims.Count; i++)
				if (string.IsNullOrEmpty(Claims[i]) || Claims[i].Length > 512) return false;
			ZoneId = first;
			return true;
		}

		private bool PendingSettlementTupleValid(out string Failure)
		{
			Failure = null;
			bool any = !string.IsNullOrEmpty(PendingSettlementId) ||
				!string.IsNullOrEmpty(PendingSettlementTransactionId) ||
				!string.IsNullOrEmpty(PendingSettlementZoneId) ||
				!string.IsNullOrEmpty(PendingSettlementAuthority);
			if (!any) return true;
			string expected;
			KingdomIdentityFault fault;
			KingdomFoundingAuthority authority;
			if (string.IsNullOrEmpty(PendingSettlementId) ||
				string.IsNullOrEmpty(PendingSettlementZoneId) ||
				PendingSettlementZoneId.Length > 512 ||
				string.IsNullOrEmpty(PendingSettlementAuthority) ||
				PendingSettlementAuthority.Length > 4096 ||
				!KingdomIdentityRules.TryMintSettlement(RealmId,
					PendingSettlementTransactionId, out expected, out fault) ||
				expected != PendingSettlementId ||
				!KingdomFoundingTransactionRules.TryParseAuthority(
					PendingSettlementAuthority, out authority) ||
				authority.Kind != KingdomFoundingKind.SecondCity ||
				authority.TransactionID != PendingSettlementTransactionId ||
				authority.ZoneID != PendingSettlementZoneId ||
				authority.RealmFaction != KingdomFactionName)
			{
				Failure = "pending settlement identity evidence is partial or malformed";
				return false;
			}
			return true;
		}

		private bool NewIdentityEvidenceEmpty()
		{
			return string.IsNullOrEmpty(RealmId) && RealmIdentityVersion == 0 &&
				RealmIdentityOrigin == KingdomIdentityOrigin.None &&
				string.IsNullOrEmpty(RealmIdentityTransactionId) &&
				string.IsNullOrEmpty(RealmIdentityLegacyFaction) &&
				RealmIdentityFoundedTick == 0L && RealmIdentitySeedHigh == 0UL &&
				RealmIdentitySeedLow == 0UL &&
				string.IsNullOrEmpty(RealmIdentityFirstClaimedZone) &&
				string.IsNullOrEmpty(IdentityFault) && SettlementIdentityVersion == 0 &&
				SettlementIdentityOrigin == KingdomIdentityOrigin.None &&
				string.IsNullOrEmpty(SettlementIdentityTransactionId) &&
				SettlementIdentityFoundedTick == 0L &&
				string.IsNullOrEmpty(SettlementIdentityFirstClaimedZone) &&
				string.IsNullOrEmpty(SettlementIdentityLegacyId);
		}

		/// <summary>Binds Trade only from the complete immutable topology. Positional name rows
		/// remain quarantined evidence and are never promoted into live charter/manifest authority.</summary>
		private void NormalizeTradeBook()
		{
			try
			{
				if (TradeBook == null)
				{
					TradeBook = new KingdomTradeBook();
				}
				// Builds before Manifest became a derived API projection could save a distinct
				// mutable-name row here. Move only a mismatch into its own evidence slot before
				// refreshing the public field; exact projections must not quarantine their authority.
				// Manifest is a frozen save-wire projection. Reading it here is the one migration
				// boundary that compares old bytes with current Trade authority.
#pragma warning disable 618
				if (Manifest != null && LegacyManifestEvidence == null
					&& !KingdomTrade.LegacyManifestMatches(Manifest, TradeBook.Manifest))
				{
					LegacyManifestEvidence = KingdomTrade.LegacyManifestSnapshot(Manifest);
				}
#pragma warning restore 618
				bool hasLegacyTrade = ActiveDealKeys.Count > 0 || ActiveDealFactions.Count > 0
					|| DealNextTicks.Count > 0 || LegacyManifestEvidence != null;
				// Detect the dual graph before Trade recovery can settle or retire anything. Both
				// source graphs remain present as quarantined evidence; neither may be normalized
				// into authority first.
				if (hasLegacyTrade)
				{
					if (TradeBook.FormatVersion == KingdomTradeRules.CurrentFormatVersion &&
						TradeBook.SchemaState == KingdomTradeSchemaState.Compatible)
						KingdomTradeRules.QuarantineBook(TradeBook,
							"legacy name-based trade rows were preserved but cannot become live authority");
					return;
				}
				KingdomTradeRules.Normalize(TradeBook);
				// Unknown-future and quarantined books are evidence, not authority this build may
				// reinterpret. Preserve both the named-field graph and the legacy source rows.
				if (TradeBook.FormatVersion != KingdomTradeRules.CurrentFormatVersion ||
					TradeBook.SchemaState != KingdomTradeSchemaState.Compatible)
				{
					return;
				}
				if (ExiledRealmArchive != null &&
					ExiledRealmArchive.Phase != KingdomRealmArchivePhase.None) return;
				if (!Founded || !string.IsNullOrEmpty(IdentityFault)) return;
				if (!PendingSettlementIdentityAbsent())
				{
					// Paired second-city coordinator owns all pending topology changes. Load-time
					// normalization may recover Trade receipts, but never expand or contract Trade
					// alone across a save cut.
					return;
				}
				List<string> exact;
				string failure;
				if (!TryRetainedSettlementIds(RequirePublishedClaims: true,
					IncludePending: false, out exact, out failure)) return;
				if (!TradeBook.IdentityBound)
				{
					// Trade may be one callback ahead of Core after atomically closing exile.
					// Preserve that exact unbound receipt for Exile recovery; any malformed or
					// wrong-topology archive evidence is quarantine, never fresh bind authority.
					if (TradeBook.Archives != null && TradeBook.Archives.Count > 0)
					{
						if (KingdomTradeRules.TryAuthenticateExactExileClosedTick(TradeBook,
							RealmId, exact, out long ignoredClosedTick, out failure)) return;
						KingdomTradeRules.QuarantineBook(TradeBook,
							failure ?? "unbound Trade exile receipt cannot be authenticated");
						return;
					}
					if (!KingdomTradeRules.BindExactIdentity(TradeBook, RealmId, exact,
						out failure)) return;
				}
				KingdomTradeRules.BindExactIdentity(TradeBook, RealmId, exact,
					out failure);
			}
			finally
			{
				SynchronizeLegacyManifestProjection();
			}
		}

		/// <summary>Refreshes obsolete serialized API surface from exact Trade authority.</summary>
		internal void SynchronizeLegacyManifestProjection()
		{
#pragma warning disable 618
			Manifest = KingdomTrade.LegacyManifestSnapshot(TradeBook?.Manifest);
#pragma warning restore 618
		}

	}
}
