using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Soundboard.Models;

namespace Soundboard.ViewModels;

public sealed partial class ProfileViewModel : ObservableObject
{
    readonly Action _onChanged;

    [ObservableProperty] string _name;
    [ObservableProperty] string _icon;

    public ProfileViewModel(Profile model, IEnumerable<PadViewModel> pads, Action onChanged)
    {
        Model = model;
        _onChanged = onChanged;
        _name = model.Name;
        _icon = model.Icon;
        Pads = new ObservableCollection<PadViewModel>(pads);
    }

    public Profile Model { get; }

    public ObservableCollection<PadViewModel> Pads { get; }

    public void Add(PadViewModel pad)
    {
        Pads.Add(pad);
        Model.Pads.Add(pad.Model);
        _onChanged();
    }

    public void Remove(PadViewModel pad)
    {
        pad.Stop();
        Pads.Remove(pad);
        Model.Pads.Remove(pad.Model);
        _onChanged();
    }

    public void StopAll()
    {
        foreach (var pad in Pads) pad.Stop();
    }

    partial void OnNameChanged(string value)
    {
        Model.Name = value;
        _onChanged();
    }

    partial void OnIconChanged(string value)
    {
        Model.Icon = value;
        _onChanged();
    }
}
