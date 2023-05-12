using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ParquetFilterer
{
    public class FilterModel : ModelBase
    {
        public int ColumnIndex
        {
            get => _columnIndex;
            set => SetProperty(ref _columnIndex, value);
        }

        private int _columnIndex;

        public string ColumnName
        {
            get => _columnName;
            set => SetProperty(ref _columnName, value);
        }

        private string _columnName = "";

        public RuleSet? RuleSet
        {
            get => _ruleSet;
            set => SetProperty(ref _ruleSet, value);
        }

        private RuleSet? _ruleSet = null;

        /// <summary>   Only for default data preview in designer. </summary>
        public FilterModel()
        {
            
        }

        public FilterModel(int columnIndex, string columnName, RuleSet? ruleSet = null)
        {
            _columnIndex = columnIndex;
            _columnName = columnName;

            if(ruleSet is not null)
                _ruleSet = (RuleSet)ruleSet;
        }
    }

    public class ModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void SetProperty<T>(ref T prop, T value, [CallerMemberName] string member = "")
        {
            bool changed = prop is null || !prop.Equals(value);
            prop = value;

            if (changed)
                Changed(member);
        }

        protected void Changed([CallerMemberName] string member = "")
        {
            var localHandler = this.PropertyChanged;
            localHandler?.Invoke(this, new PropertyChangedEventArgs(member));
        }
    }
}
