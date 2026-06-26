using System;
using System.Collections.Generic;

namespace ManagerIV.Core;

public record MusicTrack(
    string Id,
    string OriginalFileName,
    string Title,
    string Artist,
    string Album,
    TimeSpan Duration,
    string FileExtension,
    bool IsEnabled = true
);

public record MusicManifest(
    List<MusicTrack> Tracks
);
