﻿using System.Collections.Generic;
using System.Collections.ObjectModel;

using FlyleafLib.MediaFramework.MediaDemuxer;
using FlyleafLib.MediaFramework.MediaStream;

namespace FlyleafLib.MediaFramework.MediaPlaylist;

public class PlaylistItem : DemuxerInput
{
    public int      Index                   { get; set; } = -1; // if we need it we need to ensure we fix it in case of removing an item

    /// <summary>
    /// While the Url can expire or be null DirectUrl can be used as a new input for re-opening
    /// </summary>
    public string   DirectUrl               { get; set; }

    //public IOpen    OpenPlugin      { get; set; }

    /// <summary>
    /// Relative folder to playlist's folder base (can be empty, not null)
    /// Use Path.Combine(Playlist.FolderBase, Folder) to get absolute path for saving related files with the current selection item (such as subtitles)
    /// </summary>
    public string   Folder                  { get; set; } = "";


    //public long     StoppedAt               { get; set; }
    //public long     SubtitlesDelay          { get; set; }
    //public long     AudioDelay              { get; set; }

    /// <summary>
    /// Item's file size
    /// </summary>
    public long     FileSize                { get; set; }

    /// <summary>
    /// Item's title
    /// (can be updated from scrapers)
    /// </summary>
    public string   Title                   { get => _Title; set { if (_Title == "") OriginalTitle = value; SetUI(ref _Title, value ?? "", false);} }
    string _Title = "";

    /// <summary>
    /// Item's original title
    /// (setted by opened plugin)
    /// </summary>
    public string   OriginalTitle           { get => _OriginalTitle; set => SetUI(ref _OriginalTitle, value ?? "", false); }
    string _OriginalTitle = "";

    public List<Demuxer.Chapter>
                    Chapters                { get; set; } = new();

    public int      Season                  { get; set; }
    public int      Episode                 { get; set; }
    public int      Year                    { get; set; }

    public Dictionary<string, object>
                    Tag                     { get; set; } = new Dictionary<string, object>();
    public void AddTag(object tag, string pluginName)
    {
        if (Tag.ContainsKey(pluginName))
            Tag[pluginName] = tag;
        else
            Tag.Add(pluginName, tag);
    }

    public object GetTag(string pluginName)
        => Tag.ContainsKey(pluginName) ? Tag[pluginName] : null;

    public bool     SearchedLocal           { get; set; }
    public bool     SearchedOnline          { get; set; }

    /// <summary>
    /// Whether the item is currently enabled or not
    /// </summary>
    public bool     Enabled                 { get => _Enabled; set { if (SetUI(ref _Enabled, value) && value == true) OpenedCounter++; } }
    bool _Enabled;
    public int      OpenedCounter           { get; set; }

    public ExternalVideoStream
                    ExternalVideoStream     { get; set; }
    public ExternalAudioStream
                    ExternalAudioStream     { get; set; }
    public ExternalSubtitlesStream[]
                    ExternalSubtitlesStreams
                                            { get; set; } = new ExternalSubtitlesStream[2];

    public ObservableCollection<ExternalVideoStream>
                    ExternalVideoStreams    { get; set; } = new ObservableCollection<ExternalVideoStream>();
    public ObservableCollection<ExternalAudioStream>
                    ExternalAudioStreams    { get; set; } = new ObservableCollection<ExternalAudioStream>();
    public ObservableCollection<ExternalSubtitlesStream>
                    ExternalSubtitlesStreamsAll{ get; set; } = new ObservableCollection<ExternalSubtitlesStream>();
    internal object lockExternalStreams = new();

    public void AddExternalStream(ExternalStream extStream, PlaylistItem item, string pluginName, object tag = null)
    {
        lock (item.lockExternalStreams)
        {
            extStream.PlaylistItem = item;
            extStream.PluginName = pluginName;

            if (extStream is ExternalAudioStream)
            {
                item.ExternalAudioStreams.Add((ExternalAudioStream)extStream);
                extStream.Index = item.ExternalAudioStreams.Count - 1;
            }
            else if (extStream is ExternalVideoStream)
            {
                item.ExternalVideoStreams.Add((ExternalVideoStream)extStream);
                extStream.Index = item.ExternalVideoStreams.Count - 1;
            }
            else if (extStream is ExternalSubtitlesStream)
            {
                item.ExternalSubtitlesStreamsAll.Add((ExternalSubtitlesStream)extStream);
                extStream.Index = item.ExternalSubtitlesStreamsAll.Count - 1;
            }

            if (tag != null)
                extStream.AddTag(tag, pluginName);
        };
    }
}
