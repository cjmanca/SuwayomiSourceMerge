namespace SuwayomiSourceMerge.UnitTests.Application.Supervision;

using System.Runtime.InteropServices;

using SuwayomiSourceMerge.Application.Supervision;

/// <summary>
/// Verifies expected, edge, and failure behavior for <see cref="ConsoleSupervisorSignalRegistrar"/>.
/// </summary>
public sealed class ConsoleSupervisorSignalRegistrarTests
{
	/// <summary>
	/// Verifies POSIX registration wires SIGTERM callback and disposes the registration handle.
	/// </summary>
	[Fact]
	public void RegisterStopSignal_Expected_ShouldRegisterSigtermAndInvokeStopCallback()
	{
		List<PosixSignal> registeredSignals = [];
		List<Action> callbacks = [];
		RecordingDisposable registrationHandle = new();
		ConsoleSupervisorSignalRegistrar registrar = new(
			(signal, callback) =>
			{
				registeredSignals.Add(signal);
				callbacks.Add(callback);
				return registrationHandle;
			},
			static () => true);
		int stopCalls = 0;

		using IDisposable registration = registrar.RegisterStopSignal(() => stopCalls++);
		Assert.Single(registeredSignals);
		Assert.Equal(PosixSignal.SIGTERM, registeredSignals[0]);
		Assert.Single(callbacks);
		callbacks[0]();
		Assert.Equal(1, stopCalls);
		Assert.False(registrationHandle.Disposed);

		registration.Dispose();
		Assert.True(registrationHandle.Disposed);
	}

	/// <summary>
	/// Verifies non-POSIX runtimes skip POSIX signal registration.
	/// </summary>
	[Fact]
	public void RegisterStopSignal_Edge_ShouldSkipPosixRegistration_WhenRuntimeIsNotPosix()
	{
		int registrationAttempts = 0;
		ConsoleSupervisorSignalRegistrar registrar = new(
			(_, _) =>
			{
				registrationAttempts++;
				return new RecordingDisposable();
			},
			static () => false);

		using IDisposable registration = registrar.RegisterStopSignal(static () => { });
		Assert.Equal(0, registrationAttempts);
	}

	/// <summary>
	/// Verifies best-effort behavior when POSIX registration is not supported by runtime policy.
	/// </summary>
	[Fact]
	public void RegisterStopSignal_Failure_ShouldNotThrow_WhenPosixRegistrationIsNotSupported()
	{
		ConsoleSupervisorSignalRegistrar registrar = new(
			static (_, _) => throw new PlatformNotSupportedException("simulated"),
			static () => true);

		using IDisposable registration = registrar.RegisterStopSignal(static () => { });
		Assert.NotNull(registration);
	}

	/// <summary>
	/// Disposable probe used by registration tests.
	/// </summary>
	private sealed class RecordingDisposable : IDisposable
	{
		/// <summary>
		/// Gets a value indicating whether <see cref="Dispose"/> was called.
		/// </summary>
		public bool Disposed
		{
			get;
			private set;
		}

		/// <inheritdoc />
		public void Dispose()
		{
			Disposed = true;
		}
	}
}
