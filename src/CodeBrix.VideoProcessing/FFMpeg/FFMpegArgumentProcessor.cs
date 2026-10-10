using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Enums;
using CodeBrix.VideoProcessing.Exceptions;
using CodeBrix.VideoProcessing.Helpers;
using CodeBrix.VideoProcessing.Instances;

namespace CodeBrix.VideoProcessing; //was previously: FFMpegCore;

public class FFMpegArgumentProcessor
{
    private static readonly Regex ProgressRegex = new(@"time=(\d\d:\d\d:\d\d.\d\d?)", RegexOptions.Compiled);
    private readonly List<string> _av1EncoderFallbackNotes = new();
    private readonly List<Action<FFOptions>> _configurations;
    private readonly FFMpegArguments _ffMpegArguments;
    private bool _av1EncoderFallbackEnabled;
    private string _av1EncoderFallbackProblem;
    private CancellationTokenRegistration? _cancellationTokenRegistration;
    private bool _cancelled;
    private FFMpegLogLevel? _logLevel;
    private Action<string> _onError;
    private Action<string> _onOutput;
    private Action<double> _onPercentageProgress;
    private Action<TimeSpan> _onTimeProgress;
    private Exception _pipeFailure;
    private TimeSpan? _totalTimespan;

    internal FFMpegArgumentProcessor(FFMpegArguments ffMpegArguments)
    {
        _configurations = new List<Action<FFOptions>>();
        _ffMpegArguments = ffMpegArguments;
    }

    public string Arguments => _ffMpegArguments.Text;

    // CODEBRIX FIX (not in upstream FFMpegCore) - see OUTPUT PIPES DRAIN BEFORE THEY ARE DISPOSED in
    // MAINTAINER-README.txt. How long after ffmpeg exits an output pipe's sink may take to read the rest of the
    // pipe before the run fails with "did not drain". Internal so tests can shorten it.
    internal TimeSpan OutputPipeDrainTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     True once <see cref="FFOptions.AllowAv1EncoderFallback" /> has rewritten an output of this processor from
    ///     <c>libsvtav1</c> to <c>libaom-av1</c>. <see cref="Arguments" /> then shows the command that actually ran.
    /// </summary>
    public bool Av1EncoderFallbackApplied => _av1EncoderFallbackNotes.Count > 0;

    /// <summary>
    ///     What <see cref="FFOptions.AllowAv1EncoderFallback" /> changed in this processor's command, one sentence per
    ///     change. Empty when the fallback was not used.
    /// </summary>
    public IReadOnlyList<string> Av1EncoderFallbackNotes => _av1EncoderFallbackNotes.AsReadOnly();

    private event EventHandler<int> CancelEvent = null;

    /// <summary>
    ///     Register action that will be invoked during the ffmpeg processing, when a progress time is output and parsed and progress percentage is
    ///     calculated.
    ///     Total time is needed to calculate the percentage that has been processed of the full file.
    /// </summary>
    /// <param name="onPercentageProgress">Action to invoke when progress percentage is updated</param>
    /// <param name="totalTimeSpan">The total timespan of the mediafile being processed</param>
    public FFMpegArgumentProcessor NotifyOnProgress(Action<double> onPercentageProgress, TimeSpan totalTimeSpan)
    {
        _totalTimespan = totalTimeSpan;
        _onPercentageProgress = onPercentageProgress;
        return this;
    }

    /// <summary>
    ///     Register action that will be invoked during the ffmpeg processing, when a progress time is output and parsed
    /// </summary>
    /// <param name="onTimeProgress">Action that will be invoked with the parsed timestamp as argument</param>
    public FFMpegArgumentProcessor NotifyOnProgress(Action<TimeSpan> onTimeProgress)
    {
        _onTimeProgress = onTimeProgress;
        return this;
    }

    /// <summary>
    ///     Register action that will be invoked during the ffmpeg processing, when a line is output
    /// </summary>
    /// <param name="onOutput"></param>
    public FFMpegArgumentProcessor NotifyOnOutput(Action<string> onOutput)
    {
        _onOutput = onOutput;
        return this;
    }

    public FFMpegArgumentProcessor NotifyOnError(Action<string> onError)
    {
        _onError = onError;
        return this;
    }

    private void Cancel(int timeout)
    {
        _cancelled = true;
        CancelEvent?.Invoke(this, timeout);
    }

    public FFMpegArgumentProcessor CancellableThrough(out Action cancel, int timeout = 0)
    {
        cancel = () => Cancel(timeout);
        return this;
    }

    public FFMpegArgumentProcessor CancellableThrough(CancellationToken token, int timeout = 0)
    {
        token.ThrowIfCancellationRequested();
        _cancellationTokenRegistration?.Dispose();
        _cancellationTokenRegistration = token.Register(() => Cancel(timeout));
        return this;
    }

    public FFMpegArgumentProcessor Configure(Action<FFOptions> configureOptions)
    {
        _configurations.Add(configureOptions);
        return this;
    }

    /// <summary>
    ///     Sets the log level of this process. Overides the <see cref="FFMpegLogLevel" />
    ///     that is set in the <see cref="FFOptions" /> for this specific process.
    /// </summary>
    /// <param name="logLevel">The log level of the ffmpeg execution.</param>
    public FFMpegArgumentProcessor WithLogLevel(FFMpegLogLevel logLevel)
    {
        _logLevel = logLevel;
        return this;
    }

    public bool ProcessSynchronously(bool throwOnError = true, FFOptions ffMpegOptions = null)
    {
        var options = GetConfiguredOptions(ffMpegOptions);
        var processArguments = PrepareProcessArguments(options);
        using var cancellationTokenSource = new CancellationTokenSource();

        IProcessResult processResult = null;
        try
        {
            processResult = Process(processArguments, cancellationTokenSource).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            if (throwOnError)
            {
                throw;
            }
        }

        return HandleCompletion(throwOnError, processResult?.ExitCode ?? -1, processResult?.ErrorData ?? Array.Empty<string>());
    }

    public async Task<bool> ProcessAsynchronously(bool throwOnError = true, FFOptions ffMpegOptions = null)
    {
        var options = GetConfiguredOptions(ffMpegOptions);
        var processArguments = PrepareProcessArguments(options);
        using var cancellationTokenSource = new CancellationTokenSource();

        IProcessResult processResult = null;
        try
        {
            processResult = await Process(processArguments, cancellationTokenSource).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (throwOnError)
            {
                throw;
            }
        }

        return HandleCompletion(throwOnError, processResult?.ExitCode ?? -1, processResult?.ErrorData ?? Array.Empty<string>());
    }

    private async Task<IProcessResult> Process(ProcessArguments processArguments, CancellationTokenSource cancellationTokenSource)
    {
        IProcessResult processResult = null;
        _pipeFailure = null;
        if (_cancelled)
        {
            _cancellationTokenRegistration?.Dispose();
            throw new OperationCanceledException("cancelled before starting processing");
        }

        _ffMpegArguments.Pre();

        using var instance = processArguments.Start();

        // CODEBRIX FIX - see OUTPUT PIPES DRAIN BEFORE THEY ARE DISPOSED in MAINTAINER-README.txt. Output-pipe
        // sinks read under this token instead of cancellationTokenSource's, which ffmpeg's exit cancels.
        using var sinkCancellationSource = new CancellationTokenSource();

        void OnCancelEvent(object sender, int timeout)
        {
            ExecuteIgnoringFinishedProcessExceptions(() => instance.SendInput("q"));

            if (!cancellationTokenSource.Token.WaitHandle.WaitOne(timeout, true))
            {
                cancellationTokenSource.Cancel();
                ExecuteIgnoringFinishedProcessExceptions(() => instance.Kill());
            }

            // CODEBRIX FIX - a cancel that arrives after ffmpeg has exited, while a sink is still draining, must
            // still stop that sink at once (the exit handler only covers cancels that came before the exit).
            ExecuteIgnoringFinishedProcessExceptions(() => sinkCancellationSource.Cancel());

            static void ExecuteIgnoringFinishedProcessExceptions(Action action)
            {
                try
                {
                    action();
                }
                catch (CodeBrix.VideoProcessing.Instances.Exceptions.InstanceProcessAlreadyExitedException)
                {
                    //ignore
                }
                catch (ObjectDisposedException)
                {
                    //ignore
                }
            }
        }

        CancelEvent += OnCancelEvent;

        try
        {
            // CODEBRIX FIX (not in upstream FFMpegCore) - see OUTPUT PIPES DRAIN BEFORE THEY ARE DISPOSED in
            // MAINTAINER-README.txt. Upstream's exit handler was
            //     processResult = t.Result; cancellationTokenSource.Cancel(); _ffMpegArguments.Post();
            // which cancelled every output-pipe sink and disposed its pipe the instant ffmpeg exited. A sink slower
            // than ffmpeg still had the pipe's buffered tail to read, lost it, and - because PipeArgument.During
            // swallows the cancellation - nobody was told. Now the exit still cancels the During token (input pipes,
            // and output pipes ffmpeg never connected to) and posts everything else, but a connected output pipe's
            // sink keeps reading to EOF under sinkCancellationSource; the pipe is disposed once it has (finally,
            // below). The wait is bounded by OutputPipeDrainTimeout, and a cancel still tears the sink down at once.
            foreach (var outputPipe in _ffMpegArguments.OutputPipes)
            {
                outputPipe.SinkCancellationToken = sinkCancellationSource.Token;
            }

            var exitTask = instance.WaitForExitAsync().ContinueWith(t =>
            {
                processResult = t.Result;
                cancellationTokenSource.Cancel();
                if (_cancelled)
                {
                    // Cancelled: no draining - tear the sinks down exactly as upstream does.
                    sinkCancellationSource.Cancel();
                    _ffMpegArguments.PostOutputPipes();
                }

                _ffMpegArguments.PostAllExceptOutputPipes();
            });

            var duringTask = _ffMpegArguments.During(cancellationTokenSource.Token);
            var sinkDrainTimedOut = false;

            async Task WaitForOutputSinksToDrainAsync()
            {
                await exitTask.ConfigureAwait(false);
                try
                {
                    await duringTask.WaitAsync(OutputPipeDrainTimeout).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    sinkDrainTimedOut = true;
                    sinkCancellationSource.Cancel();
                    _ffMpegArguments.PostOutputPipes();
                }
            }

            try
            {
                await Task.WhenAll(exitTask, duringTask, WaitForOutputSinksToDrainAsync()).ConfigureAwait(false);
            }
            catch (Exception sinkFailure) when (sinkDrainTimedOut && !_cancelled)
            {
                // Whatever the abandoned sink threw on its way out is a symptom; the story is that it did not drain.
                throw SinkDidNotDrain(sinkFailure);
            }
            catch (IOException pipeFailure) when (processResult != null && processResult.ExitCode != 0)
            {
                // DELIBERATE DIVERGENCE FROM UPSTREAM FFMpegCore - see PIPE FAILURES VERSUS FFMPEG
                // FAILURES in MAINTAINER-README.txt. When ffmpeg exits early, whatever is still
                // writing into its pipe fails with a broken-pipe IOException, and Task.WhenAll
                // surfaces THAT instead of the exit code and stderr explaining why ffmpeg quit.
                // WhenAll only throws once both tasks have settled, so the exit code is already
                // known here: stash the pipe failure and let HandleCompletion report ffmpeg's own
                // error instead, carrying this one as the inner exception. Only an incidental
                // broken pipe is held back - an FFMpegStreamFormatException raised by a pipe
                // source, or anything else a pipe throws, still propagates from here untouched.
                _pipeFailure = pipeFailure;
            }

            if (_cancelled)
            {
                // Cancellation is the story, not the pipe that cancelling broke.
                _pipeFailure = null;
                _cancellationTokenRegistration?.Dispose();
                throw new OperationCanceledException("ffmpeg processing was cancelled");
            }

            if (sinkDrainTimedOut)
            {
                throw SinkDidNotDrain(null);
            }

            return processResult;
        }
        finally
        {
            CancelEvent -= OnCancelEvent;
            _cancellationTokenRegistration?.Dispose();
            // CODEBRIX FIX - the output pipes' deferred Post (see the exit handler above). Idempotent.
            _ffMpegArguments.PostOutputPipes();
        }

        FFMpegException SinkDidNotDrain(Exception innerException)
        {
            return new FFMpegException(FFMpegExceptionType.Process,
                $"An output pipe sink did not drain: ffmpeg exited (exit code {processResult?.ExitCode}) but the sink had still not read " +
                $"everything ffmpeg wrote to the pipe {OutputPipeDrainTimeout.TotalSeconds:0.###} s later, so the piped output is incomplete.",
                innerException,
                string.Join("\n", processResult?.ErrorData ?? Array.Empty<string>()));
        }
    }

    private bool HandleCompletion(bool throwOnError, int exitCode, IReadOnlyList<string> errorData)
    {
        if (throwOnError && exitCode != 0)
        {
            var message = $"ffmpeg exited with non-zero exit-code ({exitCode} - {string.Join("\n", errorData)})";

            // ADDITION OF THIS PORT - see AV1 ENCODER FALLBACK in MAINTAINER-README.txt. A missing encoder gets its
            // own FFMpegException subtype, and a missing libsvtav1 gets an explanation IN FRONT OF the usual message.
            var unknownEncoder = Av1EncoderFallback.FindUnknownEncoder(errorData);
            if (unknownEncoder != null)
            {
                if (unknownEncoder == Av1EncoderFallback.SvtAv1EncoderName)
                {
                    message = Av1EncoderFallback.DescribeMissingSvtAv1(_av1EncoderFallbackEnabled, _av1EncoderFallbackProblem) + " " + message;
                }

                throw new FFMpegEncoderNotFoundException(unknownEncoder, message, _pipeFailure, string.Join("\n", errorData));
            }

            throw new FFMpegException(FFMpegExceptionType.Process, message, _pipeFailure,
                string.Join("\n", errorData));
        }

        // A broken pipe that Process() held back because ffmpeg's own error looked like the
        // better story. Reaching here means there was no better story after all, so surface
        // it unchanged - original stack trace included - just as it would have surfaced
        // before.
        if (_pipeFailure != null)
        {
            ExceptionDispatchInfo.Capture(_pipeFailure).Throw();
        }

        _onPercentageProgress?.Invoke(100.0);
        if (_totalTimespan.HasValue)
        {
            _onTimeProgress?.Invoke(_totalTimespan.Value);
        }

        return exitCode == 0;
    }

    internal FFOptions GetConfiguredOptions(FFOptions ffOptions)
    {
        var options = ffOptions ?? GlobalFFOptions.Current.Clone();

        foreach (var configureOptions in _configurations)
        {
            configureOptions(options);
        }

        return options;
    }

    // ADDITION OF THIS PORT - see AV1 ENCODER FALLBACK in MAINTAINER-README.txt. Runs before the arguments are
    // rendered, so the command ffmpeg is given - and Arguments afterwards - already names libaom-av1.
    private void ApplyAv1EncoderFallback(FFOptions ffOptions)
    {
        _av1EncoderFallbackEnabled = ffOptions.AllowAv1EncoderFallback;
        _av1EncoderFallbackProblem = null;
        if (!_av1EncoderFallbackEnabled)
        {
            return;
        }

        var ffmpegBinaryPath = GlobalFFOptions.GetFFMpegBinaryPath(ffOptions);
        var outcome = Av1EncoderFallback.Apply(_ffMpegArguments.Arguments,
            encoderName => Av1EncoderFallback.IsEncoderAvailable(ffmpegBinaryPath, encoderName, ffOptions.UseCache));

        _av1EncoderFallbackNotes.AddRange(outcome.Notes);
        _av1EncoderFallbackProblem = outcome.Problem;
    }

    private ProcessArguments PrepareProcessArguments(FFOptions ffOptions)
    {
        FFMpegHelper.RootExceptionCheck();
        FFMpegHelper.VerifyFFMpegExists(ffOptions);
        ApplyAv1EncoderFallback(ffOptions);

        var arguments = _ffMpegArguments.Text;

        //If local loglevel is null, set the global.
        if (_logLevel == null)
        {
            _logLevel = ffOptions.LogLevel;
        }

        //If neither local nor global loglevel is null, set the argument.
        if (_logLevel != null)
        {
            var normalizedLogLevel = _logLevel.ToString()
                .ToLower();
            arguments += $" -v {normalizedLogLevel}";
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = GlobalFFOptions.GetFFMpegBinaryPath(ffOptions),
            Arguments = arguments,
            StandardOutputEncoding = ffOptions.Encoding,
            StandardErrorEncoding = ffOptions.Encoding,
            WorkingDirectory = ffOptions.WorkingDirectory
        };
        var processArguments = new ProcessArguments(startInfo);

        if (_onOutput != null)
        {
            processArguments.OutputDataReceived += OutputData;
        }

        if (_onError != null || _onTimeProgress != null || (_onPercentageProgress != null && _totalTimespan != null))
        {
            processArguments.ErrorDataReceived += ErrorData;
        }

        return processArguments;
    }

    private void ErrorData(object sender, string msg)
    {
        _onError?.Invoke(msg);

        var match = ProgressRegex.Match(msg);
        if (!match.Success)
        {
            return;
        }

        var processed = MediaAnalysisUtils.ParseDuration(match.Groups[1].Value);
        _onTimeProgress?.Invoke(processed);

        if (_onPercentageProgress == null || _totalTimespan == null)
        {
            return;
        }

        var percentage = Math.Round(processed.TotalSeconds / _totalTimespan.Value.TotalSeconds * 100, 2);
        _onPercentageProgress(percentage);
    }

    private void OutputData(object sender, string msg)
    {
        Debug.WriteLine(msg);
        _onOutput?.Invoke(msg);
    }
}
