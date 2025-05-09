﻿using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using FlyleafLib.MediaPlayer;
using LLPlayer.Converters;
using LLPlayer.Extensions;
using LLPlayer.Services;
using MaterialDesignColors.Recommended;
using KeyBinding = FlyleafLib.MediaPlayer.KeyBinding;

namespace LLPlayer.ViewModels;

public class CheatSheetDialogVM : Bindable, IDialogAware
{
    public FlyleafManager FL { get; }

    public CheatSheetDialogVM(FlyleafManager fl)
    {
        FL = fl;

        List<KeyBinding> keys = FL.PlayerConfig.Player.KeyBindings.Keys.Where(k => k.IsEnabled).ToList();

        HitCount = keys.Count;

        KeyToStringConverter keyConverter = new();

        List<KeyBindingCSGroup> groups = keys.Select(k =>
            {
                KeyBindingCS key = new()
                {
                    Action = k.Action,
                    ActionName = k.Action.ToString(),
                    Alt = k.Alt,
                    Ctrl = k.Ctrl,
                    Shift = k.Shift,
                    Description = k.Action.GetDescription(),
                    Group = k.Action.ToGroup(),
                    Key = k.Key,
                    KeyName = (string)keyConverter.Convert(k.Key, typeof(string), null, CultureInfo.CurrentCulture),
                    ActionInternal = k.ActionInternal,
                };

                if (key.Action == KeyBindingAction.Custom)
                {
                    if (!Enum.TryParse(k.ActionName, out CustomKeyBindingAction customAction))
                    {
                        HitCount -= 1;
                        return null;
                    }

                    key.ActionName = customAction.ToString();
                    key.CustomAction = customAction;
                    key.Description = customAction.GetDescription();
                    key.Group = customAction.ToGroup();
                }

                return key;
            })
            .Where(k => k != null)
            .OrderBy(k => k!.Action)
            .ThenBy(k => k!.CustomAction)
            .GroupBy(k => k!.Group)
            .OrderBy(g => g.Key)
            .Select(g => new KeyBindingCSGroup()
            {
                Group = g.Key,
                KeyBindings = g.ToList()!
            }).ToList();

        KeyBindingGroups = new List<KeyBindingCSGroup>(groups);

        List<ListCollectionView> collectionViews = KeyBindingGroups.Select(g => (ListCollectionView)CollectionViewSource.GetDefaultView(g.KeyBindings))
            .ToList();
        _collectionViews = collectionViews;

        foreach (ListCollectionView view in collectionViews)
        {
            view.Filter = (obj) =>
            {
                if (obj is not KeyBindingCS key)
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(SearchText))
                {
                    return true;
                }

                string query = SearchText.Trim();

                bool match = key.Description.Contains(query, StringComparison.OrdinalIgnoreCase);
                if (match)
                {
                    return true;
                }

                return key.Shortcut.Contains(query, StringComparison.OrdinalIgnoreCase);
            };
        }
    }

    public string SearchText
    {
        get;
        set
        {
            if (Set(ref field, value))
            {
                _collectionViews.ForEach(v => v.Refresh());

                HitCount = _collectionViews.Sum(v => v.Count);
            }
        }
    } = string.Empty;

    public int HitCount { get; set => Set(ref field, value); }

    private readonly List<ListCollectionView> _collectionViews;
    public List<KeyBindingCSGroup> KeyBindingGroups { get; set; }

    public DelegateCommand<KeyBindingCS>? CmdAction => field ??= new((key) =>
    {
        FL.Player.Activity.ForceFullActive();
        key.ActionInternal.Invoke();
    });

    #region IDialogAware
    public string Title { get; set => Set(ref field, value); } = $"CheatSheet - {App.Name}";
    public double WindowWidth { get; set => Set(ref field, value); } = 1000;
    public double WindowHeight { get; set => Set(ref field, value); } = 800;

    public bool CanCloseDialog() => true;
    public void OnDialogClosed() { }
    public void OnDialogOpened(IDialogParameters parameters) { }
    public DialogCloseListener RequestClose { get; }
    #endregion IDialogAware
}

public class KeyBindingCS
{
    public required bool Ctrl { get; init; }
    public required bool Shift { get; init; }
    public required bool Alt { get; init; }
    public required Key Key { get; init; }
    public required string KeyName { get; init; }

    [field: AllowNull, MaybeNull]
    public string Shortcut
    {
        get
        {
            if (field == null)
            {
                string modifiers = "";
                if (Ctrl)
                    modifiers += "Ctrl + ";
                if (Alt)
                    modifiers += "Alt + ";
                if (Shift)
                    modifiers += "Shift + ";
                field = $"{modifiers}{KeyName}";
            }

            return field;
        }
    }

    public required KeyBindingAction Action { get; init; }
    public CustomKeyBindingAction? CustomAction { get; set; }
    public required string ActionName { get; set; }
    public required string Description { get; set; }
    public required KeyBindingActionGroup Group { get; set; }

    public required Action ActionInternal { get; init; }
}

public class KeyBindingCSGroup
{
    public required KeyBindingActionGroup Group { get; init; }

    [field: AllowNull, MaybeNull]
    public string GroupName => field ??= Group.ToString();
    public required List<KeyBindingCS> KeyBindings { get; init; }

    public Color GroupColor =>
        Group switch
        {
            KeyBindingActionGroup.Playback => RedSwatch.Red500,
            KeyBindingActionGroup.Player => PinkSwatch.Pink500,
            KeyBindingActionGroup.Audio => PurpleSwatch.Purple500,
            KeyBindingActionGroup.Video => BlueSwatch.Blue500,
            KeyBindingActionGroup.Subtitles => TealSwatch.Teal500,
            KeyBindingActionGroup.SubtitlesPosition => GreenSwatch.Green500,
            KeyBindingActionGroup.Window => LightGreenSwatch.LightGreen500,
            KeyBindingActionGroup.Other => DeepOrangeSwatch.DeepOrange500,
            _ => BrownSwatch.Brown500
        };
}
