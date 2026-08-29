using System;
using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		/// <summary>True only after this instance completed the custom bounded read path.
		/// It starts false so an older positional save that fails inside the engine's
		/// <c>ReadTypeFields</c> call, before <see cref="Read"/> can run, remains detectably unsafe.
		/// Freshly required systems do not receive <c>AfterLoad</c> and therefore remain valid.</summary>
		[NonSerialized]
		private bool CustomReadCompleted;

		[NonSerialized]
		private bool LoadFailureReportedThisSession;

		[NonSerialized]
		private int LoadedSerializationVersion;

		public override bool WantFieldReflection => false;

		public override void Write(SerializationWriter Writer)
		{
			SerializationVersion = CurrentSerializationVersion;
			// Named-field serializer writes compatibility field as stored data, not a property.
			// Refresh immediately before every save, including a save cut through an open receipt.
			SynchronizeLegacyManifestProjection();
			SynchronizeLegacySettlementProjection();
			SynchronizeLegacyExiledProjection();
			Writer.Write(SerializationMagic);
			Writer.Write(CurrentSerializationVersion);
			Writer.WriteNamedFields(this, typeof(KingdomSystem));
		}

		/// <summary>
		/// Reads kingdom state written by this build's named-field format.
		/// <para>
		/// A reflected-v1 save is framed as <c>ICompositeFieldType</c>. The engine runs
		/// <c>ReadTypeFields</c> before this method in one guarded block
		/// (<c>XRL/World/SerializationReader.cs:1310-1334</c>). Today's field layout no longer
		/// matches v1, so that positional read can fail before this method runs. The constructor-
		/// default <see cref="CustomReadCompleted"/> sentinel is therefore the authority used by
		/// <c>AfterLoad</c>; reaching the explicit v1 branch here is also refused.
		/// </para>
		/// <para>
		/// Named-field saves are self-describing. Versions inside the declared compatibility
		/// boundary are read; older positional and newer unknown versions are refused. Any failure
		/// latches <see cref="LoadFailed"/> and leaves the completion sentinel false so the blank
		/// recovery object cannot overwrite the existing save.
		/// </para>
		/// </summary>
		public override void Read(SerializationReader Reader)
		{
			CustomReadCompleted = false;
			try
			{
				if (SerializationVersion == LegacyReflectedSerializationVersion)
				{
					// Earlier revisions called NormalizeState(AllowLegacyIdentityMigration: true)
					// and returned here. A genuine v1 save cannot safely reach that migration.
					MetricsManager.LogError("ThousandAndFirst: reached the reflected-v1 branch " +
						"of KingdomSystem.Read; refusing an unsupported positional save.");
					throw new InvalidOperationException(
						"Unsupported ThousandAndFirst kingdom save version " +
						LegacyReflectedSerializationVersion +
						" (pre-named-field); this build does not migrate it.");
				}
				int magic = Reader.ReadInt32();
				if (magic != SerializationMagic)
					throw new InvalidOperationException(
						"Invalid ThousandAndFirst kingdom save marker.");
				int version = Reader.ReadInt32();
				if (version < FirstNamedSerializationVersion
					|| version > CurrentSerializationVersion)
					throw new InvalidOperationException(
						"Unsupported ThousandAndFirst kingdom save version " + version +
						"; this build reads named versions " + FirstNamedSerializationVersion +
						" through " + CurrentSerializationVersion + ".");
				LoadedSerializationVersion = version;
				Reader.ReadNamedFields(this, typeof(KingdomSystem));
				SerializationVersion = CurrentSerializationVersion;
				NormalizeState(AllowLegacyIdentityMigration: false);
				LoadFailed = false;
				CustomReadCompleted = true;
			}
			catch
			{
				LoadFailed = true;
				throw;
			}
		}

		/// <summary>Marks a recovery object unsafe when the engine failed before calling
		/// <see cref="Read"/>. Called first from <c>AfterLoad</c>, before any normalization.</summary>
		private bool RefuseIncompleteLoad()
		{
			if (!CustomReadCompleted) LoadFailed = true;
			return LoadFailed;
		}

		private void ReportLoadFailure()
		{
			if (LoadFailureReportedThisSession) return;
			LoadFailureReportedThisSession = true;
			MetricsManager.LogError("ThousandAndFirst: kingdom state could not be read; " +
				"saving is disabled for this session.");
			Popup.Show("The founding records cannot be read. This session cannot safely be saved, " +
				"because doing so would replace the kingdom records with blank state.\n\nQuit without " +
				"saving, keep the existing save, and report this failure.");
		}
	}
}
