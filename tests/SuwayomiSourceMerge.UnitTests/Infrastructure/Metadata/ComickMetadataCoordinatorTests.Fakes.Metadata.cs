namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Metadata-related test doubles for <see cref="ComickMetadataCoordinatorTests"/>.
/// </summary>
public sealed partial class ComickMetadataCoordinatorTests
{
	/// <summary>
	/// Recording details-service fake for coordinator tests.
	/// </summary>
	private sealed class RecordingOverrideDetailsService : IOverrideDetailsService
	{
		/// <summary>
		/// Gets or sets the next details-service result.
		/// </summary>
		public OverrideDetailsResult? NextResult
		{
			get;
			set;
		}

		/// <summary>
		/// Gets the number of details ensure calls.
		/// </summary>
		public int CallCount
		{
			get;
			private set;
		}

		/// <inheritdoc />
		public OverrideDetailsResult EnsureDetailsJson(OverrideDetailsRequest request)
		{
			CallCount++;
			if (NextResult is not null)
			{
				return NextResult;
			}

			return new OverrideDetailsResult(
				OverrideDetailsOutcome.AlreadyExists,
				Path.Combine(request.PreferredOverrideDirectoryPath, "details.json"),
				detailsJsonExists: true,
				sourceDetailsJsonPath: null,
				comicInfoXmlPath: null);
		}
	}

	/// <summary>
	/// Recording metadata-state-store fake for coordinator tests.
	/// </summary>
	private sealed class RecordingMetadataStateStore : IMetadataStateStore
	{
		/// <summary>
		/// Backing snapshot value.
		/// </summary>
		private MetadataStateSnapshot _snapshot = MetadataStateSnapshot.Empty;

		/// <summary>
		/// Gets the number of transform calls.
		/// </summary>
		public int TransformCallCount
		{
			get;
			private set;
		}

		/// <inheritdoc />
		public MetadataStateSnapshot Read()
		{
			return _snapshot;
		}

		/// <inheritdoc />
		public void Transform(Func<MetadataStateSnapshot, MetadataStateSnapshot> transformer)
		{
			ArgumentNullException.ThrowIfNull(transformer);
			TransformCallCount++;
			_snapshot = transformer(_snapshot);
		}

		/// <summary>
		/// Sets the in-memory snapshot directly.
		/// </summary>
		/// <param name="snapshot">Snapshot to apply.</param>
		public void SetSnapshot(MetadataStateSnapshot snapshot)
		{
			_snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
		}
	}
}
