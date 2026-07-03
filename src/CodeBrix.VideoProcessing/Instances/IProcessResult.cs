using System.Collections.Generic;

namespace CodeBrix.VideoProcessing.Instances; //was previously: Instances;

public interface IProcessResult 
{
    int ExitCode { get; }
    
    IReadOnlyList<string> OutputData { get; }
    
    IReadOnlyList<string> ErrorData { get; }
}
