namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using SuwayomiSourceMerge.Infrastructure.Metadata;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Fixture helpers for <see cref="ComickMetadataCoordinatorTests"/>.
/// </summary>
public sealed partial class ComickMetadataCoordinatorTests
{
	/// <summary>
	/// Aggregates coordinator dependencies and helper methods used by tests.
	/// </summary>
	private sealed class TestFixture
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="TestFixture"/> class.
		/// </summary>
		/// <param name="rootPath">Temporary root path.</param>
		/// <param name="coordinator">Coordinator under test.</param>
		/// <param name="apiGateway">API gateway fake.</param>
		/// <param name="candidateMatcher">Candidate-matcher fake.</param>
		/// <param name="coverService">Cover-service fake.</param>
		/// <param name="detailsService">Details-service fake.</param>
		/// <param name="metadataStateStore">Metadata-state-store fake.</param>
		/// <param name="logger">Logger fake.</param>
		public TestFixture(
			string rootPath,
			ComickMetadataCoordinator coordinator,
			RecordingComickApiGateway apiGateway,
			RecordingComickCandidateMatcher candidateMatcher,
			RecordingOverrideCoverService coverService,
			RecordingOverrideDetailsService detailsService,
			RecordingMetadataStateStore metadataStateStore,
			RecordingLogger logger)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
			RootPath = rootPath;
			Coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
			ApiGateway = apiGateway ?? throw new ArgumentNullException(nameof(apiGateway));
			CandidateMatcher = candidateMatcher ?? throw new ArgumentNullException(nameof(candidateMatcher));
			CoverService = coverService ?? throw new ArgumentNullException(nameof(coverService));
			DetailsService = detailsService ?? throw new ArgumentNullException(nameof(detailsService));
			MetadataStateStore = metadataStateStore ?? throw new ArgumentNullException(nameof(metadataStateStore));
			Logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		/// <summary>
		/// Gets temporary root path.
		/// </summary>
		public string RootPath
		{
			get;
		}

		/// <summary>
		/// Gets coordinator under test.
		/// </summary>
		public ComickMetadataCoordinator Coordinator
		{
			get;
		}

		/// <summary>
		/// Gets API gateway fake.
		/// </summary>
		public RecordingComickApiGateway ApiGateway
		{
			get;
		}

		/// <summary>
		/// Gets candidate matcher fake.
		/// </summary>
		public RecordingComickCandidateMatcher CandidateMatcher
		{
			get;
		}

		/// <summary>
		/// Gets cover service fake.
		/// </summary>
		public RecordingOverrideCoverService CoverService
		{
			get;
		}

		/// <summary>
		/// Gets details service fake.
		/// </summary>
		public RecordingOverrideDetailsService DetailsService
		{
			get;
		}

		/// <summary>
		/// Gets metadata state-store fake.
		/// </summary>
		public RecordingMetadataStateStore MetadataStateStore
		{
			get;
		}

		/// <summary>
		/// Gets logger fake.
		/// </summary>
		public RecordingLogger Logger
		{
			get;
		}

		/// <summary>
		/// Creates one request for a title where details.json already exists in the preferred override path.
		/// </summary>
		/// <param name="displayTitle">Display title.</param>
		/// <returns>Coordinator request.</returns>
		public ComickMetadataCoordinatorRequest CreateRequestWithExistingDetails(string displayTitle)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(displayTitle);

			string preferredOverridePath = Path.Combine(RootPath, "override", "priority", displayTitle);
			Directory.CreateDirectory(preferredOverridePath);
			File.WriteAllText(Path.Combine(preferredOverridePath, "details.json"), "{}");
			return new ComickMetadataCoordinatorRequest(
				preferredOverridePath,
				[preferredOverridePath],
				[],
				displayTitle,
				CreateMetadataOrchestrationOptions());
		}

		/// <summary>
		/// Creates one request for a title without existing metadata artifacts.
		/// </summary>
		/// <param name="displayTitle">Display title.</param>
		/// <returns>Coordinator request.</returns>
		public ComickMetadataCoordinatorRequest CreateRequestWithoutArtifacts(string displayTitle)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(displayTitle);

			string preferredOverridePath = Path.Combine(RootPath, "override", "priority", displayTitle);
			Directory.CreateDirectory(preferredOverridePath);
			return new ComickMetadataCoordinatorRequest(
				preferredOverridePath,
				[preferredOverridePath],
				[],
				displayTitle,
				CreateMetadataOrchestrationOptions());
		}
	}
}
