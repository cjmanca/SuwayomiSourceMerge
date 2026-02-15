using System.Diagnostics;
using System.Text;

namespace SuwayomiSourceMerge.IntegrationTests.TestInfrastructure;

/// <summary>
/// Executes Docker CLI commands for integration tests.
/// </summary>
internal sealed class DockerCommandRunner
{
	/// <summary>
	/// Default command timeout used by Docker invocations.
	/// </summary>
	private static readonly TimeSpan DEFAULT_TIMEOUT = TimeSpan.FromSeconds(120);

	/// <summary>
	/// Additional wait budget for docker stop command completion after requested timeout.
	/// </summary>
	private static readonly TimeSpan DOCKER_STOP_COMPLETION_GRACE_PERIOD = TimeSpan.FromSeconds(10);

	/// <summary>
	/// Additional wait budget after a timeout-triggered kill attempt before declaring command timeout.
	/// </summary>
	private static readonly TimeSpan PROCESS_TERMINATION_GRACE_PERIOD = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Ensures Docker daemon is reachable.
	/// </summary>
	public void EnsureDockerDaemonAvailable()
	{
		DockerCommandResult result = Execute(
			["info"],
			timeout: TimeSpan.FromSeconds(15));

		if (result.TimedOut || result.ExitCode != 0)
		{
			throw BuildFailureException("Docker daemon is unavailable.", result);
		}
	}

	/// <summary>
	/// Builds the integration Docker image.
	/// </summary>
	/// <param name="repositoryRootPath">Repository root path.</param>
	/// <param name="dockerfilePath">Dockerfile path.</param>
	/// <param name="imageTag">Image tag to build.</param>
	public void BuildImage(string repositoryRootPath, string dockerfilePath, string imageTag)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRootPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(dockerfilePath);
		ArgumentException.ThrowIfNullOrWhiteSpace(imageTag);

		DockerCommandResult result = Execute(
			["build", "--file", dockerfilePath, "--tag", imageTag, "."],
			workingDirectory: repositoryRootPath,
			timeout: TimeSpan.FromMinutes(10));

		EnsureSuccess("Docker image build failed.", result);
	}

	/// <summary>
	/// Starts one container in detached mode.
	/// </summary>
	/// <param name="imageTag">Image tag.</param>
	/// <param name="containerName">Container name.</param>
	/// <param name="environmentVariables">Environment variables.</param>
	/// <param name="bindMounts">Host/container bind mount triplets.</param>
	/// <returns>Started container id text.</returns>
	public string RunContainerDetached(
		string imageTag,
		string containerName,
		IReadOnlyDictionary<string, string> environmentVariables,
		IReadOnlyList<(string HostPath, string ContainerPath, bool ReadOnly)> bindMounts)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(imageTag);
		ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
		ArgumentNullException.ThrowIfNull(environmentVariables);
		ArgumentNullException.ThrowIfNull(bindMounts);

		List<string> arguments = ["run", "--detach", "--name", containerName];

		foreach ((string hostPath, string containerPath, bool readOnly) in bindMounts)
		{
			string suffix = readOnly ? ":ro" : string.Empty;
			arguments.Add("--volume");
			arguments.Add($"{hostPath}:{containerPath}{suffix}");
		}

		foreach ((string key, string value) in environmentVariables.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
		{
			arguments.Add("--env");
			arguments.Add($"{key}={value}");
		}

		arguments.Add(imageTag);

		DockerCommandResult result = Execute(arguments, timeout: TimeSpan.FromMinutes(2));
		EnsureSuccess("Container start failed.", result);
		return result.StandardOutput.Trim();
	}

	/// <summary>
	/// Sends one signal to one container.
	/// </summary>
	/// <param name="containerName">Container name.</param>
	/// <param name="signalName">Signal name, for example <c>SIGINT</c>.</param>
	public void SendSignal(string containerName, string signalName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
		ArgumentException.ThrowIfNullOrWhiteSpace(signalName);
		DockerCommandResult result = Execute(["kill", "--signal", signalName, containerName], timeout: TimeSpan.FromSeconds(20));
		EnsureSuccess("Sending container signal failed.", result);
	}

	/// <summary>
	/// Stops one container.
	/// </summary>
	/// <param name="containerName">Container name.</param>
	/// <param name="timeout">Stop timeout.</param>
	public void StopContainer(string containerName, TimeSpan timeout)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
		if (timeout <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be > 0.");
		}

		int timeoutSeconds = Math.Max(1, (int)Math.Ceiling(timeout.TotalSeconds));
		DockerCommandResult result = Execute(
			["stop", "--time", timeoutSeconds.ToString(), containerName],
			timeout: timeout + DOCKER_STOP_COMPLETION_GRACE_PERIOD);
		EnsureSuccess("Container stop failed.", result);
	}

	/// <summary>
	/// Removes one container with force semantics.
	/// </summary>
	/// <param name="containerName">Container name.</param>
	public void RemoveContainerForce(string containerName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
		Execute(["rm", "--force", containerName], timeout: TimeSpan.FromSeconds(30));
	}

	/// <summary>
	/// Waits for one container to exit and returns exit code.
	/// </summary>
	/// <param name="containerName">Container name.</param>
	/// <param name="timeout">Wait timeout.</param>
	/// <returns>Container exit code.</returns>
	public int WaitContainerExitCode(string containerName, TimeSpan timeout)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
		if (timeout <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be > 0.");
		}

		DockerCommandResult result = Execute(["wait", containerName], timeout: timeout);
		EnsureSuccess("Container wait failed.", result);

		if (!int.TryParse(result.StandardOutput.Trim(), out int exitCode))
		{
			throw new InvalidOperationException($"Could not parse docker wait exit code output: '{result.StandardOutput}'.");
		}

		return exitCode;
	}

	/// <summary>
	/// Gets one container's running state.
	/// </summary>
	/// <param name="containerName">Container name.</param>
	/// <returns><see langword="true"/> when running; otherwise <see langword="false"/>.</returns>
	public bool IsContainerRunning(string containerName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
		DockerCommandResult result = Execute(["inspect", "--format", "{{.State.Running}}", containerName], timeout: TimeSpan.FromSeconds(15));
		EnsureSuccess("Container inspect failed.", result);
		return string.Equals(result.StandardOutput.Trim(), "true", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Gets one container's exit code.
	/// </summary>
	/// <param name="containerName">Container name.</param>
	/// <returns>Exit code.</returns>
	public int GetContainerExitCode(string containerName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
		DockerCommandResult result = Execute(["inspect", "--format", "{{.State.ExitCode}}", containerName], timeout: TimeSpan.FromSeconds(15));
		EnsureSuccess("Container exit-code inspection failed.", result);

		if (!int.TryParse(result.StandardOutput.Trim(), out int exitCode))
		{
			throw new InvalidOperationException($"Could not parse exit code from docker inspect output: '{result.StandardOutput}'.");
		}

		return exitCode;
	}

	/// <summary>
	/// Gets combined logs from one container.
	/// </summary>
	/// <param name="containerName">Container name.</param>
	/// <returns>Logs output.</returns>
	public string GetContainerLogs(string containerName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
		DockerCommandResult result = Execute(["logs", containerName], timeout: TimeSpan.FromSeconds(30));
		if (result.TimedOut)
		{
			throw BuildFailureException("Container logs command timed out.", result);
		}

		return string.Concat(result.StandardOutput, result.StandardError);
	}

	/// <summary>
	/// Executes one Docker command.
	/// </summary>
	/// <param name="arguments">Docker command arguments.</param>
	/// <param name="workingDirectory">Optional working directory.</param>
	/// <param name="timeout">Command timeout.</param>
	/// <returns>Command result.</returns>
	public DockerCommandResult Execute(
		IReadOnlyList<string> arguments,
		string? workingDirectory = null,
		TimeSpan? timeout = null)
	{
		ArgumentNullException.ThrowIfNull(arguments);
		if (arguments.Count == 0)
		{
			throw new ArgumentException("Docker command arguments must not be empty.", nameof(arguments));
		}

		ProcessStartInfo startInfo = new()
		{
			FileName = "docker",
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};

		if (!string.IsNullOrWhiteSpace(workingDirectory))
		{
			startInfo.WorkingDirectory = workingDirectory;
		}

		for (int index = 0; index < arguments.Count; index++)
		{
			startInfo.ArgumentList.Add(arguments[index]);
		}

		using Process process = new()
		{
			StartInfo = startInfo
		};

		if (!process.Start())
		{
			throw new InvalidOperationException($"Failed to start Docker process: {RenderCommand(arguments)}");
		}

		Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync();
		Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();
		TimeSpan effectiveTimeout = timeout ?? DEFAULT_TIMEOUT;
		bool completedWithinTimeout = process.WaitForExit((int)effectiveTimeout.TotalMilliseconds);
		bool exited = completedWithinTimeout;

		if (!completedWithinTimeout)
		{
			TryKillProcess(process);
			exited = process.WaitForExit((int)PROCESS_TERMINATION_GRACE_PERIOD.TotalMilliseconds) || process.HasExited;
		}

		string standardOutput;
		string standardError;
		if (exited)
		{
			// Process has exited (naturally or after kill); fully drain output readers before consuming results.
			Task.WaitAll([standardOutputTask, standardErrorTask]);
			standardOutput = standardOutputTask.Result;
			standardError = standardErrorTask.Result;
		}
		else
		{
			// Process did not confirm exit after timeout and kill attempt; avoid unbounded waits on stream readers.
			standardOutput = TryGetCompletedTaskResult(standardOutputTask);
			standardError = TryGetCompletedTaskResult(standardErrorTask);
		}

		return new DockerCommandResult(
			RenderCommand(arguments),
			exited ? process.ExitCode : -1,
			standardOutput,
			standardError,
			!completedWithinTimeout);
	}

	/// <summary>
	/// Throws when command did not succeed.
	/// </summary>
	/// <param name="message">Failure heading.</param>
	/// <param name="result">Command result.</param>
	private static void EnsureSuccess(string message, DockerCommandResult result)
	{
		if (!result.TimedOut && result.ExitCode == 0)
		{
			return;
		}

		throw BuildFailureException(message, result);
	}

	/// <summary>
	/// Builds one deterministic failure exception from one command result.
	/// </summary>
	/// <param name="message">Failure heading.</param>
	/// <param name="result">Command result.</param>
	/// <returns>Failure exception.</returns>
	private static InvalidOperationException BuildFailureException(string message, DockerCommandResult result)
	{
		StringBuilder builder = new();
		builder.AppendLine(message);
		builder.AppendLine($"Command: {result.Command}");
		builder.AppendLine($"TimedOut: {result.TimedOut}");
		builder.AppendLine($"ExitCode: {result.ExitCode}");
		builder.AppendLine("Stdout:");
		builder.AppendLine(result.StandardOutput);
		builder.AppendLine("Stderr:");
		builder.AppendLine(result.StandardError);
		return new InvalidOperationException(builder.ToString());
	}

	/// <summary>
	/// Renders one docker command string for diagnostics.
	/// </summary>
	/// <param name="arguments">Docker arguments.</param>
	/// <returns>Command text.</returns>
	private static string RenderCommand(IReadOnlyList<string> arguments)
	{
		return "docker " + string.Join(" ", arguments.Select(Escape));
	}

	/// <summary>
	/// Escapes one argument for diagnostic rendering.
	/// </summary>
	/// <param name="value">Argument value.</param>
	/// <returns>Escaped value.</returns>
	private static string Escape(string value)
	{
		if (value.IndexOfAny([' ', '\t', '"']) < 0)
		{
			return value;
		}

		return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
	}

	/// <summary>
	/// Attempts to kill one process using best-effort semantics.
	/// </summary>
	/// <param name="process">Process instance.</param>
	private static void TryKillProcess(Process process)
	{
		try
		{
			process.Kill(entireProcessTree: true);
		}
		catch
		{
			// Best-effort test cleanup.
		}
	}

	/// <summary>
	/// Gets completed task result without waiting; returns empty string when not completed or faulted.
	/// </summary>
	/// <param name="task">Task to inspect.</param>
	/// <returns>Completed task result, or empty string when unavailable.</returns>
	private static string TryGetCompletedTaskResult(Task<string> task)
	{
		ArgumentNullException.ThrowIfNull(task);

		if (task.Status == TaskStatus.RanToCompletion)
		{
			return task.Result;
		}

		return string.Empty;
	}
}
