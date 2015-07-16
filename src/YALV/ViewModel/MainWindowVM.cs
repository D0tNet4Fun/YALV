using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Shell;
using System.Windows.Threading;
using YALV.Common;
using YALV.Common.Interfaces;
using YALV.Core;
using YALV.Core.Domain;
using YALV.Properties;

namespace YALV.ViewModel
{
    public class MainWindowVM
        : BindableObject
    {
        public MainWindowVM(IWinSimple win, TaskScheduler taskScheduler)
        {
            _callingWin = win;
            _taskScheduler = taskScheduler;

            CommandExit = new CommandRelay(commandExitExecute, p => true);
            CommandOpenFile = new CommandRelay(commandOpenFileExecute, commandOpenFileCanExecute);
            CommandSelectFolder = new CommandRelay(commandSelectFolderExecute, commandSelectFolderCanExecute);
            CommandSaveFolder = new CommandRelay(commandSaveFolderExecute, commandSaveFolderCanExecute);
            CommandRefresh = new CommandRelay(commandRefreshExecute, commandRefreshCanExecute);
            CommandRefreshFiles = new CommandRelay(commandRefreshFilesExecute, commandRefreshFilesCanExecute);
            CommandClear = new CommandRelay(commandClearExecute, commandClearCanExecute);
            CommandDelete = new CommandRelay(commandDeleteExecute, commandDeleteCanExecute);
            CommandOpenSelectedFolder = new CommandRelay(commandOpenSelectedFolderExecute, commandOpenSelectedFolderCanExecute);
            CommandIncreaseInterval = new CommandRelay(commandIncreaseIntervalExecute, p => true);
            CommandDecreaseInterval = new CommandRelay(commandDecreaseIntervalExecute, p => true);
            CommandSelectColumns = new CommandRelay(commandSelectColumnsExecute, _ => true);
            CommandAbout = new CommandRelay(commandAboutExecute, p => true);

            FileList = new ObservableCollection<FileItem>();
            Items = new ObservableCollection<LogItem>();
            loadFolderList();

            SelectedFile = null;
            IsFileSelectionEnabled = false;
            IsLoading = false;

            _cancellationTokenSource = new CancellationTokenSource();

            _selectAll = true;
            _selectDebug = _selectInfo = _selectWarn = _selectError = _selectFatal = false;
            _showLevelDebug = _showLevelInfo = _showLevelWarn = _showLevelError = _showLevelFatal = true;

            _allItems = new List<LogItem>();
            _dispatcherTimer = new DispatcherTimer();
            _dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);

            AutoRefreshInterval = Constants.DEFAULT_REFRESH_INTERVAL;
            IsAutoRefreshEnabled = false;
        }

        protected override void OnDispose()
        {
            if (_dispatcherTimer != null)
            {
                _dispatcherTimer.Stop();
                _dispatcherTimer.Tick -= dispatcherTimer_Tick;
            }

            if (GridManager != null)
                GridManager.Dispose();

            lock (_cancellationLock)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
            }

            Items.Clear();
            FileList.Clear();

            base.OnDispose();
        }

        #region Commands

        /// <summary>
        /// Exit Command
        /// </summary>
        public ICommandAncestor CommandExit { get; protected set; }

        /// <summary>
        /// OpenFile Command
        /// </summary>
        public ICommandAncestor CommandOpenFile { get; protected set; }

        /// <summary>
        /// SelectFolder Command
        /// </summary>
        public ICommandAncestor CommandSelectFolder { get; protected set; }

        /// <summary>
        /// SaveFolder Command
        /// </summary>
        public ICommandAncestor CommandSaveFolder { get; protected set; }

        /// <summary>
        /// Refresh Command
        /// </summary>
        public ICommandAncestor CommandRefresh { get; protected set; }

        /// <summary>
        /// RefreshFiles Command
        /// </summary>
        public ICommandAncestor CommandRefreshFiles { get; protected set; }

        /// <summary>
        /// Clear Command
        /// </summary>
        public ICommandAncestor CommandClear { get; protected set; }

        /// <summary>
        /// Delete Command
        /// </summary>
        public ICommandAncestor CommandDelete { get; protected set; }

        /// <summary>
        /// OpenSelectedFolder Command
        /// </summary>
        public ICommandAncestor CommandOpenSelectedFolder { get; protected set; }

        /// <summary>
        /// SelectColumns command
        /// </summary>
        public ICommandAncestor CommandSelectColumns { get; protected set; }

        /// <summary>
        /// About Command
        /// </summary>
        public ICommandAncestor CommandAbout { get; protected set; }

        protected virtual object commandExitExecute(object parameter)
        {
            _callingWin.Close();
            return null;
        }

        protected virtual object commandOpenFileExecute(object parameter)
        {
            using (System.Windows.Forms.OpenFileDialog dlg = new System.Windows.Forms.OpenFileDialog())
            {
                bool addFile = parameter != null && parameter.Equals("ADD");
                dlg.Filter = String.Format("{0} (*.xml)|*.xml|{1} (*.*)|*.*", Properties.Resources.MainWindowVM_commandOpenFileExecute_XmlFilesCaption, Properties.Resources.MainWindowVM_commandOpenFileExecute_AllFilesCaption);
                dlg.DefaultExt = "xml";
                dlg.Multiselect = true;
                dlg.Title = addFile ? Resources.MainWindowVM_commandOpenFileExecute_Add_Log_File : Resources.MainWindowVM_commandOpenFileExecute_Open_Log_File;

                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string[] files = dlg.FileNames;
                    SelectedFolder = null;
                    LoadFileList(files, addFile);
                }
            }
            return null;
        }

        protected virtual bool commandOpenFileCanExecute(object parameter)
        {
            return true;
        }

        protected virtual object commandSelectFolderExecute(object parameter)
        {
            using (System.Windows.Forms.FolderBrowserDialog dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                dlg.Description = Resources.MainWindowVM_commandSelectFolderExecute_Description;
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string selectedPath = dlg.SelectedPath;
                    SelectedFolder = null;
                    for (int i = 0; i < FolderList.Count; i++)
                    {
                        PathItem item = FolderList[i];
                        if (item.Path.Equals(selectedPath, StringComparison.OrdinalIgnoreCase))
                        {
                            SelectedFolder = item;
                            return null;
                        }
                    }
                    loadFolderFiles(selectedPath);
                }
            }
            return null;
        }

        protected virtual bool commandSelectFolderCanExecute(object parameter)
        {
            return true;
        }

        protected virtual object commandSaveFolderExecute(object parameter)
        {
            var win = new AddFolderPath() { Owner = _callingWin as Window };
            if (win.EditList())
                loadFolderList();
            return null;
        }

        protected virtual bool commandSaveFolderCanExecute(object parameter)
        {
            return true;
        }

        protected virtual object commandClearExecute(object parameter)
        {
            if (GridManager != null)
            {
                GridManager.ResetSearchTextBox();
                RefreshView();
            }
            return null;
        }

        protected virtual bool commandClearCanExecute(object parameter)
        {
            return true;
        }

        protected virtual object commandRefreshExecute(object parameter)
        {
            Items.Clear();

            if (IsFileSelectionEnabled)
            {
                foreach (FileItem item in FileList)
                {
                    if (item.Checked)
                        loadLogFileAsync(item.Path);
                }
            }
            else
            {
                if (FileList.Count > 0 && SelectedFile != null)
                    loadLogFileAsync(SelectedFile.Path);
            }

            return null;
        }

        protected virtual bool commandRefreshCanExecute(object parameter)
        {
            return true;
        }

        protected virtual object commandRefreshFilesExecute(object parameter)
        {
            if (_selectedFolder != null)
            {
                Items.Clear();

                if (IsFileSelectionEnabled)
                {
                    //Reload file list and restore all checked items
                    IList<string> checkedItems = (from f in FileList
                                                  where f.Checked
                                                  select f.Path).ToList<string>();
                    SelectedFile = null;
                    loadFolderFiles(_selectedFolder.Path);

                    if (checkedItems != null && checkedItems.Count > 0)
                    {
                        foreach (string filePath in checkedItems)
                        {
                            FileItem selItem = (from f in FileList
                                                where filePath.Equals(f.Path, StringComparison.OrdinalIgnoreCase)
                                                select f).FirstOrDefault<FileItem>();
                            if (selItem != null)
                                selItem.Checked = true;
                        }
                    }
                }
                else
                {
                    //Reload file list and restore selected item
                    string selectedFilePath = SelectedFile != null ? SelectedFile.Path : string.Empty;
                    SelectedFile = null;
                    loadFolderFiles(_selectedFolder.Path);

                    if (!String.IsNullOrWhiteSpace(selectedFilePath))
                    {
                        FileItem selItem = (from f in FileList
                                            where selectedFilePath.Equals(f.Path, StringComparison.OrdinalIgnoreCase)
                                            select f).FirstOrDefault<FileItem>();
                        if (selItem != null)
                            SelectedFile = selItem;
                    }
                }
            }
            return null;
        }

        protected virtual bool commandRefreshFilesCanExecute(object parameter)
        {
            return SelectedFolder != null;
        }

        protected virtual object commandDeleteExecute(object parameter)
        {
            if (IsFileSelectionEnabled)
            {
                if (MessageBox.Show(Resources.MainWindowVM_commandDeleteExecute_DeleteCheckedFiles_ConfirmText, Resources.MainWindowVM_commandDeleteExecute_DeleteCheckedFiles_ConfirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.No)
                    return null;

                //Delete all selected file
                for (int i = FileList.Count - 1; i >= 0; i--)
                {
                    FileItem item = FileList[i];
                    if (item.Checked && deleteFile(item.Path))
                    {
                        Items.Clear();
                        SelectedFile = null;
                        FileList.RemoveAt(i);
                    }
                }
            }
            else
            {
                if (MessageBox.Show(Resources.MainWindowVM_commandDeleteExecute_DeleteSelectedFile_ConfirmText, Resources.MainWindowVM_commandDeleteExecute_DeleteSelectedFile_ConfirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.No)
                    return null;

                //Delete selected file
                if (SelectedFile != null)
                {
                    int indexToDelete = FileList.IndexOf(SelectedFile);
                    if (deleteFile(SelectedFile.Path))
                    {
                        Items.Clear();
                        SelectedFile = null;
                        FileList.RemoveAt(indexToDelete);
                    }
                }
            }
            return null;
        }

        protected virtual bool commandDeleteCanExecute(object parameter)
        {
            if (IsFileSelectionEnabled)
            {
                if (FileList == null || FileList.Count == 0)
                    return false;

                return (from f in FileList
                        where f.Checked
                        select f).Count() > 0;
            }
            else
                return SelectedFile != null;
        }

        protected virtual object commandOpenSelectedFolderExecute(object parameter)
        {
            string path = SelectedFolder != null ? SelectedFolder.Path : string.Empty;
            if (!String.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                Process.Start("explorer.exe", path);
            return null;
        }

        protected virtual bool commandOpenSelectedFolderCanExecute(object parameter)
        {
            return SelectedFolder != null;
        }

        protected virtual object commandSelectColumnsExecute(object parameter)
        {
            var win = new SelectColumns() { Owner = _callingWin as Window };
            win.ShowDialog(GridManager);
            return null;
        }

        protected virtual object commandAboutExecute(object parameter)
        {
            var win = new About() { Owner = _callingWin as Window };
            win.ShowDialog();
            return null;
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// RecentFileList Manager
        /// </summary>
        public RecentFileList RecentFileList
        {
            get { return _recentFileList; }
            set
            {
                _recentFileList = value;
                if (_recentFileList != null)
                {
                    _recentFileList.MenuClick += (s, e) =>
                    {
                        SelectedFolder = null;
                        LoadFileList(new string[] { e.Filepath }, false);
                    };
                    updateJumpList();
                }
            }
        }
        private RecentFileList _recentFileList;
        public static string PROP_RecentFileList = "RecentFileList";

        /// <summary>
        /// IsLoading Property
        /// </summary>
        public bool IsLoading
        {
            get { return _isLoading; }
            set
            {
                _isLoading = value;
                RaisePropertyChanged(PROP_IsLoading);
            }
        }
        private bool _isLoading;
        public static string PROP_IsLoading = "IsLoading";

        /// <summary>
        /// IsFileSelectionEnabled Property
        /// </summary>
        public bool IsFileSelectionEnabled
        {
            get { return _isFileSelectionEnabled; }
            set
            {
                _isFileSelectionEnabled = value;
                RaisePropertyChanged(PROP_IsFileSelectionEnabled);

                var hasAllFilesItem = FileList.Count >= 1 && FileList[0] is AllFilesItem;
                if (_isFileSelectionEnabled)
                {
                    if (!hasAllFilesItem)
                    {
                        FileList.Insert(0, new AllFilesItem(FileList.ToArray()));
                    }
                    Items.Clear();
                    if (FileList.Count > 1 && SelectedFile != null)
                        SelectedFile.Checked = true;
                }
                else
                {
                    if (hasAllFilesItem)
                    {
                        FileList.RemoveAt(0);
                    }
                    Items.Clear();
                    foreach (FileItem item in FileList)
                        item.Checked = false;
                    SelectedFile = null;
                }

                refreshCommandsCanExecute();
            }
        }
        private bool _isFileSelectionEnabled;
        public static string PROP_IsFileSelectionEnabled = "IsFileSelectionEnabled";

        /// <summary>
        /// SelectedFile Property
        /// </summary>
        public FileItem SelectedFile
        {
            get { return _selectedFile; }
            set
            {
                if (value != _selectedFile)
                {
                    _selectedFile = value;
                    RaisePropertyChanged(PROP_SelectedFile);

                    if (!_loadingFileList && _selectedFile != null)
                    {
                        string path = _selectedFile.Path;
                        SelectedFileDir = !string.IsNullOrWhiteSpace(path) ? Path.GetDirectoryName(path) : string.Empty;
                        if (!IsFileSelectionEnabled)
                            loadLogFileAsync(path);
                    }

                    refreshCommandsCanExecute();
                }
            }
        }
        private FileItem _selectedFile;
        public static string PROP_SelectedFile = "SelectedFile";

        /// <summary>
        /// SelectedFolder Property
        /// </summary>
        public PathItem SelectedFolder
        {
            get { return _selectedFolder; }
            set
            {
                if (value != _selectedFolder)
                {
                    _selectedFolder = value;
                    RaisePropertyChanged(PROP_SelectedFolder);

                    Items.Clear();
                    if (_selectedFolder != null)
                        loadFolderFiles(_selectedFolder.Path);

                    refreshCommandsCanExecute();
                }
            }
        }
        private PathItem _selectedFolder;
        public static string PROP_SelectedFolder = "SelectedFolder";

        /// <summary>
        /// SelectedFileDir Property
        /// </summary>
        public string SelectedFileDir
        {
            get { return _selectedFileDir; }
            set
            {
                _selectedFileDir = value;
                RaisePropertyChanged(PROP_SelectedFileDir);
            }
        }
        private string _selectedFileDir;
        public static string PROP_SelectedFileDir = "SelectedFileDir";

        /// <summary>
        /// FolderList Property
        /// </summary>
        public ObservableCollection<PathItem> FolderList
        {
            get { return _folderList; }
            set
            {
                _folderList = value;
                RaisePropertyChanged(PROP_FolderList);
            }
        }
        private ObservableCollection<PathItem> _folderList;
        public static string PROP_FolderList = "FolderList";

        /// <summary>
        /// FileList Property
        /// </summary>
        public ObservableCollection<FileItem> FileList
        {
            get { return _fileList; }
            set
            {
                _fileList = value;
                RaisePropertyChanged(PROP_FileList);

                refreshCommandsCanExecute();
            }
        }
        private ObservableCollection<FileItem> _fileList;
        public static string PROP_FileList = "FileList";

        /// <summary>
        /// LogItems Property
        /// </summary>
        public ObservableCollection<LogItem> Items
        {
            get { return _items; }
            set
            {
                _items = value;
                RaisePropertyChanged(PROP_Items);
            }
        }
        private ObservableCollection<LogItem> _items;
        public static string PROP_Items = "Items";

        /// <summary>
        /// SelectedLogItem Property
        /// </summary>
        public LogItem SelectedLogItem
        {
            get { return _selectedLogItem; }
            set
            {
                _selectedLogItem = value;
                RaisePropertyChanged(PROP_SelectedLogItem);

                _goToLogItemId = _selectedLogItem != null ? _selectedLogItem.Id.ToString() : string.Empty;
                RaisePropertyChanged(PROP_GoToLogItemId);
            }
        }
        private LogItem _selectedLogItem;
        public static string PROP_SelectedLogItem = "SelectedLogItem";

        /// <summary>
        /// ShowLevelDebug Property
        /// </summary>
        public bool ShowLevelDebug
        {
            get { return _showLevelDebug; }
            set
            {
                if (value != _showLevelDebug)
                {
                    _showLevelDebug = value;
                    RaisePropertyChanged(PROP_ShowLevelDebug);
                    resetLevelSelection();
                    RefreshView();
                }
            }
        }
        private bool _showLevelDebug;
        public static string PROP_ShowLevelDebug = "ShowLevelDebug";

        /// <summary>
        /// ShowLevelInfo Property
        /// </summary>
        public bool ShowLevelInfo
        {
            get { return _showLevelInfo; }
            set
            {
                if (value != _showLevelInfo)
                {
                    _showLevelInfo = value;
                    RaisePropertyChanged(PROP_ShowLevelInfo);
                    resetLevelSelection();
                    RefreshView();
                }
            }
        }
        private bool _showLevelInfo;
        public static string PROP_ShowLevelInfo = "ShowLevelInfo";

        /// <summary>
        /// ShowLevelWarn Property
        /// </summary>
        public bool ShowLevelWarn
        {
            get { return _showLevelWarn; }
            set
            {
                if (value != _showLevelWarn)
                {
                    _showLevelWarn = value;
                    RaisePropertyChanged(PROP_ShowLevelWarn);
                    resetLevelSelection();
                    RefreshView();
                }
            }
        }
        private bool _showLevelWarn;
        public static string PROP_ShowLevelWarn = "ShowLevelWarn";

        /// <summary>
        /// ShowLevelError Property
        /// </summary>
        public bool ShowLevelError
        {
            get { return _showLevelError; }
            set
            {
                if (value != _showLevelError)
                {
                    _showLevelError = value;
                    RaisePropertyChanged(PROP_ShowLevelError);
                    resetLevelSelection();
                    RefreshView();
                }
            }
        }
        private bool _showLevelError;
        public static string PROP_ShowLevelError = "ShowLevelError";

        /// <summary>
        /// ShowLevelFatal Property
        /// </summary>
        public bool ShowLevelFatal
        {
            get { return _showLevelFatal; }
            set
            {
                if (value != _showLevelFatal)
                {
                    _showLevelFatal = value;
                    RaisePropertyChanged(PROP_ShowLevelFatal);
                    resetLevelSelection();
                    RefreshView();
                }
            }
        }
        private bool _showLevelFatal;
        public static string PROP_ShowLevelFatal = "ShowLevelFatal";

        /// <summary>
        /// SelectAll Property
        /// </summary>
        public bool SelectAll
        {
            get { return _selectAll; }
            set
            {
                if (value != _selectAll)
                {
                    _selectAll = value;
                    RaisePropertyChanged(PROP_SelectAll);

                    if (_selectAll)
                    {
                        _showLevelDebug = _showLevelInfo = _showLevelWarn = _showLevelError = _showLevelFatal = true;
                        refreshCheckBoxBinding();
                        RefreshView();
                    }
                }
            }
        }
        private bool _selectAll;
        public static string PROP_SelectAll = "SelectAll";

        /// <summary>
        /// SelectDebug Property
        /// </summary>
        public bool SelectDebug
        {
            get { return _selectDebug; }
            set
            {
                if (value != _selectDebug)
                {
                    _selectDebug = value;
                    RaisePropertyChanged(PROP_SelectDebug);

                    if (_selectDebug)
                    {
                        _showLevelInfo = _showLevelWarn = _showLevelError = _showLevelFatal = false;
                        _showLevelDebug = true;
                        refreshCheckBoxBinding();
                        RefreshView();
                    }
                }
            }
        }
        private bool _selectDebug;
        public static string PROP_SelectDebug = "SelectDebug";

        /// <summary>
        /// SelectInfo Property
        /// </summary>
        public bool SelectInfo
        {
            get { return _selectInfo; }
            set
            {
                if (value != _selectInfo)
                {
                    _selectInfo = value;
                    RaisePropertyChanged(PROP_SelectInfo);

                    if (_selectInfo)
                    {
                        _showLevelDebug = _showLevelWarn = _showLevelError = _showLevelFatal = false;
                        _showLevelInfo = true;
                        refreshCheckBoxBinding();
                        RefreshView();
                    }
                }
            }
        }
        private bool _selectInfo;
        public static string PROP_SelectInfo = "SelectInfo";

        /// <summary>
        /// SelectWarn Property
        /// </summary>
        public bool SelectWarn
        {
            get { return _selectWarn; }
            set
            {
                if (value != _selectWarn)
                {
                    _selectWarn = value;
                    RaisePropertyChanged(PROP_SelectWarn);

                    if (_selectWarn)
                    {
                        _showLevelDebug = _showLevelInfo = _showLevelError = _showLevelFatal = false;
                        _showLevelWarn = true;
                        refreshCheckBoxBinding();
                        RefreshView();
                    }
                }
            }
        }
        private bool _selectWarn;
        public static string PROP_SelectWarn = "SelectWarn";

        /// <summary>
        /// SelectError Property
        /// </summary>
        public bool SelectError
        {
            get { return _selectError; }
            set
            {
                if (value != _selectError)
                {
                    _selectError = value;
                    RaisePropertyChanged(PROP_SelectError);

                    if (_selectError)
                    {
                        _showLevelDebug = _showLevelInfo = _showLevelWarn = _showLevelFatal = false;
                        _showLevelError = true;
                        refreshCheckBoxBinding();
                        RefreshView();
                    }
                }
            }
        }
        private bool _selectError;
        public static string PROP_SelectError = "SelectError";

        /// <summary>
        /// SelectFatal Property
        /// </summary>
        public bool SelectFatal
        {
            get { return _selectFatal; }
            set
            {
                if (value != _selectFatal)
                {
                    _selectFatal = value;
                    RaisePropertyChanged(PROP_SelectFatal);

                    if (_selectFatal)
                    {
                        _showLevelDebug = _showLevelInfo = _showLevelWarn = _showLevelError = false;
                        _showLevelFatal = true;
                        refreshCheckBoxBinding();
                        RefreshView();
                    }
                }
            }
        }
        private bool _selectFatal;
        public static string PROP_SelectFatal = "SelectFatal";

        /// <summary>
        /// GoToLogItemId Property
        /// </summary>
        public string GoToLogItemId
        {
            get { return _goToLogItemId; }
            set
            {
                _goToLogItemId = value;

                int idGoTo = 0;
                int.TryParse(value, out idGoTo);
                int currentId = SelectedLogItem != null ? SelectedLogItem.Id : 0;

                if (idGoTo > 0 && idGoTo != currentId)
                {
                    var selectItem = (from it in Items
                                      where it.Id == idGoTo
                                      select it).FirstOrDefault<LogItem>();

                    if (selectItem != null)
                        SelectedLogItem = selectItem;
                }
                else
                    _goToLogItemId = currentId != 0 ? currentId.ToString() : string.Empty;

                RaisePropertyChanged(PROP_GoToLogItemId);
            }
        }
        private string _goToLogItemId;
        public static string PROP_GoToLogItemId = "GoToLogItemId";

        #endregion

        #region Public Methods

        public void LoadFileList(string[] pathList, bool add = false)
        {
            SelectedFile = null;

            _loadingFileList = true;

            if (!add)
                FileList.Clear();

            foreach (string path in pathList)
            {
                string fileName = Path.GetFileName(path);
                FileItem newItem = new FileItem(fileName, path);
                newItem.PropertyChanged += delegate(object sender, PropertyChangedEventArgs e)
                {
                    if (e.PropertyName.Equals(FileItem.PROP_Checked))
                    {
                        if (newItem.Checked)
                            loadLogFileAsync(newItem.Path);
                        else
                            removeItemsAsync(newItem.Path);

                        refreshCommandsCanExecute();
                    }
                };
                FileList.Add(newItem);
            }

            if (_isFileSelectionEnabled)
            {
                var hasAllFilesItem = FileList.Count >= 1 && FileList[0] is AllFilesItem;
                if (!hasAllFilesItem)
                {
                    FileList.Insert(0, new AllFilesItem(FileList.ToArray()));
                }
            }

            _loadingFileList = false;

            //Load item if only one
            if (IsFileSelectionEnabled)
            {
                if (FileList.Count == 2)
                {
                    SelectedFile = FileList[1];
                    SelectedFile.Checked = true;
                }
            }
            else if (FileList.Count == 1)
            {
                SelectedFile = FileList[0];
            }
        }

        public void OnApplicationShutdown()
        {
            try
            {
                var collection = GridManager.GetColumnSettings().ToArray();
                DataService.SaveSettings(collection, Constants.SETTINGS_FILE_PATH);
            }
            catch (Exception)
            {
                // ignore
            }
        }

        #endregion

        #region Privates

        private IWinSimple _callingWin;
        private readonly TaskScheduler _taskScheduler;
        private readonly object _allItemsLock = new object();
        private readonly object _updateActionCountLock = new object();
        private readonly object _cancellationLock = new object();
        private int _updateActionCount;
        private CancellationTokenSource _cancellationTokenSource;

        private bool _loadingFileList = false;

        private void refreshCheckBoxBinding()
        {
            RaisePropertyChanged(PROP_ShowLevelDebug);
            RaisePropertyChanged(PROP_ShowLevelInfo);
            RaisePropertyChanged(PROP_ShowLevelWarn);
            RaisePropertyChanged(PROP_ShowLevelError);
            RaisePropertyChanged(PROP_ShowLevelFatal);
        }

        private void resetLevelSelection()
        {
            SelectAll = false;
            SelectDebug = false;
            SelectInfo = false;
            SelectWarn = false;
            SelectError = false;
            SelectFatal = false;
        }

        private void loadFolderList()
        {
            FileList.Clear();
            SelectedFolder = null;
            string path = Constants.FOLDERS_FILE_PATH;
            IList<PathItem> folders = null;
            try
            {
                folders = DataService.ParseFolderFile(path);
            }
            catch (Exception ex)
            {
                string message = String.Format((string)Resources.GlobalHelper_ParseFolderFile_Error_Text, path, ex.Message);
                MessageBox.Show(message, Resources.GlobalHelper_ParseFolderFile_Error_Title, MessageBoxButton.OK, MessageBoxImage.Exclamation);

            }
            FolderList = folders != null ? new ObservableCollection<PathItem>(folders) : new ObservableCollection<PathItem>();
        }

        private void loadFolderFiles(string folderPath)
        {
            if (Directory.Exists(folderPath))
            {
                string[] files = Directory.GetFiles(folderPath);
                LoadFileList(files);
            }
            else
            {
                FileList.Clear();
                MessageBox.Show(String.Format(Resources.MainWindowVM_loadFolderFiles_ErrorMessage_Text, folderPath), Resources.MainWindowVM_loadFolderFiles_ErrorMessageText_Title, MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }

        private void loadLogFileAsync(string path)
        {
            if (!IsFileSelectionEnabled)
            {
                ReplaceCancellationTokenSource();
            }

            CancellationToken currentCancellationToken;
            lock (_cancellationLock)
            {
                currentCancellationToken = _cancellationTokenSource.Token;
            }
            UpdateItemsAsync(() => loadLogFile(path, currentCancellationToken), currentCancellationToken, path);
        }

        private void loadLogFile(string path, CancellationToken cancellationToken)
        {
            Debug.Print("Started loading items from file {0}", path);
            var items = DataService.ParseLogFile(path);
            var itemList = new List<LogItem>();
            long count = 0;
            foreach (var item in items)
            {
                if (count++ % 1000 == 0 && cancellationToken.IsCancellationRequested)
                {
                    Debug.Print("Canceled loading items from file {0}", path);
                    cancellationToken.ThrowIfCancellationRequested();
                }
                itemList.Add(item);
            }
            lock (_allItemsLock)
            {
                if (!IsFileSelectionEnabled)
                {
                    _allItems = itemList;
                }
                else
                {
                    _allItems = _allItems.Union(itemList);
                }
            }
            Debug.Print("Finished loading items from file {0}", path);
        }

        private void removeItemsAsync(string path)
        {
            UpdateItemsAsync(() => removeItems(path), CancellationToken.None);
        }

        private void removeItems(string path)
        {
            lock (_allItemsLock)
            {
                _allItems = _allItems.Where(i => !i.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
            }
        }

        private IEnumerable<LogItem> _allItems;

        private bool deleteFile(string path)
        {
            try
            {
                FileInfo fileInfo = new FileInfo(path);
                if (fileInfo != null)
                    fileInfo.Delete();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(String.Format(Resources.MainWindowVM_deleteFile_ErrorMessage_Text, path, ex.Message), Resources.MainWindowVM_deleteFile_ErrorMessage_Title, MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void refreshCommandsCanExecute()
        {
            CommandRefreshFiles.OnCanExecuteChanged();
            CommandDelete.OnCanExecuteChanged();
            CommandOpenSelectedFolder.OnCanExecuteChanged();
        }

        private void updateJumpList()
        {
            JumpList myJumpList = JumpList.GetJumpList(Application.Current);

            if (myJumpList == null)
            {
                myJumpList = new JumpList();
                JumpList.SetJumpList(Application.Current, myJumpList);
            }

            myJumpList.JumpItems.Clear();
            if (RecentFileList != null && RecentFileList.RecentFiles != null)
            {
                foreach (string item in RecentFileList.RecentFiles)
                {
                    try
                    {
                        JumpTask myJumpTask = new JumpTask();
                        myJumpTask.CustomCategory = Resources.MainWindowVM_updateJumpList_CustomCategoryName;
                        myJumpTask.Title = Path.GetFileName(item);
                        //myJumpTask.Description = "";
                        myJumpTask.ApplicationPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, System.AppDomain.CurrentDomain.FriendlyName);
                        myJumpTask.Arguments = item;
                        myJumpList.JumpItems.Add(myJumpTask);
                    }
                    catch (Exception)
                    {
                        //throw;
                    }
                }
            }
            myJumpList.Apply();
        }

        private void UpdateItemsAsync(Action allItemsUpdateAction, CancellationToken cancellationToken, string path = null)
        {
            // set IsLoading = true on UI thread
            IsLoading = true;

            // update items async on the background thread
            var updateItemsTask = new Task(() =>
            {
                lock (_updateActionCountLock)
                {
                    _updateActionCount++;
                    Debug.Print("Pending update actions before update: {0}", _updateActionCount);
                }
                allItemsUpdateAction();
            }, cancellationToken);

            // when the update items task does not complete succesffuly, show an error on the UI thread
            var uiTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            var updateItemsFailedTask = updateItemsTask.ContinueWith(t =>
            {
                lock (_updateActionCountLock)
                {
                    _updateActionCount--;
                    Debug.Print("Pending update actions after update: {0}", _updateActionCount);
                }
                if (!t.IsCanceled && !string.IsNullOrWhiteSpace(path))
                {
                    var message = String.Format(Resources.GlobalHelper_ParseLogFile_Error_Text, path, t.Exception.InnerException.Message);
                    MessageBox.Show(message, Resources.GlobalHelper_ParseLogFile_Error_Title, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }, CancellationToken.None, TaskContinuationOptions.NotOnRanToCompletion, uiTaskScheduler);

            // when the update items task completes successfully, start a post-process task which only runs if this is the last update action
            var postProcessTask = updateItemsTask.ContinueWith(t =>
            {
                lock (_updateActionCountLock)
                {
                    _updateActionCount--;
                    Debug.Print("Pending update actions after update: {0}", _updateActionCount);
                    if (_updateActionCount > 0)
                    {
                        Debug.Print("Delaying post-processing because {0} update actions are pending", _updateActionCount);
                        return false;
                    }
                }

                lock (_allItemsLock)
                {
                    Debug.Print("Post-processing all items");
                    PostProcessItems();
                    Debug.Print("Done post-processing all items");
                    ReplaceCancellationTokenSource();
                    return true;
                }
            }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, _taskScheduler);

            // when the post process task completes, handle this on UI thread
            var updateItemsSucceededTask = postProcessTask.ContinueWith(t =>
            {
                var canUpdateUi = t.Result;
                if (!canUpdateUi)
                {
                    return;
                }

                Debug.Print("Updating UI");
                Items.Clear();
                Items = new ObservableCollection<LogItem>(_allItems);
                updateCounters();

                if (!string.IsNullOrWhiteSpace(path))
                {
                    RecentFileList.InsertFile(path);
                }
                updateJumpList();
                Debug.Print("Done updating UI");
            }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, uiTaskScheduler);

            // when UI is done updating set IsLoading=false on UI thread
            Task.Factory.ContinueWhenAll(new[] { updateItemsSucceededTask, updateItemsFailedTask }, t =>
            {
                lock (_updateActionCountLock)
                {
                    IsLoading = _updateActionCount > 0;
                }
            }, CancellationToken.None, TaskContinuationOptions.None, uiTaskScheduler);

            // finnaly, start this task chain
            updateItemsTask.Start(_taskScheduler);
        }

        private void PostProcessItems()
        {
            var list = _allItems.ToList();
            list.Sort((x, y) => x.TimeStamp.CompareTo(y.TimeStamp));

            _itemsDebugCount = _itemsInfoCount = _itemsWarnCount = _itemsErrorCount = _itemsFatalCount = 0;
            var id = 1;
            foreach (var item in list)
            {
                item.Id = id++;
                var levelIndex = item.LevelIndex;
                switch (levelIndex)
                {
                    case LevelIndex.DEBUG:
                        _itemsDebugCount++;
                        break;
                    case LevelIndex.INFO:
                        _itemsInfoCount++;
                        break;
                    case LevelIndex.WARN:
                        _itemsWarnCount++;
                        break;
                    case LevelIndex.ERROR:
                        _itemsErrorCount++;
                        break;
                    case LevelIndex.FATAL:
                        _itemsFatalCount++;
                        break;
                }
            }
            _allItems = list;
        }

        private void ReplaceCancellationTokenSource()
        {
            lock (_cancellationLock)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();
            }
        }

        #endregion

        #region FilteredGridManager

        /// <summary>
        /// GridManager Property
        /// </summary>
        public FilteredGridManager GridManager { get; set; }

        public void InitDataGrid()
        {
            if (GridManager != null)
            {
                var dgColumns = new List<ColumnItem>()
                {
                    new ColumnItem("Id", 37, null, CellAlignment.CENTER,string.Empty){Header = Resources.MainWindowVM_InitDataGrid_IdColumn_Header},
                    new ColumnItem("TimeStamp", 120, null, CellAlignment.CENTER, GlobalHelper.DisplayDateTimeFormat){Header = Resources.MainWindowVM_InitDataGrid_TimeStampColumn_Header},
                    new ColumnItem("Level", null, 50, CellAlignment.CENTER){Header = Resources.MainWindowVM_InitDataGrid_LevelColumn_Header},
                    new ColumnItem("Message", null, 300){Header = Resources.MainWindowVM_InitDataGrid_MessageColumn_Header},
                    new ColumnItem("Logger", 150, null){Header = Resources.MainWindowVM_InitDataGrid_LoggerColumn_Header},
                    new ColumnItem("MachineName", 110, null, CellAlignment.CENTER){Header = Resources.MainWindowVM_InitDataGrid_MachineNameColumn_Header},
                    new ColumnItem("HostName", 110, null, CellAlignment.CENTER){Header = Resources.MainWindowVM_InitDataGrid_HostNameColumn_Header},
                    new ColumnItem("UserName", 110, null, CellAlignment.CENTER){Header = Resources.MainWindowVM_InitDataGrid_UserNameColumn_Header},
                    new ColumnItem("App", 150, null){Header = Resources.MainWindowVM_InitDataGrid_AppColumn_Header},
                    new ColumnItem("Thread", 44, null, CellAlignment.CENTER){Header = Resources.MainWindowVM_InitDataGrid_ThreadColumn_Header},
                    new ColumnItem("Class", null, 300){Header = Resources.MainWindowVM_InitDataGrid_ClassColumn_Header},
                    new ColumnItem("Method", 200, null){Header = Resources.MainWindowVM_InitDataGrid_MethodColumn_Header}
                    //new ColumnItem("Delta", 60, null, CellAlignment.CENTER, null, "Δ"),
                    //new ColumnItem("Path", 50)
                };

                // override default settings if neeeded
                var columnSettingsArray = DataService.ParseSettings(Constants.SETTINGS_FILE_PATH);
                if (columnSettingsArray.Length > 0)
                {
                    var map = (from columnItem in dgColumns
                               join columnSettings in columnSettingsArray
                                   on columnItem.Field equals columnSettings.Id
                               select new { columnItem, columnSettings })
                              .ToDictionary(x => x.columnItem, x => x.columnSettings);

                    foreach (var kvp in map)
                    {
                        var column = kvp.Key;
                        var settings = kvp.Value;
                        column.Width = settings.Width;
                        column.Visible = settings.Visible;
                    }

                    dgColumns.Sort(new ColumnComparer(map));
                }

                GridManager.BuildDataGrid(dgColumns);
                GridManager.AssignSource(new Binding(MainWindowVM.PROP_Items) { Source = this, Mode = BindingMode.OneWay });
                GridManager.OnBeforeCheckFilter = levelCheckFilter;
            }
        }

        public void RefreshView()
        {
            if (GridManager != null)
            {
                ICollectionView view = GridManager.GetCollectionView();
                if (view != null)
                    view.Refresh();
                updateFilteredCounters(view);
            }
        }

        private bool levelCheckFilter(object item)
        {
            LogItem logItem = item as LogItem;
            if (logItem != null)
            {
                switch (logItem.LevelIndex)
                {
                    case LevelIndex.DEBUG:
                        return ShowLevelDebug;
                    case LevelIndex.INFO:
                        return ShowLevelInfo;
                    case LevelIndex.WARN:
                        return ShowLevelWarn;
                    case LevelIndex.ERROR:
                        return ShowLevelError;
                    case LevelIndex.FATAL:
                        return ShowLevelFatal;
                }
            }
            return true;
        }

        #endregion

        #region Counters

        /// <summary>
        /// ItemsDebugCount Property
        /// </summary>
        public int ItemsDebugCount
        {
            get { return _itemsDebugCount; }
            set
            {
                _itemsDebugCount = value;
                RaisePropertyChanged(PROP_ItemsDebugCount);
            }
        }
        private int _itemsDebugCount;
        public static string PROP_ItemsDebugCount = "ItemsDebugCount";

        /// <summary>
        /// ItemsInfoCount Property
        /// </summary>
        public int ItemsInfoCount
        {
            get { return _itemsInfoCount; }
            set
            {
                _itemsInfoCount = value;
                RaisePropertyChanged(PROP_ItemsInfoCount);
            }
        }
        private int _itemsInfoCount;
        public static string PROP_ItemsInfoCount = "ItemsInfoCount";

        /// <summary>
        /// ItemsWarnCount Property
        /// </summary>
        public int ItemsWarnCount
        {
            get { return _itemsWarnCount; }
            set
            {
                _itemsWarnCount = value;
                RaisePropertyChanged(PROP_ItemsWarnCount);
            }
        }
        private int _itemsWarnCount;
        public static string PROP_ItemsWarnCount = "ItemsWarnCount";

        /// <summary>
        /// ItemsErrorCount Property
        /// </summary>
        public int ItemsErrorCount
        {
            get { return _itemsErrorCount; }
            set
            {
                _itemsErrorCount = value;
                RaisePropertyChanged(PROP_ItemsErrorCount);
            }
        }
        private int _itemsErrorCount;
        public static string PROP_ItemsErrorCount = "ItemsErrorCount";

        /// <summary>
        /// ItemsFatalCount Property
        /// </summary>
        public int ItemsFatalCount
        {
            get { return _itemsFatalCount; }
            set
            {
                _itemsFatalCount = value;
                RaisePropertyChanged(PROP_ItemsFatalCount);
            }
        }
        private int _itemsFatalCount;
        public static string PROP_ItemsFatalCount = "ItemsFatalCount";

        /// <summary>
        /// ItemsDebugFilterCount Property
        /// </summary>
        public int ItemsDebugFilterCount
        {
            get { return _itemsDebugFilterCount; }
            set
            {
                _itemsDebugFilterCount = value;
                RaisePropertyChanged(PROP_ItemsDebugFilterCount);
            }
        }
        private int _itemsDebugFilterCount;
        public static string PROP_ItemsDebugFilterCount = "ItemsDebugFilterCount";

        /// <summary>
        /// ItemsInfoFilterCount Property
        /// </summary>
        public int ItemsInfoFilterCount
        {
            get { return _itemsInfoFilterCount; }
            set
            {
                _itemsInfoFilterCount = value;
                RaisePropertyChanged(PROP_ItemsInfoFilterCount);
            }
        }
        private int _itemsInfoFilterCount;
        public static string PROP_ItemsInfoFilterCount = "ItemsInfoFilterCount";

        /// <summary>
        /// ItemsWarnFilterCount Property
        /// </summary>
        public int ItemsWarnFilterCount
        {
            get { return _itemsWarnFilterCount; }
            set
            {
                _itemsWarnFilterCount = value;
                RaisePropertyChanged(PROP_ItemsWarnFilterCount);
            }
        }
        private int _itemsWarnFilterCount;
        public static string PROP_ItemsWarnFilterCount = "ItemsWarnFilterCount";

        /// <summary>
        /// ItemsErrorFilterCount Property
        /// </summary>
        public int ItemsErrorFilterCount
        {
            get { return _itemsErrorFilterCount; }
            set
            {
                _itemsErrorFilterCount = value;
                RaisePropertyChanged(PROP_ItemsErrorFilterCount);
            }
        }
        private int _itemsErrorFilterCount;
        public static string PROP_ItemsErrorFilterCount = "ItemsErrorFilterCount";

        /// <summary>
        /// ItemsFatalFilterCount Property
        /// </summary>
        public int ItemsFatalFilterCount
        {
            get { return _itemsFatalFilterCount; }
            set
            {
                _itemsFatalFilterCount = value;
                RaisePropertyChanged(PROP_ItemsFatalFilterCount);
            }
        }
        private int _itemsFatalFilterCount;
        public static string PROP_ItemsFatalFilterCount = "ItemsFatalFilterCount";

        /// <summary>
        /// ItemsFilterCount Property
        /// </summary>
        public int ItemsFilterCount
        {
            get { return _itemsFilterCount; }
            set
            {
                _itemsFilterCount = value;
                RaisePropertyChanged(PROP_ItemsFilterCount);
            }
        }
        private int _itemsFilterCount;
        public static string PROP_ItemsFilterCount = "ItemsFilterCount";

        private void updateCounters()
        {
            ItemsDebugCount = _itemsDebugCount;
            ItemsInfoCount = _itemsInfoCount;
            ItemsWarnCount = _itemsWarnCount;
            ItemsErrorCount = _itemsErrorCount;
            ItemsFatalCount = _itemsFatalCount;
            RefreshView();
        }

        private void updateFilteredCounters(ICollectionView filteredList)
        {
            _itemsFilterCount = _itemsDebugFilterCount = _itemsInfoFilterCount = _itemsWarnFilterCount = _itemsErrorFilterCount = _itemsFatalFilterCount = 0;
            if (filteredList != null)
            {
                var fltList = filteredList.Cast<LogItem>();
                foreach (var item in fltList)
                {
                    _itemsFilterCount++;
                    switch (item.LevelIndex)
                    {
                        case LevelIndex.DEBUG:
                            _itemsDebugFilterCount++;
                            break;
                        case LevelIndex.INFO:
                            _itemsInfoFilterCount++;
                            break;
                        case LevelIndex.WARN:
                            _itemsWarnFilterCount++;
                            break;
                        case LevelIndex.ERROR:
                            _itemsErrorFilterCount++;
                            break;
                        case LevelIndex.FATAL:
                            _itemsFatalFilterCount++;
                            break;
                    }
                }
            }
            ItemsFilterCount = _itemsFilterCount;
            ItemsDebugFilterCount = _itemsDebugFilterCount;
            ItemsInfoFilterCount = _itemsInfoFilterCount;
            ItemsWarnFilterCount = _itemsWarnFilterCount;
            ItemsErrorFilterCount = _itemsErrorFilterCount;
            ItemsFatalFilterCount = _itemsFatalFilterCount;
        }

        #endregion

        #region Auto Refresh

        /// <summary>
        /// IsAutoRefreshEnabled Property
        /// </summary>
        public bool IsAutoRefreshEnabled
        {
            get { return _isAutoRefreshEnabled; }
            set
            {
                _isAutoRefreshEnabled = value;
                RaisePropertyChanged(PROP_IsAutoRefreshEnabled);

                if (_dispatcherTimer != null)
                {
                    if (_isAutoRefreshEnabled)
                        _dispatcherTimer.Start();
                    else
                        _dispatcherTimer.Stop();
                }
            }
        }
        private bool _isAutoRefreshEnabled;
        public static string PROP_IsAutoRefreshEnabled = "IsAutoRefreshEnabled";

        /// <summary>
        /// AutoRefreshInterval Property
        /// </summary>
        public int AutoRefreshInterval
        {
            get { return _autoRefreshInterval; }
            set
            {
                _autoRefreshInterval = value;
                RaisePropertyChanged(PROP_AutoRefreshInterval);
                RaisePropertyChanged(PROP_AutoRefreshIntervalLocalized);
                if (_dispatcherTimer != null)
                    _dispatcherTimer.Interval = new TimeSpan(0, 0, _autoRefreshInterval);
            }
        }
        private int _autoRefreshInterval;
        public static string PROP_AutoRefreshInterval = "AutoRefreshInterval";

        public string AutoRefreshIntervalLocalized
        {
            get
            {
                return String.Format(Resources.MainWindowVM_AutoRefreshIntervalLocalized_Format,
                                     AutoRefreshInterval.ToString(System.Globalization.CultureInfo.GetCultureInfo(Properties.Resources.CultureName)));
            }

        }
        public static string PROP_AutoRefreshIntervalLocalized = "AutoRefreshIntervalLocalized";


        /// <summary>
        /// IncreaseInterval Command
        /// </summary>
        public ICommandAncestor CommandIncreaseInterval { get; protected set; }

        /// <summary>
        /// DecreaseInterval Command
        /// </summary>
        public ICommandAncestor CommandDecreaseInterval { get; protected set; }

        protected virtual object commandIncreaseIntervalExecute(object parameter)
        {
            AutoRefreshInterval += Constants.DEFAULT_REFRESH_INTERVAL;
            return null;
        }

        protected virtual object commandDecreaseIntervalExecute(object parameter)
        {
            if (AutoRefreshInterval > Constants.DEFAULT_REFRESH_INTERVAL)
                AutoRefreshInterval -= Constants.DEFAULT_REFRESH_INTERVAL;
            return null;
        }

        private DispatcherTimer _dispatcherTimer;

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            DateTime? currentLog = null;

            if (SelectedLogItem != null)
                currentLog = SelectedLogItem.TimeStamp;

            CommandRefresh.Execute(null);

            if (currentLog.HasValue)
            {
                while (IsLoading)
                    GlobalHelper.DoEvents();

                var currentItem = (from it in Items
                                   where DateTime.Compare(it.TimeStamp, currentLog.Value) == 0
                                   select it).FirstOrDefault<LogItem>();

                if (currentItem != null)
                    SelectedLogItem = currentItem;
            }
        }

        #endregion

        public class ColumnComparer : IComparer<ColumnItem>
        {
            private readonly IDictionary<ColumnItem, ColumnSettings> _map;

            public ColumnComparer(IDictionary<ColumnItem, ColumnSettings> map)
            {
                _map = map;
            }

            public int Compare(ColumnItem x, ColumnItem y)
            {
                if (x.Field == y.Field)
                {
                    return 0;
                }
                if (x.Visible && !y.Visible)
                {
                    return -1;
                }
                if (!x.Visible && y.Visible)
                {
                    return 1;
                }
                return _map[x].DisplayIndex.CompareTo(_map[y].DisplayIndex);
            }
        }
    }
}