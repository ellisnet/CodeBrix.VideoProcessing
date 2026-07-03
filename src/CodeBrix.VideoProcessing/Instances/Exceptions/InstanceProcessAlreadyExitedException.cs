using System;

namespace CodeBrix.VideoProcessing.Instances.Exceptions; //was previously: Instances.Exceptions;

public class InstanceProcessAlreadyExitedException : Exception
{
    public InstanceProcessAlreadyExitedException() : base("The process instance has already exited")
    {
    }
    public InstanceProcessAlreadyExitedException(Exception innerException) : base("The process instance has already exited", innerException)
    {
    }
}
