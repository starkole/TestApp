﻿namespace AsotListener.AudioPlayer
{
    using System.Collections.ObjectModel;
    using Models;

    public interface IPlayList
    {
        ObservableCollection<AudioTrack> TrackList { get; }
        AudioTrack CurrentTrack { get; set; }
        void SavePlaylistToLocalStorage();
        void LoadPlaylistFromLocalStorage();
    }
}