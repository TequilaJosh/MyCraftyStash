using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyCraftyStash.Models;
using MyCraftyStash.Services;
using System.Collections.ObjectModel;

namespace MyCraftyStash.ViewModels
{
    public partial class AddressBookViewModel : BaseViewModel
    {
        private readonly AddressBookService _service;
        private readonly MainViewModel _mainVm;

        [ObservableProperty]
        private ObservableCollection<AddressBookEntry> _entries = new();

        [ObservableProperty]
        private AddressBookEntry? _selectedEntry;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _isEditing;

        [ObservableProperty]
        private bool _isAdding;

        // Edit form fields
        [ObservableProperty] private string _editFirstName = string.Empty;
        [ObservableProperty] private string _editLastName = string.Empty;
        [ObservableProperty] private string _editAddressLine1 = string.Empty;
        [ObservableProperty] private string _editAddressLine2 = string.Empty;
        [ObservableProperty] private string _editCity = string.Empty;
        [ObservableProperty] private string _editState = string.Empty;
        [ObservableProperty] private string _editZipCode = string.Empty;
        [ObservableProperty] private string _editCountry = string.Empty;
        [ObservableProperty] private string _editPhone = string.Empty;
        [ObservableProperty] private string _editEmail = string.Empty;
        [ObservableProperty] private string _editNotes = string.Empty;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        public AddressBookViewModel(MainViewModel mainVm)
        {
            _service = new AddressBookService();
            _mainVm = mainVm;
        }

        [RelayCommand]
        private async Task LoadEntries()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;
                var results = string.IsNullOrWhiteSpace(SearchText)
                    ? await _service.GetAllEntriesAsync()
                    : await _service.SearchEntriesAsync(SearchText);

                Entries = new ObservableCollection<AddressBookEntry>(results);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load address book: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task Search()
        {
            await LoadEntries();
        }

        [RelayCommand]
        private void ClearSearch()
        {
            SearchText = string.Empty;
            LoadEntriesCommand.Execute(null);
        }

        [RelayCommand]
        private void SelectEntry(AddressBookEntry entry)
        {
            // Clear previous selection highlight
            if (SelectedEntry != null) SelectedEntry.IsSelected = false;
            SelectedEntry = entry;
            entry.IsSelected = true;
            IsEditing = false;
            IsAdding = false;
        }

        [RelayCommand]
        private void StartAdd()
        {
            if (SelectedEntry != null) { SelectedEntry.IsSelected = false; SelectedEntry = null; }
            IsAdding = true;
            IsEditing = false;
            ClearEditForm();
        }

        [RelayCommand]
        private void StartEdit()
        {
            if (SelectedEntry == null) return;
            IsEditing = true;
            IsAdding = false;
            PopulateEditForm(SelectedEntry);
        }

        [RelayCommand]
        private void CancelEdit()
        {
            IsEditing = false;
            IsAdding = false;
        }

        [RelayCommand]
        private async Task SaveEntry()
        {
            if (string.IsNullOrWhiteSpace(EditFirstName))
            {
                ErrorMessage = "First name is required.";
                return;
            }

            try
            {
                IsLoading = true;
                ErrorMessage = null;

                if (IsAdding)
                {
                    var newEntry = BuildEntryFromForm();
                    var saved = await _service.AddEntryAsync(newEntry);
                    Entries.Add(saved);
                    SelectedEntry = saved;
                    StatusMessage = $"Added {saved.FullName} successfully.";
                }
                else if (IsEditing && SelectedEntry != null)
                {
                    var updated = SelectedEntry;
                    ApplyFormToEntry(updated);
                    await _service.UpdateEntryAsync(updated);
                    // Refresh the list item
                    var idx = Entries.IndexOf(updated);
                    if (idx >= 0) Entries[idx] = updated;
                    SelectedEntry = updated;
                    StatusMessage = $"Updated {updated.FullName} successfully.";
                }

                IsEditing = false;
                IsAdding = false;
                await LoadEntries();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to save: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task DeleteEntry()
        {
            if (SelectedEntry == null) return;

            try
            {
                IsLoading = true;
                ErrorMessage = null;
                var name = SelectedEntry.FullName;
                await _service.DeleteEntryAsync(SelectedEntry.Id);
                Entries.Remove(SelectedEntry);
                SelectedEntry = null;
                IsEditing = false;
                IsAdding = false;
                StatusMessage = $"Deleted {name}.";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to delete: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ClearEditForm()
        {
            EditFirstName = string.Empty;
            EditLastName = string.Empty;
            EditAddressLine1 = string.Empty;
            EditAddressLine2 = string.Empty;
            EditCity = string.Empty;
            EditState = string.Empty;
            EditZipCode = string.Empty;
            EditCountry = string.Empty;
            EditPhone = string.Empty;
            EditEmail = string.Empty;
            EditNotes = string.Empty;
        }

        private void PopulateEditForm(AddressBookEntry entry)
        {
            EditFirstName = entry.FirstName;
            EditLastName = entry.LastName ?? string.Empty;
            EditAddressLine1 = entry.AddressLine1 ?? string.Empty;
            EditAddressLine2 = entry.AddressLine2 ?? string.Empty;
            EditCity = entry.City ?? string.Empty;
            EditState = entry.State ?? string.Empty;
            EditZipCode = entry.ZipCode ?? string.Empty;
            EditCountry = entry.Country ?? string.Empty;
            EditPhone = entry.Phone ?? string.Empty;
            EditEmail = entry.Email ?? string.Empty;
            EditNotes = entry.Notes ?? string.Empty;
        }

        private AddressBookEntry BuildEntryFromForm()
        {
            var entry = new AddressBookEntry();
            ApplyFormToEntry(entry);
            return entry;
        }

        private void ApplyFormToEntry(AddressBookEntry entry)
        {
            entry.FirstName = EditFirstName.Trim();
            entry.LastName = string.IsNullOrWhiteSpace(EditLastName) ? null : EditLastName.Trim();
            entry.AddressLine1 = string.IsNullOrWhiteSpace(EditAddressLine1) ? null : EditAddressLine1.Trim();
            entry.AddressLine2 = string.IsNullOrWhiteSpace(EditAddressLine2) ? null : EditAddressLine2.Trim();
            entry.City = string.IsNullOrWhiteSpace(EditCity) ? null : EditCity.Trim();
            entry.State = string.IsNullOrWhiteSpace(EditState) ? null : EditState.Trim();
            entry.ZipCode = string.IsNullOrWhiteSpace(EditZipCode) ? null : EditZipCode.Trim();
            entry.Country = string.IsNullOrWhiteSpace(EditCountry) ? null : EditCountry.Trim();
            entry.Phone = string.IsNullOrWhiteSpace(EditPhone) ? null : EditPhone.Trim();
            entry.Email = string.IsNullOrWhiteSpace(EditEmail) ? null : EditEmail.Trim();
            entry.Notes = string.IsNullOrWhiteSpace(EditNotes) ? null : EditNotes.Trim();
        }
    }
}
