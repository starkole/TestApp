﻿namespace AsotListener.App.Converters
{
    using System;
    using Models;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Data;

    public class ContextMenuVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Modifies the source data before passing it to the target for display in the UI.
        /// </summary>
        /// <param name="value">The source data being passed to the target</param>
        /// <param name="targetType">The type of the target property, as a type reference</param>
        /// <param name="parameter">An optional parameter to be used in the converter logic</param>
        /// <param name="language">The language of the conversion</param>
        /// <returns>The value to be passed to the target dependency property</returns>
        public object Convert(object value, Type targetType, object itemType, string language)
        {
            Visibility result = Visibility.Collapsed;
            Episode episode = value as Episode;
            ContextMenuItem menuItem;
            bool isItemTypeValid = Enum.TryParse(itemType.ToString(), out menuItem);
            if (episode == null || !isItemTypeValid)
            {
                return result;
            }


            switch (menuItem)
            {
                case ContextMenuItem.Download:
                    if (episode.Status == EpisodeStatus.CanBeLoaded)
                    {
                        result = Visibility.Visible;
                    }
                    break;
                case ContextMenuItem.CancelDownload:
                    if (episode.Status == EpisodeStatus.Downloading)
                    {
                        result = Visibility.Visible;
                    }
                    break;
                case ContextMenuItem.Delete:
                    if (episode.Status == EpisodeStatus.Loaded)
                    {
                        result = Visibility.Visible;
                    }
                    break;
                case ContextMenuItem.Play:
                    if (episode.Status == EpisodeStatus.Loaded)
                    {
                        result = Visibility.Visible;
                    }
                    break;
                case ContextMenuItem.AddToPlaylist:
                    if (episode.Status == EpisodeStatus.Loaded)
                    {
                        result = Visibility.Visible;
                    }
                    break;
                default:
                    break;
            }

            return result;
        }

        /// <summary>
        /// Modifies the target data before passing it to the source object. This method is called only in TwoWay bindings.
        /// </summary>
        /// <param name="value">The target data being passed to the source.</param>
        /// <param name="targetType">The type of the target property, as a type reference.</param>
        /// <param name="parameter">An optional parameter to be used in the converter logic.</param>
        /// <param name="language">The language of the conversion.</param>
        /// <returns>The value to be passed to the source object.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
