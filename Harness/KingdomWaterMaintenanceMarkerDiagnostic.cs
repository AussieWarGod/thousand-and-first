using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	// Failure diagnostics only. This snapshot never grants authority or changes the original verdict.
	internal sealed class KingdomWaterMaintenanceMarkerDiagnostic
	{
		private const int MaxEntries = 256, MaxChanges = 16, MaxOutput = 8192;
		private readonly GameObject Body;
		private readonly string Id;
		private readonly int BaseId;
		private readonly Dictionary<string, string> Properties, PropertyValues;
		private readonly Dictionary<string, int> IntProperties, IntValues;

		internal KingdomWaterMaintenanceMarkerDiagnostic(GameObject body)
		{
			if (body == null) throw new ArgumentNullException(nameof(body));
			Body = body; Id = body.IDIfAssigned; BaseId = body._BaseID;
			Properties = body.Property; IntProperties = body.IntProperty;
			PropertyValues = Capture(Properties); IntValues = Capture(IntProperties);
		}

		internal void Append(StringBuilder evidence, int vesselIndex, int stage, long tick)
		{
			if (evidence == null) return;
			try
			{
				var line = new StringBuilder();
				line.Append("\nwater-marker-diagnostic vessel=").Append(vesselIndex)
					.Append(" stage=").Append(stage).Append(" tick=").Append(tick)
					.Append(" original-id="); Text(line, Id, 160);
				line.Append(" current-id="); Text(line, Body.IDIfAssigned, 160);
				line.Append(" original-base=").Append(BaseId).Append(" current-base=").Append(Body._BaseID);
				Dictionary<string, string> properties = Body.Property;
				Dictionary<string, int> integers = Body.IntProperty;
				line.Append(" property-ref-same=").Append(ReferenceEquals(properties, Properties))
					.Append(" int-property-ref-same=").Append(ReferenceEquals(integers, IntProperties));
				Compare(line, "Property", PropertyValues, properties, (output, value) => Text(output, value, 160));
				Compare(line, "IntProperty", IntValues, integers,
					(output, value) => output.Append(value.ToString(CultureInfo.InvariantCulture)));
				line.Append("; diagnostic-only=true; baseline-refreshed=false; original-refusal-retained=true");
				if (line.Length <= MaxOutput) evidence.Append(line);
				else evidence.Append(line.ToString(0, MaxOutput)).Append(" [diagnostic-output-truncated]");
			}
			catch (Exception error)
			{
				// A failed diagnostic must not replace the exception from the exact custody proof.
				try { evidence.Append("\nwater-marker-diagnostic unavailable: ").Append(error.GetType().Name); }
				catch { }
			}
		}

		private static Dictionary<string, T> Capture<T>(Dictionary<string, T> source)
		{
			if (source == null) return null;
			if (source.Count > MaxEntries) throw new InvalidOperationException("marker dictionary exceeds diagnostic bound");
			return new Dictionary<string, T>(source, StringComparer.Ordinal);
		}

		private static void Compare<T>(StringBuilder line, string name, Dictionary<string, T> before,
			Dictionary<string, T> live, Action<StringBuilder, T> value)
		{
			line.Append("\n ").Append(name).Append(" before-count=").Append(before?.Count ?? -1)
				.Append(" current-count=").Append(live?.Count ?? -1);
			if (live != null && live.Count > MaxEntries)
			{
				line.Append(" delta-unavailable=entry-bound-exceeded"); return;
			}
			Dictionary<string, T> after = Capture(live);
			var keys = new SortedSet<string>(StringComparer.Ordinal);
			if (before != null) foreach (string key in before.Keys) keys.Add(key);
			if (after != null) foreach (string key in after.Keys) keys.Add(key);
			int changes = 0, written = 0;
			foreach (string key in keys)
			{
				T oldValue = default(T), newValue = default(T);
				bool had = before != null && before.TryGetValue(key, out oldValue);
				bool has = after != null && after.TryGetValue(key, out newValue);
				if (had == has && EqualityComparer<T>.Default.Equals(oldValue, newValue)) continue;
				changes++;
				if (written >= MaxChanges) continue;
				written++;
				line.Append("\n  ").Append(!had ? "added" : !has ? "removed" : "changed").Append(" key=");
				Text(line, key, 96);
				line.Append(" before-present=").Append(had).Append(" before=");
				if (had) value(line, oldValue); else line.Append("<absent>");
				line.Append(" after-present=").Append(has).Append(" after=");
				if (has) value(line, newValue); else line.Append("<absent>");
			}
			line.Append("\n ").Append(name).Append(" changed-keys=").Append(changes)
				.Append(" omitted-changes=").Append(changes - written);
		}

		private static void Text(StringBuilder output, string value, int limit)
		{
			if (value == null) { output.Append("null"); return; }
			output.Append('"');
			int length = Math.Min(value.Length, limit);
			for (int i = 0; i < length; i++)
			{
				char character = value[i];
				if (character == '"' || character == '\\') output.Append('\\').Append(character);
				else if (char.IsControl(character) || char.IsSurrogate(character))
					output.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
				else output.Append(character);
			}
			output.Append('"');
			if (value.Length > length) output.Append("[truncated,length=").Append(value.Length).Append(']');
		}
	}
}
