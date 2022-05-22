using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Win32;

namespace ProcessLater {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        public RoutedCommand ExpandOrFoldOutputTextCommand { get; } = new();
        public RoutedCommand DeleteFilesCommand { get; } = new();
        public RoutedCommand CopyFilesCommand { get; } = new();
        public RoutedCommand SaveListAsFileCommand { get; } = new();
        public RoutedCommand LoadListFromFileCommand { get; } = new();

        public ObservableCollection<FileEntity> FileEntities { get; } = new();

        public MainWindow() {
            RegisterCommand(DeleteFilesCommand, (s, e) => {
                Process(
                    fileProcessor: f => File.Delete(f),
                    directoryProcessor: f => Directory.Delete(f, true)
                );
            });
            RegisterCommand(CopyFilesCommand, (s, e) => {
                if (SelectFolder(out string path)) {
                    Process(
                        fileProcessor: f => File.Copy(f, $"{path}\\{Path.GetFileName(f)}"),
                        directoryProcessor: f => CopyDirectory(f, $"{path}\\{Path.GetFileName(f)}", true)
                    );
                }
            });
            RegisterCommand(SaveListAsFileCommand, (s, e) => {
                if (SelectFile(out string saveFile, true)) {
                    var sb = new StringBuilder();
                    foreach (var entity in FileEntities) {
                        sb.AppendLine(entity.Path);
                    }
                    using (var writer = new StreamWriter(saveFile, false, Encoding.UTF8)) {
                        writer.Write(sb.ToString());
                    }
                }
            });
            RegisterCommand(LoadListFromFileCommand, (s, e) => {
                FileEntities.Clear();
                if (SelectFile(out string listFile)) {
                    using (var reader = new StreamReader(listFile, Encoding.UTF8)) {
                        while (!reader.EndOfStream) {
                            var line = reader.ReadLine();
                            if (string.IsNullOrWhiteSpace(line)) {
                                continue;
                            }
                            AddEntity(line);
                        }
                    }
                }
            });
            RegisterCommand(
                ExpandOrFoldOutputTextCommand,
                (s, e) => {
                    if (IsOuputAreaExpanded()) {
                        OutputTextArea.Height = new GridLength(OutputTextArea.MinHeight);
                    }
                    else {
                        OutputTextArea.Height = new GridLength(MainContentArea.ActualHeight / 2);
                    }
                },
                (s, e) => {
                    ExpandButton.Content = IsOuputAreaExpanded() ? '\u25BC' : '\u25B2';
                    e.CanExecute = true;
                });

            FileEntities.CollectionChanged += (s, e) => {
                if (s is ObservableCollection<FileEntity> collcetion) {
                    if (collcetion.Count > 0) {
                        TipArea.Visibility = Visibility.Collapsed;
                    }
                    else {
                        TipArea.Visibility = Visibility.Visible;
                    }
                }
            };
            InitializeComponent();
        }

        private void ListBox_Drop(object sender, DragEventArgs e) {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[] ?? Array.Empty<string>();
            foreach (var item in files) {
                AddEntity(item);
            }
        }
        private void TextBlock_MouseRightButtonDown(object sender, MouseButtonEventArgs e) {
            if ((sender as FrameworkElement)?.Tag is FileEntity entity) {
                FileEntities.Remove(entity);
            }
        }
        private void Window_KeyDown(object sender, KeyEventArgs e) {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) && e.Key == Key.S && e.IsDown) {
                SaveListAsFileCommand.Execute(null, null);
            }
        }

        private bool _isBusying;
        private bool IsOuputAreaExpanded() => OutputTextArea.ActualHeight > OutputTextArea.MinHeight;
        private static void CopyDirectory(string sourceDir, string destinationDir, bool recursive) {
            // Get information about the source directory
            var dir = new DirectoryInfo(sourceDir);

            // Check if the source directory exists
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            // Cache directories before we start copying
            DirectoryInfo[] dirs = dir.GetDirectories();

            // Create the destination directory
            Directory.CreateDirectory(destinationDir);

            // Get the files in the source directory and copy to the destination directory
            foreach (FileInfo file in dir.GetFiles()) {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }

            // If recursive and copying subdirectories, recursively call this method
            if (recursive) {
                foreach (DirectoryInfo subDir in dirs) {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true);
                }
            }
        }
        private static bool SelectFolder(out string path) {
            var fbd = new System.Windows.Forms.FolderBrowserDialog();
            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                path = fbd.SelectedPath;
                return true;
            }
            path = "";
            return false;
        }
        private static bool SelectFile(out string file, bool useSaveMode = false) {
            FileDialog fd;
            if (useSaveMode) {
                fd = new SaveFileDialog();
            }
            else {
                fd = new OpenFileDialog();
            }
            fd.Filter = "文本文件|*.txt";
            fd.FileName = $"待处理文件项_{DateTime.Now:yyyyMMdd_HHmmss}";
            if (fd.ShowDialog() == true) {
                file = fd.FileName;
                return true;
            }
            file = "";
            return false;
        }
        private void RegisterCommand(RoutedCommand command, ExecutedRoutedEventHandler executed, CanExecuteRoutedEventHandler canExecute, EventHandler? canExecuteChanged) {
            var cb = new CommandBinding();
            cb.Command = command;
            cb.Executed += executed;
            cb.CanExecute += canExecute;
            if (canExecuteChanged is not null) {
                command.CanExecuteChanged += canExecuteChanged;
            }
            CommandBindings.Add(cb);
        }
        private void RegisterCommand(RoutedCommand command, ExecutedRoutedEventHandler executed, CanExecuteRoutedEventHandler canExecute) {
            RegisterCommand(command, executed, canExecute, null);
        }
        private void RegisterCommand(RoutedCommand command, ExecutedRoutedEventHandler executed) {
            RegisterCommand(command, executed, ProcessCommand_CanExecute);
        }
        private void ProcessCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = !_isBusying;
        }
        private void AddEntity(string item) {
            EntityType entityType = EntityType.Ambiguous;
            if (File.Exists(item)) {
                if (entityType == EntityType.Ambiguous) {
                    entityType = EntityType.File;
                }
            }
            if (Directory.Exists(item)) {
                if (entityType == EntityType.Ambiguous) {
                    entityType = EntityType.Directory;
                }
            }
            if (entityType == EntityType.Ambiguous) {
                MessageBox.Show("Ambiguous entity, file or directory?");
            }
            var entity = new FileEntity() {
                Path = item,
                Type = entityType
            };
            if (!FileEntities.Contains(entity)) {
                FileEntities.Add(entity);
            }
        }
        private async void Process(Action<string> fileProcessor, Action<string> directoryProcessor) {
            if (_isBusying) {
                return;
            }
            _isBusying = true;
            var pResults = Array.Empty<ProcessResult>();
            try {
                pResults = await ProcessFiles(fileProcessor, directoryProcessor);
            }
            catch (Exception exp) {
                MessageBox.Show(exp.Message);
            }
            finally {
                _isBusying = false;
            }
            // show error message
            var sb = new StringBuilder();
            foreach (var p in pResults) {
                var errors = new List<string>();
                if (p.State == State.None) {
                    errors.Add($"No such file or directory");
                }
                else if (p.State == State.ErrorOccured) {
                    errors.Add($"{p.ErrorMessage}");
                }
                if (errors.Count > 0) {
                    sb.AppendLine(p.Entity.ToString());
                    foreach (var error in errors) {
                        sb.AppendLine($"    {error}");
                    }
                    sb.AppendLine();
                }
            }
            Output.Text = sb.ToString();
            if (!string.IsNullOrWhiteSpace(Output.Text) && !IsOuputAreaExpanded()) {
                ExpandOrFoldOutputTextCommand.Execute(null, null);
            }
        }
        private async Task<ProcessResult[]> ProcessFiles(Action<string> fileProcessor, Action<string> directoryProcessor) {
            var pResults = new List<ProcessResult>();
            for (int i = 0; i < FileEntities.Count; i++) {
                var pResult = new ProcessResult() {
                    Entity = FileEntities[i],
                };
                await Task.Run(() => {
                    try {
                        switch (FileEntities[i].Type) {
                            case EntityType.File:
                                if (File.Exists(FileEntities[i].Path)) {
                                    pResult.State = State.Processed;
                                    fileProcessor(FileEntities[i].Path);
                                }
                                break;
                            case EntityType.Directory:
                                if (Directory.Exists(FileEntities[i].Path)) {
                                    pResult.State = State.Processed;
                                    directoryProcessor(FileEntities[i].Path);
                                }
                                break;
                        }
                        Application.Current.Dispatcher.Invoke(() => {
                            FileEntities.Remove(FileEntities[i]);
                        });
                    }
                    catch (Exception e) {
                        pResult.State = State.ErrorOccured;
                        pResult.ErrorMessage = e.Message;
                    }
                });
                if (pResult.State == State.Processed) {
                    --i;
                }
                pResults.Add(pResult);
            }
            return pResults.ToArray();
        }
    }
}
