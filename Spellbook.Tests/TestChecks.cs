using System.Collections;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Spellbook.Tests;

internal static class TestChecks
{
    public static void AreEqual<T>(T expected, T actual)
    {
        if (expected is not string && actual is not string && expected is IEnumerable expectedItems && actual is IEnumerable actualItems)
        {
            CollectionAssert.AreEqual(expectedItems.Cast<object?>().ToArray(), actualItems.Cast<object?>().ToArray());
            return;
        }

        Assert.AreEqual(expected, actual);
    }

    public static void AreNotEqual<T>(T notExpected, T actual) => Assert.AreNotEqual(notExpected, actual);
    public static void IsTrue(bool condition) => Assert.IsTrue(condition);
    public static void IsFalse(bool condition) => Assert.IsFalse(condition);
    public static void IsNull(object? value) => Assert.IsNull(value);

    public static T Single<T>(IEnumerable<T> items)
    {
        var list = items.ToList();
        Assert.AreEqual(1, list.Count);
        return list[0];
    }

    public static void Single(IEnumerable items) => Assert.AreEqual(1, items.Cast<object?>().Count());
    public static void Empty(IEnumerable items) => Assert.AreEqual(0, items.Cast<object?>().Count());
    public static void NotEmpty(IEnumerable items) => Assert.IsTrue(items.Cast<object?>().Any());

    public static void Contains(string expectedSubstring, string actualString) => StringAssert.Contains(actualString, expectedSubstring);
    public static void Contains(string expectedSubstring, string actualString, StringComparison comparisonType)
    {
        Assert.IsTrue(actualString.Contains(expectedSubstring, comparisonType), $"Expected text to contain '{expectedSubstring}'.");
    }

    public static void Contains<T>(IEnumerable<T> collection, T expected) => CollectionAssert.Contains(collection.ToList(), expected);
    public static void Contains<T>(IEnumerable<T> collection, Predicate<T> filter)
    {
        Assert.IsTrue(collection.Any(item => filter(item)), "Expected collection to contain a matching item.");
    }

    public static void DoesNotContain(string unexpectedSubstring, string actualString) => Assert.IsFalse(actualString.Contains(unexpectedSubstring), $"Did not expect text to contain '{unexpectedSubstring}'. ");
    public static void DoesNotContain<T>(IEnumerable<T> collection, T unexpected) => CollectionAssert.DoesNotContain(collection.ToList(), unexpected);
    public static void DoesNotContain<T>(IEnumerable<T> collection, Predicate<T> filter)
    {
        Assert.IsFalse(collection.Any(item => filter(item)), "Expected collection not to contain a matching item.");
    }

    public static void Matches(string pattern, string actual) => StringAssert.Matches(actual, new Regex(pattern));

    public static T IsType<T>(object? value)
    {
        Assert.IsNotNull(value);
        Assert.AreEqual(typeof(T), value!.GetType());
        return (T)value;
    }

    public static TException Throws<TException>(Action action) where TException : Exception => Assert.Throws<TException>(action);
    public static TException Throws<TException>(Func<object?> action) where TException : Exception => Assert.Throws<TException>(() => _ = action());
    public static async Task<TException> ThrowsAsync<TException>(Func<Task> action) where TException : Exception => await Assert.ThrowsAsync<TException>(action);
    public static TException ThrowsAny<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException ex)
        {
            return ex;
        }

        Assert.Fail($"Expected an exception assignable to {typeof(TException).Name}.");
        throw new InvalidOperationException("Unreachable after assertion failure.");
    }

    public static void All<T>(IEnumerable<T> items, Action<T> assertion)
    {
        foreach (var item in items)
        {
            assertion(item);
        }
    }
}
