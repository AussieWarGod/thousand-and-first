using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomConversionRules
	{
		// ==================================================================================
		// The draw. Counter-based, on a key that names the settlement, the channel, the person
		// and the milestone, so the answer is a pure function of those four and of nothing that
		// happened in between. An ordinary pseudorandom call could not promise that: its cursor
		// depends on every unrelated roll made since the game started, so a reload would convert
		// somebody the last session left alone.
		// ==================================================================================

		/// <summary>
		/// Rules version pinned into every conversion draw's <see cref="SemanticEventKey"/>. The
		/// key owns its rules version forever, so this moves only if the draw itself is redefined
		/// in a way that must not compare equal to what came before &mdash; which would re-answer
		/// every milestone standing unconverted in every save.
		/// </summary>
		private const int ConversionRulesVersion = 1;

		/// <summary>Only draw made on a conversion key. Named rather than passed as a bare
		/// literal because a second purpose on this key would have to pick the next index, not
		/// reuse this one.</summary>
		private const uint ConversionDrawIndex = 0u;

		/// <summary>
		/// Fixed, all-zero seed, for the reason <c>KingdomChronicle</c> gives at length: domain
		/// separation comes entirely from the settlement id, stream, kind and ordinal baked into
		/// the key, and whether a particular settler turns does not need to be unguessable.
		/// </summary>
		private static readonly KernelSeed128 ConversionSeed = default(KernelSeed128);

		private const string StreamPrefix = "taf:conversion:";

		private const string StreamSuffix = ":v1";

		/// <summary>
		/// Folds one settler's roll name into the frozen <c>taf:</c> semantic-id grammar so it can
		/// name its own ordinal lane. The person belongs in the STREAM rather than the ordinal:
		/// the ordinal is the milestone, and two settlers at the same milestone in the same city
		/// must not be forced to share one answer.
		/// <para>
		/// Not supported API (STANDARDS.md &sect;9): the grammar it folds into is frozen, but
		/// which string this mod hands the kernel is ours to change.
		/// </para>
		/// </summary>
		/// <param name="ResidentName">The name the roll carries them under. Null and blank yield
		/// the lane an unnamed settler would draw on, which nothing in production ever asks for
		/// &mdash; conversion is keyed to the roll, so an unnamed resident never enters it.</param>
		/// <returns>An id that always satisfies the <c>taf:</c> grammar. Never null.</returns>
		internal static string ResidentStream(string ResidentName)
		{
			StringBuilder builder = new StringBuilder(StreamPrefix);
			int room = KernelSemanticIdBudget - StreamPrefix.Length - StreamSuffix.Length;
			if (!string.IsNullOrEmpty(ResidentName))
			{
				foreach (char c in ResidentName)
				{
					if (builder.Length - StreamPrefix.Length >= room)
					{
						break;
					}
					if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-')
					{
						builder.Append(c);
					}
					else if (c >= 'A' && c <= 'Z')
					{
						builder.Append((char)(c + 32));
					}
					else
					{
						builder.Append('-');
					}
				}
			}
			if (builder.Length == StreamPrefix.Length)
			{
				builder.Append("unnamed");
			}
			builder.Append(StreamSuffix);
			return builder.ToString();
		}

		/// <summary>The byte budget <c>KernelSemanticId</c> allows an id. Stated here rather than
		/// read from the kernel because that constant is the kernel's own and this file must fold
		/// to fit it, not reach into it.</summary>
		private const int KernelSemanticIdBudget = 128;

		/// <summary>
		/// Whether this milestone turns this settler.
		/// <para>
		/// Deterministic in the settlement, the channel, the person and the milestone together:
		/// the same milestone always answers the same way, in any process, forever. Asking twice
		/// in one pass, reloading the save, or converting somebody else first cannot change it.
		/// </para>
		/// </summary>
		/// <param name="SettlementId">The settlement's kernel id
		/// (<c>KingdomChronicle.SettlementId</c>).</param>
		/// <param name="Channel">Which channel is asking. Each has its own kind code, so a shrine
		/// and a household working on the same person at the same milestone draw independently.
		/// </param>
		/// <param name="ResidentName">The name the roll carries them under.</param>
		/// <param name="Shared">Shared living accumulated toward the creed in question.</param>
		/// <returns>False when the settler is short of a milestone, and false when the kernel
		/// refuses the draw &mdash; a malformed id, or a machine whose crypto provider is failing.
		/// Failing closed is the only safe direction here: a fault must never be able to change
		/// what somebody believes.</returns>
		public static bool Converts(string SettlementId, ConversionChannel Channel, string ResidentName, int Shared)
		{
			if (!AtMilestone(Shared))
			{
				return false;
			}
			SemanticEventKey key;
			KernelFaultCode fault;
			if (!SemanticEventKey.TryCreate(ConversionRulesVersion, SettlementId, ResidentStream(ResidentName), (uint)Channel, Milestone(Shared), out key, out fault))
			{
				return false;
			}
			ulong value;
			if (!CounterRandom.TryDrawBelow(ConversionSeed, key, ConversionDrawIndex, 100uL, out value, out fault))
			{
				return false;
			}
			return (int)value < ConversionChancePercent;
		}

	}
}
