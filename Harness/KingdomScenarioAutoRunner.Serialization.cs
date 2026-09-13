using System;
using System.Reflection;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Save/load half of KingdomScenarioAutoRunner, split out only to keep the main shard
	/// under the house line cap. No behaviour lives here beyond the named-field round trip.</summary>
	public sealed partial class KingdomScenarioAutoRunner
	{
		public override void Write(SerializationWriter Writer)
		{
			SerializationVersion = CurrentSerializationVersion;
			Writer.Write(SerializationMagic);
			Writer.Write(CurrentSerializationVersion);
			Writer.WriteNamedFields(this, typeof(KingdomScenarioAutoRunner),
				BindingFlags.Instance | BindingFlags.NonPublic);
		}

		public override void Read(SerializationReader Reader)
		{
			int magic = Reader.ReadInt32();
			int version = Reader.ReadInt32();
			if (magic != SerializationMagic || version < 1
				|| version > CurrentSerializationVersion)
				throw new InvalidOperationException(
					"Unsupported ThousandAndFirst scenario auto-runner save block.");
			Reader.ReadNamedFields(this, typeof(KingdomScenarioAutoRunner),
				BindingFlags.Instance | BindingFlags.NonPublic);
			if (SerializationVersion != version)
				throw new InvalidOperationException(
					"Unsupported ThousandAndFirst scenario auto-runner named-field version.");
		}
	}
}
