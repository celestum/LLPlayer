﻿using System.Collections.ObjectModel;
using System.IO;

using FlyleafLib.MediaFramework.MediaContext;

using static FlyleafLib.Utils;

namespace FlyleafLib.MediaFramework.MediaPlaylist;

public class Playlist : NotifyPropertyChanged
{
    /// <summary>
    /// Url provided by user
    /// </summary>
    public string       Url             { get => _Url;   set { string fixedUrl = FixFileUrl(value); SetUI(ref _Url, fixedUrl); } }
    string _Url;

    /// <summary>
    /// IOStream provided by user
    /// </summary>
    public Stream       IOStream        { get; set; }

    /// <summary>
    /// Playlist's folder base which can be used to save related files
    /// </summary>
    public string       FolderBase      { get; set; }

    /// <summary>
    /// Playlist's title
    /// </summary>
    public string       Title           { get => _Title; set => SetUI(ref _Title, value); }
    string _Title;

    public int          ExpectingItems  { get => _ExpectingItems; set => SetUI(ref _ExpectingItems, value); }
    int _ExpectingItems;

    public bool         Completed       { get; set; }

    /// <summary>
    /// Playlist's opened/selected item
    /// </summary>
    public PlaylistItem Selected        { get => _Selected; internal set { SetUI(ref _Selected, value); UpdatePrevNextItem(); } }
    PlaylistItem _Selected;

    public PlaylistItem NextItem        { get => _NextItem; internal set => SetUI(ref _NextItem, value); }
    PlaylistItem _NextItem;

    public PlaylistItem PrevItem        { get => _PrevItem; internal set => SetUI(ref _PrevItem, value); }
    PlaylistItem _PrevItem;

    internal void UpdatePrevNextItem()
    {
        if (Selected == null)
        {
            PrevItem = NextItem = null;
            return;
        }

        for (int i=0; i < Items.Count; i++)
        {
            if (Items[i] == Selected)
            {
                PrevItem = i > 0 ? Items[i - 1] : null;
                NextItem = i < Items.Count - 1 ? Items[i + 1] : null;

                return;
            }
        }
    }

    /// <summary>
    /// Type of the provided input (such as File, UNC, Torrent, Web, etc.)
    /// </summary>
    public InputType    InputType       { get; set; }

    // TODO: MediaType (Music/MusicClip/Movie/TVShow/etc.) probably should go per Playlist Item

    public ObservableCollection<PlaylistItem>
                        Items           { get; set; } = new ObservableCollection<PlaylistItem>();
    object lockItems = new();

    long openCounter;
    //long openItemCounter;
    internal DecoderContext decoder;
    LogHandler Log;

    public Playlist(int uniqueId)
    {
        Log = new LogHandler(("[#" + uniqueId + "]").PadRight(8, ' ') + " [Playlist] ");
        UIInvokeIfRequired(() => System.Windows.Data.BindingOperations.EnableCollectionSynchronization(Items, lockItems));
    }

    public void Reset()
    {
        openCounter = decoder.OpenCounter;

        lock (lockItems)
            Items.Clear();

        bool noupdate = _Url == null && _Title == null && _Selected == null;

        _Url        = null;
        _Title      = null;
        _Selected   = null;
        PrevItem    = null;
        NextItem    = null;
        IOStream    = null;
        FolderBase  = null;
        Completed   = false;
        ExpectingItems = 0;

        InputType   = InputType.Unknown;

        if (!noupdate)
            UI(() =>
            {
                Raise(nameof(Url));
                Raise(nameof(Title));
                Raise(nameof(Selected));
            });
    }

    public void AddItem(PlaylistItem item, string pluginName, object tag = null)
    {
        if (openCounter != decoder.OpenCounter)
        {
            Log.Debug("AddItem Cancelled");
            return;
        }

        lock (lockItems)
        {
            Items.Add(item);
            Items[^1].Index = Items.Count - 1;

            UpdatePrevNextItem();

            if (tag != null)
                item.AddTag(tag, pluginName);
        };

        decoder.ScrapeItem(item);

        UIInvokeIfRequired(() =>
        {
            System.Windows.Data.BindingOperations.EnableCollectionSynchronization(item.ExternalAudioStreams, item.lockExternalStreams);
            System.Windows.Data.BindingOperations.EnableCollectionSynchronization(item.ExternalVideoStreams, item.lockExternalStreams);
            System.Windows.Data.BindingOperations.EnableCollectionSynchronization(item.ExternalSubtitlesStreamsAll, item.lockExternalStreams);
        });
    }
}
