using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyCraftyStash.Services;

namespace MyCraftyStash.ViewModels
{
    public partial class ProjectSelectableItem : ObservableObject
    {
        [ObservableProperty] private int _itemId;
        [ObservableProperty] private string _itemName = string.Empty;
        [ObservableProperty] private string _itemType = string.Empty;
        [ObservableProperty] private string? _itemNumber;
        [ObservableProperty] private string? _itemSubtype;
        [ObservableProperty] private string? _itemTheme;

        /// <summary>Called by the parent ViewModel when selection changes, to refresh the selected panel.</summary>
        public Action? SelectionChanged { get; set; }

        /// <summary>Called when the user clicks the remove button in the ordered items panel.</summary>
        public Action? RemoveAction { get; set; }

        [RelayCommand]
        private void Remove() => RemoveAction?.Invoke();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowAmountField))]
        [NotifyPropertyChangedFor(nameof(ShowAmountConfirmSection))]
        private bool _isSelected;

        [ObservableProperty]
        private decimal? _amountUsed;

        /// <summary>True once the user has clicked Confirm on the amount field; locks the value.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AmountIsEditable))]
        [NotifyPropertyChangedFor(nameof(ShowConfirmButton))]
        [NotifyPropertyChangedFor(nameof(ShowAmountConfirmSection))]
        private bool _isAmountConfirmed;

        public bool IsCardstock  => InventoryService.IsCardstockType(ItemType);
        public bool IsTracked    => InventoryService.IsTrackedType(ItemType);
        public bool IsWholeUnit  => IsTracked && !IsCardstock;

        public bool ShowAmountField         => IsTracked && IsSelected;
        public bool ShowAmountConfirmSection => IsTracked && IsSelected;
        public bool AmountIsEditable        => !IsAmountConfirmed;
        public bool ShowConfirmButton       => IsTracked && IsSelected && !IsAmountConfirmed;

        [RelayCommand]
        private void ConfirmAmount() => IsAmountConfirmed = true;

        [RelayCommand]
        private void UnlockAmount() => IsAmountConfirmed = false;

        partial void OnIsSelectedChanged(bool value)
        {
            OnPropertyChanged(nameof(ShowAmountField));
            OnPropertyChanged(nameof(ShowAmountConfirmSection));
            SelectionChanged?.Invoke();
            if (value && IsWholeUnit && AmountUsed == null)
                AmountUsed = 1;
            else if (!value)
            {
                AmountUsed = null;
                IsAmountConfirmed = false;
            }
        }
    }
}
