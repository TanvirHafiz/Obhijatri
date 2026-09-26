using System.Text;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Obhijatri.App.Localization;
using Obhijatri.App.Services;
using Obhijatri.Bangla.Phonetic;
using Windows.System;
using Windows.UI.Core;

namespace Obhijatri.App;

/// <summary>
/// Bangla phonetic typing in the address bar. The romanised letters of the current word are kept
/// in <see cref="_phoneticRoman"/> and the Bangla for them replaces the word as it is typed
/// ("ami" shows আমি). A space, punctuation, a click or an arrow key ends the word.
/// Web pages use the injected script instead (Web/phonetic-typing.js) with the same Avro rules.
/// </summary>
public sealed partial class MainWindow
{
    private readonly StringBuilder _phoneticRoman = new();
    private string _phoneticShown = string.Empty;
    private bool _phoneticEditing;

    private void InitializePhonetic()
    {
        AddressBar.BeforeTextChanging += AddressBar_BeforeTextChanging;
        AddressBar.LostFocus += (_, _) => ResetPhonetic();
        PhoneticPopup.PlacementTarget = AddressBar;
        AutomationProperties.SetName(PhoneticList, Strings.Get("PhoneticSuggestionsName"));
        UpdatePhoneticButton();
    }

    private static bool AddressBarPhoneticOn => AppServices.Settings.AddressBarPhonetic;

    private void UpdatePhoneticButton()
    {
        var on = AddressBarPhoneticOn;
        PhoneticButtonText.Text = Strings.Get(on ? "PhoneticButtonOn" : "PhoneticButtonOff");
        // Theme-aware styles, so the colour follows light and dark mode.
        PhoneticButtonText.Style = (Style)Application.Current.Resources[on ? "PhoneticOnTextStyle" : "PhoneticOffTextStyle"];
        SetLabel(PhoneticButton, on ? "PhoneticAddressOn" : "PhoneticAddressOff");
    }

    private void PhoneticButton_Click(object sender, RoutedEventArgs e)
    {
        AppServices.Settings.AddressBarPhonetic = !AddressBarPhoneticOn;
        FocusAddressBar();
    }

    private void AddressBar_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
    {
        if (!AddressBarPhoneticOn || _phoneticEditing)
        {
            return;
        }

        // Was exactly one character typed (possibly replacing a selection)? In this event the
        // caret is already after the new character and the old selection is no longer known, so
        // work it out from the old and new text.
        var old = sender.Text;
        var next = args.NewText;
        var at = sender.SelectionStart - 1;
        var removed = old.Length - (next.Length - 1);
        if (at >= 0 && at < next.Length && removed >= 0 && at + removed <= old.Length
            && next == old.Remove(at, removed).Insert(at, next[at].ToString()))
        {
            var typed = next[at];
            if (typed != ' ' && AvroPhonetic.Instance.IsConvertible(typed))
            {
                args.Cancel = true;
                DispatcherQueue.TryEnqueue(() => ApplyPhoneticChar(typed, at, removed));
                return;
            }
        }

        // Anything else (space, other punctuation, paste, delete) ends the current word.
        ResetPhonetic();
    }

    private void ApplyPhoneticChar(char typed, int caret, int selected)
    {
        var text = AddressBar.Text;
        if (selected > 0)
        {
            text = text.Remove(caret, selected);
            ResetPhonetic();
        }
        if (!PhoneticContextOk(text, caret))
        {
            ResetPhonetic();
        }

        _phoneticRoman.Append(typed);
        ReplacePhoneticShown(text, caret, AvroPhonetic.Instance.Convert(_phoneticRoman.ToString()));
        ShowPhoneticSuggestions();
    }

    /// <summary>Is the Bangla we showed last still right before the caret?</summary>
    private bool PhoneticContextOk(string text, int caret) =>
        _phoneticShown.Length == 0
        || (caret >= _phoneticShown.Length
            && string.CompareOrdinal(text, caret - _phoneticShown.Length, _phoneticShown, 0, _phoneticShown.Length) == 0);

    private void ReplacePhoneticShown(string text, int caret, string replacement)
    {
        var start = caret - _phoneticShown.Length;
        _phoneticEditing = true;
        try
        {
            AddressBar.Text = text.Remove(start, _phoneticShown.Length).Insert(start, replacement);
            AddressBar.SelectionStart = start + replacement.Length;
            AddressBar.SelectionLength = 0;
        }
        finally
        {
            _phoneticEditing = false;
        }
        _phoneticShown = replacement;
    }

    /// <summary>Keys that act on the current word or the suggestion list. Returns true if handled.</summary>
    private bool HandlePhoneticKey(KeyRoutedEventArgs e)
    {
        var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
        if (ctrl && e.Key == VirtualKey.M)
        {
            e.Handled = true;
            AppServices.Settings.AddressBarPhonetic = !AddressBarPhoneticOn;
            return true;
        }
        if (!AddressBarPhoneticOn)
        {
            return false;
        }

        if (PhoneticPopup.IsOpen)
        {
            switch (e.Key)
            {
                case VirtualKey.Down:
                case VirtualKey.Up:
                    var count = PhoneticList.Items.Count;
                    PhoneticList.SelectedIndex = (PhoneticList.SelectedIndex + (e.Key == VirtualKey.Down ? 1 : count - 1)) % count;
                    e.Handled = true;
                    return true;
                case VirtualKey.Enter when PhoneticList.SelectedIndex > 0:
                    ChoosePhoneticSuggestion(PhoneticList.SelectedIndex);
                    e.Handled = true;
                    return true;
                case VirtualKey.Escape:
                    HidePhoneticSuggestions();
                    e.Handled = true;
                    return true;
                case VirtualKey.Space when PhoneticList.SelectedIndex > 0:
                    // Use the picked word, then let the space be typed after it.
                    ChoosePhoneticSuggestion(PhoneticList.SelectedIndex);
                    return false;
            }
        }

        if (e.Key == VirtualKey.Back && _phoneticRoman.Length > 0 && AddressBar.SelectionLength == 0
            && PhoneticContextOk(AddressBar.Text, AddressBar.SelectionStart))
        {
            e.Handled = true;
            _phoneticRoman.Length--;
            ReplacePhoneticShown(AddressBar.Text, AddressBar.SelectionStart,
                _phoneticRoman.Length == 0 ? string.Empty : AvroPhonetic.Instance.Convert(_phoneticRoman.ToString()));
            if (_phoneticRoman.Length == 0)
            {
                ResetPhonetic();
            }
            else
            {
                ShowPhoneticSuggestions();
            }
            return true;
        }

        if (e.Key == VirtualKey.Enter)
        {
            ResetPhonetic();
        }
        return false;
    }

    private void OnAddressBarSelectionChanged()
    {
        // The user moved the caret or selected text: the current word is finished.
        if (!_phoneticEditing && _phoneticRoman.Length > 0 && !PhoneticContextOk(AddressBar.Text, AddressBar.SelectionStart))
        {
            ResetPhonetic();
        }
    }

    private void ShowPhoneticSuggestions()
    {
        var list = PhoneticSuggester.Instance.Suggest(_phoneticRoman.ToString());
        if (list.Count < 2)
        {
            HidePhoneticSuggestions();
            return;
        }
        PhoneticList.ItemsSource = list;
        PhoneticList.SelectedIndex = 0;
        PhoneticPopup.IsOpen = true;
    }

    private void HidePhoneticSuggestions()
    {
        PhoneticPopup.IsOpen = false;
        PhoneticList.ItemsSource = null;
    }

    private void PhoneticList_ItemClick(object sender, ItemClickEventArgs e)
    {
        var index = PhoneticList.Items.IndexOf(e.ClickedItem);
        if (index >= 0)
        {
            ChoosePhoneticSuggestion(index);
        }
    }

    private void ChoosePhoneticSuggestion(int index)
    {
        if (PhoneticList.ItemsSource is IReadOnlyList<string> list && index < list.Count
            && PhoneticContextOk(AddressBar.Text, AddressBar.SelectionStart))
        {
            ReplacePhoneticShown(AddressBar.Text, AddressBar.SelectionStart, list[index]);
        }
        ResetPhonetic();
    }

    private void ResetPhonetic()
    {
        _phoneticRoman.Clear();
        _phoneticShown = string.Empty;
        HidePhoneticSuggestions();
    }
}
