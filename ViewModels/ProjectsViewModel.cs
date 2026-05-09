using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;
using MyCraftyStash.Models;
using MyCraftyStash.Services;
using MyCraftyStash.Views;

namespace MyCraftyStash.ViewModels
{
    public partial class ProjectsViewModel : BaseViewModel
    {
        private readonly InventoryService _service;
        private readonly MainViewModel _mainVM;

        [ObservableProperty] private ObservableCollection<Project> _projects = new();
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private bool _hasSearched = false;
        [ObservableProperty] private Project? _selectedProject;

        // ── Project-list search filters (type / subtype / specific item) ─────────
        [ObservableProperty] private List<string> _searchTypeOptions = new();
        [ObservableProperty] private string? _selectedSearchType;
        [ObservableProperty] private ObservableCollection<SubtypeCheckboxItem> _searchSubtypeFilters = new();
        [ObservableProperty] private ObservableCollection<WizardItemOption> _searchItemOptions = new();
        [ObservableProperty] private WizardItemOption? _selectedSearchItem;

        public bool HasSearchSubtypeFilters => SearchSubtypeFilters.Count > 0;
        public bool HasSearchItemOptions    => SearchItemOptions.Count > 0;

        private void InitSearchFilterTypeItems()
        {
            var excluded = InventoryService.GetProjectExcludedItemTypes();
            SearchTypeOptions = _service.GetItemTypes().Where(t => !excluded.Contains(t)).ToList();
        }

        partial void OnSelectedSearchTypeChanged(string? value)
        {
            // Rebuild subtype checkboxes
            SearchSubtypeFilters.Clear();
            if (!string.IsNullOrEmpty(value))
            {
                var subs = UserSettingsService.GetSubtypesForType(value);
                foreach (var s in subs)
                {
                    var cb = new SubtypeCheckboxItem { Label = s };
                    cb.PropertyChanged += (_, _) => _ = OnSubtypeCheckedAsync();
                    SearchSubtypeFilters.Add(cb);
                }
            }
            OnPropertyChanged(nameof(HasSearchSubtypeFilters));

            _ = RefreshItemOptionsAndSearch();
        }

        private async Task OnSubtypeCheckedAsync()
        {
            await RefreshItemOptionsAndSearch();
        }

        private async Task RefreshItemOptionsAndSearch()
        {
            // Reload the item dropdown filtered to the current type + checked subtypes
            SearchItemOptions.Clear();
            SelectedSearchItem = null;
            OnPropertyChanged(nameof(HasSearchItemOptions));

            if (!string.IsNullOrEmpty(SelectedSearchType))
            {
                var items = await _service.GetWizardItemsAsync(type: SelectedSearchType);

                var checkedSubs = SearchSubtypeFilters.Where(s => s.IsChecked).Select(s => s.Label).ToList();
                if (checkedSubs.Count > 0)
                    items = items
                        .Where(i => i.Subtype != null &&
                            i.Subtype.Split(',').Select(p => p.Trim())
                                .Any(p => checkedSubs.Contains(p, StringComparer.OrdinalIgnoreCase)))
                        .ToList();

                foreach (var i in items) SearchItemOptions.Add(i);
                OnPropertyChanged(nameof(HasSearchItemOptions));
            }

            await LoadProjects();
        }

        partial void OnSelectedSearchItemChanged(WizardItemOption? value) => _ = LoadProjects();

        partial void OnSearchTextChanged(string value) => _ = LoadProjects();

        [RelayCommand]
        private void ClearSearchFilters()
        {
            SelectedSearchType  = null;
            SelectedSearchItem  = null;
            SearchSubtypeFilters.Clear();
            SearchItemOptions.Clear();
            OnPropertyChanged(nameof(HasSearchSubtypeFilters));
            OnPropertyChanged(nameof(HasSearchItemOptions));
            _ = LoadProjects();
        }
        [ObservableProperty] private bool _isAddingProject;
        [ObservableProperty] private bool _isViewingDetails;
        [ObservableProperty] private bool _isEditingProject;
        [ObservableProperty] private ObservableCollection<ProjectImage> _projectImages = new();
        [ObservableProperty] private int _currentImageIndex;
        [ObservableProperty] private string? _currentDisplayImage;
        [ObservableProperty] private string _newProjectName = string.Empty;
        [ObservableProperty] private string _newProjectNotes = string.Empty;

        // ── Card build wizard ─────────────────────────────────────────────────
        private string? _pendingCardBaseType;
        private List<WizardBuildStep>? _pendingBuildSteps;
        private string? _pendingStateSnapshot;
        [ObservableProperty] private bool _hasPendingCardBuild;
        [ObservableProperty] private ObservableCollection<WizardBuildStep> _pendingBuildStepList = new();
        [ObservableProperty] private ProjectCardBuild? _cardBuild;
        [ObservableProperty] private string _newProjectImageUrl = string.Empty;
        [ObservableProperty] private ObservableCollection<string> _newProjectImages = new();
        [ObservableProperty] private ObservableCollection<ProjectSelectableItem> _selectableItems = new();

        // Ordered list of items added to the project (preserves insertion order, allows duplicates)
        [ObservableProperty] private ObservableCollection<ProjectSelectableItem> _orderedProjectItems = new();

        // ── Item picker filter ────────────────────────────────────────────────

        [ObservableProperty] private ObservableCollection<SelectableStringItem> _filterTypeItems = new();
        [ObservableProperty] private bool _isTypePickerOpen;
        [ObservableProperty] private ObservableCollection<SelectableStringItem> _filterThemeItems = new();
        [ObservableProperty] private bool _isThemePickerOpen;
        [ObservableProperty] private ObservableCollection<SubtypeCheckboxItem> _subtypeFilters = new();

        private bool _allItemsLoaded = false;

        public string TypePickerDisplay
        {
            get
            {
                var selected = FilterTypeItems.Where(t => t.IsSelected).Select(t => t.Name).ToList();
                return selected.Count == 0 ? "Filter by type..." : string.Join(", ", selected);
            }
        }

        public string ThemePickerDisplay
        {
            get
            {
                var selected = FilterThemeItems.Where(t => t.IsSelected).Select(t => t.Name).ToList();
                return selected.Count == 0 ? "Filter by theme..." : string.Join(", ", selected);
            }
        }

        [RelayCommand]
        private void ToggleTypePicker() => IsTypePickerOpen = !IsTypePickerOpen;

        [RelayCommand]
        private void ToggleThemePicker() => IsThemePickerOpen = !IsThemePickerOpen;

        private void InitFilterTypeItems()
        {
            var types = _service.GetItemTypes();
            FilterTypeItems = new ObservableCollection<SelectableStringItem>(
                types.Select(t =>
                {
                    var item = new SelectableStringItem { Name = t };
                    item.SelectionChanged = OnFilterSelectionChanged;
                    return item;
                })
            );
            IsTypePickerOpen = false;

            var themes = InventoryViewModel.GetThemeOptions();
            FilterThemeItems = new ObservableCollection<SelectableStringItem>(
                themes.Select(t =>
                {
                    var item = new SelectableStringItem { Name = t };
                    item.SelectionChanged = OnFilterSelectionChanged;
                    return item;
                })
            );
            IsThemePickerOpen = false;
        }

        private void OnFilterSelectionChanged()
        {
            _ = HandleFilterChangedAsync();
        }

        private async Task HandleFilterChangedAsync()
        {
            var selectedTypes  = FilterTypeItems.Where(t => t.IsSelected).Select(t => t.Name).ToList();
            var selectedThemes = FilterThemeItems.Where(t => t.IsSelected).Select(t => t.Name).ToList();

            // Load full catalog from DB the first time any filter is selected
            if (!_allItemsLoaded && (selectedTypes.Count > 0 || selectedThemes.Count > 0))
            {
                await LoadSelectableItems();
                _allItemsLoaded = true;
            }

            // Refresh subtype checkboxes - only when exactly one type is selected
            SubtypeFilters.Clear();
            if (selectedTypes.Count == 1)
            {
                var subs = UserSettingsService.GetSubtypesForType(selectedTypes[0]);
                foreach (var s in subs)
                {
                    var cb = new SubtypeCheckboxItem { Label = s };
                    cb.PropertyChanged += (_, _) => OnPropertyChanged(nameof(FilteredSelectableItems));
                    SubtypeFilters.Add(cb);
                }
            }

            OnPropertyChanged(nameof(TypePickerDisplay));
            OnPropertyChanged(nameof(ThemePickerDisplay));
            OnPropertyChanged(nameof(FilteredSelectableItems));
            OnPropertyChanged(nameof(HasSubtypeFilters));
        }

        public IEnumerable<ProjectSelectableItem> FilteredSelectableItems
        {
            get
            {
                var selectedTypes  = FilterTypeItems.Where(t => t.IsSelected).Select(t => t.Name).ToList();
                var selectedThemes = FilterThemeItems.Where(t => t.IsSelected).Select(t => t.Name).ToList();

                // Show nothing until at least one filter is selected
                if (selectedTypes.Count == 0 && selectedThemes.Count == 0)
                    return Enumerable.Empty<ProjectSelectableItem>();

                var items = SelectableItems.AsEnumerable();

                // Type filter - "Combo Club" is a theme flag, not a real type
                if (selectedTypes.Count > 0)
                {
                    bool comboClubSelected = selectedTypes.Contains("Combo Club");
                    var realTypes = selectedTypes.Where(t => t != "Combo Club").ToList();
                    items = items.Where(s =>
                        (realTypes.Count > 0 && realTypes.Contains(s.ItemType)) ||
                        (comboClubSelected && s.ItemTheme != null && s.ItemTheme.Contains("Combo Club")));
                }

                // Theme filter (AND with type filter when both are active)
                if (selectedThemes.Count > 0)
                    items = items.Where(s => s.ItemTheme != null &&
                        selectedThemes.Any(theme => s.ItemTheme.Contains(theme)));

                // Subtype filter (only meaningful when one type is selected)
                var checkedSubs = SubtypeFilters.Where(s => s.IsChecked).Select(s => s.Label).ToList();
                if (checkedSubs.Count > 0)
                    items = items.Where(s => s.ItemSubtype != null && checkedSubs.Contains(s.ItemSubtype));

                return items;
            }
        }

        partial void OnSelectableItemsChanged(ObservableCollection<ProjectSelectableItem> value) =>
            OnPropertyChanged(nameof(FilteredSelectableItems));

        public bool HasSubtypeFilters => SubtypeFilters.Count > 0;

        partial void OnSubtypeFiltersChanged(ObservableCollection<SubtypeCheckboxItem> value) =>
            OnPropertyChanged(nameof(HasSubtypeFilters));

        // Alias used by XAML bindings in the edit/add panels
        public IEnumerable<ProjectSelectableItem> SelectedProjectItems => OrderedProjectItems;

        // Sorted items for the read-only view-details panel
        public IEnumerable<ProjectItem> ViewProjectItems =>
            SelectedProject?.ProjectItems?.OrderBy(pi => pi.SortOrder) ?? Enumerable.Empty<ProjectItem>();

        partial void OnSelectedProjectChanged(Project? value) =>
            OnPropertyChanged(nameof(ViewProjectItems));

        // "I Made One" creation tracking
        [ObservableProperty] private bool _isAddingCreation;
        [ObservableProperty] private string _newCreationNotes = string.Empty;
        [ObservableProperty] private bool _subtractMaterialsOnCreation = true;
        [ObservableProperty] private ObservableCollection<ProjectCreation> _projectCreations = new();

        public ProjectsViewModel(InventoryService service, MainViewModel mainVM)
        {
            _service = service;
            _mainVM = mainVM;
            InitSearchFilterTypeItems();
        }

        [RelayCommand]
        private async Task LoadProjects()
        {
            HasSearched = true;
            try
            {
                // Most specific filter wins: item → subtypes → type
                int?          itemId = SelectedSearchItem?.Id;
                List<string>? types  = !string.IsNullOrEmpty(SelectedSearchType)
                                           ? new List<string> { SelectedSearchType } : null;
                List<string>? subs   = SearchSubtypeFilters.Where(s => s.IsChecked)
                                           .Select(s => s.Label).ToList() is { Count: > 0 } c ? c : null;

                var projects = await _service.GetProjectsAsync(SearchText, types, subs, itemId);
                Projects = new ObservableCollection<Project>(projects);
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        [RelayCommand]
        private async Task Search() => await LoadProjects();

        [RelayCommand]
        private async Task ViewProjectDetails(Project project)
        {
            var fullProject = await _service.GetProjectAsync(project.Id);
            if (fullProject == null) { ErrorMessage = "Project not found"; return; }

            SelectedProject = fullProject;
            IsViewingDetails = true;
            IsAddingProject = false;
            IsAddingCreation = false;

            var images = await _service.GetProjectImagesAsync(project.Id);
            ProjectImages = new ObservableCollection<ProjectImage>(images);
            CurrentImageIndex = 0;
            UpdateCurrentDisplayImage();

            var creations = await _service.GetProjectCreationsAsync(project.Id);
            ProjectCreations = new ObservableCollection<ProjectCreation>(creations);

            CardBuild = await _service.GetProjectCardBuildAsync(project.Id);
        }

        private void UpdateCurrentDisplayImage()
        {
            if (CurrentImageIndex == 0 && SelectedProject?.ImageUrl != null)
                CurrentDisplayImage = SelectedProject.ImageUrl;
            else
            {
                var adjustedIndex = SelectedProject?.ImageUrl != null ? CurrentImageIndex - 1 : CurrentImageIndex;
                if (adjustedIndex >= 0 && adjustedIndex < ProjectImages.Count)
                    CurrentDisplayImage = ProjectImages[adjustedIndex].ImageUrl;
                else if (SelectedProject?.ImageUrl != null)
                    CurrentDisplayImage = SelectedProject.ImageUrl;
                else if (ProjectImages.Count > 0)
                    CurrentDisplayImage = ProjectImages[0].ImageUrl;
                else
                    CurrentDisplayImage = null;
            }
        }

        public int TotalImages => (SelectedProject?.ImageUrl != null ? 1 : 0) + ProjectImages.Count;

        [RelayCommand]
        private void PreviousImage()
        {
            if (TotalImages <= 1) return;
            CurrentImageIndex = (CurrentImageIndex - 1 + TotalImages) % TotalImages;
            UpdateCurrentDisplayImage();
        }

        [RelayCommand]
        private void NextImage()
        {
            if (TotalImages <= 1) return;
            CurrentImageIndex = (CurrentImageIndex + 1) % TotalImages;
            UpdateCurrentDisplayImage();
        }

        [RelayCommand]
        private void BackToList()
        {
            IsViewingDetails = false;
            IsAddingProject = false;
            IsAddingCreation = false;
            SelectedProject = null;
        }

        public Task StartAddProject()
        {
            IsAddingProject = true;
            IsViewingDetails = false;
            ResetNewProjectForm();
            _allItemsLoaded = false;
            SelectableItems = new ObservableCollection<ProjectSelectableItem>();
            OrderedProjectItems = new ObservableCollection<ProjectSelectableItem>();
            InitFilterTypeItems();
            return Task.CompletedTask;
        }

        private async Task LoadSelectableItems()
        {
            var allItems = await _service.GetItemsAsync();
            SelectableItems = new ObservableCollection<ProjectSelectableItem>(
                allItems.Select(i => new ProjectSelectableItem
                {
                    ItemId = i.Id, ItemName = i.Name, ItemType = i.Type,
                    ItemNumber = i.ItemNumber, ItemSubtype = i.Subtype, ItemTheme = i.Theme
                })
            );
        }

        private void ResetNewProjectForm()
        {
            NewProjectName = string.Empty;
            NewProjectNotes = string.Empty;
            NewProjectImageUrl = string.Empty;
            NewProjectImages = new ObservableCollection<string>();
            SubtypeFilters.Clear();
            _pendingCardBaseType = null;
            _pendingBuildSteps = null;
            _pendingStateSnapshot = null;
            HasPendingCardBuild = false;
            PendingBuildStepList.Clear();
        }

        public void AddNewProjectImage(string base64Image)
        {
            NewProjectImages.Add(base64Image);
            if (string.IsNullOrEmpty(NewProjectImageUrl)) NewProjectImageUrl = base64Image;
        }

        public void RemoveNewProjectImage(string base64Image)
        {
            NewProjectImages.Remove(base64Image);
            if (NewProjectImageUrl == base64Image)
                NewProjectImageUrl = NewProjectImages.Count > 0 ? NewProjectImages[0] : string.Empty;
            else if (NewProjectImages.Count == 0)
                NewProjectImageUrl = string.Empty;
        }

        [RelayCommand]
        private async Task SaveProject()
        {
            if (string.IsNullOrWhiteSpace(NewProjectName)) { ErrorMessage = "Project name is required"; return; }
            IsLoading = true;
            try
            {
                var project = new Project
                {
                    Name = NewProjectName,
                    Notes = string.IsNullOrWhiteSpace(NewProjectNotes) ? null : NewProjectNotes,
                    ImageUrl = string.IsNullOrWhiteSpace(NewProjectImageUrl) ? null : NewProjectImageUrl
                };
                var itemUsages = OrderedProjectItems.Select(s => (s.ItemId, s.AmountUsed)).ToList();
                var created = await _service.CreateProjectAsync(project, itemUsages);
                for (int i = 1; i < NewProjectImages.Count; i++)
                    await _service.AddProjectImageAsync(created.Id, NewProjectImages[i], i);
                if (_pendingCardBaseType != null && _pendingBuildSteps != null)
                    await _service.SaveProjectCardBuildAsync(created.Id, _pendingCardBaseType, _pendingBuildSteps, _pendingStateSnapshot);
                IsAddingProject = false;
                await LoadProjects();
                _mainVM.NavigateToProjectsCommand.Execute(null);
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        private void OpenCardBuildWizard()
        {
            var projectImage = NewProjectImages.Count > 0 ? NewProjectImages[0] : null;
            // When editing an existing project, prefer the in-flight pending snapshot
            // (so the user keeps in-progress wizard edits across re-opens), otherwise
            // hydrate from the saved card build's snapshot if there is one.
            var existingSnapshot = _pendingStateSnapshot
                ?? (IsEditingProject ? CardBuild?.StateSnapshot : null);
            // Wizard-side uploads call back into AddNewProjectImage so both views share one image list.
            var window = new CardBuildWizardWindow(_service, projectImage, AddNewProjectImage, existingSnapshot)
            {
                Owner = Application.Current.MainWindow
            };
            if (window.ShowDialog() == true)
            {
                var vm = window.WizardVM;
                _pendingCardBaseType = vm.CardBaseType;
                _pendingBuildSteps = vm.BuildSteps;
                _pendingStateSnapshot = vm.CaptureSnapshotJson();
                HasPendingCardBuild = true;
                PendingBuildStepList = new ObservableCollection<WizardBuildStep>(vm.BuildSteps);

                if (!string.IsNullOrWhiteSpace(vm.WizardNotes))
                {
                    NewProjectNotes = string.IsNullOrWhiteSpace(NewProjectNotes)
                        ? vm.WizardNotes.Trim()
                        : NewProjectNotes.TrimEnd() + "\n" + vm.WizardNotes.Trim();
                }

                if (!string.IsNullOrEmpty(vm.BuildOtherNotes))
                {
                    NewProjectNotes = string.IsNullOrWhiteSpace(NewProjectNotes)
                        ? vm.BuildOtherNotes
                        : NewProjectNotes.TrimEnd() + "\n" + vm.BuildOtherNotes;
                }

                // Auto-select wizard items in the picker (load all items if not yet loaded)
                _ = AutoSelectWizardItemsAsync(vm.SelectedItemIds);
            }
        }

        // Re-runs the card build wizard against an existing project. The new build
        // replaces the saved build (SaveProjectCardBuildAsync deletes the prior one),
        // and any items the wizard introduces are merged into the project's items
        // without removing existing ones.
        [RelayCommand]
        private async Task EditCardBuild()
        {
            if (SelectedProject == null) return;

            var existingSnapshot = CardBuild?.StateSnapshot;
            var window = new CardBuildWizardWindow(_service, SelectedProject.ImageUrl, null, existingSnapshot)
            {
                Owner = Application.Current.MainWindow
            };
            if (window.ShowDialog() != true) return;

            var vm = window.WizardVM;
            if (vm.BuildSteps.Count == 0) return;

            IsLoading = true;
            try
            {
                await _service.SaveProjectCardBuildAsync(SelectedProject.Id, vm.CardBaseType, vm.BuildSteps, vm.CaptureSnapshotJson());

                var mergedUsages = (SelectedProject.ProjectItems ?? Enumerable.Empty<ProjectItem>())
                    .OrderBy(pi => pi.SortOrder)
                    .Select(pi => (pi.ItemId, pi.AmountUsedPerCreation))
                    .ToList();
                var existingIds = mergedUsages.Select(u => u.ItemId).ToHashSet();
                foreach (var newId in vm.SelectedItemIds.Distinct())
                {
                    if (existingIds.Add(newId))
                        mergedUsages.Add((newId, (decimal?)null));
                }

                var notesAdditions = new List<string>();
                if (!string.IsNullOrWhiteSpace(vm.WizardNotes)) notesAdditions.Add(vm.WizardNotes.Trim());
                if (!string.IsNullOrEmpty(vm.BuildOtherNotes)) notesAdditions.Add(vm.BuildOtherNotes);
                if (notesAdditions.Count > 0)
                {
                    var combined = string.Join("\n", notesAdditions);
                    SelectedProject.Notes = string.IsNullOrWhiteSpace(SelectedProject.Notes)
                        ? combined
                        : SelectedProject.Notes.TrimEnd() + "\n" + combined;
                }

                await _service.UpdateProjectAsync(SelectedProject, mergedUsages);
                await ViewProjectDetails(SelectedProject);
                await LoadProjects();
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
            finally { IsLoading = false; }
        }

        private async Task AutoSelectWizardItemsAsync(List<int> itemIds)
        {
            if (itemIds.Count == 0) return;
            if (!_allItemsLoaded)
            {
                await LoadSelectableItems();
                _allItemsLoaded = true;
            }
            foreach (var id in itemIds)
            {
                var source = SelectableItems.FirstOrDefault(s => s.ItemId == id);
                if (source == null) continue;
                AppendToOrderedList(source);
            }
        }

        // Creates a new ordered-list entry from a catalog item and appends it.
        private void AppendToOrderedList(ProjectSelectableItem source)
        {
            ProjectSelectableItem? entry = null;
            entry = new ProjectSelectableItem
            {
                ItemId = source.ItemId, ItemName = source.ItemName, ItemType = source.ItemType,
                ItemNumber = source.ItemNumber, ItemSubtype = source.ItemSubtype, ItemTheme = source.ItemTheme,
                IsSelected = true
            };
            entry.RemoveAction = () => OrderedProjectItems.Remove(entry);
            if (entry.IsWholeUnit) entry.AmountUsed = 1;
            OrderedProjectItems.Add(entry);
        }

        [RelayCommand]
        private void AddItemToProject(ProjectSelectableItem item) => AppendToOrderedList(item);

        [RelayCommand]
        private void CancelAdd()
        {
            IsAddingProject = false;
            _mainVM.NavigateToProjectsCommand.Execute(null);
        }

        [RelayCommand]
        private async Task StartEditProject()
        {
            if (SelectedProject == null) return;
            NewProjectName = SelectedProject.Name;
            NewProjectNotes = SelectedProject.Notes ?? string.Empty;
            NewProjectImageUrl = SelectedProject.ImageUrl ?? string.Empty;
            NewProjectImages = new ObservableCollection<string>();
            if (!string.IsNullOrEmpty(SelectedProject.ImageUrl)) NewProjectImages.Add(SelectedProject.ImageUrl);
            foreach (var img in ProjectImages) NewProjectImages.Add(img.ImageUrl);

            SubtypeFilters.Clear();
            InitFilterTypeItems();

            _allItemsLoaded = false;
            SelectableItems = new ObservableCollection<ProjectSelectableItem>();

            // Populate the ordered list from saved items, sorted by SortOrder
            OrderedProjectItems = new ObservableCollection<ProjectSelectableItem>(
                (SelectedProject.ProjectItems ?? Enumerable.Empty<ProjectItem>())
                    .OrderBy(pi => pi.SortOrder)
                    .Where(pi => pi.Item != null)
                    .Select(pi =>
                    {
                        var amt = pi.AmountUsedPerCreation;
                        if (amt == null && InventoryService.IsTrackedType(pi.Item!.Type) && !InventoryService.IsCardstockType(pi.Item.Type))
                            amt = 1;
                        ProjectSelectableItem? entry = null;
                        entry = new ProjectSelectableItem
                        {
                            ItemId = pi.ItemId, ItemName = pi.Item!.Name, ItemType = pi.Item.Type,
                            ItemNumber = pi.Item.ItemNumber, ItemSubtype = pi.Item.Subtype, ItemTheme = pi.Item.Theme,
                            IsSelected = true, AmountUsed = amt
                        };
                        entry.RemoveAction = () => OrderedProjectItems.Remove(entry);
                        return entry;
                    })
            );

            _pendingCardBaseType = null;
            _pendingBuildSteps = null;
            _pendingStateSnapshot = null;
            HasPendingCardBuild = false;
            PendingBuildStepList.Clear();

            IsEditingProject = true;
            IsViewingDetails = false;
        }

        [RelayCommand]
        private async Task SaveEditProject()
        {
            if (SelectedProject == null || string.IsNullOrWhiteSpace(NewProjectName)) { ErrorMessage = "Project name is required"; return; }
            IsLoading = true;
            try
            {
                SelectedProject.Name = NewProjectName;
                SelectedProject.Notes = string.IsNullOrWhiteSpace(NewProjectNotes) ? null : NewProjectNotes;
                SelectedProject.ImageUrl = !string.IsNullOrEmpty(NewProjectImageUrl) ? NewProjectImageUrl
                    : (NewProjectImages.Count > 0 ? NewProjectImages[0] : null);
                var itemUsages = OrderedProjectItems.Select(s => (s.ItemId, s.AmountUsed)).ToList();
                await _service.UpdateProjectAsync(SelectedProject, itemUsages);
                await _service.ClearProjectImagesAsync(SelectedProject.Id);
                for (int i = 1; i < NewProjectImages.Count; i++)
                    await _service.AddProjectImageAsync(SelectedProject.Id, NewProjectImages[i], i);
                if (_pendingCardBaseType != null && _pendingBuildSteps != null)
                    await _service.SaveProjectCardBuildAsync(SelectedProject.Id, _pendingCardBaseType, _pendingBuildSteps, _pendingStateSnapshot);
                IsEditingProject = false;
                await ViewProjectDetails(SelectedProject);
                await LoadProjects();
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        private void CancelEditProject()
        {
            _pendingCardBaseType = null;
            _pendingBuildSteps = null;
            _pendingStateSnapshot = null;
            HasPendingCardBuild = false;
            PendingBuildStepList.Clear();
            IsEditingProject = false;
            IsViewingDetails = true;
        }

        [RelayCommand]
        private async Task DeleteProject(Project project)
        {
            var result = MessageBox.Show(
                $"Are you sure you want to delete \"{project.Name}\"? This cannot be undone.",
                "Delete Project", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            IsLoading = true;
            try { await _service.DeleteProjectAsync(project.Id); await LoadProjects(); BackToList(); }
            catch (Exception ex) { ErrorMessage = ex.Message; }
            finally { IsLoading = false; }
        }

        // ── Project sharing ──────────────────────────────────────────────────

        [RelayCommand]
        private async Task ShareProject(Project? project)
        {
            project ??= SelectedProject;
            if (project == null) return;

            try
            {
                IsLoading = true;
                var shareSvc  = new ProjectShareService(() => new Data.InventoryDbContext());
                var addrSvc   = new AddressBookService();
                var contacts  = await addrSvc.GetAllEntriesAsync();

                var dialog = new ShareProjectDialog(contacts)
                {
                    Owner = System.Windows.Application.Current.MainWindow,
                };
                if (dialog.ShowDialog() != true) return;

                var path = await shareSvc.ExportAsync(project.Id, exportedByName: Environment.UserName);

                if (dialog.OpenInExplorer)
                {
                    try { System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\""); } catch { }
                }

                if (dialog.ShouldSendEmail && !string.IsNullOrWhiteSpace(dialog.SelectedEmail))
                {
                    var subject = Uri.EscapeDataString($"My Crafty Stash project: {project.Name}");
                    var body = Uri.EscapeDataString(
                        $"I'd like to share my My Crafty Stash project \"{project.Name}\" with you.{Environment.NewLine}{Environment.NewLine}" +
                        $"Attach the file at: {path}{Environment.NewLine}{Environment.NewLine}" +
                        "Open it from inside My Crafty Stash via Projects → Import to add it to your list.");
                    var mailto = $"mailto:{dialog.SelectedEmail}?subject={subject}&body={body}";
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(mailto) { UseShellExecute = true }); }
                    catch (Exception ex) { LoggingService.LogError(ex, "ProjectsViewModel.ShareProject mailto launch"); }
                }

                MessageBox.Show(
                    $"Saved to:{Environment.NewLine}{path}{Environment.NewLine}{Environment.NewLine}" +
                    "Attach this file to the email your mail client just opened.",
                    "Project shared", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Couldn't share project: {ex.Message}";
                LoggingService.LogError(ex, "ProjectsViewModel.ShareProject");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task ImportSharedProject()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Open shared project",
                Filter = "My Crafty Stash project (*.mcsproject)|*.mcsproject|All files (*.*)|*.*",
                CheckFileExists = true,
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                IsLoading = true;
                var importSvc = new ProjectImportService(() => new Data.InventoryDbContext());
                var result = await importSvc.ImportAsync(dlg.FileName);
                await LoadProjects();
                MessageBox.Show(
                    $"Imported \"{result.ProjectName}\".{Environment.NewLine}{Environment.NewLine}" +
                    $"Items linked from your inventory: {result.ItemsLinked}{Environment.NewLine}" +
                    $"Items skipped (not in your inventory): {result.ItemsSkipped}{Environment.NewLine}" +
                    $"Images imported: {result.ImagesImported}{Environment.NewLine}" +
                    $"Card builds imported: {result.CardBuildsImported}",
                    "Project imported", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Couldn't import project: {ex.Message}";
                LoggingService.LogError(ex, "ProjectsViewModel.ImportSharedProject");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ---- "I Made One" ----

        [RelayCommand]
        private void StartAddCreation()
        {
            NewCreationNotes = string.Empty;
            SubtractMaterialsOnCreation = true;
            IsAddingCreation = true;
        }

        [RelayCommand]
        private void CancelAddCreation() => IsAddingCreation = false;

        [RelayCommand]
        private async Task SaveCreation()
        {
            if (SelectedProject == null) return;
            IsLoading = true;
            try
            {
                var materialUsages = SelectedProject.ProjectItems?
                    .Where(pi => pi.AmountUsedPerCreation.HasValue && pi.Item != null && InventoryService.IsTrackedType(pi.Item.Type))
                    .Select(pi => (pi.ItemId, pi.AmountUsedPerCreation!.Value))
                    .ToList() ?? new List<(int, decimal)>();

                await _service.AddProjectCreationAsync(
                    SelectedProject.Id,
                    string.IsNullOrWhiteSpace(NewCreationNotes) ? null : NewCreationNotes,
                    SubtractMaterialsOnCreation,
                    materialUsages
                );

                IsAddingCreation = false;
                var refreshed = await _service.GetProjectAsync(SelectedProject.Id);
                if (refreshed != null) SelectedProject = refreshed;
                var creations = await _service.GetProjectCreationsAsync(SelectedProject.Id);
                ProjectCreations = new ObservableCollection<ProjectCreation>(creations);
            }
            catch (Exception ex) { ErrorMessage = $"Failed to save: {ex.Message}"; }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        private async Task DeleteCreation(ProjectCreation creation)
        {
            try { await _service.DeleteProjectCreationAsync(creation.Id); ProjectCreations.Remove(creation); }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        [RelayCommand]
        private async Task NavigateToItem(ProjectItem projectItem)
        {
            if (projectItem?.Item == null) return;
            _mainVM.CurrentPage = "Inventory";
            _mainVM.CurrentView = _mainVM.InventoryVM;
            await _mainVM.InventoryVM.ViewItemDetailsCommand.ExecuteAsync(projectItem.Item);
        }

        [RelayCommand]
        private void SelectGalleryImage(object param)
        {
            if (param is int index) { CurrentImageIndex = index; UpdateCurrentDisplayImage(); }
        }
    }
}
