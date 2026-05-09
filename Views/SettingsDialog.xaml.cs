using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MyCraftyStash.Services;

namespace MyCraftyStash.Views
{
    public partial class SettingsDialog : Window
    {
        public bool WasSaved { get; private set; }
        private bool _isLoadingSettings = true;

        private static readonly List<string> SortOrderOptions = new()
        {
            "Type",
            "Name (A-Z)",
            "Name (Z-A)",
            "Date Purchased (Newest)",
            "Date Purchased (Oldest)",
            "Price (Low to High)",
            "Price (High to Low)"
        };

        private static readonly List<string> TypeSortOptionsPrimary = new()
        {
            "Color", "Type", "A-Z", "Z-A",
            "Date Purchased (Oldest)", "Date Purchased (Newest)",
            "Theme", "Location", "Subtype"
        };

        private static readonly List<string> TypeSortOptionsSecondary = new()
        {
            "", "Color", "Type", "A-Z", "Z-A",
            "Date Purchased (Oldest)", "Date Purchased (Newest)",
            "Theme", "Location", "Subtype"
        };

        public SettingsDialog()
        {
            InitializeComponent();
            Loaded += async (_, _) => await LoadAllSettingsAsync();
        }

        private async Task LoadAllSettingsAsync()
        {
            _isLoadingSettings = true;
            LoadDisplaySettings();
            LoadColorsTab();
            LoadDefaultsSettings();
            LoadTextEditors();
            LoadSubtypesTab();
            LoadCardLabelsTab();
            LoadCustomSortTab();
            LoadTrackedItemsTab();
            await LoadProjectTrackedItemsTabAsync();
            LoadCatalogSourcesTab();
            _isLoadingSettings = false;
        }

        // ── Catalog Sources tab ──────────────────────────────────────────────

        public class CatalogSourceRow
        {
            public string Id { get; init; } = string.Empty;
            public string DisplayName { get; init; } = string.Empty;
            public string Domain { get; init; } = string.Empty;
            public bool IsStub { get; init; }
            public bool IsToggleable => !IsStub;
            public bool IsEnabled { get; set; }
            public string StatusText => IsStub
                ? "Custom site — scraper TBD"
                : IsEnabled ? "Active" : "Disabled";
        }

        private void LoadCatalogSourcesTab()
        {
            var rows = new System.Collections.Generic.List<CatalogSourceRow>();
            foreach (var p in CatalogLookupService.Providers)
            {
                var isStub = p is MyCraftyStash.Services.Catalog.StubCatalogProvider;
                rows.Add(new CatalogSourceRow
                {
                    Id          = p.Id,
                    DisplayName = p.DisplayName,
                    Domain      = p.Domain,
                    IsStub      = isStub,
                    IsEnabled   = !isStub && CatalogLookupService.IsEnabled(p.Id),
                });
            }
            CatalogSourcesList.ItemsSource = rows;
        }

        private void CatalogSource_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is CatalogSourceRow row)
            {
                CatalogLookupService.SetEnabled(row.Id, cb.IsChecked == true);
                WasSaved = true;
            }
        }

        private void LoadDisplaySettings()
        {
            if (UserSettingsService.Current.DarkMode)
                DarkModeRadio.IsChecked = true;
            else
                LightModeRadio.IsChecked = true;

            switch (UserSettingsService.GetCardSize())
            {
                case CardSizeOption.Small: CardSizeSmall.IsChecked = true; break;
                case CardSizeOption.Large: CardSizeLarge.IsChecked = true; break;
                default: CardSizeMedium.IsChecked = true; break;
            }

            switch (UserSettingsService.GetTextSize())
            {
                case TextSizeOption.Small: TextSizeSmall.IsChecked = true; break;
                case TextSizeOption.Large: TextSizeLarge.IsChecked = true; break;
                default: TextSizeMedium.IsChecked = true; break;
            }
        }

        private void LoadDefaultsSettings()
        {
            var types = LoadLinesFromFile(ConfigStore.Types);
            var locations = LoadLinesFromFile(ConfigStore.Locations);

            DefaultTypeCombo.Items.Clear();
            DefaultTypeCombo.Items.Add("(None)");
            foreach (var t in types) DefaultTypeCombo.Items.Add(t);

            DefaultLocationCombo.Items.Clear();
            DefaultLocationCombo.Items.Add("(None)");
            foreach (var l in locations) DefaultLocationCombo.Items.Add(l);

            DefaultSortCombo.Items.Clear();
            foreach (var s in SortOrderOptions) DefaultSortCombo.Items.Add(s);

            var currentType = UserSettingsService.Current.DefaultItemType;
            if (!string.IsNullOrEmpty(currentType) && DefaultTypeCombo.Items.Contains(currentType))
                DefaultTypeCombo.SelectedItem = currentType;
            else
                DefaultTypeCombo.SelectedIndex = 0;

            var currentLocation = UserSettingsService.Current.DefaultLocation;
            if (!string.IsNullOrEmpty(currentLocation) && DefaultLocationCombo.Items.Contains(currentLocation))
                DefaultLocationCombo.SelectedItem = currentLocation;
            else
                DefaultLocationCombo.SelectedIndex = 0;

            var currentSort = UserSettingsService.Current.DefaultSortOrder;
            if (!string.IsNullOrEmpty(currentSort) && DefaultSortCombo.Items.Contains(currentSort))
                DefaultSortCombo.SelectedItem = currentSort;
            else
                DefaultSortCombo.SelectedItem = "Type";
        }

        private void LoadTextEditors()
        {
            TypesTextBox.Text = LoadFileContent(ConfigStore.Types);
            ThemesTextBox.Text = LoadFileContent(ConfigStore.Themes);
            ColorOrderTextBox.Text = LoadFileContent(ConfigStore.ColorOrder);
            InspirationColorsTextBox.Text = LoadFileContent(ConfigStore.InspirationColors);
            LocationsTextBox.Text = LoadFileContent(ConfigStore.Locations);
            PurchasedFromTextBox.Text = LoadFileContent(ConfigStore.PurchasedFrom);
        }

        private static string LoadFileContent(string name)
        {
            try
            {
                return ConfigStore.GetText(name).TrimEnd();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static List<string> LoadLinesFromFile(string name)
        {
            try
            {
                return ConfigStore.GetLines(name)
                    .Select(l => l.TrimStart('*').TrimEnd('`').Trim())
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList();
            }
            catch { }
            return new List<string>();
        }

        private void ThemeRadio_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;
            UserSettingsService.SetDarkMode(DarkModeRadio.IsChecked == true);
        }

        private void CardSizeRadio_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;
            CardSizeOption size = CardSizeOption.Medium;
            if (CardSizeSmall.IsChecked == true) size = CardSizeOption.Small;
            else if (CardSizeLarge.IsChecked == true) size = CardSizeOption.Large;
            UserSettingsService.SetCardSize(size);
        }

        private void TextSizeRadio_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;
            TextSizeOption size = TextSizeOption.Medium;
            if (TextSizeSmall.IsChecked == true) size = TextSizeOption.Small;
            else if (TextSizeLarge.IsChecked == true) size = TextSizeOption.Large;
            UserSettingsService.SetTextSize(size);
        }

        private void DefaultType_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingSettings) return;
            var selected = DefaultTypeCombo.SelectedItem as string;
            UserSettingsService.SetDefaultItemType(selected == "(None)" ? "" : selected ?? "");
        }

        private void DefaultLocation_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingSettings) return;
            var selected = DefaultLocationCombo.SelectedItem as string;
            UserSettingsService.SetDefaultLocation(selected == "(None)" ? "" : selected ?? "");
        }

        private void DefaultSort_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingSettings) return;
            var selected = DefaultSortCombo.SelectedItem as string;
            UserSettingsService.SetDefaultSortOrder(selected ?? "Type");
        }

        private void SaveTypes_Click(object sender, RoutedEventArgs e)
        {
            SaveTextToFile(ConfigStore.Types, TypesTextBox.Text);
            WasSaved = true;
            MessageBox.Show("Types saved successfully.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SaveThemes_Click(object sender, RoutedEventArgs e)
        {
            SaveTextToFile(ConfigStore.Themes, ThemesTextBox.Text);
            WasSaved = true;
            MessageBox.Show("Themes saved successfully.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SaveColorOrder_Click(object sender, RoutedEventArgs e)
        {
            SaveTextToFile(ConfigStore.ColorOrder, ColorOrderTextBox.Text);
            InventoryService.ReloadColorOrder();
            WasSaved = true;
            MessageBox.Show("Color order saved successfully.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SaveInspirationColors_Click(object sender, RoutedEventArgs e)
        {
            SaveTextToFile(ConfigStore.InspirationColors, InspirationColorsTextBox.Text);
            InventoryService.ReloadInspirationColors();
            WasSaved = true;
            MessageBox.Show("Inspiration colors saved successfully.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SaveLocations_Click(object sender, RoutedEventArgs e)
        {
            SaveTextToFile(ConfigStore.Locations, LocationsTextBox.Text);
            WasSaved = true;
            MessageBox.Show("Locations saved successfully.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SavePurchasedFrom_Click(object sender, RoutedEventArgs e)
        {
            SaveTextToFile(ConfigStore.PurchasedFrom, PurchasedFromTextBox.Text);
            WasSaved = true;
            MessageBox.Show("Purchased From options saved successfully.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LoadSubtypesTab()
        {
            SubtypeTypesList.Items.Clear();
            var types = LoadLinesFromFile(ConfigStore.Types);
            foreach (var t in types)
                SubtypeTypesList.Items.Add(t);
            SubtypesTextBox.IsEnabled = false;
            SubtypesTextBox.Text = string.Empty;
            SubtypeEditorLabel.Text = "Select a type to edit its subtypes";
        }

        private void SubtypeTypesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SubtypeTypesList.SelectedItem is string selectedType)
            {
                SubtypesTextBox.IsEnabled = true;
                SubtypeEditorLabel.Text = $"Subtypes for: {selectedType} (one per line)";
                var subtypes = UserSettingsService.GetSubtypesForType(selectedType);
                SubtypesTextBox.Text = string.Join(Environment.NewLine, subtypes);
            }
        }

        private void SaveSubtypes_Click(object sender, RoutedEventArgs e)
        {
            if (SubtypeTypesList.SelectedItem is not string selectedType)
            {
                MessageBox.Show("Please select a type first.", "No Type Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var lines = SubtypesTextBox.Text
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Trim())
                .ToList();

            UserSettingsService.SetTypeSubtypes(selectedType, lines);
            WasSaved = true;
            MessageBox.Show($"Subtypes for \"{selectedType}\" saved.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ── Card Builder Labels tab ──────────────────────────────────────────

        private void LoadCardLabelsTab()
        {
            CardLabelList.Items.Clear();
            foreach (var label in CardLabelMappingService.CanonicalLabelOrder)
                CardLabelList.Items.Add(label);

            CardLabelTypeBox.Items.Clear();
            CardLabelTypeBox.Items.Add(string.Empty); // "no type filter"
            foreach (var t in LoadLinesFromFile(ConfigStore.Types))
                CardLabelTypeBox.Items.Add(t);

            ResetCardLabelEditor();
        }

        private void ResetCardLabelEditor()
        {
            CardLabelEditorTitle.Text = "Select a label to edit";
            CardLabelTypeBox.IsEnabled = false;
            CardLabelTypeBox.Text = string.Empty;
            CardLabelSubtypesBox.IsEnabled = false;
            CardLabelSubtypesBox.Text = string.Empty;
            CardLabelNameContainsBox.IsEnabled = false;
            CardLabelNameContainsBox.Text = string.Empty;
        }

        private void CardLabelList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CardLabelList.SelectedItem is not string label)
            {
                ResetCardLabelEditor();
                return;
            }

            var mapping = CardLabelMappingService.Default.GetMapping(label);
            CardLabelEditorTitle.Text = $"Mapping for: {label}";
            CardLabelTypeBox.IsEnabled = true;
            CardLabelTypeBox.Text = mapping.Type ?? string.Empty;
            CardLabelSubtypesBox.IsEnabled = true;
            CardLabelSubtypesBox.Text = mapping.Subtypes != null
                ? string.Join(Environment.NewLine, mapping.Subtypes)
                : string.Empty;
            CardLabelNameContainsBox.IsEnabled = true;
            CardLabelNameContainsBox.Text = mapping.NameContains ?? string.Empty;
        }

        private void SaveCardLabel_Click(object sender, RoutedEventArgs e)
        {
            if (CardLabelList.SelectedItem is not string label)
            {
                MessageBox.Show("Pick a label on the left first.", "No label selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var subtypes = CardLabelSubtypesBox.Text
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l))
                .ToList();

            var mapping = new CardLabel
            {
                Type         = string.IsNullOrWhiteSpace(CardLabelTypeBox.Text) ? null : CardLabelTypeBox.Text.Trim(),
                Subtypes     = subtypes.Count == 0 ? null : subtypes,
                NameContains = string.IsNullOrWhiteSpace(CardLabelNameContainsBox.Text) ? null : CardLabelNameContainsBox.Text.Trim(),
            };

            CardLabelMappingService.Default.SetMapping(label, mapping);
            WasSaved = true;
            MessageBox.Show($"Mapping for \"{label}\" saved.", "Saved",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ResetCardLabels_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Reset every card builder label to its default Type/Subtypes? This overwrites your customizations.",
                "Reset to defaults", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            CardLabelMappingService.Default.ResetToDefaults();
            WasSaved = true;
            // Re-display the currently selected label so the user sees the new values immediately.
            CardLabelList_SelectionChanged(CardLabelList, null!);
            MessageBox.Show("All labels reset to defaults.", "Done",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private static void SaveTextToFile(string name, string content)
        {
            try
            {
                var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Select(l => l.Trim());
                ConfigStore.SetText(name, string.Join(Environment.NewLine, lines));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadCustomSortTab()
        {
            CustomSortTypesList.Items.Clear();
            var types = LoadLinesFromFile(ConfigStore.Types);
            foreach (var t in types)
                CustomSortTypesList.Items.Add(t);

            Sort1Combo.Items.Clear();
            foreach (var s in TypeSortOptionsPrimary) Sort1Combo.Items.Add(s);

            Sort2Combo.Items.Clear();
            Sort3Combo.Items.Clear();
            foreach (var s in TypeSortOptionsSecondary)
            {
                Sort2Combo.Items.Add(s);
                Sort3Combo.Items.Add(s);
            }
            CustomSortEditor.IsEnabled = false;
        }

        private void CustomSortTypesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CustomSortTypesList.SelectedItem is not string selectedType) return;
            _isLoadingSettings = true;
            var order = UserSettingsService.GetSortOrderForType(selectedType);
            CustomSortEditorLabel.Text = $"Sort order for: {selectedType}";
            CustomSortEditor.IsEnabled = true;

            Sort1Combo.SelectedItem = !string.IsNullOrEmpty(order.Sort1) ? order.Sort1 : TypeSortOptionsPrimary[0];
            Sort2Combo.SelectedItem = order.Sort2 ?? "";
            Sort3Combo.SelectedItem = order.Sort3 ?? "";

            if (Sort1Combo.SelectedItem == null) Sort1Combo.SelectedIndex = 0;
            if (Sort2Combo.SelectedItem == null) Sort2Combo.SelectedIndex = 0;
            if (Sort3Combo.SelectedItem == null) Sort3Combo.SelectedIndex = 0;
            _isLoadingSettings = false;
        }

        private void SortCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingSettings) return;
            if (CustomSortTypesList.SelectedItem is not string selectedType) return;

            var order = new Services.TypeSortOrder
            {
                Sort1 = Sort1Combo.SelectedItem as string ?? "Color",
                Sort2 = Sort2Combo.SelectedItem as string ?? "",
                Sort3 = Sort3Combo.SelectedItem as string ?? ""
            };
            UserSettingsService.SetSortOrderForType(selectedType, order);
            WasSaved = true;

            CustomSortSavedText.Visibility = System.Windows.Visibility.Visible;
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            timer.Tick += (s, _) => { CustomSortSavedText.Visibility = System.Windows.Visibility.Collapsed; timer.Stop(); };
            timer.Start();
        }

        private void LoadTrackedItemsTab()
        {
            var allTypes = LoadLinesFromFile(ConfigStore.Types);
            var trackedSet = InventoryService.TrackedTypes;

            AllTypesList.Items.Clear();
            TrackedTypesList.Items.Clear();

            foreach (var type in allTypes)
            {
                if (trackedSet.Contains(type))
                    TrackedTypesList.Items.Add(type);
                else
                    AllTypesList.Items.Add(type);
            }

            // Also add any tracked types not in the types file
            foreach (var tracked in trackedSet)
            {
                if (!allTypes.Contains(tracked, StringComparer.OrdinalIgnoreCase)
                    && !TrackedTypesList.Items.Contains(tracked))
                {
                    TrackedTypesList.Items.Add(tracked);
                }
            }

            // Reset warning editor
            WarningEditorLabel.Text = "Select a tracked type to set its warning levels";
            WarningLevelsGrid.IsEnabled = false;
            GoodThresholdBox.Text = string.Empty;
            LowThresholdBox.Text = string.Empty;
            OutThresholdBox.Text = string.Empty;
        }

        private void TrackedTypesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TrackedTypesList.SelectedItem is not string selectedType)
            {
                WarningEditorLabel.Text = "Select a tracked type to set its warning levels";
                WarningLevelsGrid.IsEnabled = false;
                return;
            }

            _isLoadingSettings = true;
            WarningEditorLabel.Text = $"Warning levels for: {selectedType}";
            WarningLevelsGrid.IsEnabled = true;

            var configs = InventoryService.TrackedTypeConfigs;
            if (configs.TryGetValue(selectedType, out var cfg))
            {
                GoodThresholdBox.Text = cfg.GoodThreshold?.ToString() ?? string.Empty;
                LowThresholdBox.Text  = cfg.LowThreshold?.ToString()  ?? string.Empty;
                OutThresholdBox.Text  = cfg.OutThreshold?.ToString()   ?? string.Empty;
            }
            else
            {
                GoodThresholdBox.Text = string.Empty;
                LowThresholdBox.Text  = string.Empty;
                OutThresholdBox.Text  = string.Empty;
            }
            _isLoadingSettings = false;
        }

        private void WarningThreshold_Changed(object sender, TextChangedEventArgs e)
        {
            // Changes are picked up on save - no auto-save here to avoid partial input issues
        }

        private void AddToTracked_Click(object sender, RoutedEventArgs e)
        {
            var selected = AllTypesList.SelectedItems.Cast<string>().ToList();
            foreach (var item in selected)
            {
                AllTypesList.Items.Remove(item);
                if (!TrackedTypesList.Items.Contains(item))
                    TrackedTypesList.Items.Add(item);
            }
        }

        private void RemoveFromTracked_Click(object sender, RoutedEventArgs e)
        {
            var selected = TrackedTypesList.SelectedItems.Cast<string>().ToList();
            foreach (var item in selected)
            {
                TrackedTypesList.Items.Remove(item);
                if (!AllTypesList.Items.Contains(item))
                    AllTypesList.Items.Add(item);
            }
            // Clear editor if removed type was selected
            WarningEditorLabel.Text = "Select a tracked type to set its warning levels";
            WarningLevelsGrid.IsEnabled = false;
        }

        private void SaveTrackedTypes_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var tracked = TrackedTypesList.Items.Cast<string>().ToList();

                // Build updated configs - merge existing with any edits made in this session
                var existingConfigs = InventoryService.TrackedTypeConfigs;
                var updatedConfigs = new Dictionary<string, InventoryService.TrackedTypeConfig>(
                    StringComparer.OrdinalIgnoreCase);

                foreach (var type in tracked)
                {
                    var cfg = existingConfigs.TryGetValue(type, out var existing)
                        ? existing
                        : new InventoryService.TrackedTypeConfig { Type = type };
                    updatedConfigs[type] = cfg;
                }

                // Apply whatever is currently shown in the warning editor (if a type is selected)
                if (TrackedTypesList.SelectedItem is string editedType
                    && updatedConfigs.TryGetValue(editedType, out var editingCfg))
                {
                    editingCfg.GoodThreshold = int.TryParse(GoodThresholdBox.Text, out int g) ? g : null;
                    editingCfg.LowThreshold  = int.TryParse(LowThresholdBox.Text,  out int l) ? l : null;
                    editingCfg.OutThreshold  = int.TryParse(OutThresholdBox.Text,  out int o) ? o : (int?)null;
                }

                InventoryService.SaveTrackedTypes(tracked, updatedConfigs);
                WasSaved = true;

                TrackedTypesSavedText.Visibility = Visibility.Visible;
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };
                timer.Tick += (s, _) =>
                {
                    TrackedTypesSavedText.Visibility = Visibility.Collapsed;
                    timer.Stop();
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving tracked types: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = WasSaved;
            Close();
        }

        // ── Colors Tab ─────────────────────────────────────────────────────────

        private ObservableCollection<ColorPickerEntry>? _colorEntries;

        // Plain-English labels keep this tab approachable — users see what each color
        // affects ("Buttons & highlights") rather than internal brush keys.
        // MirrorKeys: additional brush keys that get the same color applied — used
        // when several visually-related surfaces should stay in sync (e.g. cards
        // and panels share the same off-white).
        private static readonly (string Key, string Name, string Description, string[] Presets, string[] MirrorKeys)[] ColorPickerDefs = new[]
        {
            ("PrimaryBrush",         "Buttons & highlights",
             "Save buttons, page tab indicator, selected sidebar item, brand accent.",
             new[] { "#A82C32", "#2563EB", "#0EA5A4", "#7C3AED", "#D97706", "#16A34A", "#DB2777", "#475569" },
             Array.Empty<string>()),
            ("PrimaryLightBrush",    "Buttons (hover)",
             "The lighter shade buttons fade to when you hover them.",
             new[] { "#C03A41", "#3B82F6", "#14B8B6", "#9333EA", "#F59E0B", "#22C55E", "#EC4899", "#64748B" },
             Array.Empty<string>()),
            ("AccentBrush",          "Project tags & accents",
             "Project tag pills, the wizard's Save Creation button, secondary accent spots.",
             new[] { "#A82C32", "#2563EB", "#0EA5A4", "#7C3AED", "#D97706", "#16A34A", "#DB2777", "#475569" },
             Array.Empty<string>()),
            ("BackgroundBrush",      "Page background",
             "The cream wash filling the main content area behind every page.",
             new[] { "#F2EEE6", "#FFFFFF", "#F8FAFC", "#FEF3C7", "#ECFDF5", "#EFF6FF", "#F5F3FF", "#1F2937" },
             Array.Empty<string>()),
            ("SurfaceBrush",         "Panels & cards",
             "The off-white surfaces — Home dashboard panels, inventory cards, settings panels, multi-select popups.",
             new[] { "#F6F2EB", "#FFFFFF", "#FEFCE8", "#F0F9FF", "#FDF4FF", "#F0FDF4", "#E2E8F0", "#1F2937" },
             new[] { "CardBrush", "MultiPickerBrush" }),
            ("InputBackgroundBrush", "Text boxes & inputs",
             "Background of text boxes, dropdowns, search bars, and date pickers.",
             new[] { "#F6F2EB", "#FFFFFF", "#F8FAFC", "#FFFBEB", "#F0FDFA", "#EEF2FF", "#FAF5FF", "#1F2937" },
             Array.Empty<string>()),
            ("SidebarBrush",         "Sidebar panel",
             "The left navigation panel where the page links live.",
             new[] { "#EBE6D9", "#F1F5F9", "#FEF3C7", "#E0E7FF", "#FCE7F3", "#D1FAE5", "#1E293B", "#111827" },
             Array.Empty<string>()),
            ("SidebarSelectedBrush", "Sidebar selected item",
             "The lifted background under the currently selected sidebar nav item.",
             new[] { "#F6F2EB", "#FFFFFF", "#FEF9C3", "#DBEAFE", "#FAE8FF", "#A7F3D0", "#334155", "#1F2937" },
             Array.Empty<string>()),
            ("TitleBarBrush",        "Title bar",
             "The dark band at the very top of the window with the app logo.",
             new[] { "#1A1715", "#0F172A", "#1E293B", "#312E81", "#7F1D1D", "#064E3B", "#3B0764", "#000000" },
             Array.Empty<string>()),
        };

        private void LoadColorsTab()
        {
            _colorEntries = new ObservableCollection<ColorPickerEntry>();
            foreach (var def in ColorPickerDefs)
            {
                var entry = new ColorPickerEntry(def.Key, def.Name, def.Description, def.Presets, def.MirrorKeys);
                entry.RefreshFromTheme();
                _colorEntries.Add(entry);
            }
            ColorPickersHost.ItemsSource = _colorEntries;
        }

        // Apply a color (or revert to default if hex == null) to one entry's primary
        // key + every mirror key, so visually-grouped surfaces stay in sync.
        private static void ApplyColorToEntry(ColorPickerEntry entry, string? hex)
        {
            UserSettingsService.SetCustomColor(entry.Key, hex);
            foreach (var mirror in entry.MirrorKeys)
                UserSettingsService.SetCustomColor(mirror, hex);
        }

        private void RefreshAllColorEntries()
        {
            if (_colorEntries == null) return;
            foreach (var e in _colorEntries) e.RefreshFromTheme();
        }

        private void ApplyColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is string key)
                CommitColorEdit(key);
        }

        private void OpenColorPicker_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.Tag is not string key) return;
            var entry = _colorEntries?.FirstOrDefault(c => c.Key == key);
            if (entry == null) return;

            // Seed the picker with the current effective color (override or theme default).
            Color initial = Colors.Gray;
            if (Application.Current.Resources[key] is SolidColorBrush brush)
                initial = brush.Color;

            var dlg = new ColorPickerDialog(initial, $"Pick {entry.DisplayName}")
            {
                Owner = this
            };
            if (dlg.ShowDialog() == true)
            {
                var c = dlg.SelectedColor;
                var hex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                entry.HexValue = hex;
                ApplyColorToEntry(entry, hex);
                entry.RefreshFromTheme();
                WasSaved = true;
            }
        }

        private void ColorHex_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is FrameworkElement fe && fe.Tag is string key)
            {
                CommitColorEdit(key);
                e.Handled = true;
            }
        }

        private void ColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is string key)
                CommitColorEdit(key);
        }

        private void CommitColorEdit(string key)
        {
            var entry = _colorEntries?.FirstOrDefault(c => c.Key == key);
            if (entry == null) return;

            var hex = (entry.HexValue ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(hex))
            {
                // Empty input = revert to theme default for this brush.
                ApplyColorToEntry(entry, null);
                entry.RefreshFromTheme();
                WasSaved = true;
                return;
            }

            if (!ThemeService.TryParseHex(hex, out _))
            {
                MessageBox.Show($"\"{hex}\" is not a valid color. Use a hex code like #A82C32.",
                    "Invalid color", MessageBoxButton.OK, MessageBoxImage.Warning);
                entry.RefreshFromTheme();
                return;
            }

            ApplyColorToEntry(entry, hex);
            entry.RefreshFromTheme();
            WasSaved = true;
        }

        private void PresetSwatch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string presetHex) return;
            // Walk up to find the owning ColorPickerEntry via DataContext on the row Border.
            var row = FindAncestorWithDataContext(btn);
            if (row?.DataContext is ColorPickerEntry entry)
            {
                entry.HexValue = presetHex;
                ApplyColorToEntry(entry, presetHex);
                entry.RefreshFromTheme();
                WasSaved = true;
            }
        }

        private void ResetColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.Tag is not string key) return;
            var entry = _colorEntries?.FirstOrDefault(c => c.Key == key);
            if (entry != null)
            {
                ApplyColorToEntry(entry, null);
                entry.RefreshFromTheme();
            }
            else
            {
                UserSettingsService.SetCustomColor(key, null);
            }
            WasSaved = true;
        }

        private void ResetAllColors_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Reset all custom colors back to the theme defaults?",
                "Reset colors", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;
            UserSettingsService.ResetCustomColors();
            RefreshAllColorEntries();
            WasSaved = true;
        }

        private static FrameworkElement? FindAncestorWithDataContext(DependencyObject start)
        {
            var current = start;
            while (current != null)
            {
                if (current is FrameworkElement fe && fe.DataContext is ColorPickerEntry)
                    return fe;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        // ── Project Tracked Items Tab ──────────────────────────────────────────

        private async Task LoadProjectTrackedItemsTabAsync()
        {
            AllProjectsList.Items.Clear();
            TrackedProjectsList.Items.Clear();

            var tracked = InventoryService.ProjectTrackedItems;

            // Load all project names from the service
            try
            {
                var service = new InventoryService();
                var projects = await service.GetProjectsAsync();
                var allNames = projects.Select(p => p.Name).OrderBy(n => n).ToList();

                foreach (var name in allNames)
                {
                    if (tracked.Contains(name))
                        TrackedProjectsList.Items.Add(name);
                    else
                        AllProjectsList.Items.Add(name);
                }

                // Add any tracked names not yet in projects (renamed/deleted)
                foreach (var name in tracked)
                    if (!TrackedProjectsList.Items.Contains(name))
                        TrackedProjectsList.Items.Add(name);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load projects: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void AddProjectToTracked_Click(object sender, RoutedEventArgs e)
        {
            var selected = AllProjectsList.SelectedItems.Cast<string>().ToList();
            foreach (var item in selected)
            {
                AllProjectsList.Items.Remove(item);
                if (!TrackedProjectsList.Items.Contains(item))
                    TrackedProjectsList.Items.Add(item);
            }
        }

        private void RemoveProjectFromTracked_Click(object sender, RoutedEventArgs e)
        {
            var selected = TrackedProjectsList.SelectedItems.Cast<string>().ToList();
            foreach (var item in selected)
            {
                TrackedProjectsList.Items.Remove(item);
                if (!AllProjectsList.Items.Contains(item))
                    AllProjectsList.Items.Add(item);
            }
        }

        private void SaveProjectTrackedItems_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var tracked = TrackedProjectsList.Items.Cast<string>().ToList();
                InventoryService.SaveProjectTrackedItems(tracked);
                InventoryService.ReloadProjectTrackedItems();

                ProjectTrackedSavedText.Visibility = Visibility.Visible;
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };
                timer.Tick += (s, _) =>
                {
                    ProjectTrackedSavedText.Visibility = Visibility.Collapsed;
                    timer.Stop();
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving project tracked items: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }

    public class ColorPickerEntry : INotifyPropertyChanged
    {
        public string Key { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string[] Presets { get; }
        // Additional brush keys that get the same color applied — keeps related
        // surfaces (e.g. cards + popups + panels) in visual sync from one picker.
        public string[] MirrorKeys { get; }

        private string? _hexValue;
        public string? HexValue
        {
            get => _hexValue;
            set { if (_hexValue != value) { _hexValue = value; OnPropertyChanged(); } }
        }

        private Brush _currentBrush = Brushes.Transparent;
        public Brush CurrentBrush
        {
            get => _currentBrush;
            private set { _currentBrush = value; OnPropertyChanged(); }
        }

        public ColorPickerEntry(string key, string displayName, string description, string[] presets, string[]? mirrorKeys = null)
        {
            Key = key;
            DisplayName = displayName;
            Description = description;
            Presets = presets;
            MirrorKeys = mirrorKeys ?? Array.Empty<string>();
        }

        public void RefreshFromTheme()
        {
            // Show the user's saved override if any; otherwise pull the active brush from
            // the running theme so the swatch + hex always reflect what's on screen.
            var overrideHex = UserSettingsService.GetCustomColor(Key);
            if (!string.IsNullOrWhiteSpace(overrideHex))
            {
                HexValue = overrideHex;
            }
            else if (Application.Current.Resources[Key] is SolidColorBrush themeBrush)
            {
                HexValue = $"#{themeBrush.Color.R:X2}{themeBrush.Color.G:X2}{themeBrush.Color.B:X2}";
            }

            if (Application.Current.Resources[Key] is Brush b)
                CurrentBrush = b;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
