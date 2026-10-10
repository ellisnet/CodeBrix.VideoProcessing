================================================================================
README-INDEX: CodeBrix.VideoProcessing
Map of the README files in this repository
================================================================================

If you are an AI coding agent: find the NuGet package you are consuming below
and read its AGENT-README file in full. Read MAINTAINER-README.txt only if you
are changing this repository itself.

This repository produces two packages. They are complementary: the Core
package is the engine-neutral video processing contract, and
CodeBrix.VideoProcessing is the FFmpeg wrapper that also implements that
contract as the engine for Windows, Linux and macOS (it brings the Core
package with it).

AGENT-README FILES (consumer documentation, one per NuGet package)
------------------------------------------------------------------
  AGENT-README.txt
      CodeBrix.VideoProcessing.MitLicenseForever - fully managed, cross-platform
      FFmpeg / FFprobe wrapper: media analysis, transcoding, snapshots, pipes,
      metadata and an in-memory image bridge. Also the desktop ENGINE for the
      Core contract (CodeBrixVideoProcessing.Register()).
  src/CodeBrix.VideoProcessing.Core/AGENT-README.txt
      CodeBrix.VideoProcessing.Core.MitLicenseForever - the dependency-free,
      engine-neutral contract: the VideoProcessors registry, IVideoProcessor,
      media description, decoded frame and sample sources, the standard MP4
      writer, transcode / trim / concatenate requests, and the VideoEncoders
      registry that standalone encoder packages plug into. For consumers that
      must not care which engine runs, and for engine and encoder implementers.

MAINTAINER AND EXTRAS
---------------------
  MAINTAINER-README.txt
      Building, testing, packaging, versioning and provenance notes for
      maintainers.
  EXTRAS-README.txt
      Samples, tools and other non-package content in this repository.

GENERAL
-------
  README.md
      Human-facing overview shown on GitHub and nuget.org.
  README-INDEX.txt
      This file.
  THIRD-PARTY-NOTICES.txt
      What came from where, and under which licences.

================================================================================
