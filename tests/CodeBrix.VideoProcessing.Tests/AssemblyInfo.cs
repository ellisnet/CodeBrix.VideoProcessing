using Xunit;

// These are integration tests: most spawn external ffmpeg / ffprobe processes,
// and the vendored Instances tests spawn dotnet processes. Running test classes
// in parallel floods the machine with concurrent processes and grinds to a halt,
// so test-collection parallelization is disabled and the suite runs sequentially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
