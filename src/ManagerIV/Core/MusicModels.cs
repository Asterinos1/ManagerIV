using System;
using System.Collections.Generic;

namespace ManagerIV.Core;

/// <summary>
/// Represents a music track details stored in the application's music library.
/// </summary>
/// <param name="Id">The unique track identifier.</param>
/// <param name="OriginalFileName">The original file name from which the track was imported.</param>
/// <param name="Title">The track title.</param>
/// <param name="Artist">The track artist.</param>
/// <param name="Album">The album to which the track belongs.</param>
/// <param name="Duration">The play duration of the track.</param>
/// <param name="FileExtension">The track file format extension.</param>
/// <param name="IsEnabled">Whether the track is enabled for deployment.</param>
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

/// <summary>
/// Represents the manifest structure storing the library of music tracks.
/// </summary>
/// <param name="Tracks">The list of imported music tracks.</param>
public record MusicManifest(
    List<MusicTrack> Tracks
);
