using System;

namespace DrSakuu.Humr
{
  public static class ArrayUtils
  {
    private static void Resize<T>(ref T[] array, int newSize)
    {
      if (newSize < 0) { array = new T[0]; return; }
      var array2 = array;
      if (array2 == null) array = new T[newSize];
      else if (array2.Length != newSize)
      {
        var array3 = new T[newSize];
        Array.Copy(array2, 0, array3, 0, (array2.Length > newSize) ? newSize : array2.Length);
        array = array3;
      }
    }

    public static T[] Add<T>(this T[] arr, T item)
    {
      Resize(ref arr, arr.Length + 1);
      arr[arr.Length - 1] = item;
      return arr;
    }

    public static T[] Remove<T>(this T[] arr, int index)
    {
      if (index < 0 || index > arr.Length - 1) return arr;
      for (var a = index; a < arr.Length - 1; a++) arr[a] = arr[a + 1];
      Resize(ref arr, arr.Length - 1);
      return arr;
    }

    public static T[] Populate<T>(this T[] arr, T value)
    {
      for (var i = 0; i < arr.Length; i++)
        arr[i] = value;
      return arr;
    }
  }
}