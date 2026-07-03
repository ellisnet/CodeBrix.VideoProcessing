using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

public interface IDynamicArgument
{
    /// <summary>
    ///     Same as <see cref="IArgument.Text" />, but this receives the arguments generated before as parameter
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    //public string GetText(StringBuilder context);
    string GetText(IEnumerable<IArgument> context);
}
