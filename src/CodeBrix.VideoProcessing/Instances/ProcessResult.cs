using System.Collections.Generic;

namespace CodeBrix.VideoProcessing.Instances; //was previously: Instances;

public class ProcessResult : IProcessResult
{
    internal ProcessResult(int exitCode, IReadOnlyList<string> outputData, IReadOnlyList<string> errorData)
    {
        OutputData = outputData;
        ErrorData = errorData;
        ExitCode = exitCode;
    }
    
    public int ExitCode { get; }
    
    public IReadOnlyList<string> OutputData { get; }
    
    public IReadOnlyList<string> ErrorData { get; }
}
