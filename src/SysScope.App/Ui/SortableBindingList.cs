using System.ComponentModel;

namespace SysScope;

internal sealed class SortableBindingList<T> : BindingList<T>
{
    private bool _sorted;
    private ListSortDirection _direction = ListSortDirection.Ascending;
    private PropertyDescriptor? _property;

    public SortableBindingList()
    {
    }

    public SortableBindingList(IEnumerable<T> items) : base(items.ToList())
    {
    }

    protected override bool SupportsSortingCore => true;
    protected override bool IsSortedCore => _sorted;
    protected override PropertyDescriptor? SortPropertyCore => _property;
    protected override ListSortDirection SortDirectionCore => _direction;

    protected override void ApplySortCore(PropertyDescriptor prop, ListSortDirection direction)
    {
        if (Items is not List<T> items)
            return;
        items.Sort((left, right) =>
        {
            var compared = Compare(prop.GetValue(left), prop.GetValue(right));
            return direction == ListSortDirection.Ascending ? compared : -compared;
        });
        _sorted = true;
        _direction = direction;
        _property = prop;
        OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
    }

    protected override void RemoveSortCore()
    {
        _sorted = false;
        _property = null;
    }

    private static int Compare(object? left, object? right)
    {
        if (left is null && right is null)
            return 0;
        if (left is null)
            return -1;
        if (right is null)
            return 1;
        if (left is IComparable comparable && left.GetType() == right.GetType())
            return comparable.CompareTo(right);
        return StringComparer.CurrentCultureIgnoreCase.Compare(Convert.ToString(left), Convert.ToString(right));
    }
}
