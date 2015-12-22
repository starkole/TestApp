﻿namespace AsotListener.App.ViewModels
{
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading.Tasks;
    using Services.Contracts;
    using Models;
    using System.Windows.Input;
    using Windows.Foundation.Diagnostics;
    using Windows.Networking.BackgroundTransfer;
    using System.Collections.Generic;
    using Windows.UI.Core;
    using Windows.ApplicationModel.Core;
    using Models.Enums;
    using Windows.UI.Popups;
    using Windows.UI.Xaml;
    using Windows.ApplicationModel;
    using static Models.Enums.EpisodeStatus;

    /// <summary>
    /// View model of episodes list
    /// </summary>
    public sealed class EpisodesViewModel : BaseModel
    {
        #region Fields

        private const double defaultEpisodeSize = 400 * 1024 * 1024; // 400MB
        private const string episodeListFileName = "episodeList.xml";
        private ObservableCollection<Episode> episodes;
        private Dictionary<Episode, List<DownloadOperation>> activeDownloadsByEpisode;
        private Dictionary<DownloadOperation, Episode> activeDownloadsByDownload;
        private readonly ILogger logger;
        private readonly IFileUtils fileUtils;
        private readonly IPlayList playlist;
        private readonly ILoaderFactory loaderFactory;
        private readonly IParser parser;
        private readonly INavigationService navigationService;

        #endregion

        #region Properties

        /// <summary>
        /// A list of available episodes
        /// </summary>
        public ObservableCollection<Episode> EpisodeList
        {
            get { return episodes; }
            private set { SetField(ref episodes, value, nameof(EpisodeList)); }
        }

        /// <summary>
        /// Downloads fresh episodes list from server
        /// </summary>
        public ICommand RefreshCommand { get; }

        /// <summary>
        /// Downloads specified episode
        /// </summary>
        public ICommand DownloadCommand { get; }

        /// <summary>
        /// Cancels episode download
        /// </summary>
        public ICommand CancelDownloadCommand { get; }

        /// <summary>
        /// Deletes all downloaded data of given episode
        /// </summary>
        public ICommand DeleteCommand { get; }

        /// <summary>
        /// Starts playing given episode in player
        /// </summary>
        public ICommand PlayCommand { get; }

        /// <summary>
        /// Adds given episode to playlist
        /// </summary>
        public ICommand AddToPlaylistCommand { get; }

        /// <summary>
        /// Clears current playlist
        /// </summary>
        public ICommand ClearPlaylistCommand { get; }

        #endregion

        #region Ctor

        /// <summary>
        /// Creates new instance of <see cref="EpisodesViewModel"/>
        /// </summary>
        /// <param name="logger">Instance of <see cref="ILogger"/></param>
        /// <param name="fileUtils">Instance of <see cref="IFileUtils"/></param>
        /// <param name="playlist">Instance of <see cref="IPlayList"/></param>
        /// <param name="loaderFactory">Instance of <see cref="ILoaderFactory"/></param>
        /// <param name="parser">Instance of <see cref="IParser"/></param>
        /// <param name="navigationService">Instance of <see cref="INavigationService"/></param>
        public EpisodesViewModel(
            ILogger logger,
            IFileUtils fileUtils,
            IPlayList playlist,
            ILoaderFactory loaderFactory,
            IParser parser,
            INavigationService navigationService)
        {
            this.logger = logger;
            this.fileUtils = fileUtils;
            this.playlist = playlist;
            this.loaderFactory = loaderFactory;
            this.parser = parser;
            this.navigationService = navigationService;

            RefreshCommand = new RelayCommand(loadEpisodeListFromServer);
            DownloadCommand = new RelayCommand(downloadEpisode);
            CancelDownloadCommand = new RelayCommand((Action<object>)cancelDownload);
            DeleteCommand = new RelayCommand((Action<object>)deleteEpisodeFromStorage);
            PlayCommand = new RelayCommand((Action<object>)playEpisode);
            AddToPlaylistCommand = new RelayCommand((Action<object>)addToPlaylistCommand);
            ClearPlaylistCommand = new RelayCommand((Action)clearPlaylistCommand);

            activeDownloadsByEpisode = new Dictionary<Episode, List<DownloadOperation>>();
            activeDownloadsByDownload = new Dictionary<DownloadOperation, Episode>();

            Application.Current.Resuming += onAppResuming;
            Application.Current.Suspending += onAppSuspending;

            initializeAsync();
            logger.LogMessage("EpisodesViewModel: Initialized.", LoggingLevel.Information);
        }

        private async void initializeAsync()
        {
            await loadEpisodesList();
            await retrieveActiveDownloads();
            logger.LogMessage("EpisodesViewModel: State restored.", LoggingLevel.Information);
        }

        #endregion

        #region Commands

        private async Task loadEpisodeListFromServer()
        {
            logger.LogMessage("EpisodesViewModel: Loading episode list from server...");
            using (ILoader loader = loaderFactory.GetLoader())
            {
                string episodeListPage = await loader.FetchEpisodeListAsync();
                EpisodeList = parser.ParseEpisodeList(episodeListPage);
            }
            await updateEpisodesStates();
            await fileUtils.SaveToXmlFile(EpisodeList, episodeListFileName);
            logger.LogMessage("EpisodesViewModel: Episode list loaded.", LoggingLevel.Information);
        }

        private async Task downloadEpisode(object boxedEpisode)
        {
            logger.LogMessage("EpisodesViewModel: Downloading episode...");
            var episode = boxedEpisode as Episode;
            if (episode == null)
            {
                logger.LogMessage("EpisodesViewModel: Invalid episode specified for download.", LoggingLevel.Warning);
                return;
            }

            using (ILoader loader = loaderFactory.GetLoader())
            {
                string episodePage = await loader.FetchEpisodePageAsync(episode);
                episode.DownloadLinks = parser.ExtractDownloadLinks(episodePage);
            }

            episode.Status = Downloading;
            for (var i = 0; i < episode.DownloadLinks.Length; i++)
            {
                await scheduleDownloadAsync(episode, i);
            }
            logger.LogMessage($"EpisodesViewModel: All downloads for episode {episode.Name} have been scheduled successfully.");
        }

        private void cancelDownload(object boxedEpisode)
        {
            logger.LogMessage("EpisodesViewModel: Cancelling download...");
            var episode = boxedEpisode as Episode;
            if (episode == null)
            {
                logger.LogMessage("EpisodesViewModel: Cannot cancel downloads. Invalid episode specified. ", LoggingLevel.Warning);
                return;
            }

            if (episode.Status != Downloading)
            {
                logger.LogMessage("EpisodesViewModel: Cannot cancel downloads. Episode status is wrong.", LoggingLevel.Warning);
                return;
            }

            if (!activeDownloadsByEpisode.ContainsKey(episode))
            {
                logger.LogMessage("EpisodesViewModel: Cannot cancel downloads. No downloads list found.", LoggingLevel.Warning);
                return;
            }

            if (!activeDownloadsByEpisode[episode].Any())
            {
                logger.LogMessage("EpisodesViewModel: Cannot cancel downloads. Downloads list is empty.", LoggingLevel.Warning);
                return;
            }

            // Making shallow copy of the list here because on cancelling episode, 
            // handleDownloadAsync method will delete if from original list.
            var episodeDownloads = activeDownloadsByEpisode[episode].ToList();
            foreach (var download in episodeDownloads)
            {
                logger.LogMessage($"EpisodesViewModel: Stopping download from {download.RequestedUri} to {download.ResultFile.Name}", LoggingLevel.Warning);
                download.AttachAsync().Cancel();
            }
            logger.LogMessage($"EpisodesViewModel: All downloads for episode {episode.Name} have been cancelled successfully.");
        }

        private async void deleteEpisodeFromStorage(object boxedEpisode)
        {
            logger.LogMessage("EpisodesViewModel: Deleting episode...");
            var episode = boxedEpisode as Episode;
            if (canEpisodeBeDeleted(episode))
            {
                var tracksToRemove = playlist.TrackList.Where(t => t.EpisodeName == episode.Name).ToList();
                foreach (var track in tracksToRemove)
                {
                    playlist.TrackList.Remove(track);
                }

                if (playlist.CurrentTrack.EpisodeName == episode.Name)
                {
                    playlist.CurrentTrack = playlist.TrackList.FirstOrDefault();
                }

                await playlist.SavePlaylistToLocalStorage();
                await fileUtils.DeleteEpisode(episode.Name);
            }
            episode.Status = CanBeLoaded;
            logger.LogMessage("EpisodesViewModel: Episode has been deleted.");
        }

        private async void playEpisode(object boxedEpisode)
        {
            logger.LogMessage("EpisodesViewModel: Scheduling episode playback episode...");
            var episode = boxedEpisode as Episode;
            if (episode == null)
            {
                logger.LogMessage($"EpisodesViewModel: Cannot play empty episode.", LoggingLevel.Warning);
                return;
            }

            if (!(episode.Status == Loaded || episode.Status == Playing))
            {
                logger.LogMessage($"EpisodesViewModel: Cannot play episode. It has invalid status.", LoggingLevel.Warning);
                return;
            }

            var existingTracks = playlist.TrackList.Where(t => t.Name.StartsWith(episode.Name, StringComparison.CurrentCulture));
            playlist.CurrentTrack = existingTracks.Any() ?
                existingTracks.First() :
                await addEpisodeToPlaylist(episode);
            await playlist.SavePlaylistToLocalStorage();
            episode.Status = Playing;
            navigationService.Navigate(NavigationParameter.StartPlayback);
            logger.LogMessage("EpisodesViewModel: Episode scheduled to play.");
        }

        private async void addToPlaylistCommand(object boxedEpisode)
        {
            logger.LogMessage("EpisodesViewModel: Executing add to playlist command...");
            var episode = boxedEpisode as Episode;
            if (episode == null)
            {
                logger.LogMessage($"EpisodesViewModel: Cannot add empty episode to playlist.", LoggingLevel.Warning);
                return;
            }

            if (episode.Status != Loaded)
            {
                logger.LogMessage($"EpisodesViewModel: Cannot add episode to playlist. It has invalid status.", LoggingLevel.Warning);
                return;
            }

            var existingTracks = playlist.TrackList.Where(t => t.Name.StartsWith(episode.Name, StringComparison.CurrentCulture));
            if (existingTracks.Any())
            {
                logger.LogMessage("EpisodesViewModel: Episode is already in playlist.");
                return;
            }

            await addEpisodeToPlaylist(episode);
            await playlist.SavePlaylistToLocalStorage();
            episode.Status = Playing;
            logger.LogMessage("EpisodesViewModel: Add to playlist command executed.");
        }

        private async void clearPlaylistCommand()
        {
            logger.LogMessage("EpisodesViewModel: Executing ClearPlaylist command...");
            playlist.CurrentTrack = null;
            playlist.TrackList.Clear();
            await playlist.SavePlaylistToLocalStorage();
            await updateEpisodesStates();
            logger.LogMessage("EpisodesViewModel: ClearPlaylist command executed.");
        }
        #endregion

        #region Private Methods

        private async void onAppResuming(object sender, object e)
        {
            logger.LogMessage("EpisodesViewModel: Application is resuming. Restoring state...");
            await updateEpisodesStates();
            await retrieveActiveDownloads();
            logger.LogMessage("EpisodesViewModel: State restored after application resuming.", LoggingLevel.Information);
        }

        private async void onAppSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            logger.LogMessage("EpisodesViewModel: Application is suspending. Saving state...");
            await fileUtils.SaveToXmlFile(EpisodeList, episodeListFileName);
            logger.LogMessage("EpisodesViewModel: State saved on application suspending.", LoggingLevel.Information);
            deferral.Complete();
        }

        private async Task loadEpisodesList()
        {
            logger.LogMessage("EpisodesViewModel: Loading episodes list...");
            EpisodeList = await fileUtils.ReadFromXmlFile<ObservableCollection<Episode>>(episodeListFileName);
            if (EpisodeList == null || !EpisodeList.Any())
            {
                logger.LogMessage("EpisodesViewModel: Saved list hasn't been found. Loading list from server.");
                await loadEpisodeListFromServer();
            }
            else
            {
                logger.LogMessage("EpisodesViewModel: Loaded saved list from local storage. Updating episode states.");
                await updateEpisodesStates();
            }
            logger.LogMessage("EpisodesViewModel: Episodes list loaded.");
        }

        private async Task retrieveActiveDownloads()
        {
            logger.LogMessage("EpisodesViewModel: Obtaining background downloads...");
            IReadOnlyList<DownloadOperation> downloads = await BackgroundDownloader.GetCurrentDownloadsAsync();
            logger.LogMessage($"EpisodesViewModel: {downloads.Count} background downloads found.", LoggingLevel.Information);
            foreach (var download in downloads)
            {
                string filename = download.ResultFile.Name;
                string episodeName = fileUtils.ExtractEpisodeNameFromFilename(filename);

                var episode = EpisodeList.FirstOrDefault(e => e.Name.StartsWith(episodeName, StringComparison.CurrentCulture));
                if (episode == null)
                {
                    logger.LogMessage($"EpisodesViewModel: Stale download detected. Stopping download from {download.RequestedUri} to {download.ResultFile.Name}", LoggingLevel.Warning);
                    download.AttachAsync().Cancel();
                    fileUtils.TryDeleteFile(filename).Start();
                    break;
                }

                episode.Status = Downloading;
                handleDownloadAsync(download, episode, DownloadState.AlreadyRunning);
            }
            logger.LogMessage("EpisodesViewModel: Background downloads processed.");
        }

        private async Task scheduleDownloadAsync(Episode episode, int partNumber)
        {
            logger.LogMessage($"EpisodesViewModel: Scheduling download part {partNumber + 1} of {episode.DownloadLinks.Length}.");
            var downloader = new BackgroundDownloader();
            var uri = new Uri(episode.DownloadLinks[partNumber]);
            var file = await fileUtils.CreateEpisodePartFile(episode.Name, partNumber);
            if (file == null)
            {
                string message = "Cannot create file to save episode.";
                logger.LogMessage(message, LoggingLevel.Error);
                CoreDispatcher dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    MessageDialog MDialog = new MessageDialog(message, "Error in ASOT Listener");
                    await MDialog.ShowAsync();
                });
                return;
            }

            var download = downloader.CreateDownload(uri, file);
            download.CostPolicy = BackgroundTransferCostPolicy.UnrestrictedOnly;
            handleDownloadAsync(download, episode, DownloadState.NotStarted);
            logger.LogMessage($"EpisodesViewModel: Successfully scheduled download from {episode.DownloadLinks[partNumber]} to {file.Name}.");
        }

        private async void handleDownloadAsync(DownloadOperation download, Episode episode, DownloadState downloadState)
        {
            try
            {
                logger.LogMessage($"EpisodesViewModel: Registering download of file {download.ResultFile.Name}.", LoggingLevel.Information);
                activeDownloadsByDownload.Add(download, episode);
                if (activeDownloadsByEpisode.Keys.Contains(episode))
                {
                    activeDownloadsByEpisode[episode].Add(download);
                }
                else
                {
                    activeDownloadsByEpisode.Add(episode, new List<DownloadOperation> { download });
                }

                Progress<DownloadOperation> progressCallback = new Progress<DownloadOperation>(downloadProgress);
#if DEBUG
                download.CostPolicy = BackgroundTransferCostPolicy.Always;
#endif
                if (downloadState == DownloadState.NotStarted)
                {
                    logger.LogMessage($"EpisodesViewModel: Download hasn't been started yet. Starting it.");
                    await download.StartAsync().AsTask(progressCallback);
                }
                if (downloadState == DownloadState.AlreadyRunning)
                {
                    logger.LogMessage($"EpisodesViewModel: Download has been already started. Attaching progress handler to it.");
                    await download.AttachAsync().AsTask(progressCallback);
                }

                ResponseInformation response = download.GetResponseInformation();
                logger.LogMessage($"EpisodesViewModel: Download of {download.ResultFile.Name} completed. Status Code: {response.StatusCode}", LoggingLevel.Information);
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => episode.Status = Loaded);
            }
            catch (TaskCanceledException)
            {
                logger.LogMessage("Download cancelled.");
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => episode.Status = CanBeLoaded);
            }
            catch (Exception ex)
            {
                logger.LogMessage($"EpisodesViewModel: Download error. {ex.Message}", LoggingLevel.Error);
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => episode.Status = CanBeLoaded);
            }
            finally
            {
                unregisterDownlod(download, episode);
            }
        }

        private void unregisterDownlod(DownloadOperation download, Episode episode)
        {
            logger.LogMessage($"EpisodesViewModel: Unregistering download of file {download.ResultFile.Name}.");
            if (activeDownloadsByDownload.ContainsKey(download))
            {
                activeDownloadsByDownload.Remove(download);
            }

            if (activeDownloadsByEpisode.ContainsKey(episode))
            {
                activeDownloadsByEpisode[episode].Remove(download);
                if (!activeDownloadsByEpisode[episode].Any())
                {
                    logger.LogMessage($"EpisodesViewModel: No downloads left for episode {episode.Name}. Unregistering episode.");
                    activeDownloadsByEpisode.Remove(episode);
                }
            }

            logger.LogMessage($"EpisodesViewModel: Download of file {download.ResultFile.Name} has been unregistered.");
        }

        private async void downloadProgress(DownloadOperation download)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (!activeDownloadsByDownload.ContainsKey(download))
                {
                    logger.LogMessage($"EpisodesViewModel: Tried to report progress for download of {download.ResultFile.Name} file. But it is not registered.", LoggingLevel.Warning);
                    return;
                }

                Episode episode = activeDownloadsByDownload[download];
                if (!activeDownloadsByEpisode.ContainsKey(episode) || !activeDownloadsByEpisode[episode].Any())
                {
                    logger.LogMessage($"EpisodesViewModel: Tried to report progress for {episode.Name} episode download. But they are not registered.", LoggingLevel.Warning);
                    return;
                }

                episode.OverallDownloadSize = activeDownloadsByEpisode[episode].Sum((Func<DownloadOperation, double>)getTotalBytesToDownload);
                episode.DownloadedAmount = activeDownloadsByEpisode[episode].Sum(d => (double)d.Progress.BytesReceived);
                logger.LogMessage($"EpisodesViewModel: {episode.Name}: downloaded {episode.DownloadedAmount} of {episode.OverallDownloadSize} bytes.");
            });
        }

        private double getTotalBytesToDownload(DownloadOperation download)
        {
            var total = download.Progress.TotalBytesToReceive;

            // Some servers don't return file size to download, 
            // so we are using default approximated value in such a case.
            return total == 0 ? defaultEpisodeSize : total;
        }

        private async Task<AudioTrack> addEpisodeToPlaylist(Episode episode)
        {
            logger.LogMessage("EpisodesViewModel: Adding episode to playlist...");
            var fileNames = await fileUtils.GetFilesListForEpisode(episode.Name);
            int counter = 0;
            AudioTrack firstAddedTrack = null;
            foreach (var file in fileNames)
            {
                counter++;
                var track = new AudioTrack(episode.Name)
                {
                    Name = playlist.GetAudioTrackName(episode.Name, counter),
                    Uri = file.Path
                };

                if (counter == 1)
                {
                    firstAddedTrack = track;
                }
            }

            logger.LogMessage($"EpisodesViewModel: Episode {episode.Name} added to playlist.", LoggingLevel.Information);
            return firstAddedTrack;
        }

        private async Task updateEpisodesStates()
        {
            logger.LogMessage("EpisodesViewModel: Updating episode states...");
            if (EpisodeList == null || !EpisodeList.Any())
            {
                logger.LogMessage("EpisodesViewModel: Cannot update episode states. Episode list is empty.", LoggingLevel.Warning);
                return;
            }

            var existingFileNames = await fileUtils.GetDownloadedFileNamesList();
            foreach (Episode episode in EpisodeList)
            {
                if (existingFileNames.Contains(episode.Name))
                {
                    if (playlist.TrackList.Any(t => t.EpisodeName == episode.Name))
                    {
                        episode.Status = Playing;
                        continue;
                    }

                    episode.Status = Loaded;
                    continue;
                }

                if (activeDownloadsByEpisode.ContainsKey(episode))
                {
                    episode.Status = Downloading;
                }
            }
            logger.LogMessage("EpisodesViewModel: Episode states has been updated successfully.");
        }

        private bool canEpisodeBeDeleted(Episode episode) =>
            episode != null &&
            (episode.Status == Loaded ||
            episode.Status == Playing);

        #endregion
    }
}
