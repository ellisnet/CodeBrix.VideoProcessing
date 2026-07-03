using System;

namespace CodeBrix.VideoProcessing.Instances.Exceptions; //was previously: Instances.Exceptions;

public class InstanceException : Exception
{
    public InstanceException(string msg, Exception innerException) : base(msg, innerException)
    {
    }
}
