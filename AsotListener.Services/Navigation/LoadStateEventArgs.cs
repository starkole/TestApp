﻿namespace AsotListener.Services.Navigation
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Class used to hold the event data required when a page attempts to load state.
    /// </summary>
    public class LoadStateEventArgs : EventArgs
    {
        /// <summary>
        /// The parameter value passed to <see cref="Frame.Navigate(Type, object)"/> 
        /// when this page was initially requested.
        /// </summary>
        public object NavigationParameter { get; }

        /// <summary>
        /// A dictionary of state preserved by this page during an earlier
        /// session. This will be null the first time a page is visited.
        /// </summary>
        public Dictionary<string, object> PageState { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoadStateEventArgs"/> class.
        /// </summary>
        /// <param name="navigationParameter">
        /// The parameter value passed to <see cref="Frame.Navigate(Type, object)"/> 
        /// when this page was initially requested.
        /// </param>
        /// <param name="pageState">
        /// A dictionary of state preserved by this page during an earlier
        /// session.  This will be null the first time a page is visited.
        /// </param>
        public LoadStateEventArgs(object navigationParameter, Dictionary<string, object> pageState)
            : base()
        {
            this.NavigationParameter = navigationParameter;
            this.PageState = pageState;
        }
    }
}
