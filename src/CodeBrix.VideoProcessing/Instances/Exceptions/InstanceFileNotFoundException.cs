using System;

namespace CodeBrix.VideoProcessing.Instances.Exceptions; //was previously: Instances.Exceptions;

public class InstanceFileNotFoundException : InstanceException
{
    public InstanceFileNotFoundException(string fileName, Exception innerException) : base($"File not found: {fileName}", innerException)
    {
    }
}
