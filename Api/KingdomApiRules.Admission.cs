using System;
using System.Text;

namespace ThousandAndFirst.Api
{
	public static partial class KingdomApiRules
	{
		/// <summary>
		/// Whether a marked type may register, and if not, why.
		/// <para>
		/// The order is frozen so that combined-invalid input cannot vary by implementation:
		/// no name, then no contract, then the version. A nameless extension is judged first
		/// because every other refusal is reported <i>by mod name</i>, and a refusal that cannot
		/// name its owner is the one failure this contract exists to prevent.
		/// </para>
		/// </summary>
		/// <param name="ModName">The owning mod's immutable manifest ID.</param>
		/// <param name="DeclaredVersion">What the extension says it was built against.</param>
		/// <param name="ImplementsContract">Whether it implements at least one published
		/// contract interface.</param>
		/// <returns>The verdict. Only <see cref="KingdomExtensionVerdict.Accepted"/> admits.</returns>
		public static KingdomExtensionVerdict Judge(string ModName, int DeclaredVersion, bool ImplementsContract)
		{
			return Judge(ModName, DeclaredVersion, ImplementsContract, MinSupportedVersion);
		}

		/// <summary>Version judgment for a specific contract family. Additive v3 behaviour cannot be
		/// claimed by a source declaring v1, while genuine v1 ask/happening sources remain admitted.</summary>
		/// <param name="ModName">Owning mod immutable manifest ID.</param>
		/// <param name="DeclaredVersion">Declared API version.</param>
		/// <param name="ImplementsContract">Whether any published contract is implemented.</param>
		/// <param name="MinimumContractVersion">First API version containing every implemented
		/// contract on this type.</param>
		/// <returns>Registration verdict.</returns>
		public static KingdomExtensionVerdict Judge(string ModName, int DeclaredVersion,
			bool ImplementsContract, int MinimumContractVersion)
		{
			if (string.IsNullOrEmpty(Slug(ModName)))
			{
				return KingdomExtensionVerdict.RefusedUnnamed;
			}
			if (!ImplementsContract)
			{
				return KingdomExtensionVerdict.RefusedNoContract;
			}
			if (DeclaredVersion <= 0)
			{
				return KingdomExtensionVerdict.RefusedNoVersion;
			}
			if (DeclaredVersion > Version)
			{
				return KingdomExtensionVerdict.RefusedAhead;
			}
			int required = MinimumContractVersion < MinSupportedVersion
				? MinSupportedVersion : MinimumContractVersion;
			if (DeclaredVersion < required)
			{
				return KingdomExtensionVerdict.RefusedBehind;
			}
			return KingdomExtensionVerdict.Accepted;
		}

		/// <summary>
		/// What the log and the message line say about a refusal. Names the mod, the version it
		/// wanted, and the version we are &mdash; the three facts a player pasting a line into a
		/// bug report needs, and the three &sect;6.6 requires.
		/// </summary>
		/// <returns>The line, or empty for <see cref="KingdomExtensionVerdict.Accepted"/>.</returns>
		public static string RefusalLine(KingdomExtensionVerdict Verdict, string ModName, int DeclaredVersion)
		{
			return RefusalLine(Verdict, ModName, DeclaredVersion, MinSupportedVersion);
		}

		/// <summary>Refusal prose using a specific contract family's minimum version.</summary>
		/// <param name="Verdict">Registration verdict.</param>
		/// <param name="ModName">Owning mod immutable manifest ID.</param>
		/// <param name="DeclaredVersion">Declared API version.</param>
		/// <param name="MinimumContractVersion">First version containing every implemented contract.</param>
		/// <returns>Founder-facing refusal line, or empty for acceptance.</returns>
		public static string RefusalLine(KingdomExtensionVerdict Verdict, string ModName,
			int DeclaredVersion, int MinimumContractVersion)
		{
			string who = string.IsNullOrEmpty(ModName) ? "an unnamed mod" : ModName;
			switch (Verdict)
			{
			case KingdomExtensionVerdict.Accepted:
				return "";
			case KingdomExtensionVerdict.RefusedUnnamed:
				return "A kingdom extension was refused: it belongs to no mod this game can name, so a fault in it could never be attributed. Nothing of it is loaded.";
			case KingdomExtensionVerdict.RefusedNoContract:
				return who + " marks a type as a kingdom extension that implements none of the published contracts. Nothing of it is loaded.";
			case KingdomExtensionVerdict.RefusedNoVersion:
				return who + " marks a kingdom extension that declares no API version. The kingdom API is version " + Version + ". Nothing of it is loaded.";
			case KingdomExtensionVerdict.RefusedAhead:
				return who + " was built against kingdom API version " + DeclaredVersion + "; this copy of The Thousand and First publishes version " + Version + ". Update the mod, or update this one. Nothing of it is loaded.";
			case KingdomExtensionVerdict.RefusedBehind:
				int required = MinimumContractVersion < MinSupportedVersion
					? MinSupportedVersion : MinimumContractVersion;
				return who + " was built against kingdom API version " + DeclaredVersion
					+ "; this copy of The Thousand and First publishes version " + Version
					+ " and its chosen contracts require version " + required
					+ ". The mod needs an update. Nothing of it is loaded.";
			case KingdomExtensionVerdict.RefusedThrew:
				return who + " threw while the kingdom API was building its extension. The fault is in that mod and is in the log; nothing of it is loaded, and every other extension still runs.";
			case KingdomExtensionVerdict.RefusedNamespaceCollision:
				return who + " has the same bounded kingdom namespace as another installed manifest ID. Both owners are refused so load order cannot transfer durable state; change one manifest ID before publishing it.";
			default:
				return who + " was refused by the kingdom API. Nothing of it is loaded.";
			}
		}

	}
}
