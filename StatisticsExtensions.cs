using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SchoolHelpdesk;

public static class StatisticsExtensions
{
  public static TNumber Median<T, TNumber>(this IEnumerable<T> source, Func<T, TNumber> selector) where TNumber : INumber<TNumber>
  {
    ArgumentNullException.ThrowIfNull(source);
    ArgumentNullException.ThrowIfNull(selector);
    var pool = ArrayPool<TNumber>.Shared;
    var clearOnReturn = RuntimeHelpers.IsReferenceOrContainsReferences<TNumber>();
    var buffer = pool.Rent(source.TryGetNonEnumeratedCount(out var n) ? Math.Max(n, 1) : 256);
    var count = 0;
    try
    {
      foreach (var item in source)
      {
        if (count == buffer.Length) buffer = Grow(pool, buffer, count, clearOnReturn);
        buffer[count++] = selector(item);
      }
      return count == 0 ? throw new InvalidOperationException("Sequence contains no elements.") : Median(buffer.AsSpan(0, count));
    }
    finally
    {
      pool.Return(buffer, clearOnReturn);
    }
  }

  private static TNumber[] Grow<TNumber>(ArrayPool<TNumber> pool, TNumber[] buffer, int count, bool clear) where TNumber : INumber<TNumber>
  {
    var bigger = pool.Rent(buffer.Length * 2);
    Array.Copy(buffer, bigger, count);
    pool.Return(buffer, clear);
    return bigger;
  }

  private static TNumber Median<TNumber>(Span<TNumber> values) where TNumber : INumber<TNumber>
  {
    var n = values.Length;
    var mid = n / 2;
    var two = TNumber.CreateChecked(2);
    var comparer = Comparer<TNumber>.Default;
    if (n <= 64)
    {
      values.Sort(comparer);
      return (n & 1) == 1 ? values[mid] : (values[mid - 1] + values[mid]) / two;
    }
    if ((n & 1) == 1) return SelectKth(values, mid, comparer);
    var lower = SelectKth(values, mid - 1, comparer);
    var upper = SelectKth(values, mid, comparer);
    return (lower + upper) / two;
  }

  private static T SelectKth<T>(Span<T> values, int k, IComparer<T> comparer)
  {
    int left = 0, right = values.Length - 1;
    while (true)
    {
      if (left == right) return values[left];
      var pivot = MedianOfThree(values, left, right, comparer);
      pivot = Partition(values, left, right, pivot, comparer);
      if (k == pivot) return values[k];
      if (k < pivot) right = pivot - 1;
      else left = pivot + 1;
    }
  }

  private static int MedianOfThree<T>(Span<T> values, int left, int right, IComparer<T> comparer)
  {
    var mid = left + ((right - left) >> 1);
    if (comparer.Compare(values[right], values[left]) < 0) Swap(values, left, right);
    if (comparer.Compare(values[mid], values[left]) < 0) Swap(values, mid, left);
    if (comparer.Compare(values[right], values[mid]) < 0) Swap(values, right, mid);
    return mid;
  }

  private static int Partition<T>(Span<T> values, int left, int right, int pivotIndex, IComparer<T> comparer)
  {
    var pivotValue = values[pivotIndex];
    Swap(values, pivotIndex, right);
    var store = left;
    for (var i = left; i < right; i++)
    {
      if (comparer.Compare(values[i], pivotValue) < 0) Swap(values, store++, i);
    }
    Swap(values, store, right);
    return store;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private static void Swap<T>(Span<T> values, int i, int j)
  {
    if (i != j) (values[i], values[j]) = (values[j], values[i]);
  }
}