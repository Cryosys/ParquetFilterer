using System;

namespace ParquetFilterer
{
    public class RuleSet : ModelBase
    {
        public int ColumnIndex;

        public Rule Rule
        {
            get => _rule;
            set => SetProperty(ref _rule, value);
        }

        private Rule _rule;

        public RuleException RuleException
        {
            get => _ruleException;
            set => SetProperty(ref _ruleException, value);
        }

        private RuleException _ruleException;

        public IComparable MatchValue
        {
            get => _matchValue;
            set => SetProperty(ref _matchValue, value);
        }

        private IComparable _matchValue;

        public RuleSet(int columnIndex, Rule rule, RuleException ruleException, IComparable matchValue)
        {
            ColumnIndex = columnIndex;
            _rule = rule;
            _ruleException = ruleException;
            _matchValue = matchValue;
        }

        public bool IsAllowed(object? value)
        {
            switch (RuleException)
            {
                case RuleException.DisallowNull:
                    {
                        if (value is null)
                            return false;
                        break;
                    }
                case RuleException.AllowNull:
                default:
                    break;
            }

            // We assume that the value is fine if we cant compare it
            if (value is not IComparable compValue)
                return true;

            dynamic tmpValue = 0;

            if (compValue is int)
                tmpValue = Convert.ChangeType(MatchValue, typeof(int));
            if (compValue is float)
                tmpValue = Convert.ChangeType(MatchValue, typeof(float));
            if (compValue is double)
                tmpValue = Convert.ChangeType(MatchValue, typeof(double));
            if (compValue is decimal)
                tmpValue = Convert.ChangeType(MatchValue, typeof(decimal));

            switch (Rule)
            {
                case Rule.Lower:
                    {
                        if (compValue.CompareTo(tmpValue) >= 0)
                            return false;
                        break;
                    }
                case Rule.LowerOrEqual:
                    {
                        if (compValue.CompareTo(tmpValue) > 0)
                            return false;
                        break;
                    }
                case Rule.Equal:
                    {
                        if (compValue.CompareTo(tmpValue) != 0)
                            return false;
                        break;
                    }
                case Rule.HigherOrEqual:
                    {
                        if (compValue.CompareTo(tmpValue) < 0)
                            return false;
                        break;
                    }
                case Rule.Higher:
                    {
                        if (compValue.CompareTo(tmpValue) <= 0)
                            return false;
                        break;
                    }
                case Rule.None:
                default:
                    break;
            }

            return true;
        }
    }

    public enum Rule
    {
        None = 0,
        Higher = 1,
        HigherOrEqual = 2,
        Equal = 3,
        LowerOrEqual = 4,
        Lower = 5
    }

    public enum RuleException
    {
        AllowNull = 0,
        DisallowNull = 1
    }
}
