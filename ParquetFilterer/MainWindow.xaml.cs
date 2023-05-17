using CryLib;
using Parquet;
using Parquet.Data;
using Parquet.Schema;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ParquetFilterer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : CryLib.WPF.CryWindowDesignable, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private Dictionary<int, RuleSet> _rules = new Dictionary<int, RuleSet>()
        {
            // Width
            { 2, new RuleSet(2, Rule.HigherOrEqual, RuleException.DisallowNull, 1024) },
            // Height
            { 3, new RuleSet(3, Rule.HigherOrEqual, RuleException.DisallowNull, 1024) },
            // Punsafe
            { 8, new RuleSet(8, Rule.Lower, RuleException.DisallowNull, 0.7f) }
        };

        public ObservableCollection<FilterModel> Filters
        {
            get;
            set;
        } = new ObservableCollection<FilterModel>();

        public ObservableCollection<TempData> TempData
        {
            get;
            set;
        } = new ObservableCollection<TempData>();

        public string InputText
        {
            set;
            get;
        } = @"F:\Laion High Res\laion-high-resolution\";

        public MainWindow()
        {
            InitializeComponent();

            StartFilterButton.BackgroundFunc = new Func<int>(() =>
            {
                if (!File.Exists(InputText) && !Directory.Exists(InputText))
                {
                    MessageBox.Show("Input path does not exist");
                    return 0;
                }

                try
                {
                    _rules.Clear();

                    foreach (FilterModel model in Filters)
                    {
                        if (model.RuleSet is not null)
                            _rules.Add(model.ColumnIndex, model.RuleSet);
                    }

                    _Filter();
                }
                catch (Exception ex)
                {
                    CryMessagebox.Create(ex.ToString());
                }

                return 1;
            });
        }

        private async void _Filter()
        {
            try
            {
                bool isDir = false;
                string[] paths = Array.Empty<string>();

                // Determine if the path is a file or folder
                FileAttributes attr = File.GetAttributes(InputText);
                if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                    isDir = true;

                if(isDir)
                    paths = Directory.GetFiles(InputText, "*.parquet");
                else
                    paths = new string[] { InputText };

                foreach (string path in paths)
                {
                    string newPath = Path.GetDirectoryName(path) + "\\" + Path.GetFileNameWithoutExtension(path) + "_filtered" + Path.GetExtension(path);
                    // open file stream
                    using (Stream fileStream = System.IO.File.OpenRead(path))
                    using (Stream newFileStream = System.IO.File.Create(newPath))
                    {
                        // open parquet file reader
                        using (var parquetReader = await ParquetReader.CreateAsync(fileStream))
                        {
                            // get file schema (available straight after opening parquet reader)
                            // however, get only data fields as only they contain data values
                            DataField[] dataFields = parquetReader.Schema.GetDataFields();

                            using (var newParquetWriter = await ParquetWriter.CreateAsync(parquetReader.Schema, newFileStream))
                            {
                                newParquetWriter.CustomMetadata = parquetReader.CustomMetadata;
                                newParquetWriter.CompressionMethod = CompressionMethod.None;

                                // enumerate through row groups in this file
                                for (int groupIndex = 0; groupIndex < parquetReader.RowGroupCount; groupIndex++)
                                {
                                    // create row group reader
                                    using (ParquetRowGroupReader groupReader = parquetReader.OpenRowGroupReader(groupIndex))
                                    {
                                        // read all columns inside each row group (you have an option to read only
                                        // required columns if you need to.
                                        List<DataColumn> columns = new List<DataColumn>();

                                        for (int index = 0; index < dataFields.Length; index++)
                                            columns.Add(await groupReader.ReadColumnAsync(dataFields[index]));

                                        Array[] arrays = new Array[columns.Count];

                                        for (int i = 0; i < arrays.Length; i++)
                                        {
                                            if (dataFields[i].ClrType.IsValueType)
                                            {
                                                Type type = typeof(Nullable<>).MakeGenericType(dataFields[i].ClrType);
                                                arrays.SetValue(Array.CreateInstance(type, columns[i].Data.Length), i);
                                            }
                                            else
                                                arrays.SetValue(Array.CreateInstance(dataFields[i].ClrType, columns[i].Data.Length), i);
                                        }

                                        // This index is only count up if the desired column was added
                                        int referenceIndex = 0;

                                        // All columns should have the same length
                                        for (int index = 0; index < columns[0].Data.Length; index++)
                                        {
                                            bool added = false;
                                            for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
                                            {
                                                object? value = columns[columnIndex].Data.GetValue(index);

                                                if (_rules.TryGetValue(columnIndex, out RuleSet? columnRule) && !columnRule.IsAllowed(value))
                                                {
                                                    // Revoke the added and overwrite the values in the next run
                                                    added = false;
                                                    break;
                                                }

                                                arrays[columnIndex].SetValue(value, referenceIndex);
                                                added = true;
                                            }

                                            if (added)
                                                referenceIndex++;
                                        }

                                        Array[] trimmedArrays = new Array[arrays.Length];

                                        for (int i = 0; i < trimmedArrays.Length; i++)
                                        {
                                            if (dataFields[i].ClrType.IsValueType)
                                            {
                                                Type type = typeof(Nullable<>).MakeGenericType(dataFields[i].ClrType);
                                                trimmedArrays.SetValue(Array.CreateInstance(type, arrays[i].Length), i);
                                            }
                                            else
                                                trimmedArrays.SetValue(Array.CreateInstance(dataFields[i].ClrType, arrays[i].Length), i);
                                        }

                                        // All columns should have the same length
                                        for (int index = 0; index < referenceIndex; index++)
                                        {
                                            for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
                                            {
                                                object? value = arrays[columnIndex].GetValue(index);
                                                trimmedArrays[columnIndex].SetValue(value, index);
                                            }
                                        }

                                        using (ParquetRowGroupWriter writer = newParquetWriter.CreateRowGroup())
                                            for (int index = 0; index < trimmedArrays.Length; index++)
                                                await writer.WriteColumnAsync(new DataColumn(dataFields[index], trimmedArrays[index]));
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private async void StartFilterButton_StatusButtonClicked(object sender, EventArgs e)
        {
            IsEnabled = false;
            await StartFilterButton.RunTask();
            IsEnabled = true;
        }

        private async void CryButton_ButtonClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!File.Exists(InputText) && !Directory.Exists(InputText))
                {
                    MessageBox.Show("Input path does not exist");
                    return;
                }

                bool isDir = false;
                string[] paths = Array.Empty<string>();

                // Determine if the path is a file or folder
                FileAttributes attr = File.GetAttributes(InputText);
                if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                    isDir = true;

                if (isDir)
                    paths = Directory.GetFiles(InputText, "*.parquet");
                else
                    paths = new string[] { InputText };

                DataGrid.Columns.Clear();
                TempData.Clear();
                Filters.Clear();
                DataField[] dataFields;

                // Only take the first, this is only to get samples
                using (Stream fileStream = System.IO.File.OpenRead(paths[0]))
                using (var parquetReader = await ParquetReader.CreateAsync(fileStream))
                // Just take the first group
                using (ParquetRowGroupReader groupReader = parquetReader.OpenRowGroupReader(0))
                {
                    // Get the data fields of the columns
                    dataFields = parquetReader.Schema.GetDataFields();

                    // Read all columns inside each row group
                    List<DataColumn> columns = new List<DataColumn>();

                    for (int index = 0; index < dataFields.Length; index++)
                        columns.Add(await groupReader.ReadColumnAsync(dataFields[index]));

                    // All columns should have the same length
                    for (int index = 0; index < Math.Min(10, columns[0].Data.Length); index++)
                    {
                        object?[] data = new object?[dataFields.Length];
                        for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
                            data[columnIndex] = columns[columnIndex].Data.GetValue(index);

                        TempData.Add(new TempData(data, index));
                    }
                }

                DataGrid.Columns.Add(new DataGridCheckBoxColumn()
                {
                    Header = "Is Allowed",
                    Binding = new Binding("IsAllowed")
                    {
                        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                        NotifyOnSourceUpdated = true
                    }
                });

                for (int index = 0; index < dataFields.Length; index++)
                {
                    DataGrid.Columns.Add(new DataGridTextColumn()
                    {
                        Header = dataFields[index].Name,
                        Binding = new Binding($"Data[{index}]"),
                        Foreground = Brushes.White,
                        Width = 120
                    });

                    FilterModel model = new FilterModel(index, dataFields[index].Name, new RuleSet(index, Rule.None, RuleException.AllowNull, 0));
                    model.RuleSet!.PropertyChanged += Model_PropertyChanged;
                    Filters.Add(model);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        protected void Changed([CallerMemberName] string member = "")
        {
            var localHandler = this.PropertyChanged;
            localHandler?.Invoke(this, new PropertyChangedEventArgs(member));
        }

        private void Model_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is not RuleSet ruleSet || ruleSet is null)
                return;

            if(e.PropertyName == nameof(FilterModel.RuleSet.Rule) || e.PropertyName == nameof(FilterModel.RuleSet.RuleException) || e.PropertyName == nameof(FilterModel.RuleSet.MatchValue))
                foreach(TempData data in TempData)
                    data.UpdateIsAllowed(ruleSet);
        }
    }

    public class TempData : ModelBase
    {
        public readonly int Row = -1;

        public bool IsAllowed
        {
            get => _isAllowed;
            set => SetProperty(ref _isAllowed, value);
        }

        // In case of no ruleset we allow it, otherwise we overwrite the default anyway
        private bool _isAllowed = true;

        public object?[] Data
        {
            get => _data;
            set => SetProperty(ref _data, value);
        }

        private object?[] _data = Array.Empty<object>();

        public TempData()
        {

        }

        public TempData(object?[] data, int row)
        {
            _data = data;
            Row = row;
        }

        public void UpdateIsAllowed(RuleSet ruleSet)
        {
            for (int index = 0; index < Data.Length; index++)
            {
                bool isAllowed = true;
                if (ruleSet.ColumnIndex == index)
                    isAllowed = ruleSet.IsAllowed(Data[index]);

                if (!isAllowed)
                {
                    IsAllowed = false;
                    return;
                }
            }

            IsAllowed = true;
        }
    }
}
