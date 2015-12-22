﻿namespace AsotListener.Models.Enums
{
    using Common.Attributes;

    public enum EpisodeStatus
    {
        [LocalizedDisplay("", "EpisodeStatusCanBeLoaded")]
        CanBeLoaded,

        [LocalizedDisplay("Downloading", "EpisodeStatusDownloading")]
        Downloading,

        [LocalizedDisplay("Loaded", "EpisodeStatusLoaded")]
        Loaded,

        [LocalizedDisplay("Playing", "EpisodeStatusPlaying")]
        Playing
    }
}
