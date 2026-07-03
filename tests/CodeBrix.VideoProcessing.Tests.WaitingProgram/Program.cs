using System;

// Test-support helper: a process that blocks until a line of input is sent to
// its stdin, used by the vendored Instances process-wrapper tests to exercise
// cancellation, kill, and send-input behaviour.
Console.Read();
