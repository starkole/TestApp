﻿namespace AsotListener.Services.Implementations
{
    using System.Text.RegularExpressions;
    using Models;
    using System.Collections.ObjectModel;
    using System;
    using Contracts;

    /// <summary>
    /// Provides methods for parsing html pages
    /// </summary>
    public class Parser : IParser
    {
        private const string EPISODE_REGEX = @"(/episode/\?p=\d+)"">(.*)<";
        private const string DOWNLOAD_LINK_REGEX = @"http://\S*\.mp3";
        private const string EPISODE_NAME_START = @"A State Of Trance: ";

        private static Lazy<IParser> lazy = new Lazy<IParser>(() => new Parser());

        public static IParser Instance => lazy.Value;

        private Parser() { }

        /// <summary>
        /// Extracts episodes information from given html page
        /// </summary>
        /// <param name="EpisodeListHtmlPage">Html page</param>
        /// <returns>List of Episode objects or empty list</returns>
        public ObservableCollection<Episode> ParseEpisodeList(string EpisodeListHtmlPage)
        {
            Regex regex = new Regex(EPISODE_REGEX);
            MatchCollection matches = regex.Matches(EpisodeListHtmlPage);
            ObservableCollection<Episode> result = new ObservableCollection<Episode>();
            for (var i = 0; i < matches.Count; i++)
            {

                // Regex puts episode relative url into the first group
                // and episode name into the second one
                if (matches[i].Groups.Count == 3)
                {
                    result.Add(new Episode
                    {
                        Name = normalizeEpisodeName(matches[i].Groups[2].Value),
                        Url = matches[i].Groups[1].Value
                    });
                }
            };

            return result;
        }

        /// <summary>
        /// Extracts download links to mp3 files from given html page
        /// </summary>
        /// <param name="EpisodeHtmlPage">Html page</param>
        /// <returns>List of download links or empty list</returns>
        public string[] ExtractDownloadLinks(string EpisodeHtmlPage)
        {
            Regex regex = new Regex(DOWNLOAD_LINK_REGEX);
            MatchCollection matches = regex.Matches(EpisodeHtmlPage);
            if (matches.Count == 0)
            {
                return new string[0];
            }

            string[] result = new string[matches.Count];
            for (var i = 0; i < matches.Count; i++)
            {
                result[i] = matches[i].Value;
            }

            return result;
        }

        private string normalizeEpisodeName(string rawName)
        {
            Regex episodeNameStart = new Regex(EPISODE_NAME_START);
            string nameWithStrippedStart = episodeNameStart.Replace(rawName, string.Empty);
            Regex containsABadCharacter = new Regex("[" + Regex.Escape(new string(System.IO.Path.GetInvalidPathChars())) + "]");
            return containsABadCharacter.Replace(nameWithStrippedStart, string.Empty);
        }
    }
}